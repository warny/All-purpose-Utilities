# DrawTest

`DrawTest` is a Windows Forms demo application for the drawing primitives provided by `Utils.Imaging` and `Utils.Fonts`.

## Purpose

This project helps you quickly visualize rendering outputs (lines, Bézier curves, ellipses, text) to validate graphics behavior.

## Examples

### 1) Run the application

```bash
dotnet run --project DrawTest/DrawTest.csproj
```

### 2) Build only this project

```bash
dotnet build DrawTest/DrawTest.csproj
```

### 3) Explore a rendering example

The main rendering logic is in `TestForm.Draw()`:
- line and curve drawing,
- vector text rendering,
- fill and stroke operations.

See the code: `DrawTest/TestForm.cs`.

## Related projects

- [`Utils.Imaging`](../Utils.Imaging/README.md)
- [`Utils.Fonts`](../Utils.Fonts/README.md)
- [`Utils`](../Utils/README.md)
