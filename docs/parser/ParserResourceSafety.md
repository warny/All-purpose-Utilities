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
| Tokens | every emitted token, any channel, lexer-extension and `ERROR` tokens included, before it is yielded; and every token supplied directly to `ParserEngine.Parse(IEnumerable<Token>)`, any channel (phase `Parser`): a list longer than the limit is refused from its count, a lazy sequence before the first token over the limit is examined or stored. |
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
## 7. Shared-grammar concurrency (issue #709)

### Defect

`CSyntaxTokenParser` and `VBSyntaxTokenParser` share one lazily-initialized static `CompiledGrammar` per dialect, and
every generated grammar class shares one static `Grammar`. Before #709 a `CompiledGrammar` owned a single `LexerEngine`
and a single `ParserEngine`, whose per-operation state (`_activeRuleFrames`, the `ParserStateRegistry` visited states,
continuations and memoized results, the `ParserLookaheadCache`, the lexer mode stack, pending `more` text and active
rule references) was cleared and refilled by every call. Concurrent calls cleared each other's state, selected memoized
results computed for another input, and corrupted the non-thread-safe collections themselves. The S-06 probe
(`ExpressionResourceProbe parser-concurrency 8 400`) first measured it on `e0013a11` and S-06: 818–893 silently
corrupted trees and 668–730 exceptions out of 3,200 operations.

### Reproduction campaign

`ExpressionResourceProbe parser-concurrency-campaign` (see `tests/ExpressionResourceProbe/README.md`) compares every
concurrent result with a sequential oracle computed on fresh `CompiledGrammar` instances: canonical tree rendering (node
types, rule names, modes, spans with line and column, token rule/channel/text, error messages), full token metadata,
refusal classification (kind, phase, limit, observed value, offset) and, for the compile surfaces, the values of the
compiled delegate. Seven surfaces (CSyntax and VBSyntax token parsers, the generated static CSyntax grammar, a shared
ANTLR4 grammar with lexer modes, `more`, a hidden channel and left recursion, the same grammar under tight limits, and
shared CSyntax/VBSyntax expression compilers), three workloads (parse, tokenize, mixed), 2/4/8/16 dedicated threads
re-synchronized by a barrier every 25 operations, deterministic per-worker seeds, one process per surface. Windows
Release, 200 operations per worker, 3 seeds (252 runs, 378,000 operations):

| Build | Clean runs | Wrong trees | Wrong token lists | Wrong failures (refusal or compile error differs) | Unexpected exceptions | Runs leaving the shared surface corrupted |
|---|---|---|---|---|---|---|
| base `4200e0c3` | 0 / 252 | 40,328 | 31,337 | 16,369 | 198,323 | 172 |
| #709 | 252 / 252 | 0 | 0 | 0 | 0 | 0 |

Baseline exceptions: `InvalidOperationException` 167,360, `NullReferenceException` 25,891, `IndexOutOfRangeException`
3,542, `LexerValidationException` 1,145, `ArgumentException` 383, `NotSupportedException` 2. Wrong results appear from
2 threads on and grow with the thread count; in most runs the shared grammar stayed corrupted after the concurrent phase
(sequential calls kept failing), so the defect was not limited to the overlapping calls. The corrected build also
returns every expected refusal and compile error exactly (17,635 expected failures). Debug and Linux campaigns:
every configuration below is clean for the fix and never clean for the base (Windows Debug and both Linux runs
through WSL2 Ubuntu with self-contained `linux-x64` probes; Debug runs use 120 operations per worker and 2 seeds):

| Configuration | Runs | Base: clean / wrong trees / wrong token lists / wrong failures / unexpected exceptions / corrupted after | #709: clean, all other counters |
|---|---|---|---|
| Windows Release | 252 | 0 / 40,328 / 31,337 / 16,369 / 198,323 / 172 | 252 / 252, 0 |
| Windows Debug | 168 | 0 / 20,457 / 17,642 / 6,370 / 56,049 / 52 | 168 / 168, 0 |
| Linux Release | 252 | 0 / 48,634 / 28,890 / 16,618 / 172,641 / 141 | 252 / 252, 0 |
| Linux Debug | 168 | 0 / 22,353 / 20,952 / 6,158 / 52,272 / 48 | 168 / 168, 0 |

