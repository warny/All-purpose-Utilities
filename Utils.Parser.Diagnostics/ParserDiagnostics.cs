using System.Collections.Generic;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Central registry of parser diagnostic descriptors shared by runtime and generator pipelines.
/// </summary>
public static class ParserDiagnostics
{
    private const string DefaultCategory = "Utils.Parser";

    // Blocking errors (UP0xxx)
    /// <summary>Unexpected token encountered.</summary>
    public static readonly ParserDiagnosticDescriptor UnexpectedToken =
        new("UP0001", "Unexpected token", "Unexpected token '{0}'.", DefaultCategory);

    /// <summary>Invalid grammar root node.</summary>
    public static readonly ParserDiagnosticDescriptor InvalidGrammarRoot =
        new("UP0002", "Invalid grammar root", "Invalid grammar root: '{0}'.", DefaultCategory);

    /// <summary>Unknown referenced rule.</summary>
    public static readonly ParserDiagnosticDescriptor UnknownRuleReference =
        new("UP0003", "Unknown rule reference", "Rule '{0}' references unknown rule '{1}'.", DefaultCategory);

    /// <summary>Unknown lexer mode referenced by a lexer command.</summary>
    public static readonly ParserDiagnosticDescriptor UnknownLexerMode =
        new("UP0004", "Unknown lexer mode", "Unknown lexer mode '{0}' used by command '{1}'.", DefaultCategory);

    /// <summary>Parsing failure.</summary>
    public static readonly ParserDiagnosticDescriptor ParseFailure =
        new("UP0005", "Parse failure", "Parse failure: {0}", DefaultCategory);

    /// <summary>Internal consistency failure.</summary>
    public static readonly ParserDiagnosticDescriptor InternalInconsistency =
        new("UP0006", "Internal inconsistency", "Internal inconsistency: {0}", DefaultCategory);


    /// <summary>Imported grammar could not be resolved.</summary>
    public static readonly ParserDiagnosticDescriptor ImportedGrammarNotFound =
        new("UP0010", "Imported grammar not found", "Unable to resolve imported grammar '{0}'.", DefaultCategory);

    /// <summary>Import cycle detected while resolving grammars.</summary>
    public static readonly ParserDiagnosticDescriptor ImportCycleDetected =
        new("UP0011", "Import cycle detected", "Import cycle detected: {0}", DefaultCategory);

    /// <summary>Parser rule declared in a lexer grammar.</summary>
    public static readonly ParserDiagnosticDescriptor ParserRuleNotAllowedInLexerGrammar =
        new("UP0012", "Parser rule not allowed in lexer grammar", "Parser rule '{0}' is not allowed in a lexer grammar.", DefaultCategory);

    /// <summary>Lexer rule declared in a parser grammar.</summary>
    public static readonly ParserDiagnosticDescriptor LexerRuleNotAllowedInParserGrammar =
        new("UP0013", "Lexer rule not allowed in parser grammar", "Lexer rule '{0}' is not allowed in a parser grammar.", DefaultCategory);

    // Unsupported / ignored / partial support (UP1xxx)
    /// <summary>Import parsed but not resolved.</summary>
    public static readonly ParserDiagnosticDescriptor ImportParsedButNotResolved =
        new("UP1001", "Import parsed but not resolved", "Import '{0}' is parsed but not resolved at runtime.", DefaultCategory);

    /// <summary>tokens block ignored.</summary>
    public static readonly ParserDiagnosticDescriptor TokensBlockIgnored =
        new("UP1002", "tokens block ignored", "tokens { ... } is recognized but ignored.", DefaultCategory);

    /// <summary>channels block ignored.</summary>
    public static readonly ParserDiagnosticDescriptor ChannelsBlockIgnored =
        new("UP1003", "channels block ignored", "channels { ... } is recognized but ignored.", DefaultCategory);

    /// <summary>Action ignored by the current pipeline.</summary>
    public static readonly ParserDiagnosticDescriptor ActionIgnored =
        new("UP1004", "Action ignored", "Action '{0}' is ignored in this pipeline.", DefaultCategory);

    /// <summary>Inline action stored but not executed.</summary>
    public static readonly ParserDiagnosticDescriptor InlineActionStoredNotExecuted =
        new("UP1005", "Inline action not executed", "Inline action is stored but not executed.", DefaultCategory);

