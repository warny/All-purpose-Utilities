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

### Streams
```csharp
using var fs = File.OpenRead("data.bin");
byte[] header = fs.ReadBytes(16);
using var slice = new Utils.IO.PartialStream(fs, 16, 32);
byte[] chunk = slice.ReadBytes((int)slice.Length);
using var a = new MemoryStream();
using var b = new MemoryStream();
using var copier = new Utils.IO.StreamCopier(a, b);
copier.Write(chunk, 0, chunk.Length);
using var target = new MemoryStream();
using var validator = new Utils.IO.StreamValidator(target);
validator.Write(chunk, 0, chunk.Length);
validator.Validate();
```

### Expressions
```csharp
var expression = "(items) => items[0] + items[1]";
var lambda = Utils.Expressions.ExpressionParser.Parse<Func<string[], string>>(expression);
Func<string[], string> concat = lambda.Compile();
string result = concat(new[] { "Hello", "World" });
```

### Mathematics
```csharp
double rounded = Utils.Mathematics.MathEx.Round(1.26, 0.5); // 1.5
int[] pascal = Utils.Mathematics.MathEx.ComputePascalTriangleLine(4); // [1,4,6,4,1]
var vector = new Utils.Mathematics.LinearAlgebra.Vector<double>([1, 2]);
var identity = Utils.Mathematics.LinearAlgebra.Matrix<double>.Identity(2);
var result = identity * vector; // [1, 2]
Complex[] data = [1, 1, 0, 0];
var fft = new Utils.Mathematics.Fourrier.FastFourrierTransform();
fft.Transform(data);
Expression<Func<double, double>> func = x => x * x;
var derivative = (Expression<Func<double, double>>)func.Derivate();
var integrator = new Utils.Mathematics.Expressions.ExpressionIntegration("x");
var simplifier = new Utils.Mathematics.Expressions.ExpressionSimplifier();
var integral = (Expression<Func<double, double>>)simplifier.Simplify(integrator.Integrate(func));
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

### Net
```csharp
var lookup = new Utils.Net.DNSLookup();
Utils.Net.DNS.DNSHeader result = lookup.Request("A", "example.com");
var ip = ((Utils.Net.DNS.RFC1035.Address)result.Responses[0].RData).IPAddress;

var packet = new Utils.Net.Icmp.IcmpPacket
{
    PacketType = Utils.Net.Icmp.IcmpPacketType.IcmpV4EchoRequest
};
packet.CreateRandomPayload(8);
byte[] bytes = packet.ToBytes();
var parsed = Utils.Net.Icmp.IcmpPacket.ReadPacket(bytes);
```

### Geography
```csharp
var projection = new Utils.Geography.Projections.MercatorProjection<double>();
var paris = new Utils.Geography.Model.GeoPoint<double>(48.8566, 2.3522);
var mapPoint = projection.GeoPointToMapPoint(paris);
var newYork = new Utils.Geography.Model.GeoPoint<double>(40.7128, -74.0060);
double dist = Utils.Geography.Model.Planets<double>.Earth.Distance(paris, newYork);
var vector = new Utils.Geography.Model.GeoVector<double>(paris, 90);
var travelled = Utils.Geography.Model.Planets<double>.Earth.Travel(vector, 1000);
var lambert = Utils.Geography.Projections.Projections<double>.Lambert;
var planar = lambert.GeoPointToMapPoint(paris);
```

### Fonts
```csharp
// Parse a TrueType font
byte[] bytes = File.ReadAllBytes("arial.ttf");
var font = Utils.Fonts.TTF.TrueTypeFont.ParseFont(bytes);
var glyph = font.GetGlyph('A');
float width = glyph.Width;

// Retrieve glyph name from encoding table
string glyphName = Utils.Fonts.FontSupport.GetName(65); // "A"
```
