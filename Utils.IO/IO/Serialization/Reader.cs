using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Utils.Reflection;
using Utils.Objects;

namespace Utils.IO.Serialization;

/// <summary>
/// Generic reader capable of dynamically reading objects from a stream.
/// </summary>
public class Reader : IReader, IStreamMapping<Reader>
{
    /// <summary>
    /// Gets the underlying stream used by the reader.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Gets the number of bytes remaining in the stream.
    /// </summary>
    public long BytesLeft => TryGetBytesLeft(out long value) ? value : throw new NotSupportedException("The stream does not expose a coherent remaining length.");

    /// <summary>
    /// Gets or sets the current position within the stream.
    /// </summary>
    public long Position
    {
        get => Stream.Position;
        set => Stream.Position = value;
    }

    /// <summary>
    /// Stack storing stream positions pushed with <see cref="Push()"/>.
    /// </summary>
    private readonly Stack<long> positionsStack = new();

    /// <summary>
    /// Dictionary mapping a type to its reader delegate.
    /// </summary>
    private readonly IReadOnlyDictionary<Type, Delegate> readers;
    private readonly IReadOnlyDictionary<Type, WireCodecRegistration> codecs;

    /// <summary>Gets the largest accepted length-prefixed payload in bytes.</summary>
    internal int MaximumPayloadLength { get; }

    /// <summary>Coordinates one logical contract build for each type.</summary>
    private readonly ContractCache contractCache = new();

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> using default converters.
    /// </summary>
    public Reader(Stream stream) : this(stream, new ReaderOptions(), new SerializationOptions()) { }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> using default converters and explicit payload options.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="options">Payload safety options; a null maximum preserves unlimited historical reads.</param>
    public Reader(Stream stream, ReaderOptions options)
        : this(stream, options, new SerializationOptions()) { }

    /// <summary>Initializes a reader with shared wire serialization options.</summary>
    public Reader(Stream stream, SerializationOptions serializationOptions)
        : this(stream, new ReaderOptions(), serializationOptions) { }

    /// <summary>Initializes a reader with payload limits and shared wire serialization options.</summary>
    public Reader(Stream stream, ReaderOptions options, SerializationOptions serializationOptions)
        : this(stream, CreateRawReader(options).ReaderDelegates, Snapshot(serializationOptions), GetMaximumPayloadLength(options)) { }

    /// <summary>Initializes a reader with wire options and explicit converters.</summary>
    public Reader(Stream stream, SerializationOptions serializationOptions, params IEnumerable<Delegate> converters)
        : this(stream, new ReaderOptions(), serializationOptions, converters) { }

    /// <summary>Initializes a reader with payload limits, wire options, and explicit converters.</summary>
    public Reader(Stream stream, ReaderOptions options, SerializationOptions serializationOptions, params IEnumerable<Delegate> converters)
        : this(stream, converters.Union(CreateRawReader(options).ReaderDelegates), Snapshot(serializationOptions), GetMaximumPayloadLength(options)) { }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> with explicit payload options and custom converters.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="options">Payload safety options.</param>
    /// <param name="converters">Reader delegates used to deserialize objects.</param>
    public Reader(Stream stream, ReaderOptions options, params IEnumerable<Delegate> converters)
        : this(stream, converters.Union(CreateRawReader(options).ReaderDelegates), new Dictionary<Type, WireCodecRegistration>(), GetMaximumPayloadLength(options)) { }

    /// <summary>Initializes a reader from converter delegates and a codec snapshot.</summary>
    private Reader(Stream stream, IEnumerable<Delegate> converters, IReadOnlyDictionary<Type, WireCodecRegistration> codecs, int maximumPayloadLength)
        : this(stream, BuildRegistrations(converters), codecs, maximumPayloadLength) { }

