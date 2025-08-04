using System;
using System.Collections.Generic;
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
public class NewReader : IReader, IStreamMapping<NewReader>
{
        /// <summary>
        /// Gets the underlying stream used by the reader.
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Gets the number of bytes remaining in the stream.
        /// </summary>
        public long BytesLeft { get; }

        /// <summary>
        /// Gets or sets the current position within the stream.
        /// </summary>
        public long Position { get; set; }

	private readonly Stack<long> positionsStack = new Stack<long>();

	// Dictionary to store reader delegates for each type
	private readonly Dictionary<Type, Delegate> readers = [];

        /// <summary>
        /// Initializes a new instance of <see cref="NewReader"/> using default converters.
        /// </summary>
        public NewReader(Stream stream) : this(stream, new RawReader().ReaderDelegates) { }

        /// <summary>
        /// Initializes a new instance of <see cref="NewReader"/> with custom converters.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="converters">Reader delegates used to deserialize objects.</param>
        public NewReader(Stream stream, params IEnumerable<Delegate> converters)
        {
                this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                foreach (var converter in converters.Union(new RawReader().ReaderDelegates))
                {
                        var method = converter.GetMethodInfo();
                        var arguments = method.GetParameters();
                        arguments.ArgMustBeOfSize(1);
                        arguments[0].ArgMustBe(a => a.ParameterType == typeof(IReader), "The first argument of the function is not IReader");
                        if (!readers.ContainsKey(method.ReturnType))
                        {
                                readers.Add(method.ReturnType, converter);
                        }
                }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="NewReader"/> with multiple converter collections.
        /// </summary>
        public NewReader(Stream stream, params IEnumerable<IEnumerable<Delegate>> converters)
                : this(stream, converters.SelectMany(c => c)) { }


	/// <summary>
        /// <summary>
        /// Reads an object dynamically by resolving the appropriate reader.
        /// </summary>
        /// <param name="type">Type of object to read.</param>
        public object Read(Type type)
	{
		if (!TryFindReaderFor(type, out var readerDelegate))
		{
			readerDelegate = CreateReaderFor(type);
		}
		return readerDelegate.DynamicInvoke(Stream);
	}

	/// <summary>
        /// <summary>
        /// Reads a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">Type of object to read.</typeparam>
        public T Read<T>()
	{
		if (!TryFindReaderFor(typeof(T), out var readerDelegate))
		{
			readerDelegate = CreateReaderFor(typeof(T));
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
		this.positionsStack.Push(Stream.Position);
		Stream.Seek(offset, origin);
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


	public int ReadByte() => Stream.ReadByte();
	public byte[] ReadBytes(int length) => Stream.ReadBytes(length);

        /// <summary>
        /// Creates a new reader that is limited to a slice of the underlying stream.
        /// </summary>
        /// <param name="position">Start position of the slice.</param>
        /// <param name="length">Length of the slice.</param>
        public NewReader Slice(long position, long length)
	{
		PartialStream s = new PartialStream(Stream, position, length);
		return new NewReader(s);
	}

	/// <summary>
        /// <summary>
        /// Attempts to find a reader delegate for a given type.
        /// </summary>
        /// <param name="type">Type to find a reader for.</param>
        /// <param name="reader">Found reader delegate if any.</param>
        /// <returns><c>true</c> if a reader was found.</returns>
        private bool TryFindReaderFor(Type type, out Delegate reader)
	{
		foreach (var t in type.GetTypeHierarchy().SelectMany(h => h.Interfaces.Prepend(h.Type)))
		{
			if (readers.TryGetValue(t, out reader))
			{
				return true;
			}
		}
		reader = null;
		return false;
	}

	/// <summary>
        /// <summary>
        /// Creates a reader for a given type dynamically using expression trees.
        /// </summary>
        /// <param name="type">Type to create a reader for.</param>
        /// <returns>A delegate capable of reading the given type.</returns>
        private Delegate CreateReaderFor(Type type)
	{
		var expressions = new List<Expression>();

		var propertiesOrFields = type.GetMembers(BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(m => m.GetCustomAttribute<FieldAttribute>() is not null)
			.Select(m => new PropertyOrFieldInfo(m))
			.OrderBy(m => m.GetCustomAttribute<FieldAttribute>().Order)
			.ToArray();

		var readerArgument = Expression.Parameter(typeof(IReader), "reader");

		// Create a variable to store the result object
		var resultVariable = Expression.Variable(type, "result");

		// Initialize the result object
		var newObjectExpression = Expression.New(type);
		var assignNewObject = Expression.Assign(resultVariable, newObjectExpression);

		var blockExpressions = new List<Expression> { assignNewObject };

		foreach (var propertyOrField in propertiesOrFields)
		{
			if (!TryFindReaderFor(propertyOrField.Type, out var fieldReader))
			{
				fieldReader = CreateReaderFor(propertyOrField.Type);
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

			var assignValue = Expression.Assign(memberAccess, Expression.Convert(readCall, propertyOrField.Type));

			blockExpressions.Add(assignValue);
		}

		// Return the result object
		blockExpressions.Add(resultVariable);

		// Create the final block and lambda
		var block = Expression.Block([resultVariable], blockExpressions);
		var lambda = Expression.Lambda(block, readerArgument);

		var compiledLambda = lambda.Compile();
		readers.Add(type, compiledLambda);
		return compiledLambda;
	}
}
