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

## 3. Resource limits (S-06 design)

### Public surface

| Type | Role |
|---|---|
| `ParserResourceLimits` (immutable record) | Eight positive limits, `Default`, `Unlimited`, validated `init` setters (`ArgumentOutOfRangeException` below 1). |
| `ParserResourceLimitException : InvalidOperationException` | Controlled refusal: `Kind`, `Phase`, `Limit`, `Observed` (first attempted value over the limit, or the recursion depth for `Stack`), `Offset` (input character offset when known). No input text. |
| `ParserResourceLimitKind` | `InputCharacters`, `Tokens`, `LexerSteps`, `LexerDepth`, `ParseDepth`, `ParserSteps`, `ParseNodes`, `RetainedStates`, `Stack`. |
| `ParserResourcePhase` | `Lexer`, `Parser`, `TreeCompilation`. |
| `LexerEngine(definition, policy, limits)`, `ParserEngine(definition, policy, limits)`, `CompiledGrammar(definition, policy, limits)` | Additive constructors; every existing constructor keeps its signature and uses `ParserResourceLimits.Default`. Each type exposes `ResourceLimits`. |

Generated grammars (`XGrammar.Grammar`, `Parse`, `ParseWithEmbeddedCode`), `Antlr4GrammarConverter`,
`SyntaxColorisationGrammar` and the CSyntax/VBSyntax front ends use the defaults. A trusted caller that needs more builds
its own `new CompiledGrammar(XGrammar.Build(), policy, ParserResourceLimits.Unlimited)` (or any `with` variant).

### Budget ownership

- One private `ParserResourceBudget` per tokenization (created when the `Tokenize` enumeration starts) and per parse
  (owned by the per-parse `ParseContext`). It is never stored in a static, never shared between engines, threads or
  operations, and is discarded when the operation ends; the engines' internal `LastResourceUsage` snapshots are
  diagnostics only.
- Counters only grow. Backtracking restores token positions and managed semantic state, never consumed budget: steps,
  nodes and retained states charged by a rejected or pruned branch stay charged.
- The current depth is restored by `finally` blocks on every path, so a speculative branch never leaves depth behind.
- Every check runs before its counter would cross the limit, so no counter overflows (`long` counters, `int` depth).
- Limits never depend on wall-clock time.

### What is charged

| Dimension | Charged at |
|---|---|
| Lexer step | every `TryMatchContent` dispatch (one per candidate rule at a token start, then one per literal, range, set, rule reference, sequence, alternation, alternative, quantifier iteration, negation, command, action and predicate match attempt). Skipped tokens are charged as steps only. |
| Lexer depth | every active `TryMatchContent` dispatch (recursive lexer rules and fragments). |
| Input characters | every character read from the `TextReader` (end of input is not charged); `CompiledGrammar` also refuses an oversized string before any work. |
| Tokens | every emitted token, any channel, lexer-extension and `ERROR` tokens included, before it is yielded. |
| Parser step | every `ParseRule` (memo hits included), every `TryParseContent` dispatch, every scheduled alternative attempt, every look-ahead probe (preparation and execution), every quantifier iteration attempt, every left-recursive extension attempt. Negation probes are charged through the dispatch of the probed content. |
| Parse depth | every active `ParseRule` and `TryParseContent` (rule invocations and content nesting alike). |
| Parse nodes | every `ParserNode`, `QuantifierNode` and `LexerNode` built by the engine, empty predicate/action nodes included, in accepted and rejected branches alike. |
| Retained states | every new visited-state key, continuation entry, completed (memoized) result and look-ahead cache entry, charged before insertion. A refused insertion stores nothing and refuses the whole parse; nothing is evicted or silently dropped, so cached results never change meaning. |

### Stack guard

`RuntimeHelpers.TryEnsureSufficientExecutionStack()` runs at every guarded recursion boundary (`ParseRule`,
`TryParseContent`, `TryMatchContent`, `ParseTreeCompiler.Visit`). Between two boundaries the runtime executes a bounded
number of frames (section 1), so the guard's fixed headroom covers them. `StackOverflowException` is never caught. The
guard cannot be disabled. `ParseTreeNavigator.Descendants()` was converted to an explicit-stack traversal (same
depth-first pre-order) so it no longer nests one iterator per tree level.

