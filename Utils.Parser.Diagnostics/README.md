# omy.Utils.Parser.Diagnostics

`omy.Utils.Parser.Diagnostics` provides the shared diagnostics model used by parser runtime and generator components in the `omy.Utils` ecosystem.

## Purpose

Use this package when you need a common set of diagnostic primitives across parser-related tools, including:

- diagnostic descriptors,
- diagnostic severities,
- diagnostic aggregation utilities.

## Typical usage

The package is typically consumed transitively by parser packages. Reference it directly when building custom tooling that needs to exchange diagnostics with `omy.Utils.Parser` or `omy.Utils.Parser.Generators`.

```bash
dotnet add package omy.Utils.Parser.Diagnostics
```

## Diagnostic code catalogue

All named descriptors are defined in the `ParserDiagnostics` static class and accessible via `ParserDiagnostics.All` (a dictionary keyed by code string).

### UP0xxx — Errors

| Code | Name | Trigger |
|---|---|---|
| UP0001 | `UnexpectedToken` | Token did not match any expected alternative |
| UP0002 | `UnexpectedEndOfInput` | Input ended while more tokens were expected |
| UP0003 | `UnknownRule` | A rule reference could not be resolved |
| UP0004 | `UnknownLexerCommand` | Lexer command name is not recognized |
| UP0005 | `MissingImport` | An `import` target grammar could not be found |
| UP0006 | `ImportedGrammarNotFound` | Imported grammar file is absent from the resolver |
| UP0007 | `LexerRuleNotAllowedInParserGrammar` | A lexer rule was declared inside a `parser grammar` |
| UP0008 | `ParserRuleNotAllowedInLexerGrammar` | A parser rule was declared inside a `lexer grammar` |

### UP1xxx — Unsupported / ignored / partial behavior

| Code | Name | Trigger |
|---|---|---|
| UP1001 | `EmbeddedActionStoredNotExecuted` | An embedded `{...}` action is stored but not executed at runtime |
| UP1002 | `SemanticPredicateStoredNotEnforced` | A `{...}?` predicate is stored but not enforced |
| UP1003 | `ReturnsPartiallyApplied` | A `returns [...]` clause is parsed but only partially applied |
| UP1004 | `TokensBlockIgnored` | A `tokens { ... }` block is parsed but not converted |
| UP1005 | `ChannelsBlockIgnored` | A `channels { ... }` block is parsed but not converted |
| UP1006 | `ActionIgnored` | A top-level `@...` action block is recognized but not executed |
| UP1007 | `InlineActionStoredNotExecuted` | An inline `@init`/`@after` action is stored but not executed |
| UP1008 | `LocalsIgnored` | A `locals [...]` clause is parsed but ignored |
| UP1009 | `RuntimeGeneratorMismatch` | A feature is supported in one pipeline (runtime or generator) but not the other |
| UP1010 | `DirectLeftRecursionDetected` | Direct left recursion was detected and handled in a parser rule |
| UP1011 | `IndirectLeftRecursionNotSupported` | Indirect left recursion is not currently supported |
| UP1012 | `LeftRecursiveRuleWithoutBaseAlternative` | A left-recursive rule has no non-recursive base alternative |
| UP1013 | `AmbiguousAlternativesPruned` | Equivalent alternatives were pruned using alternative priority |
| UP1014 | `StaticDuplicateAlternativeRemoved` | A duplicate alternative was removed at resolution time |
| UP1015 | `ParseBranchPruned` | A parse branch was eliminated during runtime branch selection |
| UP1016 | `ParseMemoHit` | A memoized result was reused for a parser rule evaluation |
| UP1017 | `ParseMemoMiss` | No memoized result existed for a parser rule evaluation |
| UP1018 | `LeftRecursivePrecedencePartiallySupported` | Left-recursive precedence predicates are only partially handled compared to ANTLR4 |
| UP1019 | `UnsupportedAntlrLanguageOptionIgnored` | The ANTLR4 `language` option is not supported and is silently ignored |

### UP5xxx — Warnings (recovery / best-effort)

| Code | Name | Trigger |
|---|---|---|
| UP5001 | `BestEffortRecoveryUsed` | Best-effort recovery was applied during parsing |
| UP5002 | `ExpectedTokenMissing` | An expected token was absent |
| UP5003 | `FallbackStrategyUsed` | A fallback parsing strategy was activated |
| UP5004 | `TrailingTokensAfterParse` | Unconsumed tokens remained after the root rule |
| UP5005 | `AmbiguousConstructResolvedHeuristically` | An ambiguous construct was resolved by heuristic |
| UP5006 | `DefaultBehaviorApplied` | A grammar construct fell back to a default behavior |

## Related packages

- `omy.Utils.Parser`
- `omy.Utils.Parser.Generators`

See the repository root README for installation and package selection guidance.
