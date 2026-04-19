using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates tokenizer integration through <see cref="CSyntaxTokenParser"/>.
/// </summary>
[TestClass]
public class TokenizerUtilsParserIntegrationTests
{
    /// <summary>
    /// Ensures tokenization returns identifier and operator tokens.
    /// </summary>
    [TestMethod]
    public void Tokenize_ContainsIdentifierAndOperatorTokens()
    {
        var parser = new CSyntaxTokenParser();
        var tokens = parser.Tokenize("value /* skipped */ + 1");

        Assert.IsTrue(tokens.Any(token => token.Text == "value"));
        Assert.IsTrue(tokens.Any(token => token.Text == "+"));
    }
}
