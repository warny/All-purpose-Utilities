# All-purpose Utilities

This repository contains a collection of utility libraries and sample applications targeting **.NET 8** and **.NET 9**. The solution aggregates several projects under the `Utils` family, ranging from low level helpers to Windows Forms samples.

## Requirements

The solution uses the .NET SDK version 9 (preview). Build all projects with:

```bash
 dotnet build
```

## Projects and Namespaces

### `Utils`
A general purpose library exposing many helper namespaces:
- **`Utils.Arrays`** – array comparison utilities, multi dimensional helpers and key/value comparers.
- **`Utils.Collections`** – custom collections such as indexed lists, skip lists, LRU caches and dictionary extensions.
- **`Utils.Expressions`** – expression parser, builders and simplifiers for lambda expressions.
- **`Utils.Files`** – file and path utilities.
- **`Utils.Mathematics`** (base) – mathematical extensions and expression transformers.
- **`Utils.Net`** – helpers for URIs, query strings, mail addresses and IP ranges.
- **`Utils.Objects`** – data conversion, advanced string formatting and miscellaneous object utilities.
- **`Utils.Reflection`** – extra reflection helpers like `PropertyOrFieldInfo`.
- **`Utils.Resources`** – utilities for working with embedded resources.
- **`Utils.Security`** – Google authenticator helpers.
- **`Utils.XML`** – XML processing helpers.

### `Utils.IO`
I/O related helpers including:
- base16/base32/base64 stream encoders and decoders
- binary serialization framework
- stream copying and validation utilities

### `Utils.Net` (System.Net)
Network focused utilities:
- full DNS protocol implementation and packet helpers
- ICMP utilities and basic traceroute support
- gathering system network parameters

### `Utils.Data`
Attributes and helpers to map `IDataRecord`/`IDataReader` data to typed objects.

### `Utils.Imaging`
Bitmap accessors and drawing primitives. Provides ARGB/AHSV color structures and basic vector drawing support.

### `Utils.Fonts`
Font management library able to read and interpret TrueType and PostScript fonts. Also contains utilities for encoding tables and glyph metrics.

### `Utils.Geography`
Models and tools for geographic coordinates, tile representations and various map projections.

### `Utils.Mathematics`
Advanced mathematics library featuring:
- expression derivation and integration
- fast Fourier transform support
- conversion helpers for SI units
- generic linear algebra types

### `Utils.Reflection`
Runtime reflection helpers, notably a dynamic DLL mapping system and platform detection utilities.

### `Utils.VirtualMachine`
Minimal virtual machine framework. Instructions are defined using attributes and executed through a byte‑code processor with configurable endianness.

### `Fractals`
Windows Forms sample application that renders fractals using the imaging library.

### `DrawTest`
Another Windows Forms sample demonstrating the drawing primitives available in `Utils.Imaging`.


### `UtilsTest`
Unit test suite using MSTest and SpecFlow covering the utilities and components from the other projects.

## Usage examples

Below are short snippets demonstrating a few commonly used classes.

