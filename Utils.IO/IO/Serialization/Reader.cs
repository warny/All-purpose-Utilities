using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

    /// <summary>Coordinates one logical contract build for each type.</summary>
    private readonly ConcurrentDictionary<Type, Lazy<Delegate>> contractCache = new();

    /// <summary>Tracks the current dependency path so recursive contracts fail deterministically.</summary>
    private readonly System.Threading.AsyncLocal<List<Type>?> buildPath = new();

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> using default converters.
    /// </summary>
    public Reader(Stream stream) : this(stream, new RawReader().ReaderDelegates) { }

    /// <summary>
    /// Initializes a new instance of <see cref="Reader"/> copying converters.
    /// </summary>
    private Reader(Stream stream, IReadOnlyDictionary<Type, Delegate> readers)
    {
        this.Stream = stream;
        this.readers = readers.ToDictionary();
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
        if (!TryFindReaderFor(type, out var readerDelegate))
        {
            readerDelegate = GetOrCreateReader(type);
        }
        return readerDelegate.DynamicInvoke(this);
    }

    /// <summary>
    /// Reads a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">Type of object to read.</typeparam>
    public T Read<T>()
    {
        if (!TryFindReaderFor(typeof(T), out var readerDelegate))
        {
            readerDelegate = GetOrCreateReader(typeof(T));
        }
        var reader = (Func<IReader, T>)readerDelegate;
        return reader.Invoke(this);
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
        return new Reader(s, this.readers);
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
        Delegate[] candidates = readers.Where(pair => pair.Key.IsAssignableFrom(type)).Select(pair => pair.Value).Distinct().ToArray();
        if (candidates.Length == 1)
        {
            reader = candidates[0];
            return true;
        }
        if (candidates.Length > 1) throw new SerializationContractException(type, [new("UIORT005", "Multiple equally applicable reader converters were found.")]);
        reader = null;
        return false;
    }

    /// <summary>Gets the shared result of the single logical contract build for a type.</summary>
    private Delegate GetOrCreateReader(Type type)
    {
        List<Type> path = buildPath.Value ??= [];
        int cycleIndex = path.IndexOf(type);
        if (cycleIndex >= 0)
        {
            string cycle = string.Join(" -> ", path.Skip(cycleIndex).Append(type).Select(t => t.FullName ?? t.Name));
            throw new SerializationContractException(type, [new("UIORT007", $"Recursive serialization contract detected: {cycle}.")]);
        }
        Lazy<Delegate> entry = contractCache.GetOrAdd(type, key => new Lazy<Delegate>(() => CreateReaderFor(key), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
        path.Add(type);
        try { return entry.Value; }
        finally
        {
            path.RemoveAt(path.Count - 1);
            if (path.Count == 0) buildPath.Value = null;
        }
    }

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
            if (!TryFindReaderFor(propertyOrField.ValueType, out var fieldReader))
            {
                fieldReader = GetOrCreateReader(propertyOrField.ValueType);
            }

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
}
