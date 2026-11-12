# P19d — Re-evaluation of the inert-classification memo

Status: **studied, not kept** (2026-11-12). No production change; the prototype stays on a local experiment branch.

This note answers the question left open by [SimplificationPolicies.md](SimplificationPolicies.md) §9.7 and
[ArchitectureC-Audit.md](ArchitectureC-Audit.md) §8.4: now that P19d (#688) has removed most of the comparer probe work, is the
per-operation memo of `IsInertArithmetic`, withdrawn at C2b, worth shipping?

## 1. What was measured

| Variant | Source | Content |
|---|---|---|
| A | `6a14a78c` (`master` `c027aa7f` + the P19d review finalization tests, PR #689, squash-merged as `e64c0ba1`) | base, no memo |
| B | a copy of A's binaries | A/A control (noise of the protocol) |
| C | `cefdb53d`, experiment branch `experiment/s5-p19d-inert-memo` (not pushed) | the C2b memo ported onto A |
| D | C with `INERT_MEMO_DIAGNOSTICS` defined | classification counters; never timed |

The prototype is the C2b patch of §9.7 (`Dictionary<Expression, bool>` keyed by reference identity, held by the
`ExpressionResourceBudget` session, intermediate and negative results stored, nodes whose classification reads user-suppliable
metadata left uncached), with two deliberate differences:

- the memoized walk counts **no** structural visit, like the historical walk, so budgets and refusals cannot differ from A;
- the diagnostics counters exist only under `INERT_MEMO_DIAGNOSTICS`, so the timed build C carries no counter.

## 2. Exactness and work (deterministic)

Growth harness, 11 families (`N` = `nanpow`, `U`, `A`, `B`, `K`, `C`, `D`, `E`, `G`, `V`, `M3`), n = 8, 16, 32, 64, 128, 256,
policies default, aggressive, trust and permissive: 260 rows per variant. A case whose first, unlimited simplification takes more
than 2 s is recorded as stopped, without measured columns, and its policy is skipped for the larger n of the family; the 25
stopped rows are the same for A, C and D (n = 256 of `A`, `K`, `M3`, `N`, `V` under the four policies, of `B` under the default,
and n = 128 of `G` under the four policies). On the 235 measured rows, results, result sizes, transform and structural visits,
comparer simplifications, verdict under the default limits and opaque type reads are **identical** for A, C and D.

Classification work, permissive policy (variant D; the base walk is the `2n² + n` of §9.7):

| Family | n | walk, base | walk, memo | table entries | structural visits (all variants) |
|---|---|---|---|---|---|
| `nanpow` (N) | 8 / 32 / 64 / 128 | 136 / 2,080 / 8,256 / 32,896 | 31 / 127 / 255 / 511 | 15 / 63 / 127 / 255 | 1,863 / 103,059 / 805,155 / 6,365,763 |
| `nanpow` with `Add` levels (M3) | 64 / 128 | 8,384 at 64 | 257 / 513 | 128 / 256 | 823,682 / 6,439,682 |
| shared DAG (G) | 64 | — | 2,303 | 1,151 | 5,101,748 |
| opaque leaf (U) | 256 | 98,432 | 98,432 (65,536 uncacheable) | 0 | 394,752 |
| method-call left operand (D) | 256 | 1,532 | 1,532 (766 uncacheable) | 0 | 398,630 |

The memo still makes the walk linear. The walk is not counted among the structural visits, so the two figures measure different
traversals; as a ratio of traversal volumes, the nodes the walk examines are about 1 % of the structural visits at n = 64 (8,256 /
805,155) and 0.5 % at n = 128 (32,896 / 6,365,763). This is not a measure of CPU time (a walk step and a structural visit do not
cost the same); §3 measures the time directly. The volume of `nanpow` is elsewhere: per doubling of n, structural visits grow
×7.8 (≈ n³) and comparer simplifications are exactly `2n² + 2n` (144, 544, 2,112, 8,320, 33,024 at n = 8 to 128). That is the
remaining comparer-probe term of S5A-14/S5A-15 on this family, which P19d reduced by a constant factor (907 → 66 ms at n = 64)
but not in order. The default policy never reaches the memo path and is unaffected by it.

## 3. Timing (BenchmarkDotNet)

BenchmarkDotNet 0.14.0, in-process, 8 warmups, 20 iterations, memory diagnoser, affinity `0x14`, high priority, one process per
variant and round, three rounds in rotated order (A, B, C / B, C, A / C, A, B), median of the round means. Round 0 and the B run of
round 1 were taken on 2026-11-11, the other runs on 2026-11-12 (the campaign was interrupted by a shutdown). The memo is
therefore always compared with a base measured the same day; only the A/A pair of round 1 spans the two days. The A/A column
estimates the dispersion of the protocol, but it does not neutralize a systematic drift between two measurement sessions, and
the per-round values below show such a drift.

| Case | A/A (B vs A) | memo (C vs A) | Allocated, A → C |
|---|---|---|---|
| permissive / aggressive / default / trust `NaN + x ** y` (two-node operand) | −1.5 / −0.3 / −3.0 / −2.5 % | **+5.2 / +5.5** / +1.2 / +0.8 % | +215 B (the table) except default (identical) |
| permissive U64 / U256 (uncacheable) | −0.6 / −1.7 % | **+8.8 / +4.9 %** | identical |
| permissive `sum12` | −0.5 % | +4.4 % | identical |
| permissive N8 / N16 / N32 / N64 / N128 | −2.0 / −2.3 / −0.7 / −0.8 / 0.0 % | +0.3 / −1.8 / +1.3 / +0.6 / −0.2 % | +993 B to +22 KB |
| default / aggressive / trust N64 | −1.4 / −1.1 / −0.1 % | +1.5 / +1.3 / 0.0 % | +0 to +13 KB |
| permissive M3_32, G10, G100, V64 | −4.1, +2.3, −3.0, +0.6 % | +2.3, −2.6, −0.2, +1.2 % | +4.6 KB, +10 KB, +100 KB, +10 KB |
| default / permissive C100 | −9.8 / −12.0 % | −3.2 / −5.7 % | identical (bimodal, path untouched) |
| default / permissive A16, K16 | −5.9 / −4.5 %, −3.0 / −3.9 % | +2.2 / −0.5 %, +1.5 / −1.7 % | identical |
| other 11 cases | −3.7 % to +2.7 % | −1.2 % to +2.4 % | identical |

No case gains beyond the A/A spread, `nanpow` included: at n = 128 the variants are within 0.2 % (485 ms), with memo deltas of
+0.1 / +3.8 / −2.7 % per round. This absence of gain is the robust result.

The time costs are less stable than at C2b. Per round (memo vs base, rounds 0 / 1 / 2):

| Case | memo vs base per round |
|---|---|
| permissive / aggressive `NaN + x ** y` | +4.4 / +10.5 / −1.4 %, +5.5 / +9.8 / +3.1 % |
| permissive U64 / U256 | +8.8 / +10.6 / +0.2 %, +7.1 / +8.5 / +0.3 % |

Rounds 0 and 1 reproduce the C2b costs, round 2, measured entirely on 2026-11-12, does not. The +215 B of the table on small
operands is deterministic; the time costs (about +150 ns on `NaN + x ** y`, +5 to +9 % on the uncacheable trees) are likely but
not established by this campaign. Dropping the counted visits is not expected to remove them, since they come from the table and
the lookups, not from the budget.

**Raw data.** The BenchmarkDotNet reports of the nine runs (and, for the 2026-11-12 runs, the logs and per-iteration
measurements), the growth tables, the bench program, the scripts with the exact commands, and the prototype as a `git am` patch
are archived locally as `p19d-inert-memo-raw-2026-11-12.zip` (484,099 bytes, SHA-256
`1228F1B3499A1C3F83F2794F63C9B8D796B5E8A925B1894EDA9B78754F521AE6`), with a README listing the variants, the build and run
commands and the stopped cases. It is not committed: the repository keeps no measurement data.

### Comparison with the C2b study (§9.7, `ecfacda4`, before P19d)

| Case | C2b memo vs base | P19d memo vs base |
|---|---|---|
| permissive `NaN + x ** y` | +5.1 %, +215 B | +5.2 %, +215 B |
| aggressive `NaN + x ** y` | +6.5 % | +5.5 % |
| permissive U64 / U256 | +8.8 / +8.9 % | +8.8 / +4.9 % |
| permissive `nanpow` 8 / 16 / 32 | +2.4 / −0.6 / −0.8 % | +0.3 / −1.8 / +1.3 % |
| `nanpow` n = 64, time | 907 ms (every policy) | 64–65 ms (every policy, every variant) |
| `nanpow` n = 64, structural visits | 12,679,500 | 805,155 |

P19d divided the surrounding work by about 16, yet the walk's share stays negligible because the surrounding work is still
super-quadratic; the medians of the memo's costs on small and uncacheable trees are of the same order as at C2b (with the
round-to-round instability noted above).

## 4. Decision

Following the decision rule of the study (keep only a net benefit with no regression on small trees or memory): **the memo is not
shipped**, the result matches C2b. The decision rests on the absence of any gain and on the deterministic +215 B per small
operand; it does not depend on the time costs, which this campaign does not establish firmly. `IsInertArithmetic` stays an uncounted, unmemoized walk; the branch `experiment/s5-p19d-inert-memo`
is kept locally for reference only and is not to be merged.

Redoing the study only makes sense if the comparer probes of `nanpow` (≈ n³ structural visits, `2n² + 2n` comparer simplifications)
become linear or close to it; that is an S5A-14/S5A-15 follow-up, outside the scope of C3 and of this note. The harness family
`nanpow` stays for that purpose.
