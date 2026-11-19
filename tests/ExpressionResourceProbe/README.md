# ExpressionResourceProbe — ETC R0 resource campaign

Diagnostic harness for the ETC roadmap (`Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md`, §7 and the
"ETC Security" stage). It measures stack, CPU and memory behavior of `ExpressionSimplifier`, `ExpressionTransformer`,
`ExpressionComparer`, `ExpressionCanonicalOrder` and the `Utils.Expressions.CSyntax` parser on families of expression trees.

**It runs one scenario per process on purpose**: several scenarios end with a stack overflow, which terminates the process
and cannot be caught. Never call these scenarios from a test project. The project is not part of `Utils.sln`, is not
packable and is not built by CI.

## Build

**Environment:** Windows PowerShell 5.1

```powershell
$output = Join-Path $env:TEMP 'etc-probe\win'
dotnet build '.\tests\ExpressionResourceProbe\ExpressionResourceProbe.csproj' --configuration Release --output $output
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
```

Linux (run through WSL; the WSL distribution only needs to execute a self-contained binary, no .NET SDK):

```powershell
$output = Join-Path $env:TEMP 'etc-probe\linux'
dotnet publish '.\tests\ExpressionResourceProbe\ExpressionResourceProbe.csproj' --configuration Release `
    --runtime linux-x64 --self-contained true --output $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
```

## Run

```powershell
$probe = Join-Path $env:TEMP 'etc-probe\win\ExpressionResourceProbe.exe'
& $probe list
& $probe run A-left-sum 1000 rawhash
& $probe run E-csyntax-parens 19 parse --thread stack:256
& $probe ess --thread pool
```

Operations: `build`, `shape`, `simplify`, `transform` (generic recursive `ExpressionTransformer`), `substitute`
(`ReplaceArguments`), `hash` and `equals` (public `ExpressionComparer`, which simplify first), `rawhash` and `rawequals`
(structural hash/equality without simplification), `canonical` (S4 canonical order), `tokenize`, `parse`, `compile`
(CSyntax only), and `recover-<operation>` (runs the operation, swallows `InsufficientExecutionStackException`, then reports
the ambient thread-static state and runs a sanity simplification on the same thread). `--counters` activates the
comparer's internal diagnostic capture (this changes the code path; do not combine with timing comparisons). `ess`
calibrates `RuntimeHelpers.EnsureSufficientExecutionStack` (cost per call and guarded frame count).

Exit codes: `0` ok, `10` `InsufficientExecutionStackException`, `11` `OutOfMemoryException`, `12` other exception, `13`
`ExpressionResourceLimitException` (a controlled refusal of the ETC R1a-Security limits; the outcome names its kind, for
example `resource-limit:Stack+InsufficientExecutionStackException`); a stack overflow ends the process with `0xC00000FD`
(`-1073741571`) on Windows or `SIGABRT` (134) on Linux.

`--limits` (ETC R1a-Security) selects the resource limits of the simplifier, the generic transformer and the comparer:
`default` (`ExpressionResourceLimits.Default`: inputs over depth 512 or 50,000 logical nodes are refused before any
work), `budgets` (default visit budgets, no depth or node limit: the recursion itself meets the stack guard) or
`unlimited` (only the stack guard). The raw operations (`rawhash`, `rawequals`, `canonical`) always run with the default
budgets and no input validation. Since R1a-Security every scenario is expected to end with `ok` or `resource-limit`,
never with `stack-overflow`, on any thread kind.

Campaigns (ascending sizes, stop at the first failure of each scenario/operation pair, timeout, Windows working-set limit):

```powershell
.\tests\ExpressionResourceProbe\run-campaign.ps1 -ProbePath $probe -Scenarios A-left-sum,B-right-sum `
    -Operations transform,rawhash -Limits budgets -Thread stack:256 -Output (Join-Path $env:TEMP 'etc-probe\a-b.csv')
```

Add `-Linux` with the published linux-x64 binary to run the same campaign through WSL (`timeout --signal=KILL` enforces
the time limit; no memory limit is applied on Linux).

## Instrumented build (optional)