### Exception and state semantics

- A refusal is never a syntax error: it is not converted into an `ErrorNode`, no truncated tree is returned.
- Exceptions thrown by injected policies (predicate evaluators, action executors, state managers, lifecycle executors,
  frame managers, rule-call policies, lexer extensions) propagate unchanged; they are never wrapped into a refusal.
- On a refusal every entered invocation frame is exited (`Exit`), every rule call gets `AfterRuleCall`, the active
  rule-frame set and the lexer's active rule references are released by the existing `finally` blocks, and the parse's
  registry, memo and look-ahead state is cleared. The next operation on the same engine starts clean.
- **Lifecycle change:** registry, memo and look-ahead state is now released when every parse ends, not only when the next
  parse starts. Before S-06 a long-lived engine (for example the shared CSyntax grammar) kept the state of its last
  parse alive (437 MB after the historical 20,000-term input).
- Limits count parser-managed work only. They cannot interrupt user code running inside a policy callback; such code is
  charged only for the parser work it causes. No `Thread.Abort`, asynchronous cancellation or broad catch is used.

## 4. Default thresholds and rationale

| Limit | Default | Rationale (measured) |
|---|---|---|
| `MaxInputCharacters` | 1,000,000 | Coarse input-size bound; the historical 20,000-term input has 168,887 characters. |
| `MaxTokens` | 500,000 | At most one token per character in practice; 12.5 times the historical input's 39,999 tokens. |
| `MaxLexerSteps` | 50,000,000 | CSyntax needs about 14.5 steps per character (2.45 M for the historical input), so ordinary input meets `MaxInputCharacters` first; quadratic lexer patterns are stopped after a few thousand characters. |
| `MaxLexerDepth` | 4,000 | Ordinary tokens need depth 4 to 5; a recursive lexer rule needs about 4 levels per nesting level, so 4,000 accepts about 1,000 nested levels (the Windows 1 MiB stack guard refuses at about 2,600). |
| `MaxParseDepth` | 4,000 | One right-recursive rule level costs 3 depth units, one CSyntax parenthesis level about 33. On Windows 1 MiB stacks the stack guard refuses first (about 620–665 units: CSyntax 19 pairs accepted in Release, baseline crashed at 20); on Linux 8 MiB main threads the stack guard would refuse at about 4,100–4,350 units, so 4,000 makes that refusal deterministic while keeping the baseline capacity (CSyntax 110 pairs accepted; baseline crashed at 150). |
| `MaxParserSteps` | 10,000,000 | The historical input needs 3.94 M steps (about 98 per token). |
| `MaxParseNodes` | 5,000,000 | The historical input builds 260,021 nodes, failed branches included. |
| `MaxRetainedStates` | 5,000,000 | The historical input retains 3.0 M states (about 75 per token, about 145 bytes each before S-06), which bounds the live registry memory of one parse. |

## 5. Stack-safety results (after S-06)

Campaign `results/2026-11-19/final-{default,unlimited}-*-nesting-*.csv`: 12 families × 20 sizes (5 to 50,000) in one
process per run, `-ContinueAfterRefusal` so every larger size runs too, Windows Release (main, pool, 256 KiB), Windows
Debug (main, 256 KiB), Linux Release (main, 256 KiB) and Linux Debug (main), with default and unlimited limits:
**3,948 isolated runs including the flat and miscellaneous families, 1,590 `ok`, 2,358 `resource-limit`, 0 stack
overflows, 0 crashes, 0 timeouts.**

Largest size accepted with the default limits, and the kind of the first refusal:

| Scenario | Win main / pool | Win 256 KiB | Win Debug main | Win Debug 256 KiB | Linux main | Linux 256 KiB | Linux Debug main |
|---|---|---|---|---|---|---|---|
| `P-csyntax-parens` | 15 / Stack | < 5 / Stack | 10 / Stack | < 5 / Stack | 100 / ParseDepth | < 5 / Stack | 100 / Stack |
| `P-csyntax-unary` | 200 / Stack | 5 / Stack | 150 / Stack | < 5 / Stack | 1000 / ParseDepth | 5 / Stack | 1000 / Stack |
| `P-vbsyntax-parens` | 15 / Stack | < 5 / Stack | 10 / Stack | < 5 / Stack | 100 / ParseDepth | < 5 / Stack | 75 / Stack |
| `P-vbsyntax-unary` | 200 / Stack | 5 / Stack | 150 / Stack | < 5 / Stack | 1000 / ParseDepth | 5 / Stack | 1000 / Stack |
| `P-g-unary` | 200 / Stack | 15 / Stack | 150 / Stack | 15 / Stack | 1000 / ParseDepth | 20 / Stack | 1000 / Stack |
| `P-g-rulechain` | 75 / Stack | 5 / Stack | 50 / Stack | < 5 / Stack | 500 / Stack | 5 / Stack | 300 / Stack |
| `P-g-quantifier` | 100 / Stack | 5 / Stack | 75 / Stack | 5 / Stack | 500 / ParseDepth | 10 / Stack | 500 / Stack |
| `P-g-alternation` | 75 / Stack | 5 / Stack | 75 / Stack | 5 / Stack | 500 / Stack | 5 / Stack | 500 / Stack |
| `P-g-leftrec` | 200 / Stack | 15 / Stack | 150 / Stack | 10 / Stack | 1000 / ParseDepth | 20 / Stack | 1000 / Stack |
| `P-g-epsilon` | 200 / Stack | 15 / Stack | 150 / Stack | 10 / Stack | 1000 / ParseDepth | 20 / Stack | 1000 / Stack |
| `P-g-lexrec` | 500 / Stack (lexer) | 50 / Stack | 500 / Stack | 40 / Stack | 500 / LexerDepth | 50 / Stack | 500 / LexerDepth |
| `P-g-ambiguous` | 100 / Stack | 5 / Stack | 75 / Stack | 5 / Stack | 500 / ParseDepth | 10 / Stack | 500 / Stack |

Compared with the baseline (section 2), the accepted sizes on 1 MiB and 8 MiB main threads are unchanged except
`P-g-alternation` on Windows (100 → 75), CSyntax parentheses on Windows Debug (15 → 10) and `P-g-lexrec` on Linux (2,000 → 500, the deterministic `MaxLexerDepth`): the guard's headroom costs the last levels before the physical limit;
every size above them is now refused instead of crashing. 256 KiB threads lose most of their former capacity to the
guard's fixed headroom (for example `P-g-unary` 30 → 15 on Windows), but no longer crash. With unlimited limits the
stack guard alone stops every family on every configuration (`final-unlimited-*`). The stack guard is the first refusal
wherever the depth limit is above the physical capacity (all Windows configurations, all 256 KiB threads, Linux Debug);
`ParseDepth`/`LexerDepth` refusals are deterministic and platform-independent.

The 42-character ETC-16 reproducer (`P-csyntax-parens` 20) is refused with `Parser/Stack` on Windows 1 MiB threads and
accepted on Linux 8 MiB threads. The ANTLR4 meta-grammar (`Antlr4GrammarConverter`) is refused on 256 KiB threads even
for small grammar texts (controlled; the probe converts grammars on a 16 MiB helper thread).

## 6. Allocation results (after S-06)

Allocation profile (probe `--alloc-profile`, `GCAllocationTick` sampling, CSyntax sum of 1,000 terms): before S-06 the
lexer spent about 90 % of tokenization allocations re-filtering and re-sorting every mode's rules for every token
(boxed enumerators in `IsPureLiteralRule`, `OrderBy` buffers, per-candidate command/action lists); the parser's volume
was spread over scheduling records (`ActiveParseState`, 9 %), repeated sorts and enumerators, look-ahead/registry
dictionary growth, structural/continuation metadata arrays, observation payloads and per-call delegates. The measured,
behavior-neutral changes are listed in `Utils.Parser/ROADMAP.md` Phase 9. Work counters (steps, nodes, retained states)
are identical before and after, which confirms the same parse was performed.

Cumulative allocation, Windows Release, fresh grammar per process (`baseline-win-flat.csv` → `final-default-win-flat.csv`):

