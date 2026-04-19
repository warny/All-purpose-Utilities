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

## More examples

### 1) Draw lines and circles on an in-memory image

```csharp
using System;
using System.Drawing;
using Utils.Drawing;

var image = new ColorArgb32[800, 600];
var draw = new DrawI<ColorArgb32>(image);

draw.DrawLine(new Point(10, 10), new Point(300, 180), new ColorArgb32(255, 0, 0));
draw.FillCircle(new Point(400, 300), 80, new ColorArgb32(0, 128, 255));
draw.DrawEllipse(new Point(400, 300), 120, 60, new ColorArgb32(255, 255, 255), 0);
```

### 2) Build gradients from HSV/ARGB helpers

```csharp
ColorArgb32 start = new ColorArgb32(255, 0, 0);
ColorArgb32 end = new ColorArgb32(0, 0, 255);

ColorArgb32 middle = ColorArgb32.LinearGrandient(start, end, 0.5f);
ColorAhsv32 middleHsv = ColorAhsv32.FromArgbColor(middle);
```

### 3) Draw thick shape outlines with join styles

```csharp
using Utils.Drawing;

var image = new int[256, 256];
var draw = new DrawI<int>(image);

draw.DrawShapeThick(
    1,
    8f,
    JoinStyle.Round,
    new Segment(new Point(20, 20), new Point(220, 20)),
    new Segment(new Point(220, 20), new Point(220, 220)));
```

### 4) Use lambda brushes/fills

```csharp
using Utils.Drawing;

var image = new ColorArgb32[256, 256];
var draw = new DrawI<ColorArgb32>(image);

draw.FillCircle(
    new Point(128, 128),
    100,
    new MapBrush<ColorArgb32>((position, shape) =>
        ColorArgb32.LinearGrandient(new ColorArgb32(255, 128, 0), new ColorArgb32(0, 0, 0), position), 10));
```

## Related packages
- `omy.Utils.Fonts` – font parsing and glyph metrics.
- `omy.Utils.Mathematics` – numerical helpers leveraged by drawing routines.
