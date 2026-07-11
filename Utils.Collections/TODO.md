# Utils.Collections — Quality and contract audit (2026-07-11)

Initial review of the standalone `Utils.Collections` package, focused on `SkipList<T>`, `SkipListDictionary<K,V>`, collection-interface compliance, adaptive indexing, mutation/enumeration semantics, complexity claims, validation, tests, and package documentation. No production code is changed by this commit.

## High-priority findings

### 1. Lookup-driven structural optimization is intentional but absent from the public contract

`Contains`, `TryGet`, and dictionary lookup methods call `FindElementPosition`. During traversal, `FindElementPositionAtLevel` may call `CreateNewSkipNode` once the threshold is exceeded. The lookup therefore uses observed traversal cost to enrich the upper index for later searches.

This is a deliberate adaptive optimization rather than an accidental mutation. The problem is that the class and README present ordinary collection lookup semantics without explaining that reads can rewrite internal links.

**Risk:** callers may incorrectly assume that concurrent readers are harmless, that read-only access cannot affect an active enumerator, or that synchronization is required only around `Add`/`Remove`. Instrumentation and reproducibility tests may also observe different structure after identical values are queried in different orders.

**Fix:** retain the adaptive lookup design, but document it explicitly as a self-optimizing deterministic index. Define the thread-safety policy and whether promotion during lookup invalidates active enumerators. If concurrent reads are intended, protect structural promotion or introduce a pure-read mode/snapshot enumerator.

**Priority: P1 public contract and concurrency safety.**

### 2. Enumerators are not versioned despite structural changes during both writes and reads

`GetEnumerator` yields directly from the bottom-level linked nodes. The collection has no modification version, and `Add`, `Remove`, `Clear`, dictionary value replacement, and lookup-driven promotion do not invalidate enumerators.

Lookup promotion usually leaves the bottom-level value chain unchanged, so it may be safe for the current bottom-only iterator. However, that safety is an implementation detail rather than an enforced invariant, and write mutations can still silently skip elements, include newly inserted elements, or continue through detached nodes.

**Fix:** define an enumeration contract. At minimum, version and fail fast on value-chain mutations (`Add`, `Remove`, `Clear`, and possibly value replacement). Either prove/test that upper-index-only promotion cannot affect bottom enumeration and exclude it from the version, or use snapshots when concurrent/adaptive reads must coexist with enumeration.

**Priority: P1 correctness and API predictability.**

### 3. The implementation is a deterministic adaptive skip index, not a randomized skip list

The removal of random level assignment is an intentional performance choice: upper nodes are created deterministically after traversal counters exceed `_threshold`, including during lookups. This allows the structure to optimize paths based on actual access/insertion patterns and avoids randomized performance variability.

The remaining issue is terminology and guarantees. The class and README still call the structure “probabilistic” and advertise conventional average `O(log n)` skip-list behavior, while this implementation follows a different adaptive algorithm.

**Risk:** users and maintainers evaluate the type against the wrong algorithm, and complexity/memory claims are not tied to the actual threshold-based promotion strategy. Performance may depend on insertion and query order in ways that are valuable but undocumented.

**Fix:** preserve the deterministic adaptive algorithm. Rename or describe it precisely (for example, “deterministic adaptive skip list/index”), remove “probabilistic” wording, and document:

- when promotion occurs;
- how `_threshold` bounds local traversal before index enrichment;
- whether promotions are permanent;
- memory-growth behavior;
- amortized/worst-case expectations;
- benchmark results against randomized skip lists, sorted arrays/lists, trees, and dictionaries for representative workloads.

**Priority: P1 public contract accuracy and performance documentation.**

### 4. `CopyTo` implementations do not satisfy collection validation contracts

`SkipList<T>.CopyTo`, dictionary `CopyTo`, and key/value collection `CopyTo` simply enumerate and assign. They do not explicitly validate:

- null destination arrays;
- negative indexes;
- indexes greater than array length;
- insufficient remaining capacity;
- invalid multidimensional/non-zero-based arrays through non-generic interfaces where relevant.

Failures therefore occur after partial copying or through incidental `IndexOutOfRangeException`/`NullReferenceException` rather than the expected argument exceptions.

**Fix:** validate all arguments and capacity before writing any element. Share one helper across the four implementations.

