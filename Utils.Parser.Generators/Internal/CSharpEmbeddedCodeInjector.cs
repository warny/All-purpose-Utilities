using System;
using System.Collections.Generic;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Writes transformed embedded C# fragments into generated source with centralized marker, line, and indentation handling.
/// </summary>
internal sealed class CSharpEmbeddedCodeInjector
{
    private const int SpacesPerIndentationLevel = 4;
    private readonly StringBuilder _builder;

    /// <summary>
    /// Initializes an injector bound to one generated-source builder.
    /// </summary>
    /// <param name="builder">Generated-source builder that receives transformed embedded C#.</param>
    public CSharpEmbeddedCodeInjector(StringBuilder builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// Injects a known grammar-level embedded-code region with its generated markers.
    /// </summary>
    /// <param name="code">Transformed embedded C# to inject.</param>
    /// <param name="region">Known generated C# region receiving the code.</param>
    public void InjectRegion(TransformedEmbeddedCode code, CSharpEmbeddedCodeRegion region)
    {
        CSharpEmbeddedCodeRegionDescriptor descriptor = CSharpEmbeddedCodeRegionDescriptor.For(region);
        AppendIndentedLine(descriptor.StartMarker, descriptor.IndentationLevel);
        AppendCodeLines(code, descriptor.IndentationLevel, trim: false);
        AppendIndentedLine(descriptor.EndMarker, descriptor.IndentationLevel);
        if (descriptor.AppendTrailingBlankLine)
        {
            _builder.AppendLine();
        }
    }

    /// <summary>
    /// Injects transformed embedded C# as statements inside a generated method body.
    /// </summary>
    /// <param name="code">Transformed embedded C# to inject.</param>
    /// <param name="indentationLevel">Generated-source indentation level to apply to each emitted line.</param>
    public void InjectMethodBody(TransformedEmbeddedCode code, int indentationLevel)
    {
        AppendCodeLines(code, indentationLevel, trim: true);
    }

    /// <summary>
    /// Injects transformed embedded C# as the expression portion of the current generated statement.
    /// </summary>
    /// <param name="code">Transformed embedded C# expression to append.</param>
    public void InjectExpression(TransformedEmbeddedCode code)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }
        _builder.Append(code.Text.Trim());
    }

    /// <summary>
    /// Injects transformed embedded C# as a complete generated return statement.
    /// </summary>
    /// <param name="code">Transformed embedded C# expression returned by the generated method.</param>
    /// <param name="indentationLevel">Generated-source indentation level of the return statement.</param>
    public void InjectReturnExpression(TransformedEmbeddedCode code, int indentationLevel)
    {
        AppendIndentation(indentationLevel);
        _builder.Append("return ");
        InjectExpression(code);
        _builder.AppendLine(";");
    }

    /// <summary>
    /// Injects transformed embedded C# as an already complete block or statement sequence without wrapping it again.
    /// </summary>
    /// <param name="code">Transformed embedded C# block or statement sequence.</param>
    /// <param name="indentationLevel">Generated-source indentation level to apply to each emitted line.</param>
    public void InjectCompleteFragment(TransformedEmbeddedCode code, int indentationLevel)
    {
        AppendCodeLines(code, indentationLevel, trim: true);
    }

    /// <summary>
    /// Appends all normalized lines from transformed embedded C#.
    /// </summary>
    /// <param name="code">Transformed embedded C# whose text is split into deterministic lines.</param>
    /// <param name="indentationLevel">Generated-source indentation level.</param>
    /// <param name="trim">Whether surrounding whitespace should be trimmed before line splitting.</param>
    private void AppendCodeLines(TransformedEmbeddedCode code, int indentationLevel, bool trim)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }
        string text = trim ? code.Text.Trim() : code.Text;
        foreach (string line in SplitEmbeddedCodeLines(text))
        {
            AppendIndentedLine(line, indentationLevel);
        }
    }

    /// <summary>
    /// Splits embedded C# text after normalizing CRLF and CR newlines to LF.
    /// </summary>
    /// <param name="code">Embedded C# text to split.</param>
    /// <returns>Lines to emit; empty text emits no lines, while a final newline produces a final empty line.</returns>
    private static IEnumerable<string> SplitEmbeddedCodeLines(string code)
    {
        if (code.Length == 0)
        {
            yield break;
        }

        foreach (string line in code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Appends one line using the requested generated-source indentation level.
    /// </summary>
    /// <param name="line">Line content to append after indentation.</param>
    /// <param name="indentationLevel">Generated-source indentation level.</param>
    private void AppendIndentedLine(string line, int indentationLevel)
    {
        AppendIndentation(indentationLevel);
        _builder.AppendLine(line);
    }

    /// <summary>
    /// Appends spaces for the requested generated-source indentation level.
    /// </summary>
    /// <param name="indentationLevel">Generated-source indentation level.</param>
    private void AppendIndentation(int indentationLevel)
    {
        if (indentationLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentationLevel), indentationLevel, "Indentation level cannot be negative.");
        }

        _builder.Append(' ', indentationLevel * SpacesPerIndentationLevel);
    }
}

