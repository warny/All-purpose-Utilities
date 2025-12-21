# omy.Utils.Imaging (graphics primitives)

`omy.Utils.Imaging` provides bitmap accessors, color structures, and vector drawing helpers built on top of `System.Drawing`.

## Install
```bash
dotnet add package omy.Utils.Imaging
```

## Supported frameworks
- net8.0

## Features
- `Argb32` and `Ahsv` color types supporting 8, 32, and 64-bit formats.
- Helpers to manipulate bitmaps and pixel data efficiently.
- Minimal vector drawing system for basic shapes and paths.
- Integrates with `omy.Utils.Fonts` and `omy.Utils.Mathematics` for text and geometry.

## Quick usage
```csharp
var hsv = new ColorAhsv(0.5, 180, 1, 1); // cyan
ColorArgb argb = hsv.ToArgbColor();
ColorAhsv32 compact = ColorAhsv32.FromArgbColor((ColorArgb32)argb);
```

## Related packages
- `omy.Utils.Fonts` – font parsing and glyph metrics.
- `omy.Utils.Mathematics` – numerical helpers leveraged by drawing routines.
