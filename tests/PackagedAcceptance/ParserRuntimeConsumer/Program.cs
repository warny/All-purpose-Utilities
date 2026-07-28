using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.ProjectCompilation;
using Utils.Parser.Resolution;

namespace PackagedAcceptance.ParserRuntime;

/// <summary>
/// Exercises grammar-project composition exclusively through the packaged parser runtime.
/// </summary>
internal static class Program
{
    /// <summary>Runs the packaged runtime composition scenarios.</summary>
    private static void Main()
    {
        VerifyDirectAndTransitiveImports();
        VerifyTokenVocabularyAndLexerModes();
        VerifyLocalPriorityAndRootOwnership();
        VerifyImportedCollisionPrecedence();
        VerifyMissingCycleAndAmbiguousDiagnostics();
        Console.WriteLine("omy.Utils.Parser packaged composition consumer passed.");
    }

    /// <summary>Verifies direct and transitive imports contribute executable parser rules.</summary>
    private static void VerifyDirectAndTransitiveImports()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "grammar Entry; import Middle; start : middle ;"),
            ("Middle", "parser grammar Middle; import Leaf; middle : leaf ;"),
            ("Leaf", "parser grammar Leaf; leaf : TOKEN ; TOKEN : 'a' ;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);
        Require(definition.AllRules.ContainsKey("middle"), "Direct imported rule was not composed.");
        Require(definition.AllRules.ContainsKey("leaf"), "Transitive imported rule was not composed.");
        Require(Antlr4GrammarProjectCompiler.Compile("Entry", resolver).Parse("a") is not null, "Composed grammar did not execute.");
    }

    /// <summary>Verifies a separate lexer vocabulary contributes rules, a populated mode, and an empty mode.</summary>
    private static void VerifyTokenVocabularyAndLexerModes()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "parser grammar Entry; options { tokenVocab=Tokens; } start : TOKEN ;"),
            ("Tokens", "lexer grammar Tokens; TOKEN : 'a'; mode EXTRA; EXTRA_TOKEN : 'b'; mode EMPTY;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);
        Require(definition.AllRules.ContainsKey("TOKEN"), "tokenVocab lexer rule was not composed.");
        Require(definition.Modes.Any(mode => mode.Name == "EXTRA" && mode.Rules.Any(rule => rule.Name == "EXTRA_TOKEN")), "Populated lexer mode was not composed.");
        Require(definition.Modes.Any(mode => mode.Name == "EMPTY" && mode.Rules.Count == 0), "Empty lexer mode was not preserved.");
        Require(Antlr4GrammarProjectCompiler.Compile("Entry", resolver).Parse("a") is not null, "Separate lexer/parser grammar did not execute.");
    }

    /// <summary>Verifies local rules mask imported rules and the entry grammar retains root ownership.</summary>
    private static void VerifyLocalPriorityAndRootOwnership()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "grammar Entry; import Shared; start : item ; item : 'l' ;"),
            ("Shared", "parser grammar Shared; importedRoot : item ; item : 'i' ;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);
        Require(definition.RootRule?.Name == "start", "The local entry root was not preserved.");
        Require(Antlr4GrammarProjectCompiler.Compile("Entry", resolver).Parse("l") is not null, "The local rule did not take precedence.");
    }

    /// <summary>Verifies imported collisions retain deterministic first-import precedence.</summary>
    private static void VerifyImportedCollisionPrecedence()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "grammar Entry; import One, Two; start : item ;"),
            ("One", "parser grammar One; item : '1' ;"),
            ("Two", "parser grammar Two; item : '2' ;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);
        Require(definition.ParserRules.Count(rule => rule.Name == "item") == 1, "Imported collision emitted duplicate rules.");
        Require(Antlr4GrammarProjectCompiler.Compile("Entry", resolver).Parse("1") is not null, "First imported rule did not retain precedence.");
    }

    /// <summary>Verifies missing imports, cycles, and ambiguous sources are rejected with diagnostics.</summary>
    private static void VerifyMissingCycleAndAmbiguousDiagnostics()
    {
        ExpectCompositionFailure(
            CreateResolver(("Entry", "grammar Entry; import Missing; start : 'a' ;")),
            "Entry",
            "Missing import was accepted.");
        ExpectCompositionFailure(
            CreateResolver(
                ("Entry", "grammar Entry; import Other; start : 'a' ;"),
                ("Other", "parser grammar Other; import Entry; other : 'b' ;")),
            "Entry",
            "Import cycle was accepted.");

        var ambiguous = new InMemoryGrammarSourceResolver([
            new GrammarSource("Entry", "/Entry.g4", "grammar Entry; import Shared; start : item ;"),
            new GrammarSource("Shared", "/one/Shared.g4", "parser grammar Shared; item : '1' ;"),
            new GrammarSource("Shared", "/two/Shared.g4", "parser grammar Shared; item : '2' ;")]);
        ExpectCompositionFailure(ambiguous, "Entry", "Ambiguous import was accepted.");
    }

    /// <summary>Requires a condition or throws a consumer-visible acceptance failure.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Requires project compilation to fail and emit at least one diagnostic.</summary>
    private static void ExpectCompositionFailure(InMemoryGrammarSourceResolver resolver, string entry, string message)
    {
        var diagnostics = new DiagnosticBag();
        try
        {
            _ = Antlr4GrammarProjectCompiler.Parse(entry, resolver, diagnostics);
            throw new InvalidOperationException(message);
        }
        catch (GrammarValidationException)
        {
            Require(diagnostics.Count > 0, "Composition failed without a diagnostic.");
        }
    }

    /// <summary>Creates a deterministic in-memory grammar source resolver.</summary>
    private static InMemoryGrammarSourceResolver CreateResolver(params (string Name, string Text)[] grammars) =>
        new(grammars.Select(grammar => new GrammarSource(grammar.Name, $"/{grammar.Name}.g4", grammar.Text)));
}
