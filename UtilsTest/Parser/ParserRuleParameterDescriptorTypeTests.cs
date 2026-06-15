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
        AssertParameter(descriptor.Parameters[0], "int", "value", null, "int value");
        AssertParameter(descriptor.Parameters[1], "System.Int32", "frameworkValue", null, "System.Int32 frameworkValue");
        AssertParameter(descriptor.Parameters[2], "int?", "nullableValue", null, "int? nullableValue");
    }

    /// <summary>
    /// Verifies simple defaults are preserved verbatim after a top-level assignment separator.
    /// </summary>
    [TestMethod]
    public void FromRule_ParameterDefaults_PreserveRawLiteralText()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            child[int value = 42, string text = "hello, world", string equality = "a=b", char separator = '=', int? nullableValue = null, System.Int32 frameworkValue = 42] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.Single(static item => item.Name == "child");

        ParserRuleInvocationDescriptor descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        Assert.AreEqual(6, descriptor.Parameters.Count);
        AssertParameter(descriptor.Parameters[0], "int", "value", "42", "int value = 42");
        AssertParameter(descriptor.Parameters[1], "string", "text", "\"hello, world\"", "string text = \"hello, world\"");
        AssertParameter(descriptor.Parameters[2], "string", "equality", "\"a=b\"", "string equality = \"a=b\"");
        AssertParameter(descriptor.Parameters[3], "char", "separator", "'='", "char separator = '='");
        AssertParameter(descriptor.Parameters[4], "int?", "nullableValue", "null", "int? nullableValue = null");
        AssertParameter(descriptor.Parameters[5], "System.Int32", "frameworkValue", "42", "System.Int32 frameworkValue = 42");
    }

    /// <summary>
    /// Verifies an unsupported declaration shape does not invent typed default metadata.
    /// </summary>
    [TestMethod]
    public void FromRule_UnsupportedParameterDeclaration_LeavesTypeAndDefaultUnresolved()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            child[value = 42] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.Single(static item => item.Name == "child");

        ParserRuleParameterDescriptor descriptor = ParserRuleInvocationDescriptor.FromRule(rule).Parameters.Single();

        Assert.AreEqual("value", descriptor.Name);
        Assert.IsNull(descriptor.RawType);
        Assert.IsNull(descriptor.RawDefaultValue);
        Assert.AreEqual("value = 42", descriptor.RawDeclaration);
    }

    /// <summary>
    /// Asserts the independently preserved parameter metadata fields.
    /// </summary>
    /// <param name="descriptor">Parameter descriptor.</param>
    /// <param name="rawType">Expected raw type.</param>
    /// <param name="name">Expected lexical name.</param>
    /// <param name="rawDefaultValue">Expected raw default metadata.</param>
    /// <param name="rawDeclaration">Expected complete declaration.</param>
    private static void AssertParameter(
        ParserRuleParameterDescriptor descriptor,
        string rawType,
        string name,
        string? rawDefaultValue,
        string rawDeclaration)
    {
        Assert.AreEqual(rawType, descriptor.RawType);
        Assert.AreEqual(name, descriptor.Name);
        Assert.AreEqual(rawDefaultValue, descriptor.RawDefaultValue);
        Assert.AreEqual(rawDeclaration, descriptor.RawDeclaration);
    }
}