**Priority: P1 `ICollection` contract compliance.**

### 5. Runtime null-key behavior is undefined despite `K : notnull`

The generic constraint is only a compile-time annotation for reference types. Public dictionary methods do not guard `key` at runtime. `Entry.Probe(key)` accepts it, and `EntryComparer` forwards it to the configured comparer with null-forgiving operators.

**Risk:** behavior varies by comparer: null may be accepted as a real key, throw an unrelated exception, or corrupt ordering assumptions. This differs from the conventional `Dictionary<TKey,TValue>`/`SortedDictionary<TKey,TValue>` null-key contract.

**Fix:** reject null keys explicitly at every public key boundary, preferably through one helper. If null keys are intentionally supported, remove `notnull` and document/comprehensively test comparer semantics.

**Priority: P1 dictionary correctness.**

### 6. Duplicate semantics of `SkipList<T>` are undocumented and interact ambiguously with lookup/removal

`Add` inserts another node when the comparer reports equality. `Contains`/`TryGet` return one comparer-equal stored instance, and `Remove` removes one matching node per call. The README refers to a sorted collection/data set but does not state whether this is a multiset.

**Risk:** callers may assume set semantics from the skip-list description, while duplicates are retained. With a comparer that ignores part of an object's state, `TryGet` and `Remove` do not define which equivalent instance is selected.

**Fix:** choose and document one contract: set (`Add` rejects/ignores comparer duplicates), multiset (define stable duplicate ordering and one/all removal), or expose separate types/options.

**Priority: P1 semantic correctness.**

### 7. Mutable keys can invalidate ordering permanently

`SkipList<T>` stores the original item and relies on the comparer every time it searches. `SkipListDictionary` protects `Entry.Key` from assignment, but reference-type keys and general skip-list values may mutate fields used by the comparer after insertion.

**Risk:** the linked order and upper-level search structure no longer match comparer order, causing missed lookups, incorrect insertion positions, and failed removals.

**Fix:** document immutable-key requirements prominently, optionally accept a stable key selector and snapshot the key, or provide validation/rebuild mechanisms. Tests should cover mutation of comparer-relevant state.

**Priority: P1 data-structure invariant safety.**

## Medium-priority findings

### 8. Threshold validation reports an unrelated density error

The constructor requires `threshold >= 2`, but the exception message says “Density must be between 0.001 and 0.5.” There is no density parameter and no upper bound.

**Fix:** report the actual threshold precondition and use consistent adaptive-index terminology across XML documentation, README, benchmarks, and exceptions.

**Priority: P2 diagnostics.**

### 9. `Count` can overflow silently

`Count` is incremented with unchecked `Count++`. Although reaching more than `int.MaxValue` nodes is impractical for many processes, `ICollection<T>.Count` cannot represent larger sizes and the implementation has no capacity guard.

**Fix:** use checked increment and fail before mutation, or explicitly document the `int.MaxValue` maximum.

**Priority: P2 defensive correctness.**

### 10. Dictionary key/value views are live but their mutation/enumeration behavior is undocumented

`Keys` and `Values` return live wrappers over the owner. Their enumerators share the owner's unversioned traversal, and `ValueCollection.Contains` performs a full scan while key-based lookup may adapt the upper index.

**Fix:** document live-view semantics and apply the same fail-fast/snapshot policy as the dictionary. Consider caching immutable wrapper instances rather than allocating a new wrapper for every property access.

**Priority: P2 API consistency.**

### 11. Tests use nondeterministic random data without recording a seed

Large tests instantiate `new Random()` and generate thousands of values. A rare structural failure cannot be reproduced from test output. One test also contains `Debugger.Break()`, which is inappropriate in automated test execution.

**Fix:** use fixed seeds or property-based testing with reported/shrunk seeds and remove debugger hooks.

**Priority: P2 test reliability.**

### 12. Tests do not exercise core collection contracts and adaptive-index invariants

Existing tests primarily verify sorted enumeration, positive membership, and removal of unique integer values. Missing coverage includes:

