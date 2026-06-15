using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Antlr4.Common;
using Utils.Parser.Antlr4.Common.Diagnostics;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4PrequelValidatorTests
{
    [TestMethod]
    public void Validate_Imports_EmitsOneDiagnosticPerImport()
    {
        var model = new Antlr4PrequelModel(
            null,
            new List<Antlr4ImportInfo>
            {
                new("A", null),
                new("B", "BAlias"),
            },
            new List<Antlr4ActionInfo>(),
            Antlr4NameSet.Create([]),
            Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]),
            HasTokensBlock: false,
            HasChannelsBlock: false);

        var result = Antlr4PrequelValidator.Validate(model);

        CollectionAssert.AreEqual(
            new[] { Antlr4PrequelDiagnosticCode.ImportParsedButNotResolved, Antlr4PrequelDiagnosticCode.ImportParsedButNotResolved },
            result.Diagnostics.Select(static d => d.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "A", "B" }, result.Diagnostics.Select(static d => d.Subject).ToArray());
    }

    [TestMethod]
    public void Validate_TokensBlock_EmitsSingleDiagnostic()
    {
        var model = new Antlr4PrequelModel(null, [], [], Antlr4NameSet.Create(["ID"]), Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]),
            HasTokensBlock: true,
            HasChannelsBlock: false);

        var result = Antlr4PrequelValidator.Validate(model);

        Assert.AreEqual(1, result.Diagnostics.Count);
        Assert.AreEqual(Antlr4PrequelDiagnosticCode.TokensBlockIgnored, result.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Validate_ChannelsBlock_EmitsSingleDiagnosticExcludingDefaults()
    {
        var withoutChannelsBlock = new Antlr4PrequelModel(null, [], [], Antlr4NameSet.Create([]), Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]),
            HasTokensBlock: false,
            HasChannelsBlock: false);
        var withExtra = new Antlr4PrequelModel(null, [], [], Antlr4NameSet.Create([]), Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN", "COMMENTS"]), HasTokensBlock: false, HasChannelsBlock: true);

        var noBlockResult = Antlr4PrequelValidator.Validate(withoutChannelsBlock);
        var extraResult = Antlr4PrequelValidator.Validate(withExtra);

        Assert.AreEqual(0, noBlockResult.Diagnostics.Count);
        Assert.AreEqual(1, extraResult.Diagnostics.Count);
        Assert.AreEqual(Antlr4PrequelDiagnosticCode.ChannelsBlockIgnored, extraResult.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Validate_Actions_EmitsOneDiagnosticPerAction()
    {
        var model = new Antlr4PrequelModel(
            null,
            [],
            [
                new Antlr4ActionInfo("header", "using System;", null),
                new Antlr4ActionInfo("members", "int _p;", "parser"),
            ],
            Antlr4NameSet.Create([]),
            Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]),
            HasTokensBlock: false,
            HasChannelsBlock: false);

        var result = Antlr4PrequelValidator.Validate(model);

        CollectionAssert.AreEqual(
            new[] { Antlr4PrequelDiagnosticCode.GrammarActionIgnored, Antlr4PrequelDiagnosticCode.GrammarActionIgnored },
            result.Diagnostics.Select(static d => d.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "@header", "@parser::members" }, result.Diagnostics.Select(static d => d.Subject).ToArray());
    }



    [TestMethod]
    public void NameSetCreate_ReturnsReadOnlyCollectionContract()
    {
        IReadOnlyCollection<string> names = Antlr4NameSet.Create(["ID"]);

        Assert.AreEqual(1, names.Count);
        Assert.IsFalse(names is ISet<string>);
    }

    [TestMethod]
    public void Validate_ChannelsBlockWithOnlyDefaults_EmitsDiagnostic()
    {
        var model = new Antlr4PrequelModel(null, [], [], Antlr4NameSet.Create([]), Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]), HasTokensBlock: false, HasChannelsBlock: true);

        var result = Antlr4PrequelValidator.Validate(model);

        Assert.AreEqual(1, result.Diagnostics.Count);
        Assert.AreEqual(Antlr4PrequelDiagnosticCode.ChannelsBlockIgnored, result.Diagnostics[0].Code);
    }
    [TestMethod]
    public void Validate_EmptyPrequel_EmitsNoDiagnostics()
    {
        var model = new Antlr4PrequelModel(null, [], [], Antlr4NameSet.Create([]), Antlr4NameSet.Create(["DEFAULT_CHANNEL", "HIDDEN"]),
            HasTokensBlock: false,
            HasChannelsBlock: false);

        var result = Antlr4PrequelValidator.Validate(model);

        Assert.AreEqual(0, result.Diagnostics.Count);
    }
}
