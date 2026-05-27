using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;

namespace UtilsTest.Parser;

[TestClass]
public class SyntaxColorisationGrammarPublicApiTests
{
    [TestMethod]
    public void Parse_ReturnsReadOnlyCollectionContracts()
    {
        const string descriptor = """
            @FileExtension: .g4
            @StringSyntaxExtension: ANTLR4
            Keyword: grammar|parserRuleSpec
            """;

        SyntaxColorisationDocument document = SyntaxColorisationGrammar.Parse(descriptor);

        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.FileExtensions))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.StringSyntaxExtensions))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<SyntaxColorisationSection>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.Sections))!.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationSection).GetProperty(nameof(SyntaxColorisationSection.Rules))!.PropertyType);
    }

    [TestMethod]
    public void Parse_PreservesDocumentContentWithReadOnlyContracts()
    {
        const string descriptor = """
            @FileExtension: .g4
            @StringSyntaxExtension: ANTLR4
            Keyword: grammar|parserRuleSpec
            """;

        SyntaxColorisationDocument document = SyntaxColorisationGrammar.Parse(descriptor);

        CollectionAssert.AreEqual(new[] { ".g4" }, document.FileExtensions.ToArray());
        CollectionAssert.AreEqual(new[] { "ANTLR4" }, document.StringSyntaxExtensions.ToArray());
        Assert.AreEqual(1, document.Sections.Count);
        Assert.AreEqual("Keyword", document.Sections[0].Classification);
        CollectionAssert.AreEqual(new[] { "grammar", "parserRuleSpec" }, document.Sections[0].Rules.ToArray());
    }
}