    /// <summary>Semantic predicate recognized but not enforced.</summary>
    public static readonly ParserDiagnosticDescriptor SemanticPredicateNotEnforced =
        new("UP1006", "Semantic predicate not enforced", "Semantic predicate is recognized but not enforced semantically.", DefaultCategory);

    /// <summary>returns clause ignored by runtime semantics.</summary>
    public static readonly ParserDiagnosticDescriptor RuleReturnsIgnored =
        new("UP1007", "Rule returns ignored", "Rule returns clause for rule '{0}' is recognized but ignored by the current runtime model.", DefaultCategory);

    /// <summary>
    /// Compatibility alias for the legacy descriptor name.
    /// </summary>
    [System.Obsolete("Use RuleReturnsIgnored. This alias is kept for compatibility.")]
    public static readonly ParserDiagnosticDescriptor ReturnsPartiallyApplied =
        RuleReturnsIgnored;

    /// <summary>Rule locals clause recognized but ignored by runtime semantics.</summary>
    public static readonly ParserDiagnosticDescriptor RuleLocalsIgnored =
        new("UP1008", "Rule locals ignored", "Rule locals clause for rule '{0}' is recognized but ignored by the current runtime model.", DefaultCategory);

    /// <summary>Rule throws/catch/finally metadata recognized but ignored by runtime semantics.</summary>
    public static readonly ParserDiagnosticDescriptor RuleExceptionMetadataIgnored =
        new("UP1023", "Rule exception metadata ignored", "Rule exception metadata (throws/catch/finally) for rule '{0}' is recognized but ignored by the current runtime model.", DefaultCategory);

    /// <summary>
    /// Compatibility alias for the legacy descriptor name.
    /// </summary>
    [System.Obsolete("Use RuleLocalsIgnored. This alias is kept for compatibility.")]
    public static readonly ParserDiagnosticDescriptor LocalsIgnored =
        RuleLocalsIgnored;

    /// <summary>Runtime and generator support mismatch.</summary>
    public static readonly ParserDiagnosticDescriptor RuntimeGeneratorMismatch =
        new("UP1009", "Runtime/generator mismatch", "Runtime and source generator support differ for '{0}'.", DefaultCategory);

    /// <summary>Direct left recursion detected on a parser rule.</summary>
    public static readonly ParserDiagnosticDescriptor DirectLeftRecursionDetected =
        new("UP1010", "Direct left recursion detected", "Direct left recursion detected on rule '{0}'.", DefaultCategory);

    /// <summary>Indirect left recursion is currently unsupported.</summary>
    public static readonly ParserDiagnosticDescriptor IndirectLeftRecursionNotSupported =
        new("UP1011", "Indirect left recursion not supported", "Indirect left recursion is not supported yet for rule '{0}'.", DefaultCategory);

    /// <summary>Left-recursive rule does not define any base alternative.</summary>
    public static readonly ParserDiagnosticDescriptor LeftRecursiveRuleWithoutBaseAlternative =
        new("UP1012", "Left-recursive rule without base alternative", "Left-recursive rule '{0}' has no base alternative.", DefaultCategory);

    /// <summary>Equivalent parse branches were pruned using alternative priority.</summary>
    public static readonly ParserDiagnosticDescriptor AmbiguousAlternativesPruned =
        new("UP1013", "Ambiguous alternatives pruned", "Equivalent alternatives were pruned in rule '{0}'.", DefaultCategory);

    /// <summary>Strict duplicate alternatives were removed at resolution time.</summary>
    public static readonly ParserDiagnosticDescriptor StaticDuplicateAlternativeRemoved =
        new("UP1014", "Static duplicate alternative removed", "Duplicate alternative removed in rule '{0}'.", DefaultCategory);

    /// <summary>A parse branch was pruned during runtime branch selection.</summary>
    public static readonly ParserDiagnosticDescriptor ParseBranchPruned =
        new("UP1015", "Parse branch pruned", "Parse branch pruned in rule '{0}'.", DefaultCategory);

