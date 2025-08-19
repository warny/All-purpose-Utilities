# Utils.Imaging Library

The **Utils.Imaging** package contains bitmap accessors, color structures and drawing primitives built on top of `System.Drawing`.
It targets **.NET 9** and provides the graphical foundation used by the sample applications in this repository.

## Features

- `Argb32` and `Ahsv` color types supporting 8, 32 and 64 bit formats
- Helpers to manipulate bitmaps and pixel data efficiently
- A minimal vector drawing system for basic shapes and paths
- Integration with the `Utils.Fonts` and `Utils.Mathematics` packages

## Usage example
```csharp
var hsv = new ColorAhsv(0.5, 180, 1, 1); // cyan
ColorArgb argb = hsv.ToArgbColor();
ColorAhsv32 compact = ColorAhsv32.FromArgbColor((ColorArgb32)argb);
```
