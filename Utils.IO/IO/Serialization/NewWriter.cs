using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.IO.Serialization;

public class NewWriter : IWriter, IStreamMapping<NewWriter>
{
	public Stream Stream { get; }

	private readonly Stack<long> positionsStack = new();

	// Initialize the writers dictionary using the [] syntax
	private readonly Dictionary<Type, Delegate> writers = [];

	public long Position
	{
		get => Stream.Position;
		set => Stream.Position = value;
	}
	public long BytesLeft => Stream.Length - Stream.Position;

	public NewWriter(Stream stream) : this(stream, new RawWriter().WriterDelegates) { }

                public NewWriter(Stream stream, params IEnumerable<Delegate> converters)
                {
                        this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                        foreach (var converter in converters.Union(new RawWriter().WriterDelegates))
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

                public NewWriter(Stream stream, params IEnumerable<IEnumerable<Delegate>> converters)
                        : this(stream, converters.SelectMany(c => c)) { }


	public void WriteByte(byte value) => Stream.WriteByte(value);
	public void WriteBytes(ReadOnlySpan<byte> bytes) => Stream.Write(bytes);


	/// <summary>
	/// Write an object dynamically by resolving the appropriate writer.
	/// </summary>
	public void Write(object obj)
	{
		var type = obj.GetType();
		if (!TryFindWriterFor(type, out var writerDelegate))
		{
			writerDelegate = CreateWriterFor(type);
		}
		writerDelegate.DynamicInvoke(this, obj);
	}

	/// <summary>
	/// Write a strongly-typed object.
	/// </summary>
	public void Write<T>(T obj)
	{
		if (!TryFindWriterFor(typeof(T), out var writerDelegate))
		{
			writerDelegate = CreateWriterFor(typeof(T));
		}
		var writer = (Action<IWriter, T>)writerDelegate;
		writer.Invoke(this, obj);
	}

	public void Seek(int offset, SeekOrigin origin) => Stream.Seek(offset, origin);

	public void Push()
	{
		if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
		this.positionsStack.Push(Stream.Position);
	}

	public void Push(int offset, SeekOrigin origin)
	{
		if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
		this.positionsStack.Push(Stream.Position);
		Stream.Seek(offset, origin);
	}

	public void Pop()
	{
		if (!Stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
		Stream.Seek(this.positionsStack.Pop(), SeekOrigin.Begin);
	}

	public NewWriter Slice(long position, long length)
	{
		PartialStream s = new PartialStream(Stream, position, length);
		return new NewWriter(s);
	}


	/// <summary>
	/// Tries to find a writer delegate for a given type.
	/// </summary>
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