- duplicate and comparer-equivalent values;
- failed and successful queries that trigger promotion;
- proof that lookup promotion preserves the bottom-level value chain;
- promotion behavior under repeated hot-key and scan workloads;
- mutation during enumeration;
- `Clear` during enumeration;
- `CopyTo` validation and no-partial-write guarantees;
- null keys/items where applicable;
- ascending, descending, constant, and pathological insertion orders;
- repeated boundary removal across many levels;
- dictionary indexer replacement and live key/value views;
- mutable comparer keys;
- concurrent readers/writers or an explicit non-thread-safe contract.

**Fix:** add model-based tests against `List<T>`/`SortedDictionary<K,V>` under the chosen duplicate semantics, plus invariant checks for every horizontal/vertical link after adaptive query and mutation sequences.

**Priority: P2 test completeness.**

### 13. Preview features and `LangVersion=Latest` are enabled without an apparent package requirement

The net8 package opts into preview features and the latest compiler language version. The visible implementation uses ordinary language/runtime features.

**Risk:** consumers can inherit preview-feature metadata constraints, builds become compiler-version-sensitive, and reproducibility decreases without a demonstrated benefit.

**Fix:** target a fixed supported language version and disable preview features unless a documented API requires them.

**Priority: P2 packaging stability.**

### 14. Package scope and README omit `SkipListDictionary<K,V>`

The project description and README present only `SkipList<T>`, despite the package also publishing a sorted dictionary implementation.

**Fix:** document the dictionary, its comparer-based key identity, iteration order, duplicate-key behavior, key/value views, adaptive lookup behavior, complexity, and thread-safety contract. Ensure examples compile in CI.

**Priority: P2 documentation completeness.**

## Duplications of intent to reduce

- Search and index enrichment are currently fused. This is intentional, but the implementation should expose one well-defined adaptive-search primitive and optionally a pure-search primitive for diagnostics, snapshots, or concurrent-read scenarios.
- `CopyTo` validation is duplicated across the list, dictionary, key view, and value view.
- Enumerator/version semantics should be implemented once and reused by entry/key/value projections.
- Key validation is repeated implicitly through `Entry.Probe`; use one explicit public-boundary guard.
- Adaptive-index ordering/invariant logic should have a debug/test invariant checker shared by lookup promotion, insertion, removal, and model tests.
- Package documentation, XML comments, exceptions, and benchmarks should use one precise definition of threshold, promotion, and complexity.

## Required tests

- Verify that successful and failed lookups trigger promotion exactly according to the threshold policy.
- Assert that lookup-driven promotion preserves sorted bottom-level values, `Count`, duplicate order, and all horizontal/vertical backlinks.
- Benchmark cold lookup, repeated hot-key lookup, sequential scans, random lookup, ascending/descending insertion, and mixed read/write workloads before and after adaptation.
- Mutate through add/remove/clear/value replacement during entry, key, and value enumeration and verify the documented fail-fast/snapshot policy.
- Enumerate while lookups cause upper-index promotion and verify the documented safe/invalidating behavior.
- Exhaustively test `CopyTo` null/index/capacity errors and confirm destinations remain untouched on failure.
- Pass runtime null keys through every dictionary API under default and custom comparers.
- Define duplicate semantics and test insertion order, `TryGet`, one/all removal, count, and enumeration stability.
- Mutate comparer-relevant fields after insertion and verify rejection, documented undefined behavior, or rebuild support.
- Model-test random operations against a reference collection using fixed/reported seeds.
- Validate all horizontal/vertical backlinks, boundary pointers, bottom-level count, and sorted order after every mutation and adaptive promotion.
- Compile and execute every README example.

## Recommended order

| Priority | Action |
|---|---|
| P1 | Document and formalize deterministic adaptive lookup/promotion semantics |
| P1 | Define versioned enumeration behavior for writes and lookup promotion |
| P1 | Substantiate complexity and performance claims with adversarial and representative benchmarks |
| P1 | Correct `CopyTo`, null-key, duplicate, and mutable-key contracts |
| P2 | Add invariant/model/property tests with reproducible seeds |
| P2 | Correct diagnostics, package settings, and dictionary documentation |

## Deployment warning

Until items 1–7 are addressed, treat `SkipList<T>` and `SkipListDictionary<K,V>` as single-threaded adaptive structures: reads may intentionally rewrite upper index links, and enumeration/mutation interaction is not formally defined. Do not assume concurrent readers are safe without synchronization, do not mutate during enumeration, and rely on measured behavior rather than conventional randomized skip-list guarantees.