    /// <summary>Memoization hit for parser rule evaluation.</summary>
    public static readonly ParserDiagnosticDescriptor ParseMemoHit =
        new("UP1016", "Parse memo hit", "Parse memoization hit for rule '{0}'.", DefaultCategory);

    /// <summary>Memoization miss for parser rule evaluation.</summary>
    public static readonly ParserDiagnosticDescriptor ParseMemoMiss =
        new("UP1017", "Parse memo miss", "Parse memoization miss for rule '{0}'.", DefaultCategory);

    /// <summary>Left-recursive precedence support is partial compared to ANTLR4.</summary>
    public static readonly ParserDiagnosticDescriptor LeftRecursivePrecedencePartiallySupported =
        new("UP1018", "Left-recursive precedence partially supported", "Left-recursive precedence is partially supported for rule '{0}'.", DefaultCategory);

    /// <summary>ANTLR language option ignored by this runtime.</summary>
    public static readonly ParserDiagnosticDescriptor UnsupportedAntlrLanguageOptionIgnored =
        new("UP1019", "ANTLR language option ignored", "The ANTLR option 'language' is not supported by Utils.Parser and will be ignored (value: '{0}').", DefaultCategory);

    /// <summary>Unsupported lexer command encountered in grammar source.</summary>
    public static readonly ParserDiagnosticDescriptor UnsupportedLexerCommand =
        new("UP1020", "Unsupported lexer command", "Lexer command '{0}' is parsed but not supported by Utils.Parser.", DefaultCategory);

    /// <summary>ANTLR option parsed but not supported by this runtime.</summary>
    public static readonly ParserDiagnosticDescriptor UnsupportedAntlrOptionIgnored =
        new("UP1021", "ANTLR option ignored", "ANTLR option '{0}' is currently unsupported and will be ignored.", DefaultCategory);

    /// <summary>Embedded ANTLR code language is not supported in the current execution path.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodeLanguageUnsupported =
        new("UP1024", "Embedded code language unsupported", "Embedded ANTLR code language '{0}' is not supported in the current execution path.", DefaultCategory);

    /// <summary>Embedded ANTLR code requires a compiler, but no compiler is configured.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodeCompilerNotConfigured =
        new("UP1025", "Embedded code compiler not configured", "Embedded ANTLR code for '{0}' requires an explicit embedded-code compiler, but none was configured.", DefaultCategory);

    /// <summary>Embedded ANTLR code compilation failed for the requested construct.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodeCompilationFailed =
        new("UP1026", "Embedded code compilation failed", "Embedded ANTLR code for '{0}' could not be compiled: {1}", DefaultCategory);

    /// <summary>Embedded ANTLR code is preserved as metadata and not compiled in the current execution path.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodePreservedNotCompiled =
        new("UP1027", "Embedded code preserved and not compiled", "Embedded ANTLR code for '{0}' is preserved as metadata and is not compiled in the current execution path.", DefaultCategory);

    /// <summary>Embedded ANTLR code execution is disabled by the current runtime policy.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodeExecutionDisabled =
        new("UP1028", "Embedded code execution disabled", "Embedded ANTLR code execution for '{0}' is disabled by the current runtime policy.", DefaultCategory);

    /// <summary>Embedded ANTLR code is visible to the source generator but is not promoted to executable hooks.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedCodeConstructNotExecutedByGenerator =
        new(
            "UP1029",
            "Embedded code construct not executed by generator",
            "{0} embedded code in '{1}' is visible to Utils.Parser.Generators but is not executed by the generated parser. {2} Supported generated C# embedded constructs are limited to parser semantic predicates, inline parser actions, rule lifecycle actions, parser headers, and parser members.",
            DefaultCategory);

    /// <summary>Semantic predicate options block (<c>&lt;fail=...&gt;</c>) recognized but not applied by the current runtime model.</summary>
    public static readonly ParserDiagnosticDescriptor PredicateOptionsIgnored =
        new("UP1030", "Predicate options ignored", "Predicate options on predicate '{0}' are recognized but ignored by the current runtime model.", DefaultCategory);

