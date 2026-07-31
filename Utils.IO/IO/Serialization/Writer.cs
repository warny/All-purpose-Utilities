using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.IO.Serialization;
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
/// <summary>
/// Provides methods to serialize objects to a <see cref="Stream"/>. Writers are
/// resolved dynamically based on the runtime type of the value to write.
/// </summary>
public class Writer : IWriter, IStreamMapping<Writer>
{
    /// <summary>
    /// Gets the underlying stream used for writing.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Stack used to store positions when <see cref="Push()"/> is called.
    /// </summary>
    private readonly Stack<long> positionsStack = new();

    /// <summary>
    /// Dictionary mapping a type to its associated writer delegate.
    /// </summary>
    private readonly IReadOnlyDictionary<Type, Delegate> writers;
    private readonly ContractCache contractCache = new();

    /// <summary>
    /// Gets or sets the current position within the stream.
    /// </summary>
    public long Position
    {
        get => Stream.Position;
        set => Stream.Position = value;
    }

    /// <summary>
    /// Gets the number of bytes left to write in the stream.
    /// </summary>
    [Obsolete("BytesLeft is not remaining capacity. Use TryGetBytesUntilCurrentLength instead.")]
    public long BytesLeft => TryGetBytesUntilCurrentLength(out long value) ? value : throw new NotSupportedException("The stream does not expose a coherent current length.");

