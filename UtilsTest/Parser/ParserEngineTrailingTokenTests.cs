using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;
using static UtilsTest.Parser.TestInfrastructure.ParserEngineTestHelpers;

namespace UtilsTest.Parser;

/// <summary>
/// Tests that enforce full-input consumption and trailing token rejection.
/// </summary>
[TestClass]
public class ParserEngineTrailingTokenTests
{
    // ═══════════════════════════════════════════════════════════════
    // Trailing-token rejection (P1 review fix)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parse_TrailingTokens_ReturnsErrorNode()
    {
        // "2+3" is valid, but the extra "???" tokens should not be accepted.
        // The lexer will emit ERROR tokens for '?', so the parse must fail.
        var result = Parse("2+3 ???");
        Assert.IsInstanceOfType<ErrorNode>(result,
            "A parse with trailing unrecognized tokens must return ErrorNode, not a valid tree.");
    }

    [TestMethod]
    public void Parse_ValidFullInput_ReturnsParserNode()
    {
        // Regression: ensure the trailing-token check does not break valid inputs.
        var result = Parse("2+3");
        Assert.IsInstanceOfType<ParserNode>(result);
    }



}