    /// <summary>Parser members code is injected into the generated per-parse execution context by the source generator.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedMembersInjectedByGenerator =
        new(
            "UP1031",
            "Embedded members injected by generator",
            "{0} code in '{1}' was injected into the generated per-parse execution context as C# source. This is a source-generator C# compatibility bridge for imported grammars. Prefer a separate partial execution-context class for new code. Invalid C# or member-name collisions are reported by Roslyn.",
            DefaultCategory);

    /// <summary>Parser header code is injected near the top of generated C# source by the source generator.</summary>
    public static readonly ParserDiagnosticDescriptor EmbeddedHeaderInjectedByGenerator =
        new(
            "UP1035",
            "Embedded header injected by generator",
            "{0} code in '{1}' was injected as generated C# source near the top of the generated parser file. This is a source-generator C# compatibility bridge only, so invalid C# is reported by Roslyn and this does not imply full ANTLR target-language compatibility.",
            DefaultCategory);

    /// <summary>Label parsed on a non-rule-ref element and ignored.</summary>
    public static readonly ParserDiagnosticDescriptor LabelOnNonRuleReferenceIgnored =
        new("UP1022", "Label ignored on non-rule reference", "Label '{0}' is recognized but ignored because it targets a non-rule-reference element.", DefaultCategory);

    /// <summary>Element option on an alternative recognized but not applied by the current runtime model.</summary>
    public static readonly ParserDiagnosticDescriptor ElementOptionIgnored =
        new("UP1032", "Element option ignored", "Element option '{0}' is recognized but not applied by the current runtime model.", DefaultCategory);

    /// <summary>Options block on a lexer rule recognized but ignored by the current runtime model.</summary>
    public static readonly ParserDiagnosticDescriptor LexerRuleOptionsIgnored =
        new("UP1033", "Lexer rule options ignored", "Options block on lexer rule '{0}' is recognized but ignored by the current runtime model.", DefaultCategory);

    /// <summary>Options block on a parser rule recognized but ignored by the current runtime model.</summary>
    public static readonly ParserDiagnosticDescriptor ParserRuleOptionsIgnored =
        new("UP1034", "Parser rule options ignored", "Options block on parser rule '{0}' is recognized but ignored by the current runtime model.", DefaultCategory);

    // Warnings (UP5xxx)
    /// <summary>Best-effort recovery used.</summary>
    public static readonly ParserDiagnosticDescriptor BestEffortRecoveryUsed =
        new("UP5001", "Best-effort recovery used", "Best-effort recovery was used while parsing '{0}'.", DefaultCategory);

    /// <summary>
    /// Expected token missing.
    /// Currently reserved for missing-token recovery diagnostics.
    /// </summary>
    public static readonly ParserDiagnosticDescriptor ExpectedTokenMissing =
        new("UP5002", "Expected token missing", "Expected token '{0}' was not found.", DefaultCategory);

    /// <summary>Fallback strategy used.</summary>
    public static readonly ParserDiagnosticDescriptor FallbackStrategyUsed =
        new("UP5003", "Fallback strategy used", "Fallback strategy used: {0}", DefaultCategory);

    /// <summary>Trailing tokens remain after parse.</summary>
    public static readonly ParserDiagnosticDescriptor TrailingTokensAfterParse =
        new("UP5004", "Trailing tokens after parse", "Trailing tokens remain after parse: '{0}'.", DefaultCategory);

    /// <summary>Ambiguous construct resolved heuristically.</summary>
    public static readonly ParserDiagnosticDescriptor AmbiguousConstructResolvedHeuristically =
        new("UP5005", "Ambiguous construct resolved", "Ambiguous construct resolved heuristically: {0}", DefaultCategory);

    /// <summary>Parsing branch aborted because the parser entered a repeated state.</summary>
    public static readonly ParserDiagnosticDescriptor ParserStateCycleDetected =
        new("PARSER001", "Parser state cycle detected", "Parsing branch aborted due to repeated parser state.", DefaultCategory);

    /// <summary>Quantifier iteration stopped because the inner rule made no progress.</summary>
    public static readonly ParserDiagnosticDescriptor NonProgressiveQuantifierStopped =
        new("PARSER002", "Non-progressive quantifier stopped", "Quantifier stopped because inner rule matched without consuming input.", DefaultCategory);

