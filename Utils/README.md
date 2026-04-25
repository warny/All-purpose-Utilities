# omy.Utils (base library)

`omy.Utils` is the foundational package for the omy utility family. It gathers cross-cutting helpers (arrays, collections, expressions, strings, streams, security, reflection, math primitives, and more) that other `omy.Utils.*` packages build upon. The library targets stable .NET TFMs and ships as a consumable NuGet package.

## What it contains

- **Async** – asynchronous execution helpers
- **Arrays** – comparison helpers, multi-dimensional utilities, specialized comparers
- **Collections** – indexed lists, skip lists, LRU caches, dictionary extensions
- **Expressions** – expression parsing, compilation, lambda utilities, shared compiler context (`ExpressionCompilerContext`), and expression tree optimization (`ExpressionOptimiser`)
- **Files** – filesystem helpers to manipulate paths and temporary files
- **Mathematics** – base classes for expression transformation and math functions
- **Net** – advanced URI builder and network helpers
- **Objects/Strings** – data conversion routines and advanced string formatter
- **Reflection** – `PropertyOrFieldInfo` and delegate invocation helpers
- **Resources** – utilities for embedded resources
- **Security** – Google Authenticator helpers
- **Streams** – base16/base32/base64 converters and binary serialization
- **Transactions** – batch reversible actions with commit/rollback handling

> XML helpers are packaged separately in `omy.Utils.XML`. Add a reference to access `XmlDataProcessor` and related extensions.

## Usage examples

### Transactions
```csharp
using Utils.Transactions;

class SampleAction : ITransactionalAction
{
    public void Execute() { /* work */ }
    public void Commit() { /* finalize */ }
    public void Rollback() { /* undo */ }
}

TransactionExecutor executor = new TransactionExecutor();
executor.Execute([
    new SampleAction(),
    new SampleAction(),
]);
```

### Async
```csharp
using Utils.Async;
IAsyncExecutor executor = new AsyncExecutor();
Func<Task>[] actions =
[
    async () => await Task.Delay(100),
    async () => await Task.Delay(100),
    async () => await Task.Delay(100),
];
await executor.ExecuteAsync(actions, 3);
```

### Arrays
```csharp
using Utils.Arrays;
int[] values = [0, 1, 2, 0];
int[] trimmed = values.Trim(0); // [1, 2]
```

### Collections
```csharp
var cache = new Utils.Collections.LRUCache<int, string>(2);
cache.Add(1, "one");
cache.Add(2, "two");
cache[1];
cache.Add(3, "three"); // evicts key 2
```

### Reflection
```csharp
var info = new Utils.Reflection.PropertyOrFieldInfo(typeof(MyType).GetField("Id"));
int id = (int)info.GetValue(myObj);
info.SetValue(myObj, 42);
```

## Stability and versioning

The package follows semantic versioning and is treated as a stable dependency for the rest of the `omy.Utils.*` ecosystem. Expect patch releases to remain backward compatible. Review the root [`CHANGELOG.md`](../CHANGELOG.md) for documentation and metadata updates.

## Related packages

- **omy.Utils.Net** – networking helpers (DNS, ICMP, Wake-on-LAN)
- **omy.Utils.IO** – stream utilities, converters, and binary serialization
- **omy.Utils.Data** – data record mappers
- **omy.Utils.Imaging** – imaging and drawing utilities

See the root README for the full package list and installation instructions.

## Expression compiler context highlights

`ExpressionCompilerContext` can be shared between expression compilers and supports:

- dynamic symbol registration (`Set` / dynamic members),
- callable overload storage under a single symbol name,
- stream persistence (`WriteToStream` / `ReadFromStream`) for values and static callable entries.

## ExpressionOptimiser

`ExpressionOptimiser` rewrites LINQ `Expression` trees to produce simpler, faster-executing equivalents without changing observable behavior.

Supported rewrites include:

- **Constant folding** — `1 + 2` → `3`, `true && false` → `false`
- **Algebraic identities** — `x * 1` → `x`, `x + 0` → `x`, `x * 0` → `0`, `x - x` → `0`
- **Boolean short-circuit** — `expr || true` → `true` (preserving side effects in `expr`), `expr && false` → evaluates `expr` for side effects only
- **Negation elimination** — `!!x` → `x`
- **Conditional simplification** — `cond ? x : x` → `x`

```csharp
using Utils.Expressions;
using System.Linq.Expressions;

var optimiser = new ExpressionOptimiser();

Expression<Func<int>> expr = () => (1 + 1) * 3 - 4 + 2;
var optimised = (Expression<Func<int>>)optimiser.Optimize(expr);

int result = optimised.Compile()(); // 2, computed without any arithmetic at runtime
```

Side effects are always preserved: the optimiser never removes sub-expressions that may have observable effects (method calls, increments, etc.).
