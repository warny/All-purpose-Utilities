using System.IO;
using System.Reflection;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;

namespace UtilsTest.Parser;

/// <summary>
/// Provides the Exp grammar as a resolved <see cref="ParserDefinition"/> by compiling
/// the ANTLR4 source file <c>Exp.g4</c> embedded in this assembly.
/// <para>
/// This validates that <see cref="Antlr4GrammarConverter"/> (and the broader
/// Utils.Parser library) can bootstrap itself: a grammar defined in ANTLR4 syntax is
/// compiled by the library's own parser into an operational grammar definition.
/// </para>
/// </summary>
internal static class ExpGrammar
{
    /// <summary>
    /// Loads <c>Exp.g4</c> from the embedded resources and compiles it into a
    /// resolved <see cref="ParserDefinition"/> via <see cref="Antlr4GrammarConverter.Parse"/>.
    /// </summary>
    /// <returns>Fully resolved grammar definition for the Exp language.</returns>
    public static ParserDefinition Build()
    {
        var grammarText = ReadEmbeddedGrammar();
        return Antlr4GrammarConverter.Parse(grammarText);
    }

    /// <summary>
    /// Reads the contents of the <c>Exp.g4</c> embedded resource and returns it as a string.
    /// </summary>
    private static string ReadEmbeddedGrammar()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("UtilsTest.Parser.Exp.g4")
            ?? throw new InvalidOperationException(
                "Embedded resource 'UtilsTest.Parser.Exp.g4' not found. " +
                "Ensure the file is included as <EmbeddedResource> in UtilsTest.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