    /// <summary>Left-recursive extension stopped because no progress was observed.</summary>
    public static readonly ParserDiagnosticDescriptor NonProgressiveLeftRecursionStopped =
        new("PARSER003", "Non-progressive left recursion stopped", "Left-recursive extension stopped because no input progress was made.", DefaultCategory);

    // Info (UP8xxx)
    /// <summary>Default behavior applied.</summary>
    public static readonly ParserDiagnosticDescriptor DefaultBehaviorApplied =
        new("UP8001", "Default behavior applied", "Default behavior applied: {0}", DefaultCategory);


    /// <summary>Imported rule ignored because a primary rule already exists.</summary>
    public static readonly ParserDiagnosticDescriptor ImportedRuleIgnoredBecauseAlreadyDefined =
        new("UP8002", "Imported rule ignored", "Imported rule '{0}' was ignored because the entry grammar already defines it.", DefaultCategory);

    // Debug (UP9xxx)
    /// <summary>Debug trace for entering a rule.</summary>
    public static readonly ParserDiagnosticDescriptor EnteringRule =
        new("UP9001", "Entering rule", "Entering rule '{0}'.", DefaultCategory);

    /// <summary>Debug trace for leaving a rule.</summary>
    public static readonly ParserDiagnosticDescriptor LeavingRule =
        new("UP9002", "Leaving rule", "Leaving rule '{0}'.", DefaultCategory);

    /// <summary>Debug trace for matched token.</summary>
    public static readonly ParserDiagnosticDescriptor TokenMatched =
        new("UP9003", "Token matched", "Token matched by rule '{0}': '{1}'.", DefaultCategory);

    /// <summary>Debug trace for backtracking usage.</summary>
    public static readonly ParserDiagnosticDescriptor BacktrackingUsed =
        new("UP9004", "Backtracking used", "Backtracking used in rule '{0}'.", DefaultCategory);

    /// <summary>Debug trace for parser states rejected by runtime safeguards.</summary>
    public static readonly ParserDiagnosticDescriptor ParserStateRejected =
        new("UP9005", "Parser state rejected", "Parser state rejected in rule '{0}': {1}.", DefaultCategory);

