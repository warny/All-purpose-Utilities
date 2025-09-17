using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.IO.Serialization;

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
	private readonly Dictionary<Type, Delegate> writers = [];

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
	public long BytesLeft => Stream.Length - Stream.Position;

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
		var defaultDelegates = new RawWriter().WriterDelegates;
		foreach (var converter in converters.Union(defaultDelegates))
		{
			var method = converter.GetMethodInfo();
			var arguments = method.GetParameters();
			arguments.ArgMustBeOfSize(2);
			arguments[0].ArgMustBe(a => a.ParameterType == typeof(IWriter), "The first argument of the function is not IWriter");
			if (!writers.ContainsKey(arguments[1].ParameterType))
			{
				writers.Add(arguments[1].ParameterType, converter);
			}
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Writer"/> class by copying existing writers.
	/// </summary>
	private Writer(Stream stream, IDictionary<Type, Delegate> writers)
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
	/// <param name="obj">Object to write.</param>
	public void Write(object obj)
	{
		if (obj is null) throw new ArgumentNullException(nameof(obj));
		var type = obj.GetType();
		if (!TryFindWriterFor(type, out var writerDelegate))
		{
			writerDelegate = CreateWriterFor(type);
		}
		writerDelegate.DynamicInvoke(this, obj);
	}

	/// <summary>
	/// Writes a strongly-typed object using the cached writer delegate.
	/// </summary>
	/// <typeparam name="T">Type of the object to write.</typeparam>
	/// <param name="obj">Object instance to write.</param>
	public void Write<T>(T obj)
	{
		if (obj is null) throw new ArgumentNullException(nameof(obj));
		if (!TryFindWriterFor(typeof(T), out var writerDelegate))
		{
			writerDelegate = CreateWriterFor(typeof(T));
		}
		var writer = (Action<IWriter, T>)writerDelegate;
		writer.Invoke(this, obj);
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
		this.positionsStack.Push(Stream.Position);
		Stream.Seek(offset, origin);
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

	/// <summary>
	/// Tries to find a writer delegate for a given type.
	/// </summary>
	/// <param name="type">Type to find a writer for.</param>
	/// <param name="writer">Writer delegate if one was found.</param>
	/// <returns><c>true</c> if a writer was found; otherwise, <c>false</c>.</returns>
	private bool TryFindWriterFor(Type type, out Delegate writer)
	{
		foreach (var t in type.GetTypeHierarchy().SelectMany(h => h.Interfaces.Prepend(h.Type)))
		{
			if (writers.TryGetValue(t, out writer))
			{
				return true;
			}
		}
		writer = null;
		return false;
	}

	/// <summary>
	/// Creates a writer for a given type dynamically using expression trees.
	/// </summary>
	/// <param name="type">Type to create a writer for.</param>
	/// <returns>A delegate capable of writing the specified type.</returns>
	private Delegate CreateWriterFor(Type type)
	{
		var expressions = new List<Expression>();

		// Get fields or properties with custom FieldAttribute
		var propertiesOrFields = type.GetMembers(BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(m => m.GetCustomAttribute<FieldAttribute>() is not null)
				.Select(m => new PropertyOrFieldInfo(m))
				.OrderBy(m => m.GetCustomAttribute<FieldAttribute>().Order)
				.ToArray();

		var writerArgument = Expression.Parameter(typeof(IWriter), "writer");
		var objectArgument = Expression.Parameter(typeof(object), "obj");

		// Cast the object to its original type before accessing members
		var typedObject = Expression.Convert(objectArgument, type);

		foreach (var propertyOrField in propertiesOrFields)
		{
			if (!TryFindWriterFor(propertyOrField.Type, out var fieldWriter))
			{
				fieldWriter = CreateWriterFor(propertyOrField.Type);
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
		var lambda = Expression.Lambda(block, writerArgument, objectArgument);

		var compiledLambda = lambda.Compile();
		writers.Add(type, compiledLambda);
		return compiledLambda;
	}
}
