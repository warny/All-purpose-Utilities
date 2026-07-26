using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Antlr4.Common.Composition;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>Locks the shared grammar graph and effective-rule composition semantics.</summary>
[TestClass]
public sealed class GrammarImportCompositionPlannerTests
{
    /// <summary>Verifies a standalone grammar retains its local root and rule.</summary>
    [TestMethod]
    public void Build_NoImport_ContainsOnlyEntryDeclarations()
    {
        Source entry = Create("Entry", "/entry.g4", rules: [Parser("start")]);
        GrammarImportCompositionPlan plan = Build(entry, entry);
        Assert.AreEqual("start", ((RulePayload)plan.RootRulePayload!).Name);
        CollectionAssert.AreEqual(new[] { "start" }, plan.EffectiveRules.Select(rule => rule.Rule.Name).ToArray());
    }

    /// <summary>Verifies direct and transitive dependencies follow declaration-order depth-first traversal.</summary>
    [TestMethod]
    public void Build_TransitiveImport_UsesSourceOrderDepthFirst()
    {
        Source leaf = Create("Leaf", "/leaf.g4", rules: [Parser("leaf")]);
        Source middle = Create("Middle", "/middle.g4", [Import("Leaf")], [Parser("middle")]);
        Source entry = Create("Entry", "/entry.g4", [Import("Middle")], [Parser("start")]);
        GrammarImportCompositionPlan plan = Build(entry, leaf, middle, entry);
        CollectionAssert.AreEqual(new[] { "Entry", "Middle", "Leaf" }, plan.Grammars.Select(source => source.Identity.DeclaredName).ToArray());
        CollectionAssert.AreEqual(new[] { "start", "middle", "leaf" }, plan.EffectiveRules.Select(rule => rule.Rule.Name).ToArray());
    }

    /// <summary>Verifies a diamond deduplicates the same structural declaration while retaining both paths as edges.</summary>
    [TestMethod]
    public void Build_Diamond_DeduplicatesSameDeclaration()
    {
        Source leaf = Create("Leaf", "/leaf.g4", rules: [Parser("shared")]);
        Source left = Create("Left", "/left.g4", [Import("Leaf")]);
        Source right = Create("Right", "/right.g4", [Import("Leaf")]);
        Source entry = Create("Entry", "/entry.g4", [Import("Left"), Import("Right")]);
        GrammarImportCompositionPlan plan = Build(entry, right, leaf, left);
        Assert.AreEqual(1, plan.EffectiveRules.Count(rule => rule.Rule.Name == "shared"));
        Assert.AreEqual(4, plan.Dependencies.Count);
    }