    /// <summary>Validates reader converter delegates and indexes them by exact return type.</summary>
    private static IReadOnlyDictionary<Type, Delegate> BuildRegistrations(IEnumerable<Delegate> converters)
    {
        Dictionary<Type, Delegate> registrations = [];
        foreach (Delegate converter in converters)
        {
            MethodInfo method = converter.GetMethodInfo();
            ParameterInfo[] arguments = method.GetParameters();
            arguments.ArgMustBeOfSizes([1]);
            arguments[0].ArgMustBe(a => a.ParameterType == typeof(IReader), "The first argument is not IReader");
            registrations.TryAdd(method.ReturnType, converter);
        }
        return registrations;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> copying converters.
    /// </summary>
    private Reader(Stream stream, IReadOnlyDictionary<Type, Delegate> readers, IReadOnlyDictionary<Type, WireCodecRegistration>? codecs = null, int maximumPayloadLength = int.MaxValue)
    {
        this.Stream = stream;
        this.readers = readers.ToDictionary();
        this.codecs = codecs ?? new Dictionary<Type, WireCodecRegistration>();
        MaximumPayloadLength = maximumPayloadLength;
    }

    /// <summary>Creates and validates the primitive reader configured for this reader instance.</summary>
    private static RawReader CreateRawReader(ReaderOptions options)
    {
        return new RawReader { MaximumLength = GetMaximumPayloadLength(options) };
    }

    /// <summary>Validates reader options and resolves their effective payload limit.</summary>
    private static int GetMaximumPayloadLength(ReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumPayloadLength < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumPayloadLength must be non-negative or null.");

        return options.MaximumPayloadLength ?? int.MaxValue;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> with custom converters.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="converters">Reader delegates used to deserialize objects.</param>
    public Reader(Stream stream, params IEnumerable<Delegate> converters)
    {
        this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Dictionary<Type, Delegate> registrations = [];
        foreach (var converter in converters.Union(new RawReader().ReaderDelegates))
        {
            var method = converter.GetMethodInfo();
            var arguments = method.GetParameters();
            arguments.ArgMustBeOfSizes([1]);
            arguments[0].ArgMustBe(a => a.ParameterType == typeof(IReader), "The first argument of the function {method.Name} is not IReader");
            registrations.TryAdd(method.ReturnType, converter);
        }
        readers = registrations;
        codecs = new Dictionary<Type, WireCodecRegistration>();
        MaximumPayloadLength = int.MaxValue;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> with multiple converter collections.
    /// </summary>
    public Reader(Stream stream, params IEnumerable<IEnumerable<Delegate>> converters)
            : this(stream, converters.SelectMany(c => c)) { }


    /// <summary>
    /// Reads an object dynamically by resolving the appropriate reader.
    /// </summary>
    /// <param name="type">Type of object to read.</param>
    public object Read(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (TryReadCodec(type, null, null, out object? codecValue)) return codecValue!;
        if (!TryFindReaderFor(type, out var readerDelegate)) readerDelegate = GetOrCreateReader(type);
        return readerDelegate.DynamicInvoke(this)!;
    }

    /// <summary>
    /// Reads a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">Type of object to read.</typeparam>
    public T Read<T>()
    {
        if (TryReadCodec(typeof(T), null, null, out object? codecValue)) return (T)codecValue!;
        if (!TryFindReaderFor(typeof(T), out var readerDelegate)) readerDelegate = GetOrCreateReader(typeof(T));
        return ((Func<IReader, T>)readerDelegate).Invoke(this);
    }

    /// <summary>
    /// Saves the current stream position onto the internal stack.
    /// </summary>
    public void Push()
    {
        if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        this.positionsStack.Push(Stream.Position);
    }

    /// <summary>
    /// Saves the current position and seeks relative to the given offset.
    /// </summary>
    /// <param name="offset">Offset to seek to.</param>
    /// <param name="origin">Reference point for seeking.</param>
    public void Push(int offset, SeekOrigin origin)
    {
        if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        long original = Stream.Position;
        try
        {
            Stream.Seek(offset, origin);
            this.positionsStack.Push(original);
        }
        catch
        {
            try { Stream.Position = original; }
            catch { /* Position restoration is best effort; preserve the seek exception. */ }
            throw;
        }
    }

    /// <summary>
    /// Restores the last saved stream position.
    /// </summary>
    public void Pop()
    {
        if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        Stream.Seek(this.positionsStack.Pop(), SeekOrigin.Begin);
    }
    /// <summary>
    /// Moves the stream position without saving it.
    /// </summary>
    public void Seek(int offset, SeekOrigin origin) => Stream.Seek(offset, origin);


    /// <summary>
    /// Read one byte from the underlying <see cref="Stream"/>
    /// </summary>
    /// <returns>The byte value or -1 if read failed</returns>
    public int ReadByte() => Stream.ReadByte();

    /// <summary>
    /// Read a <see cref="byte"/> array from the <see cref="Stream"/> of <paramref name="length"/>
    /// </summary>
    /// <param name="length">bytes to be read</param>
    /// <returns><see cref="byte"/>array</returns>
    public byte[] ReadBytes(int length) => Stream.ReadBytes(length);

    /// <summary>Attempts to obtain the number of readable bytes between the current position and stream length.</summary>
    public bool TryGetBytesLeft(out long bytesLeft)
    {
        bytesLeft = 0;
        if (!Stream.CanSeek) return false;
        try
        {
            long length = Stream.Length;
            long position = Stream.Position;
            if (position < 0 || position > length) return false;
            bytesLeft = length - position;
            return true;
        }
        catch (NotSupportedException) { return false; }
    }

    /// <summary>
    /// Creates a new reader that is limited to a slice of the underlying stream.
    /// </summary>
    /// <param name="position">Start position of the slice.</param>
    /// <param name="length">Length of the slice.</param>
    public Reader Slice(long position, long length)
    {
        PartialStream s = new PartialStream(Stream, position, length);
        return new Reader(s, this.readers, codecs, MaximumPayloadLength);
    }

    /// <summary>
    /// Attempts to find a reader delegate for a given type.
    /// </summary>
    /// <param name="type">Type to find a reader for.</param>
    /// <param name="reader">Found reader delegate if any.</param>
    /// <returns><see langword="true"/> if a reader was found.</returns>
    private bool TryFindReaderFor(Type type, out Delegate reader)
    {
        if (readers.TryGetValue(type, out reader)) return true;
        Type[] broaderRegistrations = readers.Keys.Where(registeredType => registeredType.IsAssignableFrom(type)).ToArray();
        if (broaderRegistrations.Length > 0)
        {
            string registrations = string.Join(", ", broaderRegistrations.Select(candidate => candidate.FullName ?? candidate.Name).OrderBy(name => name, StringComparer.Ordinal));
            throw new SerializationContractException(type,
                [new SerializationContractDiagnostic("UIORT005", $"Reader converter(s) registered for {registrations} cannot read {type.FullName ?? type.Name} because their return types do not guarantee that concrete result.")]);
        }
        reader = null;
        return false;
    }

    /// <summary>Gets the shared result of the single logical contract build for a type.</summary>
    private Delegate GetOrCreateReader(Type type)
        => contractCache.GetOrBuild(type, CreateReaderFor);

    /// <summary>
    /// Creates a reader for a given type dynamically using expression trees.
    /// </summary>
    /// <param name="type">Type to create a reader for.</param>
    /// <returns>A delegate capable of reading the given type.</returns>
    private Delegate CreateReaderFor(Type type)
    {
        var contract = ReflectionContractBuilder.Build(type, SerializationDirection.Read);

        var readerArgument = Expression.Parameter(typeof(IReader), "reader");

        // Create a variable to store the result object
        var resultVariable = Expression.Variable(type, "result");

        // Initialize the result object
        var newObjectExpression = Expression.New(type);
        var assignNewObject = Expression.Assign(resultVariable, newObjectExpression);

        var blockExpressions = new List<Expression> { assignNewObject };

        foreach (var propertyOrField in contract.Members)
        {
            Delegate fieldReader;
            if (propertyOrField.CodecType is not null || propertyOrField.FramingType is not null || HasReadableCodec(propertyOrField.ValueType))
                fieldReader = CreateConfiguredReaderDelegate(propertyOrField.ValueType, propertyOrField.CodecType, propertyOrField.FramingType);
            else if (!TryFindReaderFor(propertyOrField.ValueType, out fieldReader))
                fieldReader = GetOrCreateReader(propertyOrField.ValueType);

            // Generate the call to the reader delegate
            var readerMethod = fieldReader.GetType().GetMethod("Invoke");

            var readCall = Expression.Call(
                Expression.Constant(fieldReader),
                readerMethod,
                readerArgument
            );

            // Set the read value to the corresponding property or field
            var memberAccess = propertyOrField.Member switch
            {
                PropertyInfo property => Expression.Property(resultVariable, property),
                FieldInfo field => Expression.Field(resultVariable, field),
                _ => throw new NotSupportedException("Unsupported member type.")
            };

            var assignValue = Expression.Assign(memberAccess, Expression.Convert(readCall, propertyOrField.ValueType));

            blockExpressions.Add(assignValue);
        }

        // Return the result object
        blockExpressions.Add(resultVariable);

        // Create the final block and lambda
        var block = Expression.Block([resultVariable], blockExpressions);
        var lambda = Expression.Lambda(block, readerArgument);

        var compiledLambda = lambda.Compile();
        return compiledLambda;
    }
    /// <summary>Checks whether an exact codec registration provides the reader direction.</summary>
    private bool HasReadableCodec(Type type) => codecs.TryGetValue(type, out WireCodecRegistration? registration) && registration.Reader is not null;

    /// <summary>Takes an immutable snapshot of user-registered codecs.</summary>
    private static IReadOnlyDictionary<Type, WireCodecRegistration> Snapshot(SerializationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Codecs.Snapshot();
    }

    /// <summary>Creates a bounded child reader retaining the exact codec snapshot.</summary>
    internal Reader CreateCodecReader(Stream stream) => new(stream, readers, codecs, MaximumPayloadLength);

    /// <summary>Creates a mapped reader retaining converter and codec snapshots.</summary>
    internal Reader CreateMappedReader(Stream stream) => new(stream, readers, codecs, MaximumPayloadLength);

    /// <summary>Attempts an exact configured codec read without constructing a reflection contract.</summary>
    internal bool TryReadConfigured<T>(out T value)
    {
        if (TryReadCodec(typeof(T), null, null, out object? result)) { value = (T)result!; return true; }
        value = default!;
        return false;
    }

    /// <summary>Reads through an exact runtime or member-specific codec selection.</summary>
    internal T ReadConfigured<T>(Type? codecType, Type? framingType)
    {
        if (!TryReadCodec(typeof(T), codecType, framingType, out object? value))
            throw new SerializationContractException(typeof(T), [new("UIORT015", $"No readable wire codec is configured for {typeof(T).FullName}.")]);
        return (T)value!;
    }

    /// <summary>Creates a strongly typed configured reader delegate for compiled contracts.</summary>
    private Delegate CreateConfiguredReaderDelegate(Type type, Type? codecType, Type? framingType) =>
        (Delegate)typeof(Reader).GetMethod(nameof(CreateConfiguredReaderDelegateGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type).Invoke(this, [codecType, framingType])!;

    /// <summary>Creates one generic configured reader delegate.</summary>
    private Delegate CreateConfiguredReaderDelegateGeneric<T>(Type? codecType, Type? framingType) => new Func<IReader, T>(_ => ReadConfigured<T>(codecType, framingType));

    /// <summary>Resolves and executes a codec with member metadata taking precedence over exact registrations.</summary>
    private bool TryReadCodec(Type type, Type? codecType, Type? framingType, out object? value)
    {
        object? codec = codecType is null ? (codecs.TryGetValue(type, out WireCodecRegistration? registration) ? registration.Reader : null) : Activator.CreateInstance(codecType);
        if (codec is null && framingType is not null && type == typeof(DateTime)) codec = new DotNetBinaryDateTimeCodec();
        IWireFraming? framing = framingType is null ? (codecType is null && codecs.TryGetValue(type, out WireCodecRegistration? registered) ? registered.Framing : null) : (IWireFraming?)Activator.CreateInstance(framingType);
        if (codec is null) { value = null; return false; }
        MethodInfo method = typeof(Reader).GetMethod(nameof(ReadCodecGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type);
        try { value = method.Invoke(this, [codec, framing]); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
        return true;
    }

    /// <summary>Executes one typed codec after validating its read direction and default framing.</summary>
    private T ReadCodecGeneric<T>(object codec, IWireFraming? framing)
    {
        if (codec is not IWireReader<T> reader) throw new SerializationContractException(typeof(T), [new("UIORT016", $"Codec {codec.GetType().FullName} has no reader for {typeof(T).FullName}.")]);
        framing ??= codec is IFixedWireCodec<T> fixedCodec ? new FixedWireFraming(fixedCodec.Size) : new CodecOwnedWireFraming();
        return WireCodecEngine.Read(this, reader, framing);
    }

}
