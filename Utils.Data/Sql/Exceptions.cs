using System;

namespace Utils.Data.Sql;

/// <summary>
/// Represents errors that occur during SQL parsing.
/// </summary>
public sealed class SqlParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    public SqlParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SqlParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SqlParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
