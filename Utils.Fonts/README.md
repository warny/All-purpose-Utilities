# omy.Utils.Fonts (font utilities)

`omy.Utils.Fonts` parses TrueType/PostScript fonts and exposes encoding tables, glyph metrics, and helper structures for rendering scenarios.

## Install
```bash
dotnet add package omy.Utils.Fonts
```

## Supported frameworks
- net8.0

## Features
- Parse TrueType fonts and inspect glyph flags, metrics, and tables.
- Convert between encodings and character maps.
- Helpers for ligatures, kerning, and embedded font data extraction.

## Quick usage
```csharp
using Utils.Fonts;
using Utils.Fonts.Tables.Sfnt;

using var reader = new SfntReader(File.OpenRead("MyFont.ttf"));
var font = reader.ReadFont();
short ascent = font.Hhea.Ascender;
ushort glyphCount = font.Maxp.NumGlyphs;
```

## Related packages
- `omy.Utils.Imaging` – drawing utilities that consume font metrics.
- `omy.Utils.IO` – stream helpers used for font parsing.