Visit counts (`visits=`) need a build of `Utils` exposing `internal static long EtcTransformCoreVisits` on
`ExpressionTransformer`, incremented by `TransformCore`. Such a build is an experiment only and is never merged: create a
detached worktree, add the counter (and, for the stack-guard experiment, `RuntimeHelpers.EnsureSufficientExecutionStack()`
calls gated by an environment variable), copy this project into the worktree and build it there, or build it here with
`-p:EtcUtilsRoot=<worktree>\`. Without the counter the harness prints `visits=n/a`.

Internal diagnostic members are read by reflection (`InternalDiagnostics.cs`), so no `InternalsVisibleTo` is added to the
production project.

## Parser scenarios (S-06)

`parser-list` and `parser <scenario> <size> tokenize|parse|compile [--thread ...] [--plimits default|unlimited]` run the
`Utils.Parser` resource scenarios (`ParserProbe.cs`): CSyntax/VBSyntax nesting, unary chains and flat sums, plus small
ANTLR4 grammars isolating one recursion shape each (right recursion, rule chains, quantifier bodies, inline alternations,
direct left recursion, zero-width levels, an ambiguous grammar, a recursive lexer rule, a hidden channel). Each run builds
a fresh, instrumented `CompiledGrammar` (never the shared dialect instance) before the measurement starts and prints one
`PRESULT` line: characters, tokens, rule invocations and maximum rule depth (counting invocation-frame manager),
alternative attempts (passive runtime observer), tree nodes and depth, registry/memo/look-ahead sizes (read by reflection
after the parse), elapsed time, cumulative allocated bytes, live-memory delta with the engine and the result still
reachable, peak working set, the internal S-06 budget counters when the build has them, and the outcome
(`resource-limit:<phase>/<kind>@<attempted>` for a controlled refusal, exit code 13).

`--plimits` selects `ParserResourceLimits.Default` or `ParserResourceLimits.Unlimited` (the stack guard stays active).
Campaigns use `run-campaign.ps1 -Parser [-ParserLimits unlimited]`; the S-06 raw results are in `results/2026-11-19/`.

**Environment:** Windows PowerShell 5.1

```powershell
$probe = Join-Path $env:TEMP 'etc-probe\win\ExpressionResourceProbe.exe'
& $probe parser-list
& $probe parser P-csyntax-parens 15 parse --thread stack:256
.\tests\ExpressionResourceProbe\run-campaign.ps1 -ProbePath $probe -Parser -Scenarios P-csyntax-sum -Operations tokenize,parse `
    -Sizes 100,1000,20000 -TimeoutSeconds 300 -MemoryLimitMB 16384 -Output (Join-Path $env:TEMP 'etc-probe\p-sum.csv')
```

## Shared-grammar concurrency (issue #709)

`parser-concurrency-campaign [--ops <per worker>] [--seeds <count>] [--threads 2,4,8,16] [--surfaces a,b] [--show-oracle]`
(`ParserConcurrencyCampaign.cs`) runs concurrent operations on shared surfaces and compares every result with a
sequential oracle computed beforehand on independent, freshly built `CompiledGrammar` instances. Surfaces:
`csyntax` / `vbsyntax` (distinct token parsers sharing the static dialect grammar), `generated-static` (the generated
`CSyntaxTokenizerGrammar.Grammar` with full token metadata), `modes` (a shared ANTLR4 grammar with lexer modes, `more`, a
hidden channel and left recursion), `limits` (the same grammar under tight limits: refusals must match the oracle
exactly, kind, phase, limit, observed value and offset) and `compile-csyntax` / `compile-vbsyntax` (one shared
expression compiler, compile then evaluate). Workloads `parse`, `tokenize` and `mixed` run on 2, 4, 8 and 16 dedicated
threads that re-synchronize on a barrier every 25 operations, with a deterministic per-worker seed. Each run prints one
`CCAMPAIGN` line (wrong trees, wrong token lists, wrong failures, expected failures, unexpected exceptions by type,
whether the shared surface is still exact sequentially afterwards, throughput and bytes per operation). Run one surface
per process (`--surfaces`) when comparing builds: a corrupted shared dialect grammar stays corrupted for the next
surfaces of the same process.

`parser-concurrency-bench [--ms <per measurement>] [--threads 1,2,4,8,16] [--mode shared|perthread|percall] [--label x]`
measures `CompiledGrammar` construction costs and CSyntax throughput (`short`, `medium`, `deep`, `flat`, `tokenize`,
`mixed`) with one shared grammar, one grammar per thread (the sequentially correct parallel baseline of builds where a
shared grammar is not thread-safe) or one grammar per operation, and prints `CBENCH` lines. `grammar-check <file|->`
converts an ANTLR4 grammar and reports conversion failures. Raw #709 results are in `results/2026-11-19/issue709-*`.

**Environment:** Windows PowerShell 5.1

```powershell
$probe = Join-Path $env:TEMP 'etc-probe\win\ExpressionResourceProbe.exe'
& $probe parser-concurrency-campaign --ops 200 --seeds 3 --surfaces csyntax
& $probe parser-concurrency-bench --ms 1500 --mode shared --threads 1,2,4,8,16 --label fixed
```
