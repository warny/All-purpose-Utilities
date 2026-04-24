using System.Dynamic;
using System.Reflection;
using System.Text.Json;

namespace Utils.Expressions;

/// <summary>
/// Represents a dynamic symbol context shared by expression compilers.
/// </summary>
public class ExpressionCompilerContext : DynamicObject
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Gets the symbol table used for runtime resolution.
    /// </summary>
    public IDictionary<string, object?> Symbols { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Adds or replaces a symbol in the context.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Symbol value.</param>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Symbols.TryGetValue(name, out object? existing))
        {
            Symbols[name] = MergeSymbols(existing, value);
            return;
        }

        Symbols[name] = value;
    }

    /// <summary>
    /// Tries to resolve a symbol by name.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Resolved value.</param>
    /// <returns><see langword="true"/> when found; otherwise <see langword="false"/>.</returns>
    public bool TryGet(string name, out object? value)
    {
        return Symbols.TryGetValue(name, out value);
    }

    /// <summary>
    /// Writes this context to a stream.
    /// </summary>
    /// <remarks>
    /// Stream persistence serializes symbol names and values, including delegates and method groups.
    /// </remarks>
    /// <param name="stream">Destination stream.</param>
    public void WriteToStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var snapshot = new ContextSnapshot(
            Symbols.Select(symbol => new ContextEntry(symbol.Key, CreateSerializedValue(symbol.Value))).ToArray());

        JsonSerializer.Serialize(stream, snapshot, SerializationOptions);
    }

    /// <summary>
    /// Reads a context from a stream.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>A populated context.</returns>
    public static ExpressionCompilerContext ReadFromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ContextSnapshot snapshot = JsonSerializer.Deserialize<ContextSnapshot>(stream, SerializationOptions)
            ?? throw new InvalidOperationException("Unable to deserialize compiler context snapshot.");

        ExpressionCompilerContext context = new();
        foreach (ContextEntry entry in snapshot.Symbols)
        {
            context.Symbols[entry.Name] = RestoreSerializedValue(entry.Value);
        }

        return context;
    }

    /// <inheritdoc />
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        return Symbols.TryGetValue(binder.Name, out result);
    }

    /// <inheritdoc />
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        Set(binder.Name, value);
        return true;
    }

    /// <inheritdoc />
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (!Symbols.TryGetValue(binder.Name, out object? symbol))
        {
            result = null;
            return false;
        }

        args ??= [];

        if (symbol is Delegate delegateSymbol)
        {
            result = delegateSymbol.DynamicInvoke(args);
            return true;
        }

        if (symbol is MethodInfo[] methodInfos)
        {
            MethodInfo? method = methodInfos.FirstOrDefault(methodInfo =>
            {
                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length != args.Length) return false;

                for (int index = 0; index < parameters.Length; index++)
                {
                    if (args[index] is null)
                    {
                        if (parameters[index].ParameterType.IsValueType) return false;
                        continue;
                    }

                    if (!parameters[index].ParameterType.IsInstanceOfType(args[index]))
                    {
                        return false;
                    }
                }

                return true;
            });

            if (method is null)
            {
                result = null;
                return false;
            }

            result = method.Invoke(null, args);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Merges symbol values when the same name is registered multiple times.
    /// </summary>
    /// <param name="existing">Existing value.</param>
    /// <param name="incoming">Incoming value.</param>
    /// <returns>Merged symbol value.</returns>
    private static object? MergeSymbols(object? existing, object? incoming)
    {
        MethodInfo[]? existingMethods = AsMethodArray(existing);
        MethodInfo[]? incomingMethods = AsMethodArray(incoming);
        if (existingMethods is null || incomingMethods is null)
        {
            return incoming;
        }

        return existingMethods
            .Concat(incomingMethods)
            .Distinct(MethodSignatureComparer.Instance)
            .ToArray();
    }

    /// <summary>
    /// Converts a callable symbol into a method array representation.
    /// </summary>
    /// <param name="value">Symbol value.</param>
    /// <returns>Method array when value is callable; otherwise <see langword="null"/>.</returns>
    private static MethodInfo[]? AsMethodArray(object? value)
    {
        return value switch
        {
            null => null,
            MethodInfo[] methods => methods,
            Delegate delegateValue => [delegateValue.Method],
            _ => null
        };
    }

    private static SerializedValue CreateSerializedValue(object? value)
    {
        if (value is null)
        {
            return new SerializedValue("null", null, null, null, null, null);
        }

        if (value is Delegate delegateValue)
        {
            return new SerializedValue(
                "delegate",
                null,
                null,
                delegateValue.GetType().AssemblyQualifiedName,
                CreateMethodReference(delegateValue.Method),
                null);
        }

        if (value is MethodInfo[] methods)
        {
            return new SerializedValue(
                "method-array",
                null,
                null,
                null,
                null,
                methods.Select(CreateMethodReference).ToArray());
        }

        Type valueType = value.GetType();
        byte[] jsonValue = JsonSerializer.SerializeToUtf8Bytes(value, valueType, SerializationOptions);
        return new SerializedValue(
            "json",
            valueType.AssemblyQualifiedName,
            Convert.ToBase64String(jsonValue),
            null,
            null,
            null);
    }

    private static object? RestoreSerializedValue(SerializedValue serializedValue)
    {
        return serializedValue.Kind switch
        {
            "null" => null,
            "json" => DeserializeJsonValue(serializedValue),
            "delegate" => DeserializeDelegate(serializedValue),
            "method-array" => DeserializeMethodArray(serializedValue),
            _ => throw new NotSupportedException($"Unsupported serialized symbol kind: {serializedValue.Kind}")
        };
    }

    private static object DeserializeJsonValue(SerializedValue serializedValue)
    {
        Type type = ResolveType(serializedValue.Type)
            ?? throw new NotSupportedException("Serialized symbol type is missing or unknown.");

        string payload = serializedValue.Payload
            ?? throw new NotSupportedException("Serialized symbol payload is missing.");

        byte[] data = Convert.FromBase64String(payload);
        return JsonSerializer.Deserialize(data, type, SerializationOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize a value of type {type.FullName}.");
    }

    private static Delegate DeserializeDelegate(SerializedValue serializedValue)
    {
        Type delegateType = ResolveType(serializedValue.DelegateType)
            ?? throw new NotSupportedException("Serialized delegate type is missing or unknown.");

        MethodInfo method = ResolveMethod(serializedValue.Method)
            ?? throw new NotSupportedException("Serialized delegate method is missing or unknown.");

        return Delegate.CreateDelegate(delegateType, method);
    }

    private static MethodInfo[] DeserializeMethodArray(SerializedValue serializedValue)
    {
        MethodReference[] methods = serializedValue.Methods
            ?? throw new NotSupportedException("Serialized method array is missing method descriptors.");

        return methods.Select(method => ResolveMethod(method)
            ?? throw new NotSupportedException("Serialized method descriptor is unknown.")).ToArray();
    }

    private static MethodReference CreateMethodReference(MethodInfo method)
    {
        if (!method.IsStatic)
        {
            throw new NotSupportedException("Only static methods can be persisted in compiler contexts.");
        }

        return new MethodReference(
            method.DeclaringType?.AssemblyQualifiedName,
            method.Name,
            method.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName).ToArray());
    }

    private static MethodInfo? ResolveMethod(MethodReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        Type declaringType = ResolveType(reference.DeclaringType)
            ?? throw new NotSupportedException("Serialized method declaring type is missing or unknown.");

        Type[] parameterTypes = reference.ParameterTypes
            .Select(typeName => ResolveType(typeName)
                ?? throw new NotSupportedException("Serialized method parameter type is unknown."))
            .ToArray();

        return declaringType.GetMethod(
            reference.Name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            parameterTypes,
            null);
    }

    private static Type? ResolveType(string? typeName)
    {
        return typeName is null ? null : Type.GetType(typeName, throwOnError: false);
    }

    private sealed record ContextSnapshot(ContextEntry[] Symbols);

    private sealed record ContextEntry(string Name, SerializedValue Value);

    private sealed record SerializedValue(
        string Kind,
        string? Type,
        string? Payload,
        string? DelegateType,
        MethodReference? Method,
        MethodReference[]? Methods);

    private sealed record MethodReference(string? DeclaringType, string Name, string?[] ParameterTypes);

    /// <summary>
    /// Compares methods by declaration, name and parameter types to remove duplicate overloads.
    /// </summary>
    private sealed class MethodSignatureComparer : IEqualityComparer<MethodInfo>
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static MethodSignatureComparer Instance { get; } = new();

        /// <inheritdoc />
        public bool Equals(MethodInfo? x, MethodInfo? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            if (!Equals(x.DeclaringType, y.DeclaringType) || x.Name != y.Name)
            {
                return false;
            }

            Type[] xParameters = x.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Type[] yParameters = y.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            return xParameters.SequenceEqual(yParameters);
        }

        /// <inheritdoc />
        public int GetHashCode(MethodInfo obj)
        {
            HashCode hash = new();
            hash.Add(obj.DeclaringType);
            hash.Add(obj.Name);
            foreach (ParameterInfo parameter in obj.GetParameters())
            {
                hash.Add(parameter.ParameterType);
            }

            return hash.ToHashCode();
        }
    }
}
