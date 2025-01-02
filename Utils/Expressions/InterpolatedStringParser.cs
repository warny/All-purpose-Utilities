using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Expressions;

/// <summary>
/// Parses an interpolated string into its constituent parts, which can be either literals or formatted expressions.
/// </summary>
public partial class InterpolatedStringParser : IEnumerable<IInterpolatedStringPart>
{
	// Regex pattern for parsing the interpolated string.
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
	/// The compiled regex used to parse interpolated strings.
	/// </summary>
	private static readonly Regex ParseFormatStringRegex = CreateParsingRegex();

	private readonly IReadOnlyList<IInterpolatedStringPart> _parts;

	/// <summary>
	/// Initializes a new instance of <see cref="InterpolatedStringParser" />
	/// by parsing the provided interpolated string.
	/// </summary>
	/// <param name="interpolatedString">The interpolated string to parse.</param>
	/// <exception cref="FormatException">Thrown when the string contains unmatched or incorrect braces.</exception>
	public InterpolatedStringParser(string interpolatedString)
	{
		ArgumentNullException.ThrowIfNull(interpolatedString, nameof(interpolatedString));

		var parts = new List<IInterpolatedStringPart>();

		foreach (Match match in ParseFormatStringRegex.Matches(interpolatedString))
		{
			if (match.Groups["error"].Success)
			{
				throw new FormatException($"Incorrect format string: '{match.Groups["error"].Value}' was unexpected at index {match.Groups["error"].Index}.");
			}

			if (match.Groups["text"].Success)
			{
				string text = match.Groups["text"].Value;
				if (parts.LastOrDefault() is LiteralPart lastLiteral)
				{
					lastLiteral.Append(text);
				}
				else
				{
					parts.Add(new LiteralPart(text));
				}
			}
			else if (match.Groups["expression"].Success)
			{
				string expression = match.Groups["expression"].Value;
				string? format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
				int? alignment = match.Groups["alignment"].Success ? int.Parse(match.Groups["alignment"].Value) : null;

				parts.Add(new FormattedPart(expression)
				{
					Format = format,
					Alignment = alignment
				});
			}
		}

		_parts = parts.AsReadOnly();
	}

	/// <summary>
	/// Returns an enumerator that iterates through the parsed string parts.
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
	/// Initializes a new instance of <see cref="LiteralPart" /> with the specified text.
	/// </summary>
	/// <param name="value">The text content of the literal part.</param>
	public LiteralPart(string value)
	{
		ArgumentNullException.ThrowIfNull(value, nameof(value));
		_value.Append(value);
	}

	/// <summary>
	/// Appends additional text to this literal part.
	/// </summary>
	/// <param name="value">The text to append.</param>
	public void Append(string value) => _value.Append(value);
}

/// <summary>
/// Represents a formatted part of an interpolated string, containing an expression and optional format/alignment specifications.
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
	/// Initializes a new instance of <see cref="FormattedPart" /> with the specified expression text.
	/// </summary>
	/// <param name="expressionText">The text of the expression within the interpolated string.</param>
	public FormattedPart(string expressionText)
	{
		ArgumentNullException.ThrowIfNull(expressionText, nameof(expressionText));
		ExpressionText = expressionText;
	}
}
