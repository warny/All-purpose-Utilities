# Utils.Imaging Library

The **Utils.Imaging** package contains bitmap accessors, color structures and drawing primitives built on top of `System.Drawing`.
It provides the graphical foundation used by the sample applications in this repository.

## Features

- `Argb32` and `Ahsv` color types supporting 8, 32 and 64 bit formats
- Helpers to manipulate bitmaps and pixel data efficiently
- A minimal vector drawing system for basic shapes and paths
- Integration with the `Utils.Fonts` and `Utils.Mathematics` packages

## Usage example

```csharp
// Convert from ARGB to ACYM and back
var argb = new Utils.Imaging.ColorArgb(1.0, 0.2, 0.4, 0.6);
var acym = new Utils.Imaging.ColorAcym(argb);
var roundTrip = new Utils.Imaging.ColorArgb(acym);
```
