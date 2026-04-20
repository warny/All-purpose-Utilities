using System;
using System.Text.RegularExpressions;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Immutable descriptor that defines a reusable parser diagnostic template.
/// </summary>
public sealed class ParserDiagnosticDescriptor
{
    private static readonly Regex s_codeRegex = new("^UP[0-9]{4}$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new diagnostic descriptor and validates the diagnostic code format.
    /// </summary>
    /// <param name="code">Diagnostic code in the form <c>UPxxxx</c>.</param>
    /// <param name="title">Short diagnostic title.</param>
    /// <param name="messageFormat">Composite format string used to build diagnostic messages.</param>
    /// <param name="category">Optional diagnostic category.</param>
    /// <exception cref="ArgumentException">Thrown when the code format is invalid.</exception>
    public ParserDiagnosticDescriptor(string code, string title, string messageFormat, string? category = null)
    {
        if (!s_codeRegex.IsMatch(code))
        {
            throw new ArgumentException($"Invalid diagnostic code '{code}'. Expected format UPxxxx.", nameof(code));
        }

        Code = code;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        Category = category;
        Severity = ParserDiagnosticSeverityMapper.FromCode(code);
    }

    /// <summary>
    /// Gets the diagnostic code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the short diagnostic title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the message format.
    /// </summary>
    public string MessageFormat { get; }

    /// <summary>
    /// Gets the severity derived from <see cref="Code"/>.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the optional category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Creates a formatted message using <see cref="MessageFormat"/>.
    /// </summary>
    /// <param name="arguments">Message format arguments.</param>
    /// <returns>Formatted diagnostic message.</returns>
    public string FormatMessage(params object?[] arguments)
    {
        return arguments.Length == 0
            ? MessageFormat
            : string.Format(MessageFormat, arguments);
    }
}
