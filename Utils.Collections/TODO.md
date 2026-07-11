# Utils.Collections — Quality and contract audit (2026-07-11)

Initial review of the standalone `Utils.Collections` package, focused on `SkipList<T>`, `SkipListDictionary<K,V>`, collection-interface compliance, mutation/enumeration semantics, complexity claims, validation, tests, and package documentation. No production code is changed by this commit.

## High-priority findings

### 1. Read-only lookup operations mutate the skip-list structure

`Contains`, `TryGet`, and dictionary lookup methods call `FindElementPosition`. During traversal, `FindElementPositionAtLevel` may call `CreateNewSkipNode` once the threshold is exceeded. Therefore a membership test or failed lookup can create upper-level nodes and rewrite horizontal/vertical links.

**Risk:** callers reasonably expect `Contains`, `TryGet`, `ContainsKey`, indexer reads, and `TryGetValue` to be observationally read-only. Instead they mutate shared state, can race with other readers, and can alter an active enumerator's node graph even though `Count` and values do not change.

**Fix:** separate lookup from maintenance. Pure lookup must never modify links. Perform promotion/rebalancing only during insertion/removal or through an explicit maintenance operation protected by a synchronization policy.

**Priority: P1 collection semantics and concurrency safety.**

### 2. Enumerators are not versioned and collection mutations are not detected

`GetEnumerator` yields directly from the bottom-level linked nodes. The collection has no modification version, and `Add`, `Remove`, `Clear`, dictionary value replacement, and even lookup-driven promotion do not invalidate enumerators.

**Risk:** mutation during enumeration can silently skip elements, include newly inserted elements, continue through detached nodes, or expose mixed old/new dictionary values. Standard mutable .NET collections generally fail fast with `InvalidOperationException` for structural mutation.

**Fix:** maintain a version incremented on every observable mutation and capture it in a dedicated enumerator. Decide whether value replacement invalidates dictionary enumeration and apply that policy consistently to entries, keys, and values.

**Priority: P1 correctness and API predictability.**

### 3. The implementation and documentation call the structure probabilistic, but it is deterministic and lookup-adaptive

The class/README describe a probabilistic skip list with average O(log n) operations. No random level assignment exists. Upper nodes are created deterministically only after traversal counters exceed `_threshold`, including during reads.

**Risk:** complexity, memory use, shape, concurrency behavior, and performance expectations differ materially from a conventional probabilistic skip list. Worst-case behavior and amortization are undocumented and unproven.

**Fix:** either implement a conventional randomized/geometric level policy, or rename/document the structure as a deterministic adaptive indexed list and provide measured/proven complexity bounds for insertion order and lookup workloads.

**Priority: P1 public contract accuracy.**

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

`Add` inserts another node when the comparer reports equality. `Contains`/`TryGet` return one arbitrary comparer-equal stored instance, and `Remove` removes one matching node per call. The README refers to a sorted collection/data set but does not state whether this is a multiset.

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

**Fix:** report the actual threshold precondition and use consistent terminology across XML documentation, README, and exceptions.

**Priority: P2 diagnostics.**

### 9. `Count` can overflow silently

`Count` is incremented with unchecked `Count++`. Although reaching more than `int.MaxValue` nodes is impractical for many processes, `ICollection<T>.Count` cannot represent larger sizes and the implementation has no capacity guard.

**Fix:** use checked increment and fail before mutation, or explicitly document the `int.MaxValue` maximum.

**Priority: P2 defensive correctness.**

### 10. Dictionary key/value views are live but their mutation/enumeration behavior is undocumented

`Keys` and `Values` return live wrappers over the owner. Their enumerators share the owner's unversioned traversal, and `ValueCollection.Contains` performs a full scan while lookups by key may mutate skip levels.

**Fix:** document live-view semantics and apply the same versioning/fail-fast policy as the dictionary. Consider caching immutable wrapper instances rather than allocating a new wrapper for every property access.

**Priority: P2 API consistency.**

### 11. Tests use nondeterministic random data without recording a seed

Large tests instantiate `new Random()` and generate thousands of values. A rare structural failure cannot be reproduced from test output. One test also contains `Debugger.Break()`, which is inappropriate in automated test execution.

