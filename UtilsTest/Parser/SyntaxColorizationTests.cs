using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Utils.Parser.Bootstrap;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates syntax colorization descriptor parsing and runtime color lookup behavior.
/// </summary>
[TestClass]
public class SyntaxColorizationTests
{
    [TestMethod]
    public void DescriptorParser_SupportsMultipleFileAndStringSyntaxExtensions()
    {
        var descriptor = SyntaxColorizationDescriptorParser.Parse("""
            @FileExtension : ".cscript"
            @FileExtension : ".sharpscript"
            @StringSyntaxExtension : "SQL" // comment
            # comment line
            Number :
                NUMBER | DECIMAL
            """);

        CollectionAssert.AreEquivalent(new[] { ".cscript", ".sharpscript" }, descriptor.FileExtensions);
        CollectionAssert.AreEquivalent(new[] { "SQL" }, descriptor.StringSyntaxExtensions);
        Assert.AreEqual(1, descriptor.Entries.Count);
        Assert.AreEqual("Number", descriptor.Entries[0].ClassificationName);
        CollectionAssert.AreEquivalent(new[] { "NUMBER", "DECIMAL" }, descriptor.Entries[0].Rules);
    }


    [TestMethod]
    public void ColorizationEmitter_MapsBuiltInClassificationsToVisualStudioNames()
    {
        var descriptor = SyntaxColorizationDescriptorParser.Parse("""
            @FileExtension : ".cscript"
            Keyword :
                FOR | WHILE
            Number :
                NUMBER | DECIMAL
            String :
                QUOTED_STRING
            Operator :
                PLUS | MINUS
            "Raw text" :
                TEXT
            """);

        string source = SyntaxColorizationEmitter.Emit(descriptor, "MyApp.Syntax", "ScriptColorization", "script.syntaxcolor");

        StringAssert.Contains(source, "[\"FOR\"] = \"Keyword\"");
        StringAssert.Contains(source, "[\"NUMBER\"] = \"Number\"");
        StringAssert.Contains(source, "[\"QUOTED_STRING\"] = \"String\"");
        StringAssert.Contains(source, "[\"PLUS\"] = \"Operator\"");
        StringAssert.Contains(source, "[\"TEXT\"] = \"Plain Text\"");
    }

    [TestMethod]
    public void ConventionSyntaxColorisation_PrioritizesMostSpecificRule()
    {
        var colorisation = new ConventionSyntaxColorisation(
            new[] { ".demo" },
            new[] { "Demo" },
            new[] { "STATEMENT" },
            new[] { "NUMBER" },
            new[] { "STRING" });

        string? classification = colorisation.GetClassification(new[] { "STATEMENT", "NUMBER" });

        Assert.AreEqual(VisualStudioClassificationNames.Number, classification);
    }

    [TestMethod]
    public void ConventionSyntaxColorisation_ReturnsNullForUnknownRule()
    {
        var colorisation = new ConventionSyntaxColorisation(
            new[] { ".demo" },
            new[] { "Demo" },
            new[] { "STATEMENT" },
            new[] { "NUMBER" },
            new[] { "STRING" });

        string? classification = colorisation.GetClassification("IDENTIFIER");

        Assert.IsNull(classification);
    }

    [TestMethod]
    public void VisualStudioClassificationNames_ContainsExtendedStandardNames()
    {
        Assert.AreEqual("Comment", VisualStudioClassificationNames.Comment);
        Assert.AreEqual("Identifier", VisualStudioClassificationNames.Identifier);
        Assert.AreEqual("Type", VisualStudioClassificationNames.Type);
        Assert.AreEqual("Class Name", VisualStudioClassificationNames.ClassName);
        Assert.AreEqual("Struct Name", VisualStudioClassificationNames.StructName);
        Assert.AreEqual("Interface Name", VisualStudioClassificationNames.InterfaceName);
        Assert.AreEqual("Enum Name", VisualStudioClassificationNames.EnumName);
        Assert.AreEqual("Namespace Name", VisualStudioClassificationNames.NamespaceName);
        Assert.AreEqual("Method Name", VisualStudioClassificationNames.MethodName);
        Assert.AreEqual("Extension Method Name", VisualStudioClassificationNames.ExtensionMethodName);
        Assert.AreEqual("Property Name", VisualStudioClassificationNames.PropertyName);
        Assert.AreEqual("Field Name", VisualStudioClassificationNames.FieldName);
        Assert.AreEqual("Parameter Name", VisualStudioClassificationNames.ParameterName);
        Assert.AreEqual("Punctuation", VisualStudioClassificationNames.Punctuation);
        Assert.AreEqual("Tag Name", VisualStudioClassificationNames.TagName);
        Assert.AreEqual("Tag Delimiter", VisualStudioClassificationNames.TagDelimiter);
        Assert.AreEqual("String Escape Character", VisualStudioClassificationNames.CharacterEscape);
    }

    [TestMethod]
    public void SyntaxColorisationGrammar_ParseLargeDescriptorWithoutRecursiveTraversal()
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("@FileExtension : \".demo\"");

        for (int i = 0; i < 4000; i++)
        {
            sourceBuilder.Append("Rule").Append(i).AppendLine(" : TOKEN");
        }

        SyntaxColorisationDocument document = SyntaxColorisationGrammar.Parse(sourceBuilder.ToString());

        Assert.AreEqual(1, document.FileExtensions.Count);
        Assert.AreEqual(4000, document.Sections.Count);
    }
}
