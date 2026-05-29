# omy.Utils.Parser.Diagnostics

`omy.Utils.Parser.Diagnostics` provides the shared diagnostics model used by parser runtime and generator components in the `omy.Utils` ecosystem.

## Purpose

Use this package when you need a common set of diagnostic primitives across parser-related tools, including:

- diagnostic descriptors,
- diagnostic severities,
- diagnostic aggregation utilities.

Source-location contracts such as `SourceCodeLocation` and `SourceCodeRange` are provided by `omy.Utils.Parser.Source` so non-diagnostic surfaces can share them without depending on diagnostics.

## Typical usage

The package is typically consumed transitively by parser packages. Reference it directly when building custom tooling that needs to exchange diagnostics with `omy.Utils.Parser` or `omy.Utils.Parser.Generators`.

```bash
dotnet add package omy.Utils.Parser.Diagnostics
```

## Diagnostic code catalogue

All named descriptors are defined in the `ParserDiagnostics` static class and accessible via `ParserDiagnostics.All` (a dictionary keyed by code string).

### UP0xxx — Blocking errors

| Code | Name | Trigger |
|---|---|---|
| UP0001 | `UnexpectedToken` | A token did not match any expected alternative |
| UP0002 | `InvalidGrammarRoot` | The grammar root node is not a recognized grammar declaration |
| UP0003 | `UnknownRuleReference` | A rule refers to another rule that cannot be resolved |
| UP0004 | `UnknownLexerMode` | A lexer command references a mode that is not declared |
| UP0005 | `ParseFailure` | The parser failed to match input from the root rule |
| UP0006 | `InternalInconsistency` | An internal consistency check failed |
| UP0010 | `ImportedGrammarNotFound` | An `import` target grammar could not be located by the resolver |
| UP0011 | `ImportCycleDetected` | A circular dependency was found while resolving grammar imports |
| UP0012 | `ParserRuleNotAllowedInLexerGrammar` | A parser rule was declared inside a `lexer grammar` |
| UP0013 | `LexerRuleNotAllowedInParserGrammar` | A lexer rule was declared inside a `parser grammar` |

### UP1xxx — Unsupported / ignored / partial behavior

| Code | Name | Trigger |
|---|---|---|
| UP1001 | `ImportParsedButNotResolved` | An `import` directive was recognized but not resolved at runtime |
| UP1002 | `TokensBlockIgnored` | A `tokens { ... }` block is recognized but not mapped into the model |
| UP1003 | `ChannelsBlockIgnored` | A `channels { ... }` block is recognized but not mapped into the model |
| UP1004 | `ActionIgnored` | A top-level `@...` action block is recognized but not executed |
| UP1005 | `InlineActionStoredNotExecuted` | An inline parser action is stored but not executed at runtime |
| UP1006 | `SemanticPredicateNotEnforced` | A `{...}?` predicate is recognized but not evaluated; it always succeeds |
| UP1007 | `RuleReturnsIgnored` | A `returns [...]` clause is parsed but stored only as raw text (`ReturnsPartiallyApplied` is a compatibility alias) |
| UP1008 | `RuleLocalsIgnored` | A rule `locals [...]` clause is recognized but ignored by the current runtime model |
| UP1023 | `RuleExceptionMetadataIgnored` | Rule `throws` / `catch` / `finally` metadata is recognized but ignored by the current runtime model |
| UP1009 | `RuntimeGeneratorMismatch` | A feature behaves differently between the runtime and the source generator |
| UP1010 | `DirectLeftRecursionDetected` | Direct left recursion was detected and restructured during rule resolution |
| UP1011 | `IndirectLeftRecursionNotSupported` | Indirect left recursion is not supported and raises `GrammarValidationException` |
| UP1012 | `LeftRecursiveRuleWithoutBaseAlternative` | A left-recursive rule defines no non-recursive (base) alternative |
| UP1013 | `AmbiguousAlternativesPruned` | Structurally equivalent alternatives were pruned; the lower-priority winner is kept |
| UP1014 | `StaticDuplicateAlternativeRemoved` | A duplicate alternative was eliminated at resolution time |
| UP1015 | `ParseBranchPruned` | A parse branch was discarded during runtime best-match selection |
| UP1016 | `ParseMemoHit` | A cached result was reused for a (rule, position, precedence) triple |
| UP1017 | `ParseMemoMiss` | No cached result existed for a (rule, position, precedence) triple |
| UP1018 | `LeftRecursivePrecedencePartiallySupported` | Left-recursive precedence predicates are only partially handled |
| UP1019 | `UnsupportedAntlrLanguageOptionIgnored` | The ANTLR4 `language` option is not supported and is silently ignored |
| UP1020 | `UnsupportedLexerCommand` | A lexer command was parsed but is outside the currently supported command set |