```csharp
// Manipulate a query string with UriBuilderEx
var builder = new Utils.Net.UriBuilderEx("http://example.com/?key1=value1&key2=value2");
builder.QueryString["key3"].Add("value3");
string url = builder.ToString(); // "http://example.com/?key1=value1&key2=value2&key3=value3"

// Generate and verify an authenticator code
byte[] key = Convert.FromBase64String("MFRGGZDFMZTWQ2LK");
var authenticator = Utils.Security.Authenticator.GoogleAuthenticator(key);
string code = authenticator.ComputeAuthenticator();
bool isValid = authenticator.VerifyAuthenticator(1, code);

// Compile a lambda expression from a string
var expression = "(items) => items[0] + items[1]";
var lambda = Utils.Expressions.ExpressionParser.Parse<Func<string[], string>>(expression);
Func<string[], string> concat = lambda.Compile();
string result = concat(new[] { "Hello", "World" });

// StringExtensions usage
bool match = "File123.log".Like("File???.log");
string sanitized = "---abc---".Trim(c => c == '-');
string filePath = "report".AddSuffix(".txt").AddPrefix("logs/");
string title = "hello".FirstLetterUpperCase();

// Stream helpers
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

// Utils.Mathematics examples
double rounded = Utils.Mathematics.MathEx.Round(1.26, 0.5); // 1.5
int[] line = Utils.Mathematics.MathEx.ComputePascalTriangleLine(4); // [1,4,6,4,1]
var vector = new Utils.Mathematics.LinearAlgebra.Vector<double>([1, 2]);
var identity = Utils.Mathematics.LinearAlgebra.Matrix<double>.Identity(2);
var resultVec = identity * vector; // [1, 2]
Expression<Func<double, double>> func = x => x * x;
var derivative = (Expression<Func<double, double>>)func.Derivate();
var integrator = new Utils.Mathematics.Expressions.ExpressionIntegration("x");
var simplifier = new Utils.Mathematics.Expressions.ExpressionSimplifier();
var integral = (Expression<Func<double, double>>)simplifier.Simplify(integrator.Integrate(func));

// Perform a DNS lookup
var dns = new Utils.Net.DNSLookup();
DNSHeader header = dns.Request("A", "example.com");
var address = ((Utils.Net.DNS.RFC1035.Address)header.Responses[0].RData).IPAddress;

// Send an ICMP Echo Request
int rtt = await Utils.Net.IcmpUtils.SendEchoRequestAsync(System.Net.IPAddress.Parse("8.8.8.8"));

// Convert geographic coordinates using Mercator projection
var mercator = new Utils.Geography.Projections.MercatorProjection<double>();
var paris = new Utils.Geography.Model.GeoPoint<double>(48.8566, 2.3522);
Utils.Geography.Model.ProjectedPoint<double> map = mercator.GeoPointToMapPoint(paris);

// Measure distance and travel along a geodesic
var newYork = new Utils.Geography.Model.GeoPoint<double>(40.7128, -74.0060);
double distance = Utils.Geography.Model.Planets<double>.Earth.Distance(paris, newYork);
var vector = new Utils.Geography.Model.GeoVector<double>(paris, 90);
var destination = Utils.Geography.Model.Planets<double>.Earth.Travel(vector, 1000);

// Project coordinates using a planar transformation
var lambert = Utils.Geography.Projections.Projections<double>.Lambert;
var lambertPoint = lambert.GeoPointToMapPoint(paris);

// Parse a TrueType font and inspect glyph metrics
byte[] fontBytes = File.ReadAllBytes("arial.ttf");
var ttf = Utils.Fonts.TTF.TrueTypeFont.ParseFont(fontBytes);
float glyphWidth = ttf.GetGlyph('A').Width;

// Look up glyph names from the standard table
string name = Utils.Fonts.FontSupport.GetName(65); // "A"

// Dynamically map native functions
class KernelApi : Utils.Reflection.LibraryMapper
{
    [Utils.Reflection.LibraryMapper.External("GetTickCount")]
    public Func<uint> GetTickCount = null!;
}
var kernel = Utils.Reflection.LibraryMapper.Create<KernelApi>(
    Utils.Reflection.Platform.IsWindows ? "kernel32.dll" : "libc.so.6");
uint ticks = kernel.GetTickCount();
```

## NuGet packages

All libraries are configured to generate NuGet packages. Pushing changes to the
`release` branch triggers the **Publish NuGet** workflow. The workflow builds
the solution and publishes the library projects only when their version number
has changed. The script queries NuGet to verify that the package version is not
already available before packaging and uploading the corresponding `.nupkg`
file using the `NUGET_API_KEY` secret.

The NuGet package metadata links back to this GitHub repository:
<https://github.com/warny/All-purpose-Utilities>. French translations of the
XML documentation are available under `docs/fr`.

## License

This project is distributed under the Apache 2.0 license (see `LICENSE-apache-2.0.txt`).
