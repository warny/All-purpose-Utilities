namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Identifies the preparation path that will consume embedded ANTLR source code.
/// </summary>
internal enum EmbeddedCodeTarget
{
    /// <summary>Prepare embedded code as C# source that a Roslyn source generator can emit.</summary>
    SourceGeneratorCSharp,

    /// <summary>Prepare embedded code through an explicitly configured runtime-inline expression compiler.</summary>
    RuntimeInlineExpression
}