/// <summary>
/// Known generated C# regions that can receive transformed grammar-level embedded code.
/// </summary>
internal enum CSharpEmbeddedCodeRegion
{
    /// <summary>Parser header region emitted before generated type declarations.</summary>
    ParserHeader,

    /// <summary>Parser members region emitted inside the generated parser execution context.</summary>
    ParserMembers,

    /// <summary>Parser footer region emitted after generated type declarations.</summary>
    ParserFooter,

    /// <summary>Lexer header region emitted before generated type declarations.</summary>
    LexerHeader,

    /// <summary>Lexer members region emitted inside the generated parser execution context.</summary>
    LexerMembers,

    /// <summary>Lexer footer region emitted after generated type declarations.</summary>
    LexerFooter
}

/// <summary>
/// Describes the marker and spacing policy for a known generated C# embedded-code region.
/// </summary>
internal sealed class CSharpEmbeddedCodeRegionDescriptor
{
    /// <summary>
    /// Initializes a generated C# embedded-code region descriptor.
    /// </summary>
    /// <param name="startMarker">Generated source marker written before the embedded code.</param>
    /// <param name="endMarker">Generated source marker written after the embedded code.</param>
    /// <param name="indentationLevel">Indentation level applied to markers and embedded lines.</param>
    /// <param name="appendTrailingBlankLine">Whether a blank line is appended after the closing marker.</param>
    private CSharpEmbeddedCodeRegionDescriptor(string startMarker, string endMarker, int indentationLevel, bool appendTrailingBlankLine)
    {
        StartMarker = startMarker;
        EndMarker = endMarker;
        IndentationLevel = indentationLevel;
        AppendTrailingBlankLine = appendTrailingBlankLine;
    }

    /// <summary>Gets the generated source marker written before the embedded code.</summary>
    public string StartMarker { get; }

    /// <summary>Gets the generated source marker written after the embedded code.</summary>
    public string EndMarker { get; }

    /// <summary>Gets the indentation level applied to markers and embedded lines.</summary>
    public int IndentationLevel { get; }

    /// <summary>Gets whether a blank line is appended after the closing marker.</summary>
    public bool AppendTrailingBlankLine { get; }

    /// <summary>
    /// Gets the descriptor for a known generated C# embedded-code region.
    /// </summary>
    /// <param name="region">Known generated C# region.</param>
    /// <returns>Marker, indentation, and spacing policy for the region.</returns>
    public static CSharpEmbeddedCodeRegionDescriptor For(CSharpEmbeddedCodeRegion region)
    {
        switch (region)
        {
            case CSharpEmbeddedCodeRegion.ParserHeader:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-parser-header>", "// </auto-generated-parser-header>", 0, true);
            case CSharpEmbeddedCodeRegion.ParserMembers:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-parser-members>", "// </auto-generated-parser-members>", 1, true);
            case CSharpEmbeddedCodeRegion.ParserFooter:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-parser-footer>", "// </auto-generated-parser-footer>", 0, true);
            case CSharpEmbeddedCodeRegion.LexerHeader:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-lexer-header>", "// </auto-generated-lexer-header>", 0, true);
            case CSharpEmbeddedCodeRegion.LexerMembers:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-lexer-members>", "// </auto-generated-lexer-members>", 1, true);
            case CSharpEmbeddedCodeRegion.LexerFooter:
                return new CSharpEmbeddedCodeRegionDescriptor("// <auto-generated-lexer-footer>", "// </auto-generated-lexer-footer>", 0, true);
            default:
                throw new ArgumentOutOfRangeException(nameof(region), region, "Unknown embedded-code region.");
        }
    }
}
