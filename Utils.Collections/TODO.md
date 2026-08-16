# Utils.Collections — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

This file is now the canonical backlog for both the original audit and `TODO-2026-07-11-pass2.md`. Duplicate findings from the two passes are consolidated by root cause. See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide plan.

The deterministic adaptive index and lookup-driven promotion are intentional design choices. The TODO is to make their contracts safe and explicit, not to replace them with a randomized textbook skip list.

## P1

### COL-01 — Comparer-equal duplicates are still inserted

`SkipList<T>.Add` calls `FindElementPosition`, but when equality returns the same node for both sides the method still falls through to `InsertAfter(newElement)`. `Count` increases and two physical nodes can represent one logical comparer key.

This duplicates first-pass item 6 and second-pass item 20.

**Fix:** create a single-traversal insertion result / `TryAdd` primitive. Decide the public `ICollection<T>.Add` behavior for duplicates (throw vs idempotent) before implementation. `SkipListDictionary.Add` should reuse the same primitive instead of searching twice.

**Breaking change:** yes, current duplicate behavior changes.

### COL-02 — Concurrent readers can race during adaptive promotion

`Contains`/`TryGet` intentionally call the adaptive traversal. `CreateUp` publishes several reciprocal links without synchronization, so two readers can race while promoting the same region.

This consolidates first-pass item 1 and second-pass item 15.

**Fix:** prefer an explicit non-thread-safe/single-threaded public contract unless concurrent reads are a required feature. If concurrent reads are required, promotion needs a real publication protocol or synchronization.

### COL-03 — Content mutation does not invalidate enumerators

Enumeration walks the bottom linked list with `yield` and there is no content version. `Add`, `Remove` and `Clear` can therefore silently change an active enumeration.

Lookup-only upper-level promotion does not change the bottom sequence and should not automatically invalidate enumeration if that invariant is proved by tests.

**Fix:** content versioning and a dedicated fail-fast enumerator.

### COL-04 — Comparer exceptions can leave hidden promotion side effects

`FindElementPositionAtLevel` may promote a node before calling `comparer.Compare` for that node. If the comparer then throws, the public lookup fails after changing upper-level structure.

**Fix:** defer maintenance until required comparisons succeed, or explicitly document/test best-effort maintenance on comparer failure.

### COL-05 — The adaptive structure has no invariant checker

Current tests can verify sorted bottom-level values while missing orphaned/cyclic/non-reciprocal upper links.

**Fix:** an internal test-visible validator covering horizontal/vertical reciprocity, sorted levels, boundary towers, reachability, bottom count and comparer uniqueness. Use it after every step in model/property tests and boundary-removal scenarios.

### COL-06 — Runtime null keys are not guarded

`where K : notnull` is compile-time only. `SkipListDictionary` public key methods create `Entry.Probe(key)` without an explicit runtime null check, so behavior depends on the configured comparer.

**Fix:** one public-boundary key guard used by indexer, Add, ContainsKey, TryGetValue and Remove.

### COL-07 — `CopyTo` methods do not preflight collection-contract arguments

`SkipList<T>`, dictionary, key-view and value-view `CopyTo` implementations enumerate and assign directly. Null arrays, invalid indices and insufficient capacity can fail after partial copying with incidental exceptions.

**Fix:** shared preflight validation before any write.

## P2

### COL-08 — Dictionary insertion performs two adaptive traversals

`SkipListDictionary.Add` performs `Contains` followed by `Add`.

**Fix:** consume COL-01's single-traversal insertion result.

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

## Recommended implementation order

1. COL-01 + COL-06 + COL-08 — uniqueness and single-traversal insertion foundation.
2. COL-05 first, then COL-02 + COL-03 + COL-04 — invariants, concurrency policy and enumeration behavior.
3. COL-07 + COL-09 + COL-10 + COL-11 + COL-12 — contract compliance, tooling and documentation.
