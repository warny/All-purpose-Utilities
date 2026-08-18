# Utils.Collections — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

This file is now the canonical backlog for both the original audit and `TODO-2026-07-11-pass2.md`. Duplicate findings from the two passes are consolidated by root cause. See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide plan.

The deterministic adaptive index and lookup-driven promotion are intentional design choices. The TODO is to make their contracts safe and explicit, not to replace them with a randomized textbook skip list.

## P1

### COL-07 — `CopyTo` methods do not preflight collection-contract arguments

`SkipList<T>`, dictionary, key-view and value-view `CopyTo` implementations enumerate and assign directly. Null arrays, invalid indices and insufficient capacity can fail after partial copying with incidental exceptions.

**Fix:** shared preflight validation before any write.

## P2

### COL-09 — Threshold semantics and diagnostics are inconsistent

The constructor still reports `"Density must be between 0.001 and 0.5"` although the parameter is an integer threshold >= 2. Promotion uses `counter > _threshold`, while XML/docs describe a maximum traversal distance.

**Fix:** define the exact threshold meaning, correct the error message/condition/docs, and add exact shape tests for thresholds 2, 3 and 10.

### COL-10 — Comparer/live-view API contract is incomplete

Neither collection exposes the comparer defining ordering/key identity. `Keys` and `Values` are intended live views but a new wrapper is allocated on every getter and the live semantics are not documented.

**Fix:** expose read-only comparer properties, cache the key/value wrappers once, and document that they are live read-only views.

### COL-11 — Nullable/package compiler settings are not intentionalized

`Utils.Collections.csproj` still enables preview features and `LangVersion=Latest`, while nullable analysis is not enabled even though the linked structure relies heavily on null sentinel state.

**Fix:** enable nullable and annotate sentinel links correctly; pin the supported language version and remove preview opt-in unless a concrete dependency requires it.

### COL-12 — Tests and package documentation do not yet prove the custom algorithm's contract

The production XML summary still calls `SkipList<T>` "probabilistic" although construction/promotion is deterministic and adaptive. Test coverage needs reproducible seeds, invariant/model checks, mutation-during-enumeration coverage and promotion scenarios. Mutable comparer-relevant key state should be documented as the ordinary restriction of sorted/indexed collections.

**Fix:** deterministic model/stress tests plus README/XML documentation aligned with the actual algorithm. Avoid strict timing assertions in normal unit tests; preserve historical timeout-sensitive workloads in an optional performance/stress job.

## Intentional / not defects

- Deterministic adaptive promotion is intentional.
- Lookup-driven promotion is intentional; the missing concurrency contract is the issue.
- Mutable comparer-relevant state after insertion is an ordinary sorted-collection restriction; document it instead of trying to clone arbitrary user objects.
- `Keys` and `Values` should remain live views, not snapshots.

## Closed work

### COL-02 — Explicit concurrency contract (2026-08-18)

`SkipList<T>` and `SkipListDictionary<TKey, TValue>` now explicitly document that instance members are not thread-safe and shared instances require external synchronization. This includes lookup operations because they may perform intentional adaptive index maintenance.

### COL-03 — Content-versioned fail-fast enumeration (2026-08-18)

`SkipList<T>` now tracks logical content changes and uses a dedicated enumerator that captures the version at creation and checks every `MoveNext`. Successful additions, removals, non-empty clears, and dictionary value replacements invalidate active enumerators; failed/no-op operations and pure adaptive promotions do not. Dictionary, key, and value enumeration all share this version contract.

### COL-04 — Exception-safe deferred adaptive maintenance (2026-08-18)

Adaptive promotions are now planned as explicit per-level data during comparison and committed in deterministic traversal order only after the complete search succeeds. If a comparer throws, the local plan is abandoned and the topology remains unchanged.

### COL-05 — Adaptive structure invariant checker (2026-08-17)

`SkipList<T>` now has an internal, test-visible invariant checker covering horizontal and vertical reciprocity, comparer-based ordering and uniqueness, cycles and reachability, coherent boundary towers, and the bottom-level node count. Deterministic insertion, boundary and middle removal, duplicate, lookup-promotion, clear, threshold, and fixed-seed model scenarios invoke the checker after mutations and adaptive promotions.

### COL-01 + COL-06 + COL-08 — BCL-compatible insertion and key guards (2026-08-16)

`SkipList<T>.Add` now returns whether its single adaptive traversal inserted an element and rejects comparer-equal duplicates without changing content. Its explicit `ICollection<T>.Add` implementation preserves the interface contract. `SkipListDictionary.Add` consumes that result directly, throwing `ArgumentException` for comparer-equal keys without a preliminary lookup, while the indexer continues to update comparer-equal keys and insert missing keys. All public dictionary key boundaries reject `null` before constructing a probe or invoking the configured comparer.

## Recommended implementation order

1. COL-07 + COL-09 + COL-10 + COL-11 + COL-12 — contract compliance, tooling and documentation.
