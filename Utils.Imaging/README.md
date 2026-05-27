# omy.Utils.Imaging (graphics primitives)

`omy.Utils.Imaging` provides bitmap accessors, ARGB/HSV color structures, a convolution-based image transformer, and a vector drawing system for basic shapes and paths.

## Install
```bash
dotnet add package omy.Utils.Imaging
```

## Supported frameworks
- net8.0

## Features
- `ColorArgb32` / `ColorArgb64` / `ColorArgb` — 8-bit, 16-bit, and floating-point ARGB colors.
- `ColorAhsv32` / `ColorAhsv64` / `ColorAhsv` — HSV color representations with ARGB conversion.
- `BitmapArgb32Accessor` — fast locked pixel access for `System.Drawing.Bitmap`.
- `DrawI<T>` — integer-coordinate rasterization (points, shapes, thick outlines).
- `MapBrush<T>` — solid-color or gradient brush for drawing operations.
- `ConvolutionMatrixFactory` — common kernels (blur, sharpen, edge detection).
- `MatrixImageTransformer<A,T>` — applies a convolution kernel to an image accessor.

## Quick usage
```csharp
using Utils.Imaging;

// Opaque red in 32-bit ARGB
var red = new ColorArgb32(255, 0, 0);

// Convert to HSV
var hsv = new ColorAhsv(red);
Console.WriteLine($"hue={hsv.Hue:F1}°"); // 0.0° (red)

// Convert back
ColorArgb argb = hsv.ToArgbColor();
var red32 = new ColorArgb32(argb);
```

## Color examples

### ARGB colors

```csharp
using Utils.Imaging;

// Opaque color from RGB bytes
var cyan = new ColorArgb32(0, 255, 255);

// Semi-transparent blue (alpha=128)
var blue = new ColorArgb32(alpha: 128, red: 0, green: 0, blue: 255);

// From packed uint
var white = new ColorArgb32(0xFFFFFFFF);

// Linear gradient between two colors
ColorArgb32 mid = ColorArgb32.LinearGrandient(cyan, blue, position: 0.5f);

// Porter-Duff "over" compositing
var composite = (ColorArgb32)blue.Over(cyan);

// Convert to/from System.Drawing.Color
var sysColor = System.Drawing.Color.FromArgb(red32.Alpha, red32.Red, red32.Green, red32.Blue);
var fromSys  = new ColorArgb32(sysColor);
```

### HSV colors

```csharp
using Utils.Imaging;

// Opaque cyan: hue=180°, S=1, V=1
var hsv = new ColorAhsv(alpha: 1.0, hue: 180.0, saturation: 1.0, value: 1.0);

// Opaque shorthand (alpha defaults to 1)
var green = new ColorAhsv(hue: 120.0, saturation: 1.0, value: 1.0);

// Convert HSV → ARGB
ColorArgb argb = hsv.ToArgbColor();
var argb32 = new ColorArgb32(argb);

// Convert ARGB → HSV
ColorAhsv fromArgb = ColorAhsv.FromArgbColor(argb);
Console.WriteLine($"hue={fromArgb.Hue:F1}°");

// 32-bit compact HSV (byte components)
ColorAhsv32 compact = new ColorAhsv32(/* ... */);
ColorArgb32 fromCompact = compact.ToArgbColor();

// Implicit conversions
ColorAhsv wide = compact;          // ColorAhsv32 → ColorAhsv
ColorArgb32 direct = compact;      // ColorAhsv32 → ColorArgb32
```

## BitmapArgb32Accessor examples

`BitmapArgb32Accessor` locks a `System.Drawing.Bitmap` for direct pixel access. Dispose it to unlock.

```csharp
using System.Drawing;
using Utils.Imaging;

using var bmp = new Bitmap(800, 600, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
using var acc = new BitmapArgb32Accessor(bmp);

// Read a pixel
ColorArgb32 px = acc[100, 200];

// Write a pixel
acc[100, 200] = new ColorArgb32(255, 0, 0); // red

// Iterate all pixels
for (int y = 0; y < acc.Height; y++)
    for (int x = 0; x < acc.Width; x++)
        acc[x, y] = new ColorArgb32(0, (byte)x, (byte)y, 0);
```

## DrawI examples

`DrawI<T>` takes any `IImageAccessor<T>` and provides rasterization helpers. For `System.Drawing.Bitmap` use `BitmapArgb32Accessor`; for in-memory arrays create a simple array-backed accessor.

```csharp
using System.Drawing;
using Utils.Drawing;
using Utils.Imaging;

using var bmp = new Bitmap(400, 300, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
using var acc = new BitmapArgb32Accessor(bmp);
var draw = new DrawI<ColorArgb32>(acc);

var red  = new ColorArgb32(255, 0, 0);
var blue = new ColorArgb32(0, 0, 255);

// Single pixel
draw.DrawPoint(new Point(10, 10), red);

// Shape outlines (solid color)
var seg = new Segment(new Point(20, 20), new Point(380, 20));
var circle = new Circle(new Point(200, 150), 80);
draw.DrawShape(red, seg, circle);

// Shape with a gradient brush (position in [0,1] along the shape)
var gradientBrush = new MapBrush<ColorArgb32>(
    (position, offset) => ColorArgb32.LinearGrandient(red, blue, position));
draw.DrawShape(gradientBrush, circle);

// Thick outline
draw.DrawShapeThick(red, thickness: 4f, JoinStyle.Round, seg);
```

## MapBrush examples

```csharp
using Utils.Drawing;
using Utils.Imaging;

// Solid-color brush (width=1 px)
var solid = new MapBrush<ColorArgb32>(new ColorArgb32(0, 200, 100));

// Gradient brush — position goes from 0 (start) to 1 (end) along the shape
var gradient = new MapBrush<ColorArgb32>(
    (position, offset) =>
        ColorArgb32.LinearGrandient(
            new ColorArgb32(255, 0, 0),
            new ColorArgb32(0, 0, 255),
            position),
    width: 2f);
```

## ConvolutionMatrixFactory examples

```csharp
using Utils.Imaging;

double[,] blur      = ConvolutionMatrixFactory.Blur(size: 5);    // 5×5 box blur
double[,] sharpen   = ConvolutionMatrixFactory.Sharpen();         // 3×3 unsharp
double[,] edges     = ConvolutionMatrixFactory.EdgeDetection();   // 3×3 Laplacian
```

## MatrixImageTransformer examples

```csharp
using System.Drawing;
using Utils.Imaging;

using var bmp = new Bitmap("photo.png");
using var acc = new BitmapArgb32Accessor(bmp);

// Apply a 3×3 blur
double[,] kernel = ConvolutionMatrixFactory.Blur(3);
var transformer = new MatrixImageTransformer<ColorArgb32, byte>(
    kernel,
    offset: new Point(1, 1),
    creator: (a, r, g, b) => new ColorArgb32(a, r, g, b));

transformer.Transform(acc);
bmp.Save("blurred.png");
```

## Related packages
- `omy.Utils.Fonts` – font parsing and glyph metrics consumed via `IGraphicConverter`.
- `omy.Utils.Mathematics` – numerical helpers leveraged by drawing routines.