| Terms | Operation | Before | After | Change |
|---|---|---|---|---|
| 1,000 | CSyntax tokenize | 34 MB | 8.5 MB | −75 % |
| 20,000 | CSyntax tokenize | 682 MB | 174 MB | −75 % |
| 1,000 | CSyntax compile | 294 MB | 181 MB | −38 % |
| 5,000 | CSyntax compile | 1,457 MB | 895 MB | −39 % |
| 20,000 | CSyntax compile | 5,861 MB | 3,613 MB | −38 % |
| 20,000 | VBSyntax compile | 4,655 MB | 2,741 MB | −41 % |
| 100,000 tokens | CSyntax tokenize, invalid input | 1,727 MB | 458 MB | −73 % |

The reduction is a constant factor at every size; allocation stays linear (about 21 KB per character for CSyntax
compile, 16 KB for VBSyntax). Peak working set is unchanged (about 600 MB for the 20,000-term CSyntax input): the live
peak is the registry state retained during the parse (3.0 M entries), which `MaxRetainedStates` now bounds and which is
released when the parse ends (live memory after the parse: 437 MB → about 230 MB including the result tree). Parse
counters and allocations are identical on Linux (`final-default-linux-flat.csv`). Elapsed times are indicative only
(the final campaign shared the machine with test runs); the 20,000-term CSyntax compile took 6.0 s before and 4.5 s
after in an undisturbed run.
## 7. Shared-grammar concurrency (independent issue #709)

`CSyntaxTokenParser` and `VBSyntaxTokenParser` share one lazily-initialized static `CompiledGrammar` per dialect, while
`CompiledGrammar`, `LexerEngine` and `ParserEngine` are documented as not thread-safe (per-parse engine state:
`_activeRuleFrames`, registry/memo/look-ahead, lexer mode stack and active rule references). The probe
`ExpressionResourceProbe parser-concurrency 8 400` (8 threads × 400 alternating `Parse`/`Tokenize` calls compared with
single-threaded references) measured:

| Build | Corrupted trees | Corrupted token lists | Exceptions |
|---|---|---|---|
| base `e0013a11` | 893 | 0 | 668 (`InvalidOperationException` 381, `IndexOutOfRangeException` 263, `ArgumentException` 15, `NullReferenceException` 8, `ArgumentNullException` 1) |
| S-06 | 818 | 0 | 730 (`InvalidOperationException` 407, `IndexOutOfRangeException` 283, `NullReferenceException` 22, `ArgumentException` 18) |
| 1 thread | 0 | 0 | 0 |

The defect is pre-existing and independent of S-06: budgets are owned by each operation (`ParseContext`, per-enumeration
lexer budget) and stay enforced under concurrent misuse. Corrupted trees are returned silently. Fixing it (per-call or
per-thread engines, per-operation engine state, or synchronization) needs throughput measurements and is tracked
separately as issue #709; S-06 does not add locking. Until then, callers must not use the CSyntax/VBSyntax front ends
from several threads at once.

## 8. Remaining limitations and follow-up

- **Stack capacity.** About 1.3 KB of stack per depth unit (Windows Release): a 1 MiB thread accepts 19 CSyntax
  parenthesis levels and a 256 KiB thread accepts unparenthesized CSyntax expressions but refuses `(x0)` (the baseline parsed 2 levels there and crashed at 3: the guard's fixed headroom, reserved by `TryEnsureSufficientExecutionStack`, costs the remaining capacity of very small stacks); the ANTLR4 meta-grammar used by `Antlr4GrammarConverter` is
  refused (controlled `Stack`) on a 256 KiB thread even for small grammar texts. These are controlled refusals, not
  crashes; raising capacity needs fewer frames per recursion cycle or an explicit-stack execution model (not attempted:
  it would replace the parser execution model).
- **Exception filters.** A `ParserResourceLimitException` of kind `Stack` is thrown with only the guard's headroom left.
  `catch` blocks run after unwinding and are safe; an exception *filter* (`catch ... when (...)`) on the caller's side runs
  before unwinding, on the nearly exhausted stack: keep such filters trivial (the S-06 probe was itself caught by this).
- **Allocation.** Parsing remains linear at about 21 KB allocated per input character (CSyntax compile); live memory is
  dominated by retained registry metadata (visited states and continuations are write-only in production). Bounding or
  removing that metadata needs a metadata-lifecycle design.
- **Wall-clock time and process memory** are not limited; the budgets bound parser-managed work and retained states.
- **User code** inside policy callbacks is not interrupted and not covered by the stack guard beyond its headroom.
- **Concurrency**: see section 7.