### UP5xxx — Warnings (recovery / best-effort)

| Code | Name | Trigger |
|---|---|---|
| UP5001 | `BestEffortRecoveryUsed` | Best-effort recovery was applied during parsing |
| UP5002 | `ExpectedTokenMissing` | Reserved for missing-token recovery diagnostics when an expected token is absent |
| UP5003 | `FallbackStrategyUsed` | A fallback parsing strategy was activated |
| UP5004 | `TrailingTokensAfterParse` | Unconsumed tokens remain after the root rule matched; `Parse()` returns an `ErrorNode` |
| UP5005 | `AmbiguousConstructResolvedHeuristically` | An ambiguous construct was resolved by heuristic rather than by grammar priority |

### UP8xxx — Informational

| Code | Name | Trigger |
|---|---|---|
| UP8001 | `DefaultBehaviorApplied` | A grammar construct fell back to a built-in default behavior |
| UP8002 | `ImportedRuleIgnoredBecauseAlreadyDefined` | An imported rule was skipped because the entry grammar already defines the same name |

### PARSER0xx — Runtime safety guards

These codes are emitted by `ParserEngine` when built-in loop-termination guards activate.

| Code | Name | Trigger |
|---|---|---|
| PARSER001 | `ParserStateCycleDetected` | A repeated parser state (same rule, position, and alternative) was detected and skipped to prevent infinite recursion |
| PARSER002 | `NonProgressiveQuantifierStopped` | A quantifier iteration matched without consuming any token; the loop is stopped |
| PARSER003 | `NonProgressiveLeftRecursionStopped` | A left-recursive extension produced no token progress; the seed-and-extend loop is stopped |

### UP9xxx — Debug traces

These codes are emitted only when a `DiagnosticBag` is passed to the parse call; they are not emitted in production builds unless explicitly requested.

| Code | Name | Trigger |
|---|---|---|
| UP9001 | `EnteringRule` | The engine entered a parser rule |
| UP9002 | `LeavingRule` | The engine finished a parser rule |
| UP9003 | `TokenMatched` | A token was consumed by a lexer rule reference |
| UP9004 | `BacktrackingUsed` | The cursor was restored after a failed alternative |
| UP9005 | `ParserStateRejected` | A parser state was rejected by a safety guard |
| UP5006 | `DefaultBehaviorApplied` | A grammar construct fell back to a default behavior |


## Embedded ANTLR code diagnostics

Shared embedded-code diagnostics are available for runtime, generator, and tooling boundaries:

- `UP1024 EmbeddedCodeLanguageUnsupported`
- `UP1025 EmbeddedCodeCompilerNotConfigured`
- `UP1026 EmbeddedCodeCompilationFailed`
- `UP1027 EmbeddedCodePreservedNotCompiled`
- `UP1028 EmbeddedCodeExecutionDisabled`

These diagnostics describe language support, compilation, and execution boundaries for embedded ANTLR code. They do not imply embedded-code execution is implemented.

## Related packages

- `omy.Utils.Parser`
- `omy.Utils.Parser.Generators`

See the repository root README for installation and package selection guidance.
