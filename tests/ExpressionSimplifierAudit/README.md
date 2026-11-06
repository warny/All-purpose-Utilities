# ExpressionSimplifierAudit — S5 deep-audit campaigns

Reproducible campaigns behind the S5 deep audit of `ExpressionSimplifier`
(`Utils/TODO-2026-11-04-s5-simplifier-deep-audit.md`). The project is not part of `Utils.sln`, is not packable and is not run by
CI: the campaigns are long, and the timings depend on the machine. The deterministic regressions they found are pinned by
`UtilsTest/Mathematics/Expressions/ExpressionSimplifierDeepAuditTests.cs` (test category `S5-Audit`), which CI runs.

The harness only uses the public engine API, plus reflection on four internal diagnostics of `ExpressionComparer` and
`ExpressionCanonicalOrder` (comparer simplification capture, user-code counter, raw structural equality and hash, canonical
order); a checkout without them reports those columns as unavailable instead of failing.

## Campaigns

| Command | Brief | What it does |
|---|---|---|
| `inventory` | §3 | Rules in dispatch-plan order with their constraints, source location (portable PDB) and what their bodies call (IL scan, transitively through the simplifier's helpers); static may-reach graph and its strongly connected components; dynamic graph from `terminate-edges.tsv`. |
| `terminate` | §4 | The structural corpus of the brief, every instantiation over `x, y, z, 0, 1, -1, 2, -2, 0.5, 1e300`, plus seeded second-level compositions, under the default limits with a counting tracer. A refusal, an exception or an excessive visit count is traced again and classified by `ExpressionTraceCycleAnalyzer` (cycle, expansion, suspect), then reduced. Writes the dynamic rule-interaction edges. |
| `converge` | §5, §10 | `E(k+1) = Simplify(E(k))` for up to 8 passes, compared with a strict structural signature (no simplification during the comparison): fixed point after k passes, cycles, growth, residue shapes of the first pass and rules the second pass applies. |
| `canon` | §7 | Every permutation and association of term sets (free and lambda-bound), additive, multiplicative and signed: distinct forms, comparer agreement, hash agreement, stability. |
| `comparer` | §7 | Reflexivity on rebuilt copies, symmetry, transitivity, hash consistency, alpha-equivalence, `Equals(a, b)` against `Equals(S(a), S(b))`, raw equality against the S4 canonical order, curated scope cases. |
| `diff` | §8, §11 | Seeded trees over `double`, `float`, `int`, `long`, `decimal`, `int?` and `uint`, weighted towards the factoring, subtraction and division shapes, with shared subtrees; source and simplified trees compiled and evaluated on moderate and special values; differences classified (`rounding`, `signedZero`, `ieee`, `extreme` = S3 latitude; `VALUE`, `EXC-*`, `SIMPLIFY-THROWS`, `COMPILE-FAIL`, `TYPE-CHANGED` = candidates), cross-checked with the expression interpreter (`LINQ`) and reduced. |
| `effects` | §9 | Trees with effectful leaves (counter, constant call, getter with an effect, throwing call): value, exception and ordered evaluation log must be identical; fixed scenarios of user code run by the simplification itself (throwing, unstable and logging `Equals`). |
| `s3` | §8 | The identity matrix of the brief for each applicable type: simplified form and difference classes over every assignment of the domain values. |
| `measure <family> <n>` | §6, §13 | One complexity point in a fresh process: untraced Release timing (warm-up, median of up to 15 runs) and allocations, then separate instrumented runs for visits, rule applications, re-entries, comparer simplifications (count, depth histogram), user calls and result size. |
| `families` | | Lists the families (`p`, `B`, `g`, `F`, `z`, `e`, `k`, `n`, `o`, `r`, `H` of the S5 history, rebuilt from the P10–P16 characterization tests, and the re-entry families of this audit). |
| `trace "<template>"` | §12 | Simplifies a template (`x`, `y`, `z` and other letters are parameters) several times and prints each pass, the trace of the first pass and its cycle report. |
| `replay diff\|effects <seed>` | §11 | Re-runs one seed verbosely (same options as the campaign). |

## Running

**Environment:** Windows PowerShell 5.1

```powershell
# Every campaign, results in a new artifacts\s5-audit\<timestamp> directory (never overwritten).
.\tests\ExpressionSimplifierAudit\run-audit.ps1

# Some campaigns only, smaller.
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns terminate,converge -Seeds 1000 -Nested 500

# Complexity of a few families, limits lifted (the default limits are reported separately); a family stops after the first
# size above 10 s, refused, timed out (90 s) or over the heap limit.
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns complexity -Families p,g,F,negsum -Unlimited -StopAfterSeconds 10 -MeasureTimeoutSeconds 90

# Against another checkout (for example a candidate fix in a worktree).
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -UtilsRoot E:\worktrees\candidate -Campaigns diff,effects
```

One case or seed directly:

```powershell
$exe = '.\tests\ExpressionSimplifierAudit\bin\Release\net9.0\ExpressionSimplifierAudit.exe'
& $exe trace "(x - y) - z"
& $exe replay diff 788 --types double,float,int,long,decimal,int?,uint
& $exe measure negsum 20 --unlimited --out .\artifacts\s5-audit\manual
```

The campaigns simplify with the default policy. Set `S5_AUDIT_POLICY` to run them under another one (S5 P18 Architecture C):
`permissive` (trust declarations, aggressive, no exception preserved), or a comma-separated list of `trust`, `aggressive` and the
exception categories still preserved (`arithmetic`, `runtime`, `usercode`), for example `aggressive,runtime,usercode`.

```powershell
$env:S5_AUDIT_POLICY = 'permissive'
& $exe diff --out .\artifacts\s5-audit\permissive --seeds 1000
Remove-Item Env:\S5_AUDIT_POLICY
```

Exit codes: `0` no anomaly, `1` anomalies found, `2` usage error, `3` harness failure, `5` a case exceeded `--timeout`, `6` the
managed heap exceeded `--memory` (MB). On `5` and `6` the case is appended to `<campaign>-aborted.txt`; `run-audit.ps1` resumes
the campaign after it (`--start`, `--resume`).

## Output

Each campaign writes `<campaign>.jsonl` (one record per case), `<campaign>-anomalies.jsonl` (with the seed, the generator
options and, for the first occurrence of each signature, the reduced counterexample) and `<campaign>-summary.md`.
`environment.txt` records the commit, the machine, the runtime and the parameters; `measure.jsonl` holds the complexity points.

## Reading the results

- Timings are only comparable within one campaign on one machine; compare counters (visits, comparer simplifications, user
  calls) across machines. Measure on an idle machine, never while another campaign runs.
- `ieee`, `extreme`, `rounding` and `signedZero` differences are the latitude of the S3 symbolic contract (NaN, infinities,
  signed zeros, reassociation, extreme magnitudes). A `VALUE` difference with moderate inputs can still be a domain precondition
  of the contract (`0^x -> 0` for `x = 0`): check the reduced counterexample before calling it a defect.
- A refusal by the default limits is a controlled outcome, not a loop: `terminate` traces it and only a confirmed recurrence
  (`cycle`, `expansion`) is a rewrite loop.
