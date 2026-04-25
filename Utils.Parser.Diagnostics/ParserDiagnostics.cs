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

    /// <summary>returns clause partially applied.</summary>
    public static readonly ParserDiagnosticDescriptor ReturnsPartiallyApplied =
        new("UP1007", "Returns partially applied", "returns clause for rule '{0}' is parsed but only stored as raw text.", DefaultCategory);

    /// <summary>locals/throws/exception metadata ignored.</summary>
    public static readonly ParserDiagnosticDescriptor LocalsIgnored =
        new("UP1008", "Locals ignored", "locals/throws/exception metadata for rule '{0}' is parsed but ignored.", DefaultCategory);

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

    // Warnings (UP5xxx)
    /// <summary>Best-effort recovery used.</summary>
    public static readonly ParserDiagnosticDescriptor BestEffortRecoveryUsed =
        new("UP5001", "Best-effort recovery used", "Best-effort recovery was used while parsing '{0}'.", DefaultCategory);

    /// <summary>Expected token missing.</summary>
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

    /// <summary>
    /// Gets all known parser diagnostics keyed by code.
    /// </summary>
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
            [ReturnsPartiallyApplied.Code] = ReturnsPartiallyApplied,
            [LocalsIgnored.Code] = LocalsIgnored,
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
            [BestEffortRecoveryUsed.Code] = BestEffortRecoveryUsed,
            [ExpectedTokenMissing.Code] = ExpectedTokenMissing,
            [FallbackStrategyUsed.Code] = FallbackStrategyUsed,
            [TrailingTokensAfterParse.Code] = TrailingTokensAfterParse,
            [AmbiguousConstructResolvedHeuristically.Code] = AmbiguousConstructResolvedHeuristically,
            [DefaultBehaviorApplied.Code] = DefaultBehaviorApplied,
            [ImportedRuleIgnoredBecauseAlreadyDefined.Code] = ImportedRuleIgnoredBecauseAlreadyDefined,
            [EnteringRule.Code] = EnteringRule,
            [LeavingRule.Code] = LeavingRule,
            [TokenMatched.Code] = TokenMatched,
            [BacktrackingUsed.Code] = BacktrackingUsed,
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
