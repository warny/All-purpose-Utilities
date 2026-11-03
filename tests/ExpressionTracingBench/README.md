# ExpressionTracingBench — cost of the engine tracing

Benchmark harness kept with the engine tracing (`ExpressionTransformTracing`, ETC roadmap §9). Its purpose is to keep the
**untraced path** — every caller of the engine pays for it — from slowly degrading, and to measure the cost of a tracer when
the tracing itself changes.

The project is not part of `Utils.sln`, is not packable and is not run by CI: timings depend on the machine and must be read
against an A/A control measured in the same campaign.

## What is measured

| Scenario | Shows |
|---|---|
| `ext-root` | the fixed cost of one public operation (an opaque root, a single visit) |
| `scalar`, `binary`, `lambda` | small trees |
| `leftsum20`, `p9family16`, `dag10`, `block16`, `li1024` | growing node counts: a per-node cost shows as a drift proportional to their size |
| `cmp-pos`, `hash` | structural comparison and hash (same resource sessions, no transformer) |

Each line is `label,scenario,mode,medianNs,p25Ns,p75Ns,bytesPerOp,eventsPerOp`. Modes: `off` (no scope), `minimal` (a tracer
that counts the events) and `collector` (a bounded `ExpressionTraceCollector`).

## Campaign

**Environment:** Windows PowerShell 5.1

```powershell
$baseline = Join-Path $env:TEMP 'etc-baseline'
git worktree add $baseline master
if ($LASTEXITCODE -ne 0) { throw "git worktree add failed with exit code $LASTEXITCODE." }

.\tests\ExpressionTracingBench\run-campaign.ps1 -BaselineRoot $baseline -Rounds 3
.\tests\ExpressionTracingBench\run-campaign.ps1 -BaselineRoot $baseline -Rounds 3 -WithTracer

git worktree remove $baseline
```

The script builds the harness against both checkouts in separate copies, adds an A/A copy of the baseline binaries and runs
one scenario per process, rotating the order of the variants. Close other applications; on a laptop, keep it on mains power.

## Reference (2026-11-03, PR #678, 50 scenarios of the R4 campaign)

| Variant | Median | Range |
|---|---|---|
| A/A | −0.2 % | −2.3 % to +3.2 % |
| Instrumented, no scope | +0.2 % | −3.5 % to +6.6 % |
| Minimal tracer | +138 % | up to +342 % |
| Bounded collector | +204 % | up to +493 % |

Quiet re-run: `ext-root` 77 ns, **+4 ns (+5.2 %) fixed per public operation** without scope; every per-node scenario inside
the A/A range; allocations and visits identical to the baseline.

## Reading the results

- Without scope, a candidate is acceptable when every scenario stays inside the A/A range of the same campaign, except the
  documented fixed cost on `ext-root`, and when `bytesPerOp` matches the baseline (within a few bytes of integer rounding
  on the large scenarios; `ext-root` allocates 0 B: the untraced path allocates nothing for tracing).
- A difference that grows with the node count (`leftsum20` → `li1024`) is a per-node cost on the hot path: treat it as a
  regression even when each scenario looks small.
- A difference outside A/A on a single scenario is re-measured alone (`-Scenarios`, more `-Rounds`) before drawing
  conclusions.
