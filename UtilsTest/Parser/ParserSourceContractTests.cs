using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser source-position contracts that are exposed through runtime tokens.
/// </summary>
[TestClass]
public class ParserSourceContractTests
{
    /// <summary>
    /// Ensures that a token keeps the exact runtime source span supplied by the caller.
    /// </summary>
    [TestMethod]
    public void Token_PreservesRuntimeSourceSpanValues()
    {
        var span = new SourceSpan(12, 5, 3, 7, "sample.apu");
        var token = new Token(span, "Identifier", "DEFAULT_MODE", "DEFAULT_CHANNEL", "value");

        Assert.AreSame(span, token.Span);
        Assert.AreEqual(12, token.Span.Position);
        Assert.AreEqual(5, token.Span.Length);
        Assert.AreEqual(3, token.Span.Line);
        Assert.AreEqual(7, token.Span.Column);
        Assert.AreEqual("sample.apu", token.Span.FilePath);
    }
}
