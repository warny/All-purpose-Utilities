# Parser Resource Safety (S-06: ETC-16 / ETC-18)

## Purpose

Canonical reference for the resource safety of `Utils.Parser` before an expression tree exists: recursion depth and
execution-stack safety (ETC-16), bounded work, bounded node and state growth, and allocation volume (ETC-18). The
expression-tree engine budgets of the ETC roadmap (`ExpressionResourceLimits`) do not cover parsing.

Raw measurements: `tests/ExpressionResourceProbe/results/2026-11-19/` (probe: `tests/ExpressionResourceProbe/ParserProbe.cs`).

## 1. Recursive call graph

Every recursive path of the runtime goes through one of the following cycles. Method names are those of
`Utils.Parser/Runtime`.

### Parser (`ParserEngine`)

```
C1  rule recursion
    ParseRule
      -> TryParseScheduledAlternatives                 (also ParseLeftRecursiveRule for the seed)
           -> SchedulingPreparation.Prepare             (non-recursive: probes, descriptors, metadata)
           -> AlternativeScheduler.Run
                -> parseAlternative lambda
                     -> ScheduledAlternativeExecutor.Execute
                          -> parseAlternative lambda
                               -> TryParseContent -> ContentParsers[type]
                                    -> TryParseSequence / TryParseQuantifier / TryParseNegation -> TryParseContent ...
                                    -> TryParseRuleRef -> ParseParserRuleReference -> ParseRule        (cycle)

C2  content nesting inside one rule (no rule invocation)
    TryParseContent -> TryParseSequence | TryParseQuantifier | TryParseNegation | TryParseAlternative -> TryParseContent
    TryParseContent -> TryParseAlternation -> TryParseScheduledAlternatives -> ... -> TryParseContent

C3  direct left recursion
    ParseRule -> ParseLeftRecursiveRule -> TryExtendLeft (loop)
      -> TryParseRecursiveAlternative -> TryParseLeftRecursiveTail
           -> ParseParserRuleReference -> ParseRule      (right operand: bypasses TryParseContent)
           -> TryParseContent                            (other tail items)
```

| Cycle | Consumes a token per level? | Same rule re-entered? | State acquired per level | Released in `finally`? | Recursion without rule depth? | Repeated by failed branches? |
|---|---|---|---|---|---|---|
| C1 | no (rule chains and zero-width rules recurse without consuming) | only blocked at the same token position (`_activeRuleFrames`) | `_activeRuleFrames` entry, invocation frame, rule-call policy context, execution-state snapshots, registry/memo/look-ahead entries | frame entry, invocation frame (`Exit`), rule-call policy (`AfterRuleCall`): yes; registry entries: kept until the parse ends | no | yes: every alternative is attempted; rule results are memoized per (rule, position, precedence, state key) |
| C2 | no | no rule involved | execution-state snapshots, per-sequence visited-state set | snapshots restored on failure | **yes** | yes (inline content is not memoized) |
| C3 | the tail usually consumes an operator; the right operand re-enters `ParseRule` | yes, at a later position | as C1 | as C1 | no | yes (every recursive alternative is attempted at each extension) |

`_activeRuleFrames` detects re-entry of the same rule at the same token position only. It bounds neither ordinary
recursion where successive invocations advance (`'(' e ')'`), nor recursion through different rule names, nor C2.

### Lexer (`LexerEngine`)

```
L1  MatchLongest -> TryMatchContent -> ContentMatchers[type]
      -> MatchSequenceContent | MatchAlternationContent | MatchAlternativeContent | MatchQuantifierContent
         | MatchNegationContent | MatchRuleRefContent -> TryMatchContent              (cycle)
```

`_activeRuleRefs` blocks the same lexer rule at the same character offset only: a recursive lexer rule such as
`NEST : '(' NEST? ')' ;` recurses once per input character.

### Parse-tree consumers

```
T1  ParseTreeCompiler.Visit -> Visit (one level per tree level)
T2  ParseTreeNavigator.Descendants -> child.Descendants (one nested iterator per tree level)
```

### Guard placement

Every parser cycle (C1, C2, C3) contains `ParseRule` or `TryParseContent`; every lexer cycle contains
`TryMatchContent`; T1 contains `Visit`. Between two consecutive passes through these methods, the number of frames is a
constant of the runtime (at most the C1 chain above, about a dozen frames), independent of the grammar and of the input.
These methods are therefore the guarded recursion boundaries. Non-guarded recursive helpers (`ContainsPredicateOrAction`,
`FindPrecedenceLevel`, `ParserLookaheadProbe.ProbeContent`, structural prefix extraction) recurse over the grammar AST
only, never over input, and never follow parser rule references.