    /// <summary>Verifies simple and long cycles are represented with deterministic closed paths.</summary>
    [TestMethod]
    public void Build_Cycles_ExposeDeterministicPaths()
    {
        Source a = Create("A", "/a.g4", [Import("B")]);
        Source b = Create("B", "/b.g4", [Import("C")]);
        Source c = Create("C", "/c.g4", [Import("A")]);
        GrammarImportCompositionPlan plan = Build(a, c, b, a);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "A" }, plan.Cycles.Single().Path.Select(identity => identity.DeclaredName).ToArray());
    }

    /// <summary>Verifies missing, ambiguous, duplicate, and aliased edges remain explicit provenance.</summary>
    [TestMethod]
    public void Build_DependencyStates_AreExplicit()
    {
        Source first = Create("Shared", "/one.g4");
        Source second = Create("Shared", "/two.g4");
        Source entry = Create("Entry", "/entry.g4", [Import("Missing"), Import("Shared", "Alias"), Import("Missing")]);
        GrammarImportCompositionPlan plan = Build(entry, second, first);
        Assert.AreEqual(2, plan.MissingDependencies.Count);
        Assert.AreEqual(1, plan.AmbiguousDependencies.Count);
        Assert.AreEqual("Alias", plan.AmbiguousDependencies[0].Edge.DeclaredDependency.Alias);
    }

    /// <summary>Verifies local declarations mask every imported declaration with the same unqualified name.</summary>
    [TestMethod]
    public void Build_LocalRule_MasksImportedRules()
    {
        Source one = Create("One", "/one.g4", rules: [Parser("item")]);
        Source two = Create("Two", "/two.g4", rules: [Lexer("item")]);
        Source entry = Create("Entry", "/entry.g4", [Import("One"), Import("Two")], [Parser("item")]);
        GrammarImportCompositionPlan plan = Build(entry, one, two);
        Assert.AreEqual(1, plan.EffectiveRules.Count(rule => rule.Rule.Name == "item"));
        Assert.AreEqual(2, plan.MaskedRules.Count);
        Assert.AreEqual(0, plan.Collisions.Count);
    }

    /// <summary>Verifies distinct direct and transitive imported declarations collide instead of first-wins selection.</summary>
    [TestMethod]
    public void Build_ImportedCollision_DoesNotChooseArbitrarily()
    {
        Source transitive = Create("Transitive", "/transitive.g4", rules: [Parser("item")]);
        Source direct = Create("Direct", "/direct.g4", [Import("Transitive")], [Parser("item")]);
        Source other = Create("Other", "/other.g4", rules: [Parser("item")]);
        Source entry = Create("Entry", "/entry.g4", [Import("Direct"), Import("Other")]);
        GrammarImportCompositionPlan plan = Build(entry, other, direct, transitive);
        Assert.AreEqual(0, plan.EffectiveRules.Count(rule => rule.Rule.Name == "item"));
        Assert.AreEqual(3, plan.Collisions.Single().Candidates.Count);
    }

    /// <summary>Verifies parser/lexer and lexer-mode differences remain distinct collision candidates.</summary>
    [TestMethod]
    public void Build_DomainAndModeCollisions_RetainStructuralIdentity()
    {
        Source one = Create("One", "/one.g4", rules: [Parser("same"), Lexer("modeRule", "ONE")]);
        Source two = Create("Two", "/two.g4", rules: [Lexer("same"), Lexer("modeRule", "TWO")]);
        Source entry = Create("Entry", "/entry.g4", [Import("One"), Import("Two")]);
        GrammarImportCompositionPlan plan = Build(entry, two, one);
        Assert.AreEqual(2, plan.Collisions.Count);
        Assert.AreEqual(2, plan.Collisions.Single(collision => collision.RuleName == "same").Candidates.Select(candidate => candidate.Rule.Domain).Distinct().Count());
        Assert.AreEqual(2, plan.Collisions.Single(collision => collision.RuleName == "modeRule").Candidates.Select(candidate => candidate.Rule.LexerMode).Distinct().Count());
    }

    /// <summary>Verifies token vocabularies contribute lexer rules but not parser rules.</summary>
    [TestMethod]
    public void Build_TokenVocab_ContributesLexerOnly()
    {
        Source vocabulary = Create("Vocabulary", "/vocabulary.g4", rules: [Lexer("ID"), Parser("hidden")]);
        Source entry = Create("Entry", "/entry.g4", [TokenVocab("Vocabulary")], [Parser("start")]);
        GrammarImportCompositionPlan plan = Build(entry, vocabulary);
        CollectionAssert.AreEqual(new[] { "start", "ID" }, plan.EffectiveRules.Select(rule => rule.Rule.Name).ToArray());
        Assert.AreEqual("ID", plan.TokenVocabLexerRules.Single().Rule.Name);
    }

    /// <summary>Verifies lexer-only visibility propagates through declared full imports at every descendant level.</summary>
    [TestMethod]
    public void Build_TokenVocabFullImportDescendants_ExposeEffectiveLexerOnlyEdges()
    {
        Source leaf = Create("Leaf", "/leaf.g4", rules: [Lexer("LEAF"), Parser("hiddenLeaf")]);
        Source middle = Create("Middle", "/middle.g4", [Import("Leaf")], [Lexer("MIDDLE"), Parser("hiddenMiddle")]);
        Source vocabulary = Create("Vocabulary", "/vocabulary.g4", [Import("Middle")], [Lexer("ROOT_TOKEN"), Parser("hiddenRoot")]);
        Source entry = Create("Entry", "/entry.g4", [TokenVocab("Vocabulary")], [Parser("start")]);

        GrammarImportCompositionPlan plan = Build(entry, leaf, vocabulary, middle);

        CollectionAssert.AreEqual(
            new[] { GrammarDependencyKind.TokenVocab, GrammarDependencyKind.TokenVocab, GrammarDependencyKind.TokenVocab },
            plan.Dependencies.Select(edge => edge.EffectiveKind).ToArray());
        CollectionAssert.AreEqual(
            new[] { GrammarDependencyKind.TokenVocab, GrammarDependencyKind.FullImport, GrammarDependencyKind.FullImport },
            plan.Dependencies.Select(edge => edge.DeclaredDependency.Kind).ToArray());
        Assert.IsFalse(plan.EffectiveRules.Any(rule => rule.Rule.Domain == GrammarRuleDomain.Parser && rule.Origin != entry.Identity));
    }

    /// <summary>Verifies full import upgrades token-vocabulary visibility for the same grammar.</summary>
    [TestMethod]
    public void Build_FullImportAndTokenVocab_FullVisibilityWins()
    {
        Source shared = Create("Shared", "/shared.g4", rules: [Lexer("ID"), Parser("item")]);
        Source entry = Create("Entry", "/entry.g4", [TokenVocab("Shared"), Import("Shared")]);
        GrammarImportCompositionPlan plan = Build(entry, shared);
        CollectionAssert.AreEqual(new[] { "ID", "item" }, plan.EffectiveRules.Select(rule => rule.Rule.Name).ToArray());
    }

    /// <summary>Verifies a later full-import path upgrades descendant edge visibility without obscuring the earlier effective path.</summary>
    [TestMethod]
    public void Build_TokenVocabThenFullImport_ExposesBothEffectiveTraversalKinds()
    {
        Source leaf = Create("Leaf", "/leaf.g4", rules: [Parser("item"), Lexer("ID")]);
        Source shared = Create("Shared", "/shared.g4", [Import("Leaf")]);
        Source entry = Create("Entry", "/entry.g4", [TokenVocab("Shared"), Import("Shared")]);

        GrammarImportCompositionPlan plan = Build(entry, leaf, shared);
        GrammarDependencyEdge[] descendantEdges = plan.Dependencies.Where(edge => edge.Importer == shared.Identity).ToArray();

        CollectionAssert.AreEqual(
            new[] { GrammarDependencyKind.TokenVocab, GrammarDependencyKind.FullImport },
            descendantEdges.Select(edge => edge.EffectiveKind).ToArray());
        Assert.IsTrue(plan.EffectiveRules.Any(rule => rule.Rule.Name == "item"));
    }

    /// <summary>Verifies available-source enumeration order never changes the plan.</summary>
    [TestMethod]
    public void Build_ReversedSourceEnumeration_ProducesSamePlan()
    {
        Source one = Create("One", "/one.g4", rules: [Parser("one")]);
        Source two = Create("Two", "/two.g4", rules: [Parser("two")]);
        Source entry = Create("Entry", "/entry.g4", [Import("Two"), Import("One")]);
        string[] forward = Build(entry, one, two).EffectiveRules.Select(rule => rule.Rule.Name).ToArray();
        string[] reverse = Build(entry, two, one).EffectiveRules.Select(rule => rule.Rule.Name).ToArray();
        CollectionAssert.AreEqual(forward, reverse);
        CollectionAssert.AreEqual(new[] { "two", "one" }, forward);
    }

    /// <summary>Verifies the G4 adapter produces the same logical plan as an equivalent runtime-neutral source projection.</summary>
    [TestMethod]
    public void Build_G4AndRuntimeLogicalProjects_ProduceParity()
    {
        G4Grammar sharedG4 = ParseG4("grammar Shared; item : ID ; ID : 'a' ;");
        G4Grammar entryG4 = ParseG4("parser grammar Entry; options { tokenVocab=Shared; } import Shared; start : item ;");
        var index = new G4GrammarProjectIndex([
            new G4GrammarProjectEntry("/shared.g4", sharedG4),
            new G4GrammarProjectEntry("/entry.g4", entryG4)]);
        GrammarImportCompositionPlan g4Plan = new G4GrammarCompositionAdapter(index).Build(new G4GrammarProjectEntry("/entry.g4", entryG4));

        Source shared = Create("Shared", "/shared.g4", rules: [Lexer("ID"), Parser("item")]);
        Source entry = Create("Entry", "/entry.g4", [Import("Shared"), TokenVocab("Shared")], [Parser("start")]);
        GrammarImportCompositionPlan runtimePlan = Build(entry, shared);

        CollectionAssert.AreEqual(runtimePlan.Grammars.Select(source => source.Identity).ToArray(), g4Plan.Grammars.Select(source => source.Identity).ToArray());
        CollectionAssert.AreEqual(runtimePlan.Dependencies.Select(edge => edge.DeclaredDependency.Kind).ToArray(), g4Plan.Dependencies.Select(edge => edge.DeclaredDependency.Kind).ToArray());
        CollectionAssert.AreEqual(runtimePlan.Dependencies.Select(edge => edge.EffectiveKind).ToArray(), g4Plan.Dependencies.Select(edge => edge.EffectiveKind).ToArray());
        CollectionAssert.AreEqual(runtimePlan.EffectiveRules.Select(rule => $"{rule.Rule.Domain}:{rule.Rule.Name}:{rule.Rule.LexerMode}").ToArray(), g4Plan.EffectiveRules.Select(rule => $"{rule.Rule.Domain}:{rule.Rule.Name}:{rule.Rule.LexerMode}").ToArray());
        Assert.AreEqual(((RulePayload)runtimePlan.RootRulePayload!).Name, ((G4Rule)g4Plan.RootRulePayload!).Name);
        Assert.AreEqual(runtimePlan.Collisions.Count, g4Plan.Collisions.Count);
        Assert.AreEqual(runtimePlan.MissingDependencies.Count, g4Plan.MissingDependencies.Count);
    }

    /// <summary>Verifies runtime-neutral and G4 adapters agree on propagated effective edge kinds.</summary>
    [TestMethod]
    public void Build_G4AndRuntimeTokenVocabChains_ProduceEffectiveEdgeKindParity()
    {
        G4Grammar leafG4 = ParseG4("lexer grammar Leaf; LEAF : 'x' ;");
        G4Grammar vocabularyG4 = ParseG4("lexer grammar Vocabulary; import Leaf; ROOT : 'r' ;");
        G4Grammar entryG4 = ParseG4("parser grammar Entry; options { tokenVocab=Vocabulary; } start : ROOT ;");
        var index = new G4GrammarProjectIndex([
            new G4GrammarProjectEntry("/leaf.g4", leafG4),
            new G4GrammarProjectEntry("/vocabulary.g4", vocabularyG4),
            new G4GrammarProjectEntry("/entry.g4", entryG4)]);
        GrammarImportCompositionPlan g4Plan = new G4GrammarCompositionAdapter(index).Build(new G4GrammarProjectEntry("/entry.g4", entryG4));

        Source leaf = Create("Leaf", "/leaf.g4", rules: [Lexer("LEAF")]);
        Source vocabulary = Create("Vocabulary", "/vocabulary.g4", [Import("Leaf")], [Lexer("ROOT")]);
        Source entry = Create("Entry", "/entry.g4", [TokenVocab("Vocabulary")], [Parser("start")]);
        GrammarImportCompositionPlan runtimePlan = Build(entry, leaf, vocabulary);

        CollectionAssert.AreEqual(runtimePlan.Dependencies.Select(edge => edge.DeclaredDependency.Kind).ToArray(), g4Plan.Dependencies.Select(edge => edge.DeclaredDependency.Kind).ToArray());
        CollectionAssert.AreEqual(runtimePlan.Dependencies.Select(edge => edge.EffectiveKind).ToArray(), g4Plan.Dependencies.Select(edge => edge.EffectiveKind).ToArray());
    }

    /// <summary>Verifies an exact caller payload keeps local-rule priority even when its declared grammar name is duplicated.</summary>
    [TestMethod]
    public void G4Resolver_DuplicateCallerName_ResolvesExactCallerLocalRule()
    {
        G4Grammar caller = ParseG4("parser grammar Root; start : child[1] ; child[int x] : TOKEN ;");
        G4Grammar duplicate = ParseG4("parser grammar Root; other : TOKEN ;");
        var resolver = new G4ImportedRuleResolver(new G4GrammarProjectIndex([
            new G4GrammarProjectEntry("/one/Root.g4", caller),
            new G4GrammarProjectEntry("/two/Root.g4", duplicate)]));

        G4RuleResolution resolution = resolver.Resolve(caller, "child");

        Assert.AreEqual(G4RuleResolutionKind.Local, resolution.Kind);
        Assert.AreSame(caller.ParserRules.Single(rule => rule.Name == "child"), resolution.Rule);
    }

    /// <summary>Parses one G4 source for adapter parity tests.</summary>
    private static G4Grammar ParseG4(string text) => new G4Parser(new G4Tokenizer(text).Tokenize(), new DiagnosticBag()).Parse();

    /// <summary>Builds a plan from a name-indexed fake project.</summary>
    private static GrammarImportCompositionPlan Build(Source entry, params Source[] available)
    {
        Source[] all = available.Concat([entry]).DistinctBy(source => source.Identity).ToArray();
        return GrammarImportCompositionPlanner.Build(entry, name => all.Where(source => source.Identity.DeclaredName == name).Cast<IGrammarCompositionSource>().ToArray());
    }

    /// <summary>Creates a fake composition source.</summary>
    private static Source Create(string name, string path, IReadOnlyList<GrammarDependency>? dependencies = null, IReadOnlyList<GrammarRuleDescriptor>? rules = null) =>
        new(new GrammarIdentity(name, path), dependencies ?? [], rules ?? []);

    /// <summary>Creates a full-import edge.</summary>
    private static GrammarDependency Import(string name, string? alias = null) => new(name, alias, GrammarDependencyKind.FullImport, name);

    /// <summary>Creates a token-vocabulary edge.</summary>
    private static GrammarDependency TokenVocab(string name) => new(name, null, GrammarDependencyKind.TokenVocab, name);

    /// <summary>Creates a parser-rule descriptor.</summary>
    private static GrammarRuleDescriptor Parser(string name) => new(name, GrammarRuleDomain.Parser, null, new RulePayload(name));

    /// <summary>Creates a lexer-rule descriptor.</summary>
    private static GrammarRuleDescriptor Lexer(string name, string mode = "DEFAULT_MODE") => new(name, GrammarRuleDomain.Lexer, mode, new RulePayload(name));

    /// <summary>Minimal source used to isolate planner semantics from runtime and generator models.</summary>
    private sealed record Source(GrammarIdentity Identity, IReadOnlyList<GrammarDependency> Dependencies, IReadOnlyList<GrammarRuleDescriptor> Rules) : IGrammarCompositionSource
    {
        /// <inheritdoc />
        public object Payload => this;
        /// <inheritdoc />
        public object? RootRulePayload => Rules.FirstOrDefault(rule => rule.Domain == GrammarRuleDomain.Parser)?.Payload;
    }

    /// <summary>Minimal rule payload proving the planner does not convert consumer objects.</summary>
    private sealed record RulePayload(string Name);
}
