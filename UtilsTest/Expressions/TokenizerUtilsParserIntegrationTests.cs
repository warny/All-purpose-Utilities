using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Expressions.Builders;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates the tokenizer integration that relies on the Utils.Parser-based grammar.
/// </summary>
[TestClass]
public class TokenizerUtilsParserIntegrationTests
{
    /// <summary>
    /// Ensures that grammar-based tokenization can parse mixed identifier/operator input.
    /// </summary>
    [TestMethod]
    public void ReadTokenParsesIdentifierAndOperatorSequence()
    {
        var tokenizer = new Tokenizer("value /* skipped */ + 1", new CStyleBuilder());

        Assert.AreEqual("value", tokenizer.ReadToken());

        bool foundPlus = false;
        string? token;
        while ((token = tokenizer.ReadToken()) is not null)
        {
            if (token == "+")
            {
                foundPlus = true;
                break;
            }
        }

        Assert.IsTrue(foundPlus);
    }

    /// <summary>
    /// Ensures that the tokenizer still exposes transformed string content for escaped strings.
    /// </summary>
    [TestMethod]
    public void ReadTokenPopulatesDefineStringForEscapedStringLiteral()
    {
        var tokenizer = new Tokenizer("\"va\\\"lue\"", new CStyleBuilder());

        Assert.AreEqual("\"va\\\"lue\"", tokenizer.ReadToken());
        Assert.AreEqual("va\"lue", tokenizer.DefineString);
    }
}