## 2. Baseline (before S-06)

Environment: base `e0013a11`, Windows 11 Pro 10.0.26300 x64 (i7-12700KF, 20 logical CPUs, 64 GB), WSL2 Ubuntu (same
machine) for Linux, .NET 8.0.34 runtime (probe target `net8.0`, `Utils.Parser` targets `net8.0`), SDK 11.0.100, Release
unless stated, default thread stacks (Windows main thread 1 MiB, Linux main thread 8 MiB) and dedicated 256 KiB threads.

### ETC-16: nesting

Largest size completed before the process was terminated by a stack overflow (the next size of the campaign crashed;
sizes 5, 10, 15, 20, 25, 30, 40, 50, 75, 100, 150, 200, 300, 500, 1000, 2000, ...):

| Scenario | Win main | Win pool | Win 256 KiB | Win Debug main | Linux main | Linux 256 KiB |
|---|---|---|---|---|---|---|
| `P-csyntax-parens` (pairs) | 15 | 15 | < 5 | 15 | 100 | < 5 |
| `P-csyntax-unary` (operators) | 200 | 200 | 20 | 150 | 1000 | 30 |
| `P-vbsyntax-parens` | 15 | 15 | < 5 | 10 | 100 | < 5 |
| `P-vbsyntax-unary` | 200 | 200 | 20 | 150 | 1000 | 25 |
| `P-g-unary` (`e : '-' e`) | 200 | 200 | 30 | 150 | 1000 | 40 |
| `P-g-rulechain` (3 rules/level) | 75 | 75 | 10 | 50 | 500 | 15 |
| `P-g-quantifier` | 100 | 100 | 15 | 75 | 500 | 20 |
| `P-g-alternation` (inline) | 100 | 100 | 15 | 75 | 500 | 15 |
| `P-g-leftrec` (parenthesized) | 200 | 200 | 30 | 150 | 1000 | 40 |
| `P-g-epsilon` (zero-width levels) | 200 | 200 | 30 | 150 | 1000 | 40 |
| `P-g-lexrec` (lexer recursion) | 500 | 500 | 100 | 500 | 2000 | 100 |
| `P-g-ambiguous` (tokens, `parse`) | 100 (misc campaign, 1000 crashed) | - | - | - | - | - |

Every family ends with an uncatchable stack overflow; no family ends with a controlled exception. One CSyntax
parenthesis level costs about 12 rule invocations (rule depth 73 at 5 pairs, 193 at 15) and roughly 50 KB of stack; a
plain right-recursive rule level costs about 4 KB on Windows Release.

### ETC-18: flat sums

`x0 + x1 + ... + x(n-1)`, Windows Release, fresh grammar per process:

| Terms | Characters | Operation | Time (ms) | Allocated | Live after parse | Peak working set |
|---|---|---|---|---|---|---|
| 1,000 | 6,887 | CSyntax tokenize | 55 | 34 MB | 0.3 MB | 57 MB |
| 1,000 | 6,887 | CSyntax parse | 523 | 274 MB | 23 MB | 90 MB |
| 20,000 | 168,887 | CSyntax tokenize | 706 | 682 MB | 5.6 MB | 69 MB |
| 20,000 | 168,887 | CSyntax parse | 4,842 | 5,436 MB | 437 MB | 595 MB |
| 20,000 | 168,887 | CSyntax compile | 5,994 | 5,861 MB | 439 MB | 602 MB |
| 20,000 | 168,887 | VBSyntax parse | 4,332 | 4,589 MB | 390 MB | 556 MB |
| 20,000 | 168,887 | VBSyntax compile | 7,982 | 4,655 MB | 392 MB | 553 MB |

Allocation and retained memory are linear in the input (about 32 KB allocated and 2.6 KB retained per character for
CSyntax parse); tokenization alone allocates about 4 KB per character. For the 20,000-term CSyntax parse (40,000
tokens) the engine performed 300,036 rule invocations and 1,140,099 alternative attempts, and retained 300,036 memo
results, 1,060,095 visited-state keys, 560,059 continuation entries and 1,080,089 look-ahead entries until the next
parse on the same engine (about 3 million entries, about 145 bytes each). Cumulative allocation (5.4 GB) is an order of
magnitude above the live peak (0.6 GB working set).
