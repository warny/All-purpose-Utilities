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
