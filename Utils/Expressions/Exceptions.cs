using System.Runtime.Serialization;

namespace Utils.Expressions;

/// <summary>
/// Represents the base class for all parsing exceptions that occur during
/// expression parsing in the <c>Utils.Expressions</c> namespace.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ParseException"/> class
/// with the specified error message, error index, and an inner exception.
/// </remarks>
/// <param name="message">A short description of the parsing error.</param>
/// <param name="errorIndex">The position (index) near where the parsing error occurred.</param>
/// <param name="inner">The inner exception that caused this parsing error.</param>
public abstract class ParseException(string message, int errorIndex, Exception inner) : Exception($"position {errorIndex} near：{message}", inner)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseException"/> class
	/// with the specified error message and error index.
	/// </summary>
	/// <param name="message">A short description of the parsing error.</param>
	/// <param name="errorIndex">The position (index) near where the parsing error occurred.</param>
	protected ParseException(string message, int errorIndex)
		: this(message, errorIndex, null)
	{
	}

	/// <summary>
	/// Asserts that the given input string matches the required string.
	/// If they do not match, a <see cref="ParseWrongSymbolException"/> is thrown.
	/// </summary>
	/// <param name="strInput">The actual string to check.</param>
	/// <param name="strNeed">The expected string.</param>
	/// <param name="index">The position (index) in the input where this check occurs.</param>
	/// <exception cref="ParseWrongSymbolException">
	/// Thrown if <paramref name="strInput"/> does not match <paramref name="strNeed"/>.
	/// </exception>
	public static void Assert(string strInput, string strNeed, int index)
	{
		if (strInput != strNeed)
		{
			throw new ParseWrongSymbolException(strNeed, strInput, index);
		}
	}
}

/// <summary>
/// Represents the base class for compile-time exceptions that occur during
/// expression compilation in the <c>Utils.Expressions</c> namespace.
/// </summary>
public abstract class CompileException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CompileException"/> class
	/// with the specified error message and error index.
	/// </summary>
	/// <param name="message">A short description of the compilation error.</param>
	/// <param name="errorIndex">The position (index) near where the compilation error occurred.</param>
	protected CompileException(string message, int errorIndex)
		: this(message, errorIndex, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CompileException"/> class
	/// with the specified error message, error index, and an inner exception.
	/// </summary>
	/// <param name="message">A short description of the compilation error.</param>
	/// <param name="errorIndex">The position (index) near where the compilation error occurred.</param>
	/// <param name="inner">The inner exception that caused this compilation error.</param>
	protected CompileException(string message, int errorIndex, Exception inner)
		: base($"position {errorIndex} near：{message}", inner)
	{
	}
}

/// <summary>
/// Thrown when an expected closing symbol is not found in the parsed expression.
/// </summary>
public class ParseNoEndException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseNoEndException"/> class,
	/// indicating that the expected closing symbol was missing.
	/// </summary>
	/// <param name="symbol">The symbol for which the parser was searching.</param>
	/// <param name="errorIndex">The position in the input where the error was detected.</param>
	public ParseNoEndException(string symbol, int errorIndex)
		: base($"Undefined symbol：“{symbol}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when an unknown symbol is encountered by the parser.
/// </summary>
public class ParseUnknownException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseUnknownException"/> class,
	/// indicating that the parser encountered an unrecognized symbol.
	/// </summary>
	/// <param name="symbol">The unknown symbol detected by the parser.</param>
	/// <param name="errorIndex">The position in the input where the error was detected.</param>
	public ParseUnknownException(string symbol, int errorIndex)
		: base($"Unknown symbol：“{symbol}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when a pair of opening and closing symbols do not match in the parsed expression.
/// </summary>
public class ParseUnmatchException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseUnmatchException"/> class,
	/// indicating mismatched opening and closing symbols.
	/// </summary>
	/// <param name="startSymbol">The opening symbol.</param>
	/// <param name="endSymbol">The closing symbol that does not match.</param>
	/// <param name="errorIndex">The position in the input where the mismatch occurred.</param>
	public ParseUnmatchException(string startSymbol, string endSymbol, int errorIndex)
		: base($"Unmatched symbols. Start character“{startSymbol}”VS end character“{endSymbol}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when the parser encounters a symbol that is incorrect for the current context.
/// </summary>
public class ParseWrongSymbolException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseWrongSymbolException"/> class,
	/// indicating that the parser found an unexpected symbol instead of the required one.
	/// </summary>
	/// <param name="rightSymbol">The symbol that was expected.</param>
	/// <param name="wrongSymbol">The symbol that was actually found.</param>
	/// <param name="errorIndex">The position in the input where the error was detected.</param>
	public ParseWrongSymbolException(string rightSymbol, string wrongSymbol, int errorIndex)
		: base($"Incorrect symbol. should be“{rightSymbol}”；Now is“{wrongSymbol}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when a type name cannot be resolved or found by the parser.
/// </summary>
public class ParseUnfindTypeException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseUnfindTypeException"/> class,
	/// indicating that a type name could not be located.
	/// </summary>
	/// <param name="typeName">The name of the type that was not found.</param>
	/// <param name="errorIndex">The position in the input where the error was detected.</param>
	public ParseUnfindTypeException(string typeName, int errorIndex)
		: base($"Type not found：“{typeName}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when the parser encounters a parameter name that is already in use.
/// </summary>
public class ParseDuplicateParameterNameException : ParseException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ParseDuplicateParameterNameException"/> class,
	/// indicating that a parameter with the same name already exists.
	/// </summary>
	/// <param name="variableName">The duplicate parameter name.</param>
	/// <param name="errorIndex">The position in the input where the error was detected.</param>
	public ParseDuplicateParameterNameException(string variableName, int errorIndex)
		: base($"The variable already exists：“{variableName}”", errorIndex)
	{
	}
}

/// <summary>
/// Thrown when a type cannot be found during a type resolution process.
/// </summary>
public class FindTypeException : Exception
{
	/// <summary>
	/// Gets the name of the type that could not be located.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FindTypeException"/> class,
	/// specifying the name of the type that could not be found.
	/// </summary>
	/// <param name="typeName">The name of the type that was not found.</param>
	public FindTypeException(string typeName)
	{
		TypeName = typeName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FindTypeException"/> class,
	/// specifying the name of the type that could not be found and a custom error message.
	/// </summary>
	/// <param name="typeName">The name of the type that was not found.</param>
	/// <param name="message">A short description of the error.</param>
	public FindTypeException(string typeName, string message)
		: base(message)
	{
		TypeName = typeName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FindTypeException"/> class,
	/// specifying the name of the type that could not be found, a custom error message,
	/// and an inner exception.
	/// </summary>
	/// <param name="typeName">The name of the type that was not found.</param>
	/// <param name="message">A short description of the error.</param>
	/// <param name="innerException">The inner exception that caused this error.</param>
	public FindTypeException(string typeName, string message, Exception innerException)
		: base(message, innerException)
	{
		TypeName = typeName;
	}

#pragma warning disable SYSLIB0051 // Le type ou le membre est obsolète
	/// <summary>
	/// Initializes a new instance of the <see cref="FindTypeException"/> class
	/// with serialized data, typically used during deserialization.
	/// </summary>
	/// <param name="info">A <see cref="SerializationInfo"/> holding serialized object data.</param>
	/// <param name="context">A <see cref="StreamingContext"/> that describes the source and destination of the serialized stream.</param>
	protected FindTypeException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
#pragma warning restore SYSLIB0051 // Le type ou le membre est obsolète
}
