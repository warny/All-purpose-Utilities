# omy.Utils (base library)

`omy.Utils` is the foundational package of the omy utility family. It provides cross-cutting helpers across arrays, collections, expressions, strings, ranges, math, randomization, async, transactions, and security — all of which other `omy.Utils.*` packages build upon.

## Install
```bash
dotnet add package omy.Utils
```

## Supported frameworks
- net8.0

## Features
- **Arrays** — `Trim`, `Mid`, `PadLeft/Right`, `StartWith/EndWith`, `Replace`, `ConvertToArrayOf`
- **Collections** — `EnumerableEx` extensions, `LRUCache<K,V>`, `IndexedList<K,V>`, `DoubleIndexedDictionary<T1,T2>`
- **Expressions** — `ExpressionOptimiser` for constant-folding and algebraic rewrites
- **Mathematics** — `MathEx.Mod`, `Round`, `Deg2Rad`/`Rad2Deg` constants
- **Objects** — `FluentResult<T>` chainable predicates, `ConstrainedValue<T>`, `Enumerators.Enumerate`
- **Randomization** — `Random.RandomString` helpers
- **Range** — `IRange<T>`, `DoubleRanges`, `IntRanges` with overlap/intersect/contains
- **Security** — TOTP `Authenticator` (Google Authenticator-compatible)
- **Strings** — `Like` wildcard matching, predicate `Trim`, `AddPrefix`/`AddSuffix`
- **Async** — `AsyncExecutor` with parallel, sequential, and threshold-based dispatch
- **Transactions** — `TransactionExecutor` with commit/rollback on failure

## Arrays

```csharp
using Utils.Arrays;

int[] data = [0, 1, 2, 0, 0];

// Trim matching values from both ends
int[] trimmed = data.Trim(0);          // [1, 2]
int[] trimStart = data.TrimStart(0);   // [1, 2, 0, 0]
int[] trimEnd = data.TrimEnd(0);       // [0, 1, 2]

// Trim by predicate
int[] positives = data.Trim(v => v == 0); // [1, 2]

// Slicing
int[] slice = data.Mid(1, 3);          // [1, 2, 0]
int[] tail  = data.Mid(-2);            // last 2 elements

// Prefix / suffix checks
bool starts = data.StartWith(0, 1);    // true
bool ends   = data.EndWith(0, 0);      // true

// Padding
int[] padded = new int[] { 1, 2 }.PadLeft(5, 0);   // [0, 0, 0, 1, 2]
int[] padded2 = new int[] { 1, 2 }.PadRight(5, 0); // [1, 2, 0, 0, 0]

// Subsequence replacement
byte[] bytes = [1, 2, 3, 2, 1];
byte[] result = bytes.Replace([2, 3], [9]);  // [1, 9, 2, 1]

// Parse strings into typed arrays
int[] parsed = new[] { "1", "2", "3" }.ConvertToArrayOf<int>(); // [1, 2, 3]
```

## Collections

### EnumerableEx extensions

```csharp
using Utils.Collections;

int[] nums = [1, 2, 2, 3, 3, 3];

// Run-length encoding
foreach (var p in nums.Pack())
    Console.WriteLine($"{p.Value} × {p.Repetition}"); // 1×1, 2×2, 3×3

// Restore the original sequence
int[] restored = nums.Pack().Unpack().ToArray();

// Sliding window
foreach (var win in nums.SlideEnumerateBy(3))
    Console.WriteLine(string.Join(",", win)); // 1,2,2  then 2,2,3  etc.

// Append / prepend
int[] extended = nums.FollowedBy([4, 5]).ToArray();
int[] prepended = nums.PrecededBy([0]).ToArray();

// Slice at indexes
var segments = nums.Slice(2, 4).ToArray(); // [[1,2], [2,3], [3]]

// Flatten nested sequences
int[][] nested = [[1, 2], [3, 4]];
int[] flat = nested.Flatten().ToArray(); // [1, 2, 3, 4]

// Count guards (no full enumeration)
bool many = nums.HasManyElements();           // true (> 1)
bool atLeast = nums.HasAtLeastElements(4);   // true

// Subsequence replacement on IEnumerable
int[] seq = [1, 2, 3, 2, 1];
int[] replaced = seq.Replace([2, 3], [9]).ToArray(); // [1, 9, 2, 1]
```

### LRUCache

```csharp
using Utils.Collections;

var cache = new LRUCache<int, string>(capacity: 3);
cache.Add(1, "one");
cache.Add(2, "two");
cache.Add(3, "three");
_ = cache[1];           // promote key 1 to most-recently-used
cache.Add(4, "four");   // evicts key 2 (least recently used)

Console.WriteLine(cache.ContainsKey(2)); // false
Console.WriteLine(cache.ContainsKey(1)); // true
```

### IndexedList

```csharp
using Utils.Collections;

record Product(int Id, string Name);

var list = new IndexedList<int, Product>(p => p.Id);
list.Add(new Product(1, "Apple"));
list.Add(new Product(2, "Banana"));

Product p = list[1]; // lookup by key — O(1)
```

### DoubleIndexedDictionary

```csharp
using Utils.Collections;

// Bidirectional lookup: code ↔ label
var map = new DoubleIndexedDictionary<int, string>();
map.Left.Add(1, "one");
map.Left.Add(2, "two");

string label = map.Left[1];    // "one"
int code     = map.Right["two"]; // 2
```

## Mathematics

```csharp
using Utils.Mathematics;

// Always-positive modulo (unlike %)
int m = MathEx.Mod(-1, 3); // 2  (C# % gives -1)

// Round to arbitrary base
int rounded = MathEx.Round(17, 5);    // 15
double r2   = MathEx.Round(1234.0, 100.0); // 1200

// Degree ↔ radian constants
double radians = 45.0 * MathEx.Deg2Rad; // ≈ 0.785
double degrees = radians * MathEx.Rad2Deg; // 45.0
```

## Objects

### FluentResult — chainable predicates

```csharp
using Utils.Objects;

string? input = "hello";

bool ok = input
    .IsNotNull()
    .Is(s => s.Length > 0)
    .Result; // true

// Branch on result
string output = input
    .Test(s => s.Length > 3)
    .Then(
        v => v.ToUpper(),   // success path
        v => "(too short)"); // failure path
// → "HELLO"
```

### ConstrainedValue — validated wrapper types

```csharp
using Utils.Objects;

public class PositiveInt : ConstrainedValue<int>
{
    public PositiveInt(int value) : base(value) { }
    protected override void CheckValue(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Must be positive.");
    }
}

var n = new PositiveInt(5);
int raw = n; // implicit unwrap → 5
new PositiveInt(-1); // throws ArgumentOutOfRangeException
```

### Enumerators — parse numeric ranges from strings

```csharp
using Utils.Objects;

// "1,3,5-8,12" → yields 1, 3, 5, 6, 7, 8, 12
foreach (int i in Enumerators.Enumerate<int>("1,3,5-8,12"))
    Console.Write($"{i} ");

// Continuous range
foreach (int i in Enumerators.Enumerate<int>(1, 5))
    Console.Write($"{i} "); // 1 2 3 4 5
```

## Strings

```csharp
using Utils.String;

// Wildcard matching (* = any sequence, ? = single char)
"hello world".Like("hello*");        // true
"hello world".Like("h?llo w*");      // true
"hello world".Like("*WORLD", ignoreCase: true); // true

// Predicate-based trim
string s = "  ##hello## ";
string t = s.Trim(c => c == ' ' || c == '#'); // "hello"

// Ensure prefix / suffix (idempotent)
"path/".AddPrefix("/");   // "/path/"
"/path".AddSuffix("/");   // "/path/"
"//path".AddPrefix("/");  // "//path" (already present)
```

## Async

```csharp
using Utils.Async;

IAsyncExecutor executor = new AsyncExecutor();

Func<Task>[] tasks =
[
    async () => { await Task.Delay(100); Console.WriteLine("A"); },
    async () => { await Task.Delay(100); Console.WriteLine("B"); },
    async () => { await Task.Delay(100); Console.WriteLine("C"); },
];

// Run all in parallel
await executor.ExecuteParallelAsync(tasks);

// Run sequentially
await executor.ExecuteSequentialAsync(tasks);

// Parallel only when count >= threshold, otherwise sequential
await executor.ExecuteAsync(tasks, parallelThreshold: 3);
```

## Transactions

```csharp
using Utils.Transactions;

class FileWriteAction : ITransactionalAction
{
    public void Execute()  { /* write temp file */ }
    public void Commit()   { /* rename temp → final */ }
    public void Rollback() { /* delete temp file */ }
}

var executor = new TransactionExecutor();
// If any Execute() throws, all completed actions are rolled back in reverse order
executor.Execute([new FileWriteAction(), new FileWriteAction()]);
```

## Ranges

```csharp
using Utils.Range;

// Parse a range string
var ranges = DoubleRanges.Parse("[0;100]");  // closed [0, 100]
Console.WriteLine(ranges.Contains(50.0));   // true
Console.WriteLine(ranges.Contains(101.0));  // false

// Overlap and intersect
var a = DoubleRanges.Parse("[1;5]");
var b = DoubleRanges.Parse("[3;7]");
// Check per-range overlap via IRange<double> members
```

## Randomization

```csharp
using Utils.Randomization;

var rng = Random.Shared;

// Fixed-length random string (alphanumeric + space by default)
string token = rng.RandomString(16);

// Custom alphabet
string hex = rng.RandomString(8, "0123456789ABCDEF");

// Variable-length (between min and max)
string s = rng.RandomString(minLength: 4, maxLength: 12);
```

## Security — TOTP authenticator

```csharp
using System.Text;
using Utils.Security;

byte[] key = Encoding.UTF8.GetBytes("MySecretKey12345");

// Google Authenticator-compatible: HMAC-SHA256, 6 digits, 30-second window
var auth = Authenticator.GoogleAuthenticator(key);

string code = auth.ComputeAuthenticator();        // current 6-digit code
bool valid  = auth.VerifyAuthenticator(1, code);  // ±1 window tolerance

// Custom settings
var custom = new Authenticator("HMACSHA512", key, digits: 8, intervalLength: 60);
```

## Expression optimiser

```csharp
using Utils.Expressions;
using System.Linq.Expressions;

var optimiser = new ExpressionOptimiser();

Expression<Func<int>> expr = () => (1 + 1) * 3 - 4 + 2;
var optimised = (Expression<Func<int>>)optimiser.Optimize(expr);
int result = optimised.Compile()(); // 4 — computed without any arithmetic at runtime

// Algebraic identities: x*1→x, x+0→x, x*0→0, !!x→x
// Boolean short-circuits: expr || true → true, expr && false → expr (side effects preserved)
```

## Related packages
- `omy.Utils.Data` — data record mappers
- `omy.Utils.IO` — stream utilities and binary serialization
- `omy.Utils.Imaging` — imaging and drawing utilities
- `omy.Utils.Xml` — XPath-driven XML processing
- `omy.Utils.Net` — networking helpers (DNS, ICMP, Wake-on-LAN)