The expected failures of the corrected build are identical on Windows and Linux (17,635 in the Release
configurations, 6,918 in the Debug ones). Raw results: `tests/ExpressionResourceProbe/results/2026-11-19/issue709-*`.
### Fix

Every `Tokenize` / `Parse` call now creates its own `LexerEngine` and `ParserEngine` over two internal preparations
built once by the `CompiledGrammar` constructor and never mutated: `LexerEngine.Preparation` (case sensitivity, keyword
trie and candidate rules of every mode) and `ParserEngine.Preparation` (scheduling shapes keyed by alternative-list
reference, stable priority order). The grammar model is immutable (immutable arrays and init-only records), so sharing
the preparations cannot make them stale. No lock, `ThreadLocal`, pool or static mutable state is used, the public
constructors of the engines build their own preparation as before, and the internal `CompiledGrammar.Lexer` / `Parser`
diagnostic accessors return the engines of the last call that started. Each operation still owns its S-06 budget;
`SharedGrammarResourceIsolationTests` (Security) checks that concurrent refusals on one shared grammar keep their exact
classification and that an operation after a refusal starts from a clean budget. Ownership table:
`RuntimeStateOwnership.md`.

### Cost

Windows Release, CSyntax grammar, `parser-concurrency-bench`, two rounds in opposite order (machine otherwise lightly
loaded, about 20 % CPU from other processes). Construction: one `CompiledGrammar` 258 µs / 280 KB, unchanged (lexer
preparation about 41 µs / 102 KB, parser preparation about 186 µs / 177 KB, built once per grammar). The sequentially
correct reference for parallel throughput on the base is one grammar per thread (`perthread`); the corrected build is
measured with one grammar shared by every thread (`shared`). Operations per second (mean of the two rounds) and bytes
allocated per operation:

| Workload | 1 thread base → #709 | 8 threads base → #709 | 16 threads base → #709 | Bytes / op base → #709 |
|---|---|---|---|---|
| short `a + b * c` | 1,620 → 1,568 | 2,696 → 2,592 | 2,377 → 2,047 | 662 K → 775 K (+17 %) |
| medium (call, index, member, ternary) | 284 → 261 | 404 → 370 | 334 → 280 | 3.03 M → 3.55 M (+17 %) |
| deep (15 nested parentheses) | 132 → 106 | 94 → 86 | 66 → 58 | 5.69 M → 6.80 M (+19 %) |
| flat (200-term sum) | 28 → 23 | 36 → 33 | 32 → 30 | 28.2 M → 32.9 M (+17 %) |
| tokenize (200-term sum) | 575 → 518 | 1,554 → 1,513 | 1,456 → 1,417 | 1,698,648 → 1,699,000 |
| mixed parse / tokenize | 700 → 620 | 1,154 → 973 | 906 → 817 | 1.32 M → 1.47 M (+12 %) |

Throughput peaks at 2–4 threads in both builds (allocation and GC bound) and is otherwise within the noise of the two
rounds for tokenization; parsing is 3–20 % slower per call. The cause is measured, not the engine objects themselves:
`parser-engine-reuse` parses the same tokens with one reused engine and with a fresh engine per call, and the difference
(short input +112 KB, medium +0.5 MB, deep +1.1 MB, flat +4.8 MB once the 177 KB preparation of the public constructor
is subtracted) is the regrowth of the registry and look-ahead collections, which a reused engine kept at their previous
capacity after `Clear()`. One `CompiledGrammar` per call (the naive strategy, `percall`) costs the full construction on
every call: short 1,060 operations per second and 1.06 MB per operation. Reusing collection capacity (for example a
bounded per-grammar engine pool) would remove the regression but adds a state-reuse path that needs its own leakage
audit; it is left to a separate change.
## 8. Remaining limitations and follow-up

