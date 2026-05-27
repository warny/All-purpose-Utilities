# omy.Utils.Fonts (font utilities)

`omy.Utils.Fonts` parses TrueType (TTF) and PostScript (PFA/PFB/Type 3) fonts and exposes glyph metrics, encoding tables, and a vector drawing interface for rendering scenarios.

## Install
```bash
dotnet add package omy.Utils.Fonts
```

## Supported frameworks
- net8.0

## Features
- Parse TrueType fonts from files or byte arrays; inspect tables, glyphs, and kerning.
- Serialize modified TrueType fonts back to bytes.
- Load PostScript Type 1 fonts in text (PFA) and binary (PFB) formats.
- Load PostScript Type 3 fonts.
- Encoding tables for WinAnsi, MacRoman, ISO Latin 1, and Adobe Standard.
- `IFont` / `IGlyph` / `IGraphicConverter` abstraction for renderer-agnostic glyph output.

## Quick usage
```csharp
using System.IO;
using Utils.Fonts.TTF;

byte[] ttfBytes = File.ReadAllBytes("Arial.ttf");
TrueTypeFont font = TrueTypeFont.ParseFont(ttfBytes);

Console.WriteLine($"scale={font.Scale:F4}");    // e.g. 0.1000 for a 1000-upem font
Console.WriteLine($"baseline={font.BaseLineY}"); // y offset at 100 px cap height

IGlyph glyph = font.GetGlyph('A');
Console.WriteLine($"width={glyph.Width}, height={glyph.Height}");
```

## TrueTypeFont examples

### Load from file or stream

```csharp
using System.IO;
using Utils.Fonts.TTF;

// From bytes
byte[] bytes = File.ReadAllBytes("font.ttf");
TrueTypeFont font1 = TrueTypeFont.ParseFont(bytes);

// From a seekable stream
using var stream = File.OpenRead("font.ttf");
TrueTypeFont font2 = TrueTypeFont.ParseFont(stream);
```

### Glyph metrics and rendering

```csharp
IGlyph glyph = font.GetGlyph('g');
if (glyph is not null)
{
    Console.WriteLine($"advance width : {glyph.Width}");
    Console.WriteLine($"height        : {glyph.Height}");
    Console.WriteLine($"baseline      : {glyph.BaseLine}");

    // Render into a custom IGraphicConverter (e.g. an SVG or canvas backend)
    glyph.ToGraphic(myConverter);
}
```

### Kerning

```csharp
float kern = font.GetSpacingCorrection('T', 'o'); // negative for tight pair
```

### Inspect raw tables

```csharp
using Utils.Fonts.TTF.Tables;

HeadTable head = font.GetTable<HeadTable>(TableTypes.HEAD);
Console.WriteLine($"unitsPerEm = {head.UnitsPerEm}");

HheaTable hhea = font.GetTable<HheaTable>(TableTypes.HHEA);
Console.WriteLine($"ascent = {hhea.Ascent}");

bool hasKern = font.ContainsTable(TableTypes.KERN);
```

### Serialize back to bytes

```csharp
byte[] modified = font.WriteFont();
File.WriteAllBytes("modified.ttf", modified);
```

## IGraphicConverter example

Implement `IGraphicConverter` to feed glyph outlines into any graphics backend:

```csharp
using System.Numerics;
using Utils.Fonts;

public class SvgPathConverter : IGraphicConverter
{
    private System.Text.StringBuilder _sb = new();

    public void BeginDrawGlyph(float x, float y, Matrix3x2 transform) { }
    public void EndDrawGlyph() { }

    public void StartAt(float x, float y) => _sb.Append($"M {x} {y} ");
    public void LineTo(float x, float y)  => _sb.Append($"L {x} {y} ");
    public void ClosePath()               => _sb.Append("Z ");

    public void BezierTo(params (float x, float y)[] points)
    {
        _sb.Append("C ");
        foreach (var (x, y) in points) _sb.Append($"{x} {y} ");
    }

    public string PathData => _sb.ToString();
}

// Usage
var converter = new SvgPathConverter();
glyph.ToGraphic(converter);
string svgPath = converter.PathData;
```

## PostScriptFont examples

`PostScriptFont` supports a simplified text format, standard PFA (ASCII Type 1), and binary PFB fonts.

### Custom text format

```csharp
using System.IO;
using Utils.Fonts.PostScript;

// Text format understood by this loader:
// Glyph: A
// Width: 500
// Height: 700
// Baseline: 0
// Path:
// M 0 0
// L 250 700
// L 500 0
// Z
// EndGlyph
using var stream = File.OpenRead("myfont.txt");
PostScriptFont font = PostScriptFont.Load(stream);
IGlyph glyph = font.GetGlyph('A');
```

### PFA (ASCII Type 1)

```csharp
using var stream = File.OpenRead("myfont.pfa");
PostScriptFont font = PostScriptFont.LoadPfa(stream);
```

### PFB (binary Type 1)

```csharp
// From a stream
using var stream = File.OpenRead("myfont.pfb");
PostScriptFont font = PostScriptFont.LoadPfb(stream);

// Round-trip: write ASCII Type 1 content to PFB
string pfaAscii = File.ReadAllText("myfont.pfa");
using var output = File.Create("out.pfb");
PostScriptFont.WritePfb(pfaAscii, output);
```

## Type3Font example

```csharp
using System.IO;
using Utils.Fonts.PostScript;

using var stream = File.OpenRead("type3.ps");
Type3Font font = Type3Font.Load(stream);
IGlyph glyph = font.GetGlyph('A');
```

## FontSupport examples

`FontSupport` provides Adobe glyph-name tables and encoding vectors for Type 1 fonts.

```csharp
using Utils.Fonts;

// Glyph name lookup by index
string name = FontSupport.GetName(34);  // "A" (index 34 in Adobe standard names)

// Reverse lookup by name
int index = FontSupport.GetStrIndex("A"); // 34

// Encoding tables (index = char code, value = glyph name index)
int[] winAnsi = FontSupport.WinAnsiEncoding;
int[] macRoman = FontSupport.MacRomanEncoding;
int[] isoLatin = FontSupport.IsoLatin1Encoding;
int[] standard = FontSupport.StandardEncoding;

// Glyph name search in a custom table
int pos = FontSupport.FindName("A", FontSupport.StdNames);
```

## Related packages
- `omy.Utils.Imaging` – drawing utilities that consume font metrics via `IFont` / `IGraphicConverter`.
- `omy.Utils.IO` – stream helpers used by the font parsers.