**Fix:** use fixed seeds or property-based testing with reported/shrunk seeds and remove debugger hooks.

**Priority: P2 test reliability.**

### 12. Tests do not exercise core collection contracts and adversarial shapes

Existing tests primarily verify sorted enumeration, positive membership, and removal of unique integer values. Missing coverage includes:

- duplicates and comparer-equivalent non-equal objects;
- absent lookups before/after level creation;
- mutation during enumeration;
- `Clear` during enumeration;
- `CopyTo` validation and no-partial-write guarantees;
- null keys/items where applicable;
- ascending, descending, constant, and pathological insertion orders;
- repeated boundary removal across many levels;
- dictionary indexer replacement and live key/value views;
- mutable comparer keys;
- concurrent readers/writers or an explicit non-thread-safe contract.

**Fix:** add model-based tests against `List<T>`/`SortedDictionary<K,V>` under the chosen duplicate semantics, plus invariant checks for every horizontal/vertical link after random operation sequences.

**Priority: P2 test completeness.**

### 13. Preview features and `LangVersion=Latest` are enabled without an apparent package requirement

The net8 package opts into preview features and the latest compiler language version. The visible implementation uses ordinary language/runtime features.

**Risk:** consumers can inherit preview-feature metadata constraints, builds become compiler-version-sensitive, and reproducibility decreases without a demonstrated benefit.

**Fix:** target a fixed supported language version and disable preview features unless a documented API requires them.

**Priority: P2 packaging stability.**

### 14. Package scope and README omit `SkipListDictionary<K,V>`

The project description and README present only `SkipList<T>`, despite the package also publishing a sorted dictionary implementation.

**Fix:** document the dictionary, its comparer-based key identity, iteration order, duplicate-key behavior, key/value views, complexity, and thread-safety contract. Ensure examples compile in CI.

**Priority: P2 documentation completeness.**

## Duplications of intent to reduce

- Lookup and structural maintenance must be separate operations; currently every public read funnels into a mutating search path.
- `CopyTo` validation is duplicated across the list, dictionary, key view, and value view.
- Enumerator/version semantics should be implemented once and reused by entry/key/value projections.
- Key validation is repeated implicitly through `Entry.Probe`; use one explicit public-boundary guard.
- Skip-list ordering/invariant logic should have a debug/test invariant checker shared by insertion, removal, and randomized model tests.
- Package documentation, XML comments, exceptions, and benchmarks should use one precise definition of threshold and complexity.

## Required tests

- Assert that `Contains`, `TryGet`, `ContainsKey`, indexer reads, and failed lookups do not change structural metadata or links.
- Mutate through add/remove/clear/value replacement during entry, key, and value enumeration and verify the documented fail-fast/snapshot policy.
- Exhaustively test `CopyTo` null/index/capacity errors and confirm destinations remain untouched on failure.
- Pass runtime null keys through every dictionary API under default and custom comparers.
- Define duplicate semantics and test insertion order, `TryGet`, one/all removal, count, and enumeration stability.
- Mutate comparer-relevant fields after insertion and verify rejection, documented undefined behavior, or rebuild support.
- Model-test random operations against a reference collection using fixed/reported seeds.
- Validate all horizontal/vertical backlinks, boundary pointers, bottom-level count, and sorted order after every operation.
- Benchmark ascending, descending, random, repeated, and adversarial workloads; substantiate or correct complexity claims.
- Compile and execute every README example.

## Recommended order

| Priority | Action |
|---|---|
| P1 | Make all lookup paths structurally read-only |
| P1 | Add versioned enumerators and define mutation behavior |
| P1 | Define deterministic/probabilistic design and truthful complexity claims |
| P1 | Correct `CopyTo`, null-key, duplicate, and mutable-key contracts |
| P2 | Add invariant/model/property tests with reproducible seeds |
| P2 | Correct diagnostics, package settings, and dictionary documentation |

## Deployment warning

Until items 1–7 are addressed, treat `SkipList<T>` and `SkipListDictionary<K,V>` as single-threaded structures whose apparent read operations may mutate internal links. Do not mutate them during enumeration, do not rely on standard null-key or duplicate semantics, and do not assume the advertised probabilistic O(log n) behavior has been established.