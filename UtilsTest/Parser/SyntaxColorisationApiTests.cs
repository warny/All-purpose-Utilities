using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.Parser.Bootstrap;

namespace UtilsTest.Parser;

/// <summary>
/// API contract tests for syntax colorisation descriptor DTOs.
/// </summary>
[TestClass]
public class SyntaxColorisationApiTests
{
    /// <summary>
    /// Ensures parsed descriptor collections are exposed through read-only API contracts.
    /// </summary>
    [TestMethod]
    public void PublicContracts_UseIReadOnlyList()
    {
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.FileExtensions))?.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.StringSyntaxExtensions))?.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<SyntaxColorisationSection>), typeof(SyntaxColorisationDocument).GetProperty(nameof(SyntaxColorisationDocument.Sections))?.PropertyType);
        Assert.AreEqual(typeof(IReadOnlyList<string>), typeof(SyntaxColorisationSection).GetProperty(nameof(SyntaxColorisationSection.Rules))?.PropertyType);
    }

    /// <summary>
    /// Ensures returned read-only collection wrappers reject mutation attempts at runtime.
    /// </summary>
    [TestMethod]
    public void Parse_ReturnedCollections_RejectRuntimeMutation()
    {
        SyntaxColorisationDocument document = SyntaxColorisationGrammar.Parse(
            "@FileExtension: .g4\n" +
            "identifier: IDENT");

        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)document.FileExtensions).Add(".antlr"));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<SyntaxColorisationSection>)document.Sections).Add(new SyntaxColorisationSection("x")));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)document.Sections[0].Rules).Add("foo"));
    }
}