    /// <summary>
    /// Gets all known parser diagnostics keyed by code.
    /// </summary>
    /// <remarks>
    /// Compatibility aliases such as ReturnsPartiallyApplied and LocalsIgnored are intentionally not
    /// listed separately because <see cref="All"/> is keyed by diagnostic code and exposes canonical descriptors.
    /// </remarks>
    public static IReadOnlyDictionary<string, ParserDiagnosticDescriptor> All { get; } =
        new Dictionary<string, ParserDiagnosticDescriptor>
        {
            [UnexpectedToken.Code] = UnexpectedToken,
            [InvalidGrammarRoot.Code] = InvalidGrammarRoot,
            [UnknownRuleReference.Code] = UnknownRuleReference,
            [UnknownLexerMode.Code] = UnknownLexerMode,
            [ParseFailure.Code] = ParseFailure,
            [InternalInconsistency.Code] = InternalInconsistency,
            [ImportedGrammarNotFound.Code] = ImportedGrammarNotFound,
            [ImportCycleDetected.Code] = ImportCycleDetected,
            [ParserRuleNotAllowedInLexerGrammar.Code] = ParserRuleNotAllowedInLexerGrammar,
            [LexerRuleNotAllowedInParserGrammar.Code] = LexerRuleNotAllowedInParserGrammar,
            [ImportParsedButNotResolved.Code] = ImportParsedButNotResolved,
            [TokensBlockIgnored.Code] = TokensBlockIgnored,
            [ChannelsBlockIgnored.Code] = ChannelsBlockIgnored,
            [ActionIgnored.Code] = ActionIgnored,
            [InlineActionStoredNotExecuted.Code] = InlineActionStoredNotExecuted,
            [SemanticPredicateNotEnforced.Code] = SemanticPredicateNotEnforced,
            [RuleReturnsIgnored.Code] = RuleReturnsIgnored,
            [RuleLocalsIgnored.Code] = RuleLocalsIgnored,
            [RuleExceptionMetadataIgnored.Code] = RuleExceptionMetadataIgnored,
            [EmbeddedCodeLanguageUnsupported.Code] = EmbeddedCodeLanguageUnsupported,
            [EmbeddedCodeCompilerNotConfigured.Code] = EmbeddedCodeCompilerNotConfigured,
            [EmbeddedCodeCompilationFailed.Code] = EmbeddedCodeCompilationFailed,
            [EmbeddedCodePreservedNotCompiled.Code] = EmbeddedCodePreservedNotCompiled,
            [EmbeddedCodeExecutionDisabled.Code] = EmbeddedCodeExecutionDisabled,
            [EmbeddedCodeConstructNotExecutedByGenerator.Code] = EmbeddedCodeConstructNotExecutedByGenerator,
            [EmbeddedMembersInjectedByGenerator.Code] = EmbeddedMembersInjectedByGenerator,
            [EmbeddedHeaderInjectedByGenerator.Code] = EmbeddedHeaderInjectedByGenerator,
            [RuntimeGeneratorMismatch.Code] = RuntimeGeneratorMismatch,
            [DirectLeftRecursionDetected.Code] = DirectLeftRecursionDetected,
            [IndirectLeftRecursionNotSupported.Code] = IndirectLeftRecursionNotSupported,
            [LeftRecursiveRuleWithoutBaseAlternative.Code] = LeftRecursiveRuleWithoutBaseAlternative,
            [AmbiguousAlternativesPruned.Code] = AmbiguousAlternativesPruned,
            [StaticDuplicateAlternativeRemoved.Code] = StaticDuplicateAlternativeRemoved,
            [ParseBranchPruned.Code] = ParseBranchPruned,
            [ParseMemoHit.Code] = ParseMemoHit,
            [ParseMemoMiss.Code] = ParseMemoMiss,
            [LeftRecursivePrecedencePartiallySupported.Code] = LeftRecursivePrecedencePartiallySupported,
            [UnsupportedAntlrLanguageOptionIgnored.Code] = UnsupportedAntlrLanguageOptionIgnored,
            [UnsupportedLexerCommand.Code] = UnsupportedLexerCommand,
            [UnsupportedAntlrOptionIgnored.Code] = UnsupportedAntlrOptionIgnored,
            [LabelOnNonRuleReferenceIgnored.Code] = LabelOnNonRuleReferenceIgnored,
            [PredicateOptionsIgnored.Code] = PredicateOptionsIgnored,
            [ElementOptionIgnored.Code] = ElementOptionIgnored,
            [LexerRuleOptionsIgnored.Code] = LexerRuleOptionsIgnored,
            [ParserRuleOptionsIgnored.Code] = ParserRuleOptionsIgnored,
            [BestEffortRecoveryUsed.Code] = BestEffortRecoveryUsed,
            [ExpectedTokenMissing.Code] = ExpectedTokenMissing,
            [FallbackStrategyUsed.Code] = FallbackStrategyUsed,
            [TrailingTokensAfterParse.Code] = TrailingTokensAfterParse,
            [AmbiguousConstructResolvedHeuristically.Code] = AmbiguousConstructResolvedHeuristically,
            [ParserStateCycleDetected.Code] = ParserStateCycleDetected,
            [NonProgressiveQuantifierStopped.Code] = NonProgressiveQuantifierStopped,
            [NonProgressiveLeftRecursionStopped.Code] = NonProgressiveLeftRecursionStopped,
            [DefaultBehaviorApplied.Code] = DefaultBehaviorApplied,
            [ImportedRuleIgnoredBecauseAlreadyDefined.Code] = ImportedRuleIgnoredBecauseAlreadyDefined,
            [EnteringRule.Code] = EnteringRule,
            [LeavingRule.Code] = LeavingRule,
            [TokenMatched.Code] = TokenMatched,
            [BacktrackingUsed.Code] = BacktrackingUsed,
            [ParserStateRejected.Code] = ParserStateRejected,
        };

    /// <summary>
    /// Looks up a descriptor by diagnostic code.
    /// </summary>
    /// <param name="code">Diagnostic code.</param>
    /// <param name="descriptor">Resolved descriptor when found.</param>
    /// <returns><c>true</c> when the code exists.</returns>
    public static bool TryGet(string code, out ParserDiagnosticDescriptor descriptor)
    {
        return All.TryGetValue(code, out descriptor!);
    }
}
