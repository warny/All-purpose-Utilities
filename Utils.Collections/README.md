# omy.Utils.Collections

`omy.Utils.Collections` provides `SkipList<T>` — a deterministic adaptive sorted collection with O(log n) average-case insertion, removal, and lookup.

## Install

```bash
dotnet add package omy.Utils.Collections
```

## Supported frameworks
- net8.0

## Thread safety

`SkipList<T>` and `SkipListDictionary<TKey, TValue>` instance members are not thread-safe. Synchronize
access externally whenever the same instance is shared between threads. This requirement also applies
to lookups such as `Contains` and `TryGet`, because lookups may maintain the adaptive index.

## Features
- `SkipList<T>` — sorted `ICollection<T>` backed by a deterministic adaptive skip structure. The adaptive index is maintained from traversal patterns and the configured threshold rather than random level assignment.
- Configurable `threshold` to tune skip level density.
- Custom `IComparer<T>` support.

Like other comparer-based sorted collections, stored values and dictionary keys must not be mutated
in a way that changes their ordering or identity under the configured comparer. Such a mutation can
invalidate the collection's logical organization.

## Quick usage

```csharp
using Utils.Collections;

var numbers = new SkipList<int>();
numbers.Add(3);
numbers.Add(1);
numbers.Add(2);

// Iteration yields elements in ascending sorted order
foreach (int n in numbers)
    Console.WriteLine(n); // 1, 2, 3
```

## SkipList examples

### Custom threshold

The threshold controls how densely the skip structure is built. Lower values create taller structures with faster lookups on large lists; higher values create flatter, more memory-efficient structures.

```csharp
using Utils.Collections;

var list = new SkipList<int>(threshold: 5);
list.Add(10);
list.Add(5);
list.Add(7);

Console.WriteLine(list.Count);     // 3
Console.WriteLine(list.Contains(7)); // true
```

### Custom comparer

```csharp
using System.Collections.Generic;
using Utils.Collections;

var words = new SkipList<string>(StringComparer.OrdinalIgnoreCase, threshold: 10);
words.Add("Banana");
words.Add("apple");
words.Add("Cherry");

foreach (string w in words)
    Console.WriteLine(w); // apple, Banana, Cherry (case-insensitive order)
```

### Remove and Clear

```csharp
using Utils.Collections;

var list = new SkipList<int>();
list.Add(1);
list.Add(2);
list.Add(3);

list.Remove(2);
Console.WriteLine(list.Contains(2)); // false
Console.WriteLine(list.Count);       // 2

list.Clear();
Console.WriteLine(list.Count);       // 0
```

### CopyTo

```csharp
using Utils.Collections;

var list = new SkipList<int>();
list.Add(30);
list.Add(10);
list.Add(20);

var dest = new int[3];
list.CopyTo(dest, 0);
// dest = [10, 20, 30] (sorted)
```

## Related packages
- `omy.Utils` – core utilities including `LRUCache<K,V>`, `IndexedList<K,V>`, and `EnumerableEx` extensions.


[Versioned API documentation](https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.1/)
