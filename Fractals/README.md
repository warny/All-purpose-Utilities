# Fractals

`Fractals` is a Windows Forms demo application that generates fractals using the utility libraries from this repository.

## Purpose

Provide a quick visual test bed for fractal computation/rendering algorithms (Mandelbrot, Julia).

## Examples

### 1) Run the application

```bash
dotnet run --project Fractals/Fractals.csproj
```

### 2) Build the project

```bash
dotnet build Fractals/Fractals.csproj
```

### 3) Explore implementations

- Compute logic: `Fractals/ComputeFractal.cs`
- Fractal types: `Fractals/IFractal.cs`
- Main form: `Fractals/FactalsForm.cs`

## Related projects

- [`Utils.Imaging`](../Utils.Imaging/README.md)
- [`Utils`](../Utils/README.md)