    /// <summary>
    /// Initializes a new instance of the <see cref="Writer"/> class using default converters.
    /// </summary>
    public Writer(Stream stream) : this(stream, new RawWriter().WriterDelegates) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Writer"/> class with custom converters.
    /// </summary>
    /// <param name="stream">Stream to write to.</param>
    /// <param name="converters">Delegates capable of writing specific types.</param>
    public Writer(Stream stream, params IEnumerable<Delegate> converters)
    {
        this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Dictionary<Type, Delegate> registrations = [];
        var defaultDelegates = new RawWriter().WriterDelegates;
        foreach (var converter in converters.Union(defaultDelegates))
        {
            var method = converter.GetMethodInfo();
            var arguments = method.GetParameters();
            arguments.ArgMustBeOfSize(2);
            arguments[0].ArgMustBe(a => a.ParameterType == typeof(IWriter), "The first argument of the function is not IWriter");
            registrations.TryAdd(arguments[1].ParameterType, converter);
        }
        writers = registrations;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Writer"/> class by copying existing writers.
    /// </summary>
    private Writer(Stream stream, IReadOnlyDictionary<Type, Delegate> writers)
    {
        this.Stream = stream;
        this.writers = writers.ToDictionary();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Writer"/> class using multiple converter collections.
    /// </summary>
    public Writer(Stream stream, params IEnumerable<IEnumerable<Delegate>> converters)
                    : this(stream, converters.SelectMany(c => c)) { }

    /// <summary>
    /// Writes a single byte to the underlying stream.
    /// </summary>
    public void WriteByte(byte value) => Stream.WriteByte(value);

    /// <summary>
    /// Writes a span of bytes to the underlying stream.
    /// </summary>
    public void WriteBytes(ReadOnlySpan<byte> bytes) => Stream.Write(bytes);

    /// <summary>
    /// Writes an object dynamically by resolving the appropriate writer.
    /// </summary>
    /// <param name="value">Object to write.</param>
    public void Write(object value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var type = value.GetType();
        if (!TryFindWriterFor(type, out var writerDelegate))
        {
            writerDelegate = GetOrCreateWriter(type);
        }
        writerDelegate.DynamicInvoke(this, value);
    }

    /// <summary>
    /// Writes a strongly-typed object using the cached writer delegate.
    /// </summary>
    /// <typeparam name="T">Type of the object to write.</typeparam>
    /// <param name="value">Object instance to write.</param>
    public void Write<T>(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (!TryFindWriterFor(typeof(T), out var writerDelegate))
        {
            writerDelegate = GetOrCreateWriter(typeof(T));
        }
        var writer = (Action<IWriter, T>)writerDelegate;
        writer.Invoke(this, value);
    }

    /// <summary>
    /// Moves the current position within the stream.
    /// </summary>
    public void Seek(int offset, SeekOrigin origin) => Stream.Seek(offset, origin);

    /// <summary>
    /// Saves the current position onto an internal stack.
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
    /// Restores the last saved position from the stack.
    /// </summary>
    public void Pop()
    {
        if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        Stream.Seek(this.positionsStack.Pop(), SeekOrigin.Begin);
    }

    /// <summary>
    /// Creates a writer operating on a slice of the underlying stream.
    /// </summary>
    /// <param name="position">Start position of the slice.</param>
    /// <param name="length">Length of the slice.</param>
    public Writer Slice(long position, long length)
    {
        PartialStream s = new PartialStream(Stream, position, length);
        return new Writer(s, writers);
    }

    /// <summary>Attempts to measure the bytes between the current position and the stream's current length.</summary>
    public bool TryGetBytesUntilCurrentLength(out long bytesLeft)
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
    /// Tries to find a writer delegate for a given type.
    /// </summary>
    /// <param name="type">Type to find a writer for.</param>
    /// <param name="writer">Writer delegate if one was found.</param>
    /// <returns><see langword="true"/> if a writer was found; otherwise, <see langword="false"/>.</returns>
    private bool TryFindWriterFor(Type type, out Delegate writer)
    {
        if (writers.TryGetValue(type, out writer)) return true;
        var candidates = writers.Where(pair => pair.Key.IsAssignableFrom(type)).ToArray();
        var mostSpecific = candidates.Where(candidate => !candidates.Any(other =>
            candidate.Key != other.Key && candidate.Key.IsAssignableFrom(other.Key))).ToArray();
        if (mostSpecific.Length == 1)
        {
            writer = CreateWriterAdapter(type, mostSpecific[0].Value);
            return true;
        }
        if (mostSpecific.Length > 1)
        {
            string registrations = string.Join(", ", mostSpecific.Select(candidate => candidate.Key.FullName ?? candidate.Key.Name).OrderBy(name => name, StringComparer.Ordinal));
            throw new SerializationContractException(type,
                [new SerializationContractDiagnostic("UIORT005", $"Writer converters registered for {registrations} are equally specific for {type.FullName ?? type.Name}.")]);
        }
        writer = null;
        return false;
    }

    /// <summary>Creates a strongly typed adapter for a contravariant base or interface writer.</summary>
    private static Delegate CreateWriterAdapter(Type requestedType, Delegate converter)
    {
        MethodInfo invoke = converter.GetType().GetMethod("Invoke")!;
        Type acceptedType = invoke.GetParameters()[1].ParameterType;
        ParameterExpression writerParameter = Expression.Parameter(typeof(IWriter), "writer");
        ParameterExpression valueParameter = Expression.Parameter(requestedType, "value");
        MethodCallExpression call = Expression.Call(Expression.Constant(converter), invoke,
            writerParameter, Expression.Convert(valueParameter, acceptedType));
        Type adapterType = typeof(Action<,>).MakeGenericType(typeof(IWriter), requestedType);
        return Expression.Lambda(adapterType, call, writerParameter, valueParameter).Compile();
    }

    /// <summary>Gets the shared result of the single logical writer build for a type.</summary>
    private Delegate GetOrCreateWriter(Type type)
        => contractCache.GetOrBuild(type, CreateWriterFor);

    /// <summary>
    /// Creates a writer for a given type dynamically using expression trees.
    /// </summary>
    /// <param name="type">Type to create a writer for.</param>
    /// <returns>A delegate capable of writing the specified type.</returns>
    private Delegate CreateWriterFor(Type type)
    {
        var expressions = new List<Expression>();

        // Get fields or properties with custom FieldAttribute
        var contract = ReflectionContractBuilder.Build(type, SerializationDirection.Write);

        var writerArgument = Expression.Parameter(typeof(IWriter), "writer");
        var objectArgument = Expression.Parameter(type, "obj");

        // Cast the object to its original type before accessing members
        var typedObject = objectArgument;

        foreach (var propertyOrField in contract.Members)
        {
            if (!TryFindWriterFor(propertyOrField.ValueType, out var fieldWriter))
            {
                fieldWriter = GetOrCreateWriter(propertyOrField.ValueType);
            }

            // Generate the call to the writer delegate
            var writerMethod = fieldWriter.GetType().GetMethod("Invoke");

            Expression memberAccess = propertyOrField.Member switch
            {
                PropertyInfo property => Expression.Property(typedObject, property),
                FieldInfo field => Expression.Field(typedObject, field),
                _ => throw new NotSupportedException("Unsupported member type.")
            };

            var writerCall = Expression.Call(
                    Expression.Constant(fieldWriter),
                    writerMethod,
                    writerArgument,
                    memberAccess
            );

            expressions.Add(writerCall);
        }

        // Create the final block and lambda
        var block = Expression.Block(expressions);
        var lambdaType = typeof(Action<,>).MakeGenericType(typeof(IWriter), type);
        var lambda = Expression.Lambda(lambdaType, block, writerArgument, objectArgument);

        var compiledLambda = lambda.Compile();
        return compiledLambda;
    }
}
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
