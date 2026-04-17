using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates tokenizer integration through <see cref="CStyleTokenParser"/>.
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
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("value /* skipped */ + 1");

        Assert.IsTrue(tokens.Any(token => token.Text == "value"));
        Assert.IsTrue(tokens.Any(token => token.Text == "+"));
    }
}
