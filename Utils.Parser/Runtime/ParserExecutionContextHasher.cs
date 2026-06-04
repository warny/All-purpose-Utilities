using System.Collections;
using System.Reflection;
using System.Text;

namespace Utils.Parser.Runtime;

/// <summary>
/// Computes deterministic structural execution-state keys for generated parser execution contexts.
/// </summary>
/// <typeparam name="TContext">The parser execution-context type to hash.</typeparam>
/// <remarks>
/// Instance fields are inspected in a stable order. Static fields and field-like event backing fields are ignored,
/// matching <see cref="ParserExecutionContextCopier{TContext}"/>. Auto-property backing fields are normal instance
/// fields and therefore participate in the state key. Complex user objects must implement
/// <see cref="IParserExecutionStateHashable"/> explicitly; otherwise an exception is thrown instead of falling back
/// to reference identity or <see cref="object.GetHashCode"/>.
/// </remarks>
public static class ParserExecutionContextHasher<TContext>
    where TContext : class
{
    /// <summary>Cached context fields that participate in hashing.</summary>
    private static readonly Lazy<IReadOnlyList<FieldInfo>> HashableFields = new(BuildHashableFields);

    /// <summary>
    /// Computes a parser execution-state key for <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context instance whose state is hashed.</param>
    /// <returns>A structural key representing the current context state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A field contains an unsupported complex object.</exception>
    public static ParserExecutionStateKey GetKey(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StableHashBuilder();
        builder.AddText("context");
        builder.AddText(typeof(TContext).AssemblyQualifiedName ?? typeof(TContext).FullName ?? typeof(TContext).Name);

        foreach (FieldInfo field in HashableFields.Value)
        {
            builder.AddText(field.DeclaringType?.AssemblyQualifiedName ?? string.Empty);
            builder.AddText(field.Name);
            AddValue(builder, field.GetValue(context), field.FieldType, field.Name);
        }

        return new ParserExecutionStateKey(builder.Value);
    }

    /// <summary>
    /// Builds the ordered field list used by the closed generic hasher.
    /// </summary>
    /// <returns>Fields that contribute to the structural state key.</returns>
    private static IReadOnlyList<FieldInfo> BuildHashableFields()
    {
        return [.. GetInstanceFields(typeof(TContext))
            .Where(static field => !ShouldSkipField(field))
            .OrderBy(static field => field.DeclaringType?.FullName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static field => field.MetadataToken)
            .ThenBy(static field => field.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Enumerates all instance fields declared by the supplied type and its base types.
    /// </summary>
    /// <param name="type">The type whose field hierarchy is inspected.</param>
    /// <returns>The instance fields that may participate in hashing.</returns>
    private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
    {
        for (Type? currentType = type; currentType is not null && currentType != typeof(object); currentType = currentType.BaseType)
        {
            foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    /// <summary>
    /// Determines whether a field should be excluded from context hashing.
    /// </summary>
    /// <param name="field">The field being inspected.</param>
    /// <returns><see langword="true"/> when the field should not be hashed; otherwise, <see langword="false"/>.</returns>
    private static bool ShouldSkipField(FieldInfo field)
    {
        return field.IsStatic || IsEventBackingField(field);
    }

    /// <summary>
    /// Determines whether a field is the backing field for a field-like event.
    /// </summary>
    /// <param name="field">The field being inspected.</param>
    /// <returns><see langword="true"/> when the field backs an event; otherwise, <see langword="false"/>.</returns>
    private static bool IsEventBackingField(FieldInfo field)
    {
        Type? declaringType = field.DeclaringType;

        return declaringType?.GetEvent(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null;
    }

    /// <summary>
    /// Adds a value to the hash stream according to the supported structural hashing rules.
    /// </summary>
    /// <param name="builder">Hash builder receiving the value contribution.</param>
    /// <param name="value">Runtime value to hash.</param>
    /// <param name="declaredType">Declared field or element type.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddValue(IStableHashSink builder, object? value, Type declaredType, string path)
    {
        builder.AddText(declaredType.AssemblyQualifiedName ?? declaredType.FullName ?? declaredType.Name);
        if (value is null)
        {
            builder.AddByte(0);
            return;
        }

        builder.AddByte(1);
        Type valueType = value.GetType();
        builder.AddText(valueType.AssemblyQualifiedName ?? valueType.FullName ?? valueType.Name);
        Type effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (TryAddSimpleValue(builder, value, effectiveType))
        {
            return;
        }

        if (value is IParserExecutionStateHashable hashable)
        {
            builder.AddText("hashable");
            builder.AddUInt64(hashable.GetParserExecutionStateHash());
            return;
        }

        if (value is IDictionary dictionary)
        {
            AddDictionary(builder, dictionary, path);
            return;
        }

        if (value is not string && IsDictionaryLike(valueType))
        {
            AddDictionaryLike(builder, value, path);
            return;
        }

        if (value is not string && IsSetLike(valueType))
        {
            AddSet(builder, (IEnumerable)value, path);
            return;
        }

        if (value is not string && value is IEnumerable enumerable)
        {
            AddSequence(builder, enumerable, path);
            return;
        }

        throw CreateUnsupportedTypeException(valueType, path);
    }

    /// <summary>
    /// Adds a simple deterministic scalar value to the hash stream when supported.
    /// </summary>
    /// <param name="builder">Hash builder receiving the value contribution.</param>
    /// <param name="value">Value to hash.</param>
    /// <param name="type">Non-nullable runtime value type.</param>
    /// <returns><see langword="true"/> when the value was handled.</returns>
    private static bool TryAddSimpleValue(IStableHashSink builder, object value, Type type)
    {
        TypeCode code = Type.GetTypeCode(type);
        switch (code)
        {
            case TypeCode.Boolean:
                builder.AddByte((bool)value ? (byte)1 : (byte)0);
                return true;
            case TypeCode.Char:
                builder.AddUInt64((char)value);
                return true;
            case TypeCode.SByte:
                builder.AddInt64((sbyte)value);
                return true;
            case TypeCode.Byte:
                builder.AddUInt64((byte)value);
                return true;
            case TypeCode.Int16:
                builder.AddInt64((short)value);
                return true;
            case TypeCode.UInt16:
                builder.AddUInt64((ushort)value);
                return true;
            case TypeCode.Int32:
                builder.AddInt64((int)value);
                return true;
            case TypeCode.UInt32:
                builder.AddUInt64((uint)value);
                return true;
            case TypeCode.Int64:
                builder.AddInt64((long)value);
                return true;
            case TypeCode.UInt64:
                builder.AddUInt64((ulong)value);
                return true;
            case TypeCode.Single:
                builder.AddUInt32(BitConverter.SingleToUInt32Bits((float)value));
                return true;
            case TypeCode.Double:
                builder.AddUInt64(BitConverter.DoubleToUInt64Bits((double)value));
                return true;
            case TypeCode.Decimal:
                foreach (int part in decimal.GetBits((decimal)value))
                {
                    builder.AddInt64(part);
                }
                return true;
            case TypeCode.String:
                builder.AddText((string)value);
                return true;
            case TypeCode.DateTime:
                builder.AddInt64(((DateTime)value).ToBinary());
                return true;
        }

        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            object converted = Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
            return TryAddSimpleValue(builder, converted, underlying);
        }

        if (type == typeof(DateTimeOffset))
        {
            var dateTimeOffset = (DateTimeOffset)value;
            builder.AddInt64(dateTimeOffset.Ticks);
            builder.AddInt64(dateTimeOffset.Offset.Ticks);
            return true;
        }

        if (type == typeof(TimeSpan))
        {
            builder.AddInt64(((TimeSpan)value).Ticks);
            return true;
        }

        if (type == typeof(Guid))
        {
            builder.AddBytes(((Guid)value).ToByteArray());
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds an ordered sequence to the hash stream.
    /// </summary>
    /// <param name="builder">Hash builder receiving sequence values.</param>
    /// <param name="enumerable">Enumerable sequence whose order is significant.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddSequence(IStableHashSink builder, IEnumerable enumerable, string path)
    {
        builder.AddText("sequence");
        ulong count = 0;
        foreach (object? item in enumerable)
        {
            builder.AddUInt64(count++);
            AddValue(builder, item, item?.GetType() ?? typeof(object), $"{path}[{count - 1}]");
        }

        builder.AddUInt64(count);
    }

    /// <summary>
    /// Adds dictionary entries after sorting them independently from native dictionary enumeration order.
    /// </summary>
    /// <param name="builder">Hash builder receiving dictionary values.</param>
    /// <param name="dictionary">Dictionary whose entries are hashed.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddDictionary(IStableHashSink builder, IDictionary dictionary, string path)
    {
        List<DictionaryEntry> entries = [];
        foreach (DictionaryEntry entry in dictionary)
        {
            entries.Add(entry);
        }

        AddOrderedDictionaryEntries(builder, entries.Select(static entry => ((object?)entry.Key, entry.Value)), path);
    }

    /// <summary>
    /// Adds dictionary-like generic entries exposed through key/value properties.
    /// </summary>
    /// <param name="builder">Hash builder receiving dictionary values.</param>
    /// <param name="dictionary">Dictionary-like object whose entries are hashed.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddDictionaryLike(IStableHashSink builder, object dictionary, string path)
    {
        List<(object? Key, object? Value)> entries = [];
        foreach (object? entry in (IEnumerable)dictionary)
        {
            if (entry is null)
            {
                throw new InvalidOperationException($"Dictionary entry at '{path}' is null and cannot be hashed deterministically.");
            }

            Type entryType = entry.GetType();
            PropertyInfo? keyProperty = entryType.GetProperty("Key");
            PropertyInfo? valueProperty = entryType.GetProperty("Value");
            if (keyProperty is null || valueProperty is null)
            {
                throw new InvalidOperationException($"Dictionary entry type '{entryType.FullName ?? entryType.Name}' at '{path}' does not expose Key and Value properties.");
            }

            entries.Add((keyProperty.GetValue(entry), valueProperty.GetValue(entry)));
        }

        AddOrderedDictionaryEntries(builder, entries, path);
    }

    /// <summary>
    /// Orders and adds dictionary entries to the hash stream.
    /// </summary>
    /// <param name="builder">Hash builder receiving dictionary values.</param>
    /// <param name="entries">Entries to hash.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddOrderedDictionaryEntries(IStableHashSink builder, IEnumerable<(object? Key, object? Value)> entries, string path)
    {
        builder.AddText("dictionary");
        var orderedEntries = entries
            .Select((entry, index) => new OrderedEntry(CreateCanonicalSortKey(entry.Key, $"{path}.Key"), CreateCanonicalSortKey(entry.Value, $"{path}.Value"), entry))
            .OrderBy(static entry => entry.KeyCanonical, StringComparer.Ordinal)
            .ThenBy(static entry => entry.ValueCanonical, StringComparer.Ordinal)
            .ToList();

        ValidateDictionaryCanonicalOrdering(orderedEntries, path);

        builder.AddUInt64((ulong)orderedEntries.Count);
        foreach (OrderedEntry orderedEntry in orderedEntries)
        {
            AddValue(builder, orderedEntry.Entry.Key, orderedEntry.Entry.Key?.GetType() ?? typeof(object), $"{path}.Key");
            AddValue(builder, orderedEntry.Entry.Value, orderedEntry.Entry.Value?.GetType() ?? typeof(object), $"{path}[{orderedEntry.KeyCanonical}]");
        }
    }

    /// <summary>
    /// Adds set entries after sorting them independently from native set enumeration order.
    /// </summary>
    /// <param name="builder">Hash builder receiving set values.</param>
    /// <param name="set">Set whose values are hashed.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void AddSet(IStableHashSink builder, IEnumerable set, string path)
    {
        builder.AddText("set");
        var orderedItems = set
            .Cast<object?>()
            .Select((item, index) => new OrderedValue(CreateCanonicalSortKey(item, path), item))
            .OrderBy(static item => item.Canonical, StringComparer.Ordinal)
            .ToList();

        ValidateSetCanonicalOrdering(orderedItems, path);

        builder.AddUInt64((ulong)orderedItems.Count);
        foreach (OrderedValue item in orderedItems)
        {
            AddValue(builder, item.Value, item.Value?.GetType() ?? typeof(object), path);
        }
    }

    /// <summary>
    /// Creates a deterministic ordering key for dictionary keys and set elements.
    /// </summary>
    /// <param name="value">Value to sort.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    /// <returns>A deterministic string sort key.</returns>
    private static string CreateCanonicalSortKey(object? value, string path)
    {
        var builder = new CanonicalByteBuilder();
        AddValue(builder, value, value?.GetType() ?? typeof(object), path);
        return Convert.ToHexString(builder.ToArray());
    }

    /// <summary>
    /// Ensures dictionary keys with the same canonical bytes are not distinct values that cannot be ordered safely.
    /// </summary>
    /// <param name="orderedEntries">Entries sorted by canonical key and value.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void ValidateDictionaryCanonicalOrdering(IReadOnlyList<OrderedEntry> orderedEntries, string path)
    {
        for (int index = 1; index < orderedEntries.Count; index++)
        {
            OrderedEntry previous = orderedEntries[index - 1];
            OrderedEntry current = orderedEntries[index];
            if (previous.KeyCanonical == current.KeyCanonical && !Equals(previous.Entry.Key, current.Entry.Key))
            {
                throw new InvalidOperationException(
                    $"Parser execution-state hashing cannot order dictionary entries at '{path}' deterministically because distinct keys " +
                    "produce the same canonical representation. Provide comparable keys or a collision-free " +
                    $"{nameof(IParserExecutionStateHashable)} implementation for dictionary keys.");
            }
        }
    }

    /// <summary>
    /// Ensures set elements with the same canonical bytes are not distinct values that cannot be ordered safely.
    /// </summary>
    /// <param name="orderedItems">Set items sorted by canonical value.</param>
    /// <param name="path">Human-readable field path for exception messages.</param>
    private static void ValidateSetCanonicalOrdering(IReadOnlyList<OrderedValue> orderedItems, string path)
    {
        for (int index = 1; index < orderedItems.Count; index++)
        {
            OrderedValue previous = orderedItems[index - 1];
            OrderedValue current = orderedItems[index];
            if (previous.Canonical == current.Canonical && !Equals(previous.Value, current.Value))
            {
                throw new InvalidOperationException(
                    $"Parser execution-state hashing cannot order set elements at '{path}' deterministically because distinct elements " +
                    "produce the same canonical representation. Provide comparable elements or a collision-free " +
                    $"{nameof(IParserExecutionStateHashable)} implementation for set elements.");
            }
        }
    }

    /// <summary>
    /// Determines whether a type exposes dictionary-shaped entries.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements a generic dictionary contract.</returns>
    private static bool IsDictionaryLike(Type type)
    {
        return type.GetInterfaces().Append(type).Any(static candidate => candidate.IsGenericType
            && (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
    }

    /// <summary>
    /// Determines whether a type exposes set semantics.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements a generic set contract.</returns>
    private static bool IsSetLike(Type type)
    {
        return type.GetInterfaces().Append(type).Any(static candidate => candidate.IsGenericType
            && (candidate.GetGenericTypeDefinition() == typeof(ISet<>)
                || candidate.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)));
    }

    /// <summary>
    /// Creates the explicit unsupported-type exception used for complex user objects.
    /// </summary>
    /// <param name="type">Unsupported runtime type.</param>
    /// <param name="path">Human-readable field path.</param>
    /// <returns>An exception describing the unsupported state value.</returns>
    private static InvalidOperationException CreateUnsupportedTypeException(Type type, string path)
    {
        return new InvalidOperationException(
            $"Parser execution-state hashing does not support value type '{type.FullName ?? type.Name}' at '{path}'. " +
            $"Implement {nameof(IParserExecutionStateHashable)} to provide an explicit structural parser execution-state hash.");
    }

    /// <summary>Dictionary entry paired with deterministic canonical sort keys.</summary>
    /// <param name="KeyCanonical">Canonical key bytes encoded as hexadecimal text.</param>
    /// <param name="ValueCanonical">Canonical value bytes encoded as hexadecimal text.</param>
    /// <param name="Entry">Dictionary entry being sorted.</param>
    private readonly record struct OrderedEntry(string KeyCanonical, string ValueCanonical, (object? Key, object? Value) Entry);

    /// <summary>Set entry paired with deterministic canonical sort bytes.</summary>
    /// <param name="Canonical">Canonical element bytes encoded as hexadecimal text.</param>
    /// <param name="Value">Set value being sorted.</param>
    private readonly record struct OrderedValue(string Canonical, object? Value);

    /// <summary>
    /// Receives typed structural hash components.
    /// </summary>
    private interface IStableHashSink
    {
        /// <summary>Adds a byte marker to the stream.</summary>
        /// <param name="value">Byte value to add.</param>
        void AddByte(byte value);

        /// <summary>Adds a byte sequence with no implicit length prefix.</summary>
        /// <param name="bytes">Bytes to add.</param>
        void AddBytes(IEnumerable<byte> bytes);

        /// <summary>Adds a 32-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        void AddUInt32(uint value);

        /// <summary>Adds a 64-bit signed integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        void AddInt64(long value);

        /// <summary>Adds a 64-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        void AddUInt64(ulong value);

        /// <summary>Adds a UTF-8 string with a length prefix.</summary>
        /// <param name="value">String to add.</param>
        void AddText(string value);
    }

    /// <summary>
    /// Incremental FNV-1a hash builder with explicit type and length markers.
    /// </summary>
    private sealed class StableHashBuilder : IStableHashSink
    {
        /// <summary>FNV offset basis.</summary>
        private const ulong OffsetBasis = 14695981039346656037UL;
        /// <summary>FNV prime.</summary>
        private const ulong Prime = 1099511628211UL;
        /// <summary>Current hash value.</summary>
        private ulong _value = OffsetBasis;

        /// <summary>Gets the current hash value.</summary>
        public ulong Value => _value;

        /// <summary>Adds a byte marker to the hash stream.</summary>
        /// <param name="value">Byte value to add.</param>
        public void AddByte(byte value)
        {
            _value ^= value;
            _value *= Prime;
        }

        /// <summary>Adds a byte sequence with no implicit length prefix.</summary>
        /// <param name="bytes">Bytes to add.</param>
        public void AddBytes(IEnumerable<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                AddByte(value);
            }
        }

        /// <summary>Adds a 32-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddUInt32(uint value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a 64-bit signed integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddInt64(long value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a 64-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddUInt64(ulong value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a UTF-8 string with a length prefix.</summary>
        /// <param name="value">String to add.</param>
        public void AddText(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            AddUInt64((ulong)bytes.Length);
            AddBytes(bytes);
        }
    }

    /// <summary>
    /// Captures the full structural byte stream used as a canonical collection sort representation.
    /// </summary>
    private sealed class CanonicalByteBuilder : IStableHashSink
    {
        /// <summary>Captured structural bytes.</summary>
        private readonly List<byte> _bytes = [];

        /// <summary>Returns the captured canonical structural bytes.</summary>
        /// <returns>A canonical byte array.</returns>
        public byte[] ToArray()
        {
            return [.. _bytes];
        }

        /// <summary>Adds a byte marker to the canonical stream.</summary>
        /// <param name="value">Byte value to add.</param>
        public void AddByte(byte value)
        {
            _bytes.Add(value);
        }

        /// <summary>Adds a byte sequence with no implicit length prefix.</summary>
        /// <param name="bytes">Bytes to add.</param>
        public void AddBytes(IEnumerable<byte> bytes)
        {
            _bytes.AddRange(bytes);
        }

        /// <summary>Adds a 32-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddUInt32(uint value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a 64-bit signed integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddInt64(long value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a 64-bit unsigned integer in little-endian byte order.</summary>
        /// <param name="value">Value to add.</param>
        public void AddUInt64(ulong value)
        {
            AddBytes(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a UTF-8 string with a length prefix.</summary>
        /// <param name="value">String to add.</param>
        public void AddText(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            AddUInt64((ulong)bytes.Length);
            AddBytes(bytes);
        }
    }
}
