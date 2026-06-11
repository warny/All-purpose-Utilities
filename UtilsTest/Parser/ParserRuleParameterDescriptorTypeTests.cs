using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies conservative preservation of parser rule parameter type metadata.
/// </summary>
[TestClass]
public class ParserRuleParameterDescriptorTypeTests
{
    /// <summary>
    /// Verifies aliases, framework names, and nullable suffixes are preserved separately from names and declarations.
    /// </summary>
    [TestMethod]
    public void FromRule_ParameterDeclarations_PreserveRawTypesNamesAndDeclarations()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            child[int value, System.Int32 frameworkValue, int? nullableValue] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.Single(static item => item.Name == "child");

        ParserRuleInvocationDescriptor descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        Assert.AreEqual(3, descriptor.Parameters.Count);
        AssertParameter(descriptor.Parameters[0], "int", "value", "int value");
        AssertParameter(descriptor.Parameters[1], "System.Int32", "frameworkValue", "System.Int32 frameworkValue");
        AssertParameter(descriptor.Parameters[2], "int?", "nullableValue", "int? nullableValue");
    }

    /// <summary>
    /// Asserts the three independently preserved parameter metadata fields.
    /// </summary>
    /// <param name="descriptor">Parameter descriptor.</param>
    /// <param name="rawType">Expected raw type.</param>
    /// <param name="name">Expected lexical name.</param>
    /// <param name="rawDeclaration">Expected complete declaration.</param>
    private static void AssertParameter(
        ParserRuleParameterDescriptor descriptor,
        string rawType,
        string name,
        string rawDeclaration)
    {
        Assert.AreEqual(rawType, descriptor.RawType);
        Assert.AreEqual(name, descriptor.Name);
        Assert.AreEqual(rawDeclaration, descriptor.RawDeclaration);
    }
}
