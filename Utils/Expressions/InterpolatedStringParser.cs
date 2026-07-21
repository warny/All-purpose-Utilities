using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Expressions;

/// <summary>
/// Parses an interpolated string into its constituent parts, which can be either literals or formatted expressions.
/// </summary>
public partial class InterpolatedStringParser : IEnumerable<IInterpolatedStringPart>
{
    /// <summary>
    /// A compiled regex pattern for parsing the interpolated string syntax. 
    /// This regex is generated at compile time via the <see cref="GeneratedRegexAttribute"/>.
    /// </summary>
    [GeneratedRegex(@"
            (
                \{(?<text>\{)                 # Escaped opening brace '{{'
                |
                \}(?<text>\})                 # Escaped closing brace '}}'
                |
                \{\s*(?<expression>          # Expression within braces
                    (
                        (?>\(((?<p>\()|(?<-p>\))|[^()]*)*\))(?(p)(?!)) # Match nested parentheses
                        | [^():,]*?            # Match non-parentheses characters
                    )
                )(,(?<alignment>[+-]?\d+))?   # Optional alignment specifier
                (:(?<format>.+?))?            # Optional format specifier
                \s*\}
                |
                (?<text>[^{}]+)               # Plain text
                |
                (?<error>[{}])                # Unexpected or unmatched braces
            )
        ", RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex CreateParsingRegex();

    /// <summary>
    /// The compiled regex used to parse format-like interpolated strings.
    /// </summary>
    private static readonly Regex _parseFormatStringRegex = CreateParsingRegex();

    /// <summary>
    /// A read-only list of parsed string parts, which can be literal or formatted.
    /// </summary>
    private readonly IReadOnlyList<IInterpolatedStringPart> _parts;

    /// <summary>
    /// Initializes a new instance of <see cref="InterpolatedStringParser"/> by parsing
    /// the provided interpolated string.
    /// </summary>
    /// <param name="interpolatedString">The interpolated string to parse.</param>
    /// <exception cref="FormatException">Thrown when the string contains unmatched or incorrect braces.</exception>
    public InterpolatedStringParser(string interpolatedString)
    {
        ArgumentNullException.ThrowIfNull(interpolatedString);

        var parts = new List<IInterpolatedStringPart>();
        int expectedIndex = 0; // tracks the next expected match start position (#25)

        foreach (Match match in _parseFormatStringRegex.Matches(interpolatedString))
        {
            // Verify that matches are contiguous and cover the entire input (#25).
            // Any gap between the previous match's end and this match's start means
            // characters were silently skipped.
            if (match.Index != expectedIndex)
            {
                throw new FormatException(
                    $"Unrecognized characters at index {expectedIndex} in the interpolated string.");
            }
            expectedIndex = match.Index + match.Length;
            // If there's an unexpected brace or a mismatch, throw
            if (match.Groups["error"].Success)
            {
                throw new FormatException(
                    $"Incorrect format string: '{match.Groups["error"].Value}' was unexpected at index {match.Groups["error"].Index}."
                );
            }

            // Plain text segment
            if (match.Groups["text"].Success)
            {
                string text = match.Groups["text"].Value;

                if (parts.LastOrDefault() is LiteralPart lastLiteral)
                {
                    // Append consecutive text to the same literal part
                    lastLiteral.Append(text);
                }
                else
                {
                    // Create a new literal part
                    parts.Add(new LiteralPart(text));
                }
            }
            // Expression segment (with optional alignment and format)
            else if (match.Groups["expression"].Success)
            {
                string expression = match.Groups["expression"].Value;
                string? format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                int? alignment = null;
                if (match.Groups["alignment"].Success)
                {
                    string alignmentText = match.Groups["alignment"].Value;
                    // Use TryParse with invariant culture to avoid exposing OverflowException
                    // for out-of-range values or FormatException from incidental int.Parse (#24).
                    if (!int.TryParse(alignmentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsedAlignment))
                    {
                        throw new FormatException(
                            $"Invalid alignment specifier '{alignmentText}' at index {match.Groups["alignment"].Index}: " +
                            "the value must be a valid 32-bit integer.");
                    }
                    alignment = parsedAlignment;
                }

                parts.Add(new FormattedPart(expression)
                {
                    Format = format,
                    Alignment = alignment
                });
            }
        }

        // Verify the last match reached the end of the input string (#25).
        if (expectedIndex != interpolatedString.Length)
        {
            throw new FormatException(
                $"Unrecognized characters at index {expectedIndex} in the interpolated string.");
        }

        // Finalize the parts list
        _parts = parts.AsReadOnly();
    }

    /// <summary>
    /// Returns an enumerator over the parsed string parts.
    /// </summary>
    public IEnumerator<IInterpolatedStringPart> GetEnumerator() => _parts.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a part of an interpolated string, which can be either literal text or a formatted expression.
/// </summary>
public interface IInterpolatedStringPart { }

/// <summary>
/// Represents a literal (non-interpolated) part of a string.
/// </summary>
/// <remarks>
/// Instances are constructed and fully assembled by <see cref="InterpolatedStringParser"/>
/// before being published. The <see cref="Text"/> property is immutable from the caller's
/// perspective; mutation is only possible through the <c>internal</c> <see cref="Append"/>
/// method which is inaccessible outside this assembly (#26).
/// </remarks>
public class LiteralPart : IInterpolatedStringPart
{
    private readonly StringBuilder _value = new();

    /// <summary>
    /// Gets the text content of this literal part.
    /// </summary>
    public string Text => _value.ToString();

    /// <summary>
    /// Gets the text length of this literal part.
    /// </summary>
    public int Length => _value.Length;

    /// <summary>
    /// Initializes a new instance of <see cref="LiteralPart"/> with the specified text.
    /// </summary>
    /// <param name="value">The text content of the literal part.</param>
    public LiteralPart(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value.Append(value);
    }

    /// <summary>
    /// Appends additional text to this literal part during construction only.
    /// This method is intentionally <c>internal</c> so external callers cannot mutate
    /// a part after the parser has published it (#26).
    /// </summary>
    /// <param name="value">The text to append.</param>
    internal void Append(string value)
    {
        _value.Append(value);
    }
}

/// <summary>
/// Represents a formatted part of an interpolated string, containing an expression
/// and optional format/alignment specifications.
/// </summary>
public class FormattedPart : IInterpolatedStringPart
{
    /// <summary>
    /// Gets the expression text of this formatted part.
    /// </summary>
    public string ExpressionText { get; }

    /// <summary>
    /// Gets or sets the format string for this part (if any).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Gets or sets the alignment specification for this part (if any).
    /// </summary>
    public int? Alignment { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="FormattedPart"/> with the specified expression text.
    /// </summary>
    /// <param name="expressionText">The text of the expression within the interpolated string.</param>
    public FormattedPart(string expressionText)
    {
        ArgumentNullException.ThrowIfNull(expressionText);
        ExpressionText = expressionText;
    }
}
