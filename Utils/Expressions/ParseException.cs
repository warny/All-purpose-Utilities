using System;
using System.Runtime.Serialization;

namespace Utils.Expressions;

abstract public class ParseException : Exception
{
    protected ParseException(string message, int errorIndex) : this(message, errorIndex, null) { }

    protected ParseException(string message, int errorIndex, Exception inner)
        : base($"position {errorIndex} near：{message}", inner) { }

    static public void Assert(string strInput, string strNeed, int index)
    {
        if (strInput != strNeed)
        {
            throw new ParseWrongSymbolException(strNeed, strInput, index);
        }
    }
}


abstract public class CompileException : Exception
{
    protected CompileException(string message, int errorIndex) : this(message, errorIndex, null) { }

    protected CompileException(string message, int errorIndex, Exception inner)
        : base($"position {errorIndex} near：{message}", inner) { }
}


public class ParseNoEndException : ParseException
{
    public ParseNoEndException(string symbol, int errorIndex)
        : base($"Undefined symbol：“{symbol}”", errorIndex)
    {
    }
}


public class ParseUnknownException : ParseException
{
    public ParseUnknownException(string symbol, int errorIndex)
        : base($"Unknown symbol：“{symbol}”", errorIndex)
    {
    }
}


public class ParseUnmatchException : ParseException
{
    public ParseUnmatchException(string startSymbol, string endSymbol, int errorIndex)
        : base($"Unmatched symbols. Start character“{startSymbol}”VS end character“{endSymbol}”", errorIndex)
    {
    }
}


public class ParseWrongSymbolException : ParseException
{
    public ParseWrongSymbolException(string rightSymbol, string wrongSymbol, int errorIndex)
        : base($"Incorrect symbol. should be“{rightSymbol}”；Now is“{wrongSymbol}”", errorIndex)
    {
    }
}


public class ParseUnfindTypeException : ParseException
{
    public ParseUnfindTypeException(string typeName, int errorIndex)
        : base($"Type not found：“{typeName}”", errorIndex)
    {
    }
}

public class ParseDuplicateParameterNameException : ParseException
{
    public ParseDuplicateParameterNameException(string variableName, int errorIndex)
        : base($"The variable already exists：“{variableName}”", errorIndex)
    {
    }
}

public class FindTypeException : Exception
{
    public string TypeName { get; }

    public FindTypeException(string typeName)
    {
        TypeName = typeName;
    }

    public FindTypeException(string typeName, string message) : base(message)
    {
        TypeName = typeName;
    }

    public FindTypeException(string typeName, string message, Exception innerException) : base(message, innerException)
    {
        TypeName = typeName;
    }

    protected FindTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}