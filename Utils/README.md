# Utils Library

The **Utils** library is a large collection of helper namespaces covering many common programming needs.
It targets **.NET 8** and is the base dependency for the other utility packages contained in this repository.

## Features

- **Arrays** – helpers for comparing arrays, working with multi-dimensional data and specialized comparers
- **Collections** – indexed lists, skip lists, LRU caches and dictionary extensions
- **Expressions** – creation and transformation of expression trees and lambda utilities
- **Files** – filesystem helpers to manipulate paths and temporary files
- **Mathematics** – base classes for expression transformation and math functions
- **Net** – advanced URI builder and network helpers
- **Objects** – data conversion routines and an advanced string formatter
- **Reflection** – additional reflection primitives such as `PropertyOrFieldInfo`
- **Resources** – utilities for working with embedded resources
- **Security** – Google Authenticator helpers
- **Streams** – base16/base32/base64 converters and binary serialization
- **XML** – helpers for XML processing

The design separates data structures from processing logic wherever possible and exposes extensibility points through interfaces.

## Usage examples

Short snippets demonstrating typical API usage:

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

### Files
```csharp
// Search executables four levels deep under "Program Files"
foreach (string exe in Utils.Files.PathUtils.EnumerateFiles(@"C:\\Program Files\\*\\*\\*\\*.exe"))
{
    Console.WriteLine(exe);
}
```

### Reflection
```csharp
var info = new Utils.Reflection.PropertyOrFieldInfo(typeof(MyType).GetField("Id"));
int id = (int)info.GetValue(myObj);
info.SetValue(myObj, 42);
```

### Resources
```csharp
var res = new Utils.Resources.ExternalResource("Strings");
string text = (string)res["Welcome"];
```

### Strings
```csharp
using Utils.Objects;
bool match = "File123.log".Like("File???.log");
string normalized = "---hello---".Trim(c => c == '-');
string path = "report".AddPrefix("out_").AddSuffix(".txt");
string title = "hello world".FirstLetterUpperCase();
```

### Security
```csharp
byte[] key = Convert.FromBase64String("MFRGGZDFMZTWQ2LK");
var authenticator = Utils.Security.Authenticator.GoogleAuthenticator(key);
string code = authenticator.ComputeAuthenticator();
bool ok = authenticator.VerifyAuthenticator(1, code);
```

### Expressions
```csharp
var expression = "(items) => items[0] + items[1]";
var lambda = Utils.Expressions.ExpressionParser.Parse<Func<string[], string>>(expression);
Func<string[], string> concat = lambda.Compile();
string result = concat(new[] { "Hello", "World" });
```
```csharp
var switchExpr = "(i) => switch(i) { case 1: 10; case 2: 20; default: 0; }";
var switchLambda = Utils.Expressions.ExpressionParser.Parse<Func<int, int>>(switchExpr);
int value = switchLambda.Compile()(2); // 20
```
```csharp
var switchStmt = "(int i) => { int v = 0; switch(i) { case 1: v = 10; break; case 2: v = 20; break; default: v = 0; break; } return v; }";
var switchFunc = Utils.Expressions.ExpressionParser.Parse<Func<int, int>>(switchStmt).Compile();
int result = switchFunc(1); // 10
```
```csharp
var formatter = Utils.String.StringFormat.Create<Func<string, string>, DefaultInterpolatedStringHandler>("Name: {name}", "name");
string formatted = formatter("John");
```

### XML
```csharp
using var reader = XmlReader.Create("items.xml");
reader.ReadToDescendant("item");
foreach (var child in reader.ReadChildElements())
{
    Console.WriteLine(child.ReadOuterXml());
}
```