- **Small stacks are not supported for useful work.** The parser guarantees a controlled refusal, not successful parsing, on small stacks: on a 256 KiB thread even `(x0)` is refused by CSyntax/VBSyntax and `Antlr4GrammarConverter` refuses small grammar texts. Run parsing on threads of at least 1 MiB (the .NET default on Windows) when inputs must succeed.
- **Stack capacity.** About 1.3 KB of stack per depth unit (Windows Release): a 1 MiB thread accepts 19 CSyntax
  parenthesis levels and a 256 KiB thread accepts unparenthesized CSyntax expressions but refuses `(x0)` (the baseline parsed 2 levels there and crashed at 3: the guard's fixed headroom, reserved by `TryEnsureSufficientExecutionStack`, costs the remaining capacity of very small stacks); the ANTLR4 meta-grammar used by `Antlr4GrammarConverter` is
  refused (controlled `Stack`) on a 256 KiB thread even for small grammar texts. These are controlled refusals, not
  crashes; raising capacity needs fewer frames per recursion cycle or an explicit-stack execution model (not attempted:
  it would replace the parser execution model).
- **Exception filters.** A `ParserResourceLimitException` of kind `Stack` is thrown with only the guard's headroom left.
  `catch` blocks run after unwinding and are safe; an exception *filter* (`catch ... when (...)`) on the caller's side runs
  before unwinding, on the nearly exhausted stack: keep such filters trivial (the S-06 probe was itself caught by this).
- **Allocation.** Parsing remains linear at about 20 KB allocated per input character (CSyntax compile). Since the
  registry-metadata audit (section 9) the registry is stored compactly, and since the look-ahead cache audit (section 10)
  so is the look-ahead cache, whose remaining share is mostly one expected-name collection per entry; the write-only registry metadata (visited states, continuations) is still stored and charged,
  because removing it changes the `RetainedStates` accounting (section 9, "Contract change not made").
- **Grammar immutability assumption.** `ParserEngine` precomputes scheduling shapes (priority order, structural descriptors) and `LexerEngine` precomputes candidate rules and keyword tries from the definition, once per `CompiledGrammar` (shared by the engines of every call, #709) or once per directly constructed engine. This relies on the documented immutability of the grammar model (`Rule`, `Alternation`, `LeftRecursiveRuleInfo` capture immutable snapshots); a future mutable model would have to invalidate these tables.
- **Wall-clock time and process memory** are not limited; the budgets bound parser-managed work and retained states.
- **User code** inside policy callbacks is not interrupted and not covered by the stack guard beyond its headroom.
- **Concurrency**: shared grammars are safe since #709 (section 7); the per-call engines cost 12–19 % more parse allocation than a reused engine, and directly constructed engines and stateful policy components remain caller-owned.

## 9. Registry metadata storage (registry-metadata audit)

### Question and audit

`ParserStateRegistry` held three collections whose memory was proportional to the parse: visited states, continuations
and completed (memoized) results. The audit (producer/consumer matrix in `RuntimeStateOwnership.md`, section
"Registry usage audit and compact storage") establishes, from the code of `6e7892a0`:

- **visited states** are written by every scheduled attempt and every left-recursive extension attempt and never read
  in production (both callers ignore the result; the left-recursion cycle rejection uses a local set);
- **continuations** are written for every rule reference, lexer tokens included, and never read in production;
- **completed results** are the memoization: read by every `ParseRule`, they carry the post-rule execution-state
  snapshots restored on memo hits. In production each invocation key stores exactly one result;
- each new entry of the three collections and of the look-ahead cache costs one `RetainedStates` unit, charged before
  insertion; duplicates are free. A duplicate can only be recognized if the entry is kept, so every write-only entry
  still matters to S-06: it is the condition under which later identical writes are not charged.

### Strategies studied

| Strategy | Result |
|---|---|
| A. Compact exact structures | **Retained.** Same identities, same deduplication, same charges and refusals, less memory (below). |
| B. Charge without materializing | Not possible exactly: without the entry, a later duplicate cannot be recognized, so it would be charged (or never charged, with a counter per attempt). Exact deduplication without storing the full key needs a hash standing for the identity, which is excluded. B therefore reduces to A. |
| C. Optional materialization | Not materializing visited states or continuations by default changes the `RetainedStates` counts (see below): a contract change, not made here. |
| D. Removing the unused writes | No production read, no diagnostic and no policy depends on the two write-only collections; their only effect is their S-06 charge. Removing them is the same contract change as C. |

Strategy A, as implemented (`ParserStateRegistry` remarks):

- visited states whose element index equals their alternative index (every key the engine records) and whose alternative
  index is in `[0, 63]` are grouped by (rule, position, precedence) in one dictionary entry with one bit per alternative;
  any other key is kept whole in an overflow set. The partition depends on the key alone, so a key is always looked up
  where it would be stored;
- continuations are one flat set of (invocation, continuation) pairs instead of one nested set per invocation (a nested
  set holding a single pair cost about 200 bytes);
- completed results keep their first result inline in the dictionary value, with an overflow list only for later results
  of the same key, which the engine never stores (a one-element `List` cost about 150 bytes per memoized invocation).

Rule names keep string value equality, as before (no interning, no reference identity), and no hash stands for an
identity; there is no shared or static storage, so concurrent and sequential engines are unaffected.

### Method

Probe `ExpressionResourceProbe parser-registry` (see `tests/ExpressionResourceProbe/README.md`): one scenario per
process on a directly constructed `ParserEngine`, warm-up on the same input. `--peak` forces a full collection when the
root rule invocation exits, the last point where every per-parse collection is complete (the engine clears them right
after), and reports the managed live bytes released by the end of the parse (`peakRegistry` = live at root exit − live
after the parse). Timing runs omit `--peak` and report the median of 7 parses. Tree, diagnostics and action/predicate
trace are digested (SHA-256) for the differential comparison. Windows 11, .NET 8.0.34, Release; base `6e7892a0` built
from a separate worktree with the same probe sources; campaigns in the order base, branch, branch, base.

Scenarios: CSyntax and VBSyntax sums (1,000 and 5,000 terms), mixed CSyntax/VBSyntax expressions (calls, members,
indexers, products, logical comparisons; 1,000 terms), CSyntax parentheses (15 pairs), 20 keyword-led alternatives
(2,000 items), common prefixes with backtracking (2,000 items), a highly ambiguous grammar (150 tokens), left recursion
(2,000-term sum; 1,000 terms with two precedence levels), a deep rule chain (300 levels), and a grammar with actions and
semantic predicates driven by a counter execution state whose key changes with every action (1,000 items: memo hits
restore snapshots). Every scenario runs with and without diagnostics, and refusals are measured at the exact threshold
(N accepted, N − 1 refused) and far below it.

### Differential results

98 configurations, 4 runs each: **0 differences** in outcome, tokens, parser steps, nodes, retained states, depth,
distinct visited states, continuations, memo keys and results, look-ahead entries, action count, tree digest,
diagnostics digest and action/predicate trace digest. Refusals keep their phase, kind, limit, observed value and input
offset. Linux (WSL2 Ubuntu, self-contained `linux-x64` probes, same .NET 8.0.34): 64 configurations, 0 differences,
with the same digests, counters and registry peaks as on Windows. The shared-grammar concurrency campaign of #709
(`parser-concurrency-campaign --ops 200 --seeds 3`, seven surfaces, Windows Release) is clean on the branch: 252 / 252
runs, 378,000 operations, 0 wrong trees, token lists or failures, 0 unexpected exceptions, the 17,635 expected failures
returned exactly. Raw results: `tests/ExpressionResourceProbe/results/2026-11-20/registry-*`.

### Memory and time

Registry peak = managed bytes released at the end of the parse (registry and look-ahead cache); allocated = mean of the
timing runs (of the `--peak` runs for the diagnostics rows); time = mean of the two medians of 7 parses per build.

| Scenario | Retained states | Registry peak base → branch | Allocated base → branch | Time base → branch |
|---|---|---|---|---|
| CSyntax sum 1,000 | 150,279 | 21.2 → 12.7 MB (−40 %) | 151.7 → 142.6 MB (−6 %) | 157 → 127 ms |
| CSyntax sum 1,000, diagnostics | 170,301 | 29.4 → 15.0 MB (−49 %) | 198.4 → 178.7 MB (−10 %) | – |
| CSyntax sum 5,000 | 750,279 | 98.3 → 56.0 MB (−43 %) | 742.9 → 698.2 MB (−6 %) | 1,148 → 965 ms |
| CSyntax sum 5,000, diagnostics | 850,301 | 138.2 → 66.1 MB (−52 %) | 971.8 → 869.5 MB (−11 %) | – |
| CSyntax mixed 1,000 | 682,265 | 95.7 → 54.4 MB (−43 %) | 706.9 → 662.8 MB (−6 %) | 1,130 → 1,007 ms |
| VBSyntax sum 5,000 | 595,165 | 86.5 → 51.4 MB (−41 %) | 624.8 → 585.4 MB (−6 %) | 894 → 596 ms (noisy) |
| VBSyntax mixed 1,000 | 435,743 | 59.7 → 35.6 MB (−40 %) | 466.3 → 443.5 MB (−5 %) | 458 → 377 ms |
| 20 alternatives, 2,000 items | 92,047 | 13.0 → 9.0 MB (−31 %) | 95.6 → 89.4 MB (−6 %) | 170 → 145 ms |
| Common prefixes, 2,000 items | 20,013 | 2.50 → 1.75 MB (−30 %) | 30.2 → 29.5 MB (−2 %) | 52 → 49 ms |
| Ambiguous, 150 tokens | 1,816 | 141 → 27 KB (−81 %) | 5.0 → 4.9 MB | within noise |
| Left recursion, 2,000 terms | 15,999 | 1.94 → 1.35 MB (−30 %) | 15.7 → 15.3 MB | 25 → 22 ms |
| Rule chain, 300 levels | 4,214 | 590 → 181 KB (−69 %) | 4.6 → 4.2 MB | within noise |
| Actions and predicates, 1,000 items | 26,689 | 3.91 → 2.32 MB (−41 %) | 31.3 → 30.0 MB (−4 %) | within noise |

Live memory after a parse is unchanged (the registry is released when a parse ends since S-06). Times of the large
inputs are lower because fewer objects are allocated and collected; the small scenarios were rerun three times
alternately with 61 parses each and stay within the noise (best times equal or lower on the branch). Breakdown, each
collection released in turn at the root exit, CSyntax sum of 2,000 terms:

| Collection | Entries | Base | Branch |
|---|---|---|---|
| visited states | 106,095 (36,042 groups) | 5.63 MB | 1.31 MB |
| continuations | 56,059 | 14.98 MB | 4.53 MB |
| completed results (including nodes reachable only through the memo) | 30,036 | 6.17 MB | 2.47 MB |
| look-ahead cache (unchanged) | 108,089 | 16.37 MB | 16.37 MB |
| total | 300,279 | 43.1 MB | 24.7 MB |

### Contract change not made (separate proposal)

After strategy A, the two write-only collections hold 5.8 MB of the 24.7 MB peak above (24 %) and 162,154 of the
300,279 charged retained states (54 %). Not recording them in production (strategies C/D) would:

- lower `RetainedStates` for the same input (CSyntax 2,000 terms: 300,279 → 138,125), so `MaxRetainedStates` would
  refuse later and at other offsets, and every documented threshold and refusal measurement would move;
- remove the observations returned by the internal `GetContinuations` and the visited-state counts (tests only, no
  production consumer);
- change neither trees, diagnostics nor actions (no production read).

Such a change needs its own PR: an explicit decision on the meaning of `MaxRetainedStates` (and possibly a lower
default, to keep the same memory bound), an opt-in metadata recording for tools that read continuations, and new
threshold measurements. The look-ahead cache, then the largest per-parse collection, received the same kind of compact
exact storage in a later change (section 10).

## 10. Look-ahead cache storage (look-ahead cache audit)

### Question and audit

After section 9 the `ParserLookaheadCache` was the largest per-parse collection (CSyntax 1,000-term sum: 7.98 of
12.66 MB of parse-scoped memory). The producer/consumer matrix is in `RuntimeStateOwnership.md`, section "Look-ahead
cache usage audit and compact storage". In short: two paths read the cache before probing (`SchedulingPreparation`,
`ScheduledAlternativeExecutor`), a hit replaces a probe and its step charge, a new entry costs one `RetainedStates` unit
charged before insertion, duplicates are free and never replace the first value. Production consumers read the kind
and the expected-token names only, but the tests compare results by record equality, so the cache must return the same
kind, token-string instances and expected-name collection instance.

### Memory characterization (before)

Probe `parser-registry --lookahead` (section 9 method; `LookaheadCacheStats.cs`) at the root exit, base `3abcb1f8`:

| Scenario | Entries | Dictionary capacity | Kinds (reject / requires parse / epsilon) | Rules | Cursor kinds | Groups (rule, position, precedence, cursor kind, cursor index) | Expected names: `null` / one name / instances / distinct values |
|---|---|---|---|---|---|---|---|
| CSyntax sum 1,000 | 54,089 | 75,431 | 47,076 / 7,013 / 0 | 42 | 2 | 18,039 (9,028 singletons) | 14,033 / 40,056 / 40,056 / 68 |
| CSyntax sum 1,000, diagnostics | 45,069 | 75,431 | 38,056 / 7,013 / 0 | 39 | 2 | 17,035 | 5,013 / 40,056 / 40,056 / 68 |
| CSyntax mixed 1,000 | 235,218 | 324,449 | 203,642 / 31,576 / 0 | 48 | 2 | 90,523 (54,351 singletons) | 73,336 / 161,882 / 161,882 / 69 |
| VBSyntax mixed 1,000 | 153,909 | 156,437 | 124,133 / 29,776 / 0 | 33 | 2 | 60,761 | 38,981 / 114,928 / 114,928 / 60 |
| 20 alternatives, 2,000 items | 42,022 | 75,431 | 40,021 / 2,000 / 1 | 2 | 1 | 2,002 (21 entries each) | 1 / 42,021 / 42,021 / 21 |
| Actions and predicates, 1,000 items | 6,339 | 8,419 | 1,339 / 4,999 / 1 | 5 | 1 | 3,670 | 4,004 / 2,335 / 2,335 / 1 |
| Left recursion, two levels | 2,800 | 4,049 | 1,400 / 1,400 / 0 | 1 | 1 | 1,400 | 0 / 2,800 / 2,800 / 2 |

Every entry has precedence ≤ 3, alternative index ≤ 20 and cursor index −1 or a small sequence position; the token
rule name and text are the same instances for all entries of one origin position (every scenario); expected-name
collections, when present, always hold exactly one name, and every entry holds its own instance.

Composition, checked against the measured release of the collection: one dictionary slot costs 76 bytes (hash code,
next index, 32-byte key with two string references and four integers, 32-byte value with three references and the kind,
plus a 4-byte bucket) times the capacity, which is 1.02 to 1.79 times the count after the last doubling; each non-`null`
expected-name collection costs 56 bytes (a 24-byte boxed `ImmutableArray<string>` and a 32-byte one-element array).
CSyntax sum 1,000: 75,431 × 76 = 5.73 MB of dictionary and 40,056 × 56 = 2.24 MB of collections, total 7.976 MB
(measured 7.976 MB). Token strings and rule names belong to the tokens and the grammar and are not counted.

### Strategies studied

| Strategy | Result |
|---|---|
| A. Grouping by (rule, position, precedence, cursor kind, cursor index) with a per-group alternative mask | Not retained. Half of the CSyntax groups hold a single entry and each group still needs one (kind, collection) slot per alternative, so a per-group array (24-byte header, regrown as alternatives arrive) replaces the per-entry key. Estimated from the distributions above: CSyntax mixed about 11.3 MB against 11.7 MB for D below, with one allocation per group and a regrowth per new alternative; only grammars with wide groups (20 alternatives: about 0.8 instead of 2.7 MB) would gain. Not prototyped. |
| B. Compact values | **Retained in part.** The token snapshot is stored once per origin position (index in a per-cache table, a separate snapshot when an entry's instances differ) and the kind in 8 bits. The expected-name collection instance is kept: replacing it by a shared or rebuilt instance changes the instance returned (and allocates a box per hit if rebuilt). |
| C. Numeric identities for repeated strings | **Retained.** Rule names map to a per-cache index (ordinal comparison, at most 1,024, `null` or overflow goes to the exact overflow store), the four `ScheduledAlternativeCursorKinds` to a 2-bit code; any other cursor kind is stored unchanged. Tables cost about 3 KB on CSyntax and are emptied by `Clear()`; no global or shared table. |
| D. Minimal dictionary optimization | **Retained as the base layout**: one `Dictionary` keyed by a 64-bit packed key (32-bit origin, 8-bit alternative, 10-bit rule index, 2-bit cursor kind, 6-bit precedence, 6-bit cursor index + 1) with a multiplicative hash, values of 16 bytes: 36 bytes per slot instead of 76. Packing the key while keeping the full 32-byte value would cost 52 bytes per slot (CSyntax sum 1,000: 3.92 instead of 2.72 MB). `CollectionsMarshal.GetValueRefOrAddDefault` was not used: it inserts before the S-06 charge and would need a removal on refusal; the frequent duplicate path already costs a single lookup. |

### Differential results

Windows 11, .NET 8.0.34, Release; base `3abcb1f8` built from a separate worktree with the same probe sources; campaigns
in the order base, branch, branch, base (`results/2026-11-20/lookahead-campaign.ps1`, compared by
`lookahead-compare.ps1`). 15 scenarios (section 9 plus `g-epsilon`: optional items and an optional nested alternation),
each in six peak modes (`--peak`, `--lookahead`, `--breakdown`, with and without diagnostics) and a timing run, and 28
S-06 limits at and below the real thresholds with and without diagnostics: **161 configurations, 4 runs each, 0
differences** in outcome, tokens, parser steps (a probe is executed exactly on a cache miss, so equal steps mean equal
probe counts), nodes, retained states, depth, registry sizes, look-ahead entries, action count, tree, diagnostics and
action/predicate trace digests, look-ahead content digest (every key and result, `null` and empty collections
distinguished) and characterization. Refusals keep their phase, kind, limit, observed value and offset.
Linux (WSL2 Ubuntu, self-contained `linux-x64` probes, same .NET 8.0.34, `lookahead-linux-campaign.sh`): 146
configurations (every peak mode of the 15 scenarios and the 28 limits, no timing), 0 differences between base and
branch, and 0 differences in any behavioral field or digest against the Windows branch campaign. The parse-scoped peaks
are byte-identical to Windows (CSyntax sum 1,000: 12,657,976 → 9,754,568 bytes on both systems; CSyntax mixed 1,000:
54.42 → 41.74 MB; 20 alternatives: 9.01 → 6.11 MB). The shared-grammar concurrency campaign of #709
(`parser-concurrency-campaign --ops 200 --seeds 3`, Windows Release, branch) is clean: 252 / 252 runs, 378,000
operations, 0 wrong trees, token lists or failures, 0 unexpected exceptions, the 17,635 expected failures returned
exactly (the same counts as in section 9). Test suites on the branch: Unit 15,052 passed / 1 skipped, Security 433 / 3
skipped, Functional 383, in Debug and in Release; expression-engine Debug job 354 / 1 skipped. The equivalence campaign
(`ParserLookaheadCacheEquivalenceTests`: 400 seeded sequences of 2,500–3,500 operations with near keys, unusual fields,
every kind, `null` / empty / repeated expected names, limits near the entry count, refusals, clears and reuse) compares
every answer, stored instance, charge and refusal with the historical cache, and fails on a deliberately introduced key
collision. Raw results: `results/2026-11-20/lookahead-*`.

### Memory and time

Cache = live bytes released by the look-ahead collections at the root exit (`--breakdown`); parse peak = live bytes
released at the end of the parse (`peakRegistry`); means of two runs per build. Allocated and time from the timing runs
(median of 7 parses per file).

| Scenario | Look-ahead entries | Cache base → branch | Parse peak base → branch | Live peak base → branch | Allocated |
|---|---|---|---|---|---|
| CSyntax sum 1,000 | 54,089 | 7.98 → 5.07 MB (−36 %) | 12.66 → 9.75 MB (−23 %) | 15.04 → 12.14 MB (−19 %) | −3.9 % |
| CSyntax sum 1,000, diagnostics | 45,069 | 7.98 → 5.07 MB (−36 %) | 15.00 → 12.10 MB (−19 %) | 106.5 → 103.6 MB (−3 %) | – |
| CSyntax sum 5,000 | 270,089 | 35.86 → 23.49 MB (−34 %) | 56.01 → 43.64 MB (−22 %) | 64.84 → 52.48 MB (−19 %) | −3.4 % |
| CSyntax mixed 1,000 | 235,218 | 33.72 → 21.05 MB (−38 %) | 54.42 → 41.74 MB (−23 %) | 61.01 → 48.34 MB (−21 %) | −3.7 % |
| VBSyntax sum 5,000 | 220,052 | 34.18 → 21.81 MB (−36 %) | 51.41 → 39.05 MB (−24 %) | 60.10 → 47.73 MB (−21 %) | −4.1 % |
| VBSyntax mixed 1,000 | 153,909 | 18.32 → 12.37 MB (−33 %) | 35.56 → 29.60 MB (−17 %) | 42.28 → 36.32 MB (−14 %) | −2.6 % |
| CSyntax parentheses, 15 | 1,957 | 374 → 216 KB (−42 %) | 491 → 330 KB (−33 %) | 1.41 → 1.26 MB | −4.9 % |
| 20 alternatives, 2,000 items | 42,022 | 8.09 → 5.18 MB (−36 %) | 9.01 → 6.11 MB (−32 %) | 10.51 → 7.61 MB (−28 %) | −6.3 % |
| Common prefixes, 2,000 items | 8,005 | 1.09 → 0.86 MB (−21 %) | 1.75 → 1.53 MB (−13 %) | 3.68 → 3.46 MB | −1.4 % |
| Left recursion, 2,000 terms | 4,000 | 531 → 482 KB (−9 %) | 1.35 → 1.30 MB (−4 %) | 3.05 → 3.01 MB | −0.6 % |
| Optional items, 1,000 | 6,174 | 808 → 542 KB (−33 %) | 1.57 → 1.30 MB (−17 %) | 2.45 → 2.18 MB | −2.8 % |
| Actions and predicates, 1,000 items | 6,339 | 770 → 504 KB (−35 %) | 2.32 → 2.05 MB (−11 %) | 3.66 → 3.40 MB | −1.7 % |

The other registry collections and the live memory after a parse are unchanged (the cache is released when a parse
ends since S-06). With diagnostics the cache gain is the same in bytes but small against the diagnostics themselves.
The breakdown of the branch matches the layout: CSyntax sum 1,000 releases 4.96 MB of packed dictionary (75,431 × 36
bytes plus the 2.24 MB of expected-name collections, which it keeps) and 0.12 MB of tables. Times: the campaign medians
are within the noise on every scenario except CSyntax sum 1,000, whose apparent slowdown (130 → 180 ms) did not
reproduce: rerun alternately 12 times with 21 parses each, the branch is faster there (median 106.7 against 122.9 ms)
and equal on VBSyntax sum 1,000 (103.1 / 103.2 ms) and the optional-items grammar (34.0 / 33.6 ms)
(`lookahead-win-timing-rerun.txt`).

### Remaining (separate proposal)

The expected-name collections are now about 45 % of the cache (CSyntax sum 1,000: 2.24 of 5.07 MB), with one instance
per entry for fewer than 70 distinct values. `ParserLookaheadProbe` builds a new collection on every probe (the shallow
expected-name list, then the `ImmutableArray` copy and its box). Returning one instance per alternative content would
remove that memory and those allocations without changing any value, any probe kind or any S-06 charge, but two results
from different probes would then share their collection and compare equal by reference where they did not, and the probe
would hold per-engine state. It is a change of the probe, not of the cache, and is left to a separate PR. No other
compaction was found that keeps the stored instances.
