# omy.Utils.Mathematics (advanced math)

`omy.Utils.Mathematics` offers FFTs, symbolic math helpers, SI conversions, and generic linear algebra structures used across the utility family.

## Install
```bash
dotnet add package omy.Utils.Mathematics
```

## Supported frameworks
- net8.0

## Features
- Symbolic expression derivation and integration, including generic numeric transformers.
- Fast Fourier Transform implementation.
- SI unit conversions and number-to-text conversion utilities.
- Generic vector and matrix structures for linear algebra operations.

## Quick usage
```csharp
int[] line = Utils.Mathematics.MathEx.ComputePascalTriangleLine(4); // [1,4,6,4,1]
Complex[] signal = [1, 1, 0, 0];
var fft = new Utils.Mathematics.Fourrier.FastFourrierTransform();
fft.Transform(signal);
```

## Symbolic expressions (generic API)

```csharp
using System.Linq.Expressions;
using Utils.Mathematics;

Expression<Func<float, float>> f = x => x * x + x;

// Generic APIs
LambdaExpression derivative = f.Derivate<float>("x");
LambdaExpression integral = f.Integrate<float>("x");

float d = (float)derivative.Compile().DynamicInvoke(3f)!; // ~7
float i = (float)integral.Compile().DynamicInvoke(2f)!;   // ~4.666...
```

## Related packages
- `omy.Utils.Imaging` – leverages math helpers for drawing.
- `omy.Utils` – shared primitives and extensions.
