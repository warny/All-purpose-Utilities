# Utils.Mathematics Library

The **Utils.Mathematics** package offers advanced math helpers and generic algebra types used by other utilities.
It targets **.NET 9** and is built for extensibility so algorithms can operate on generic numeric types.

## Features

- Symbolic expression derivation and integration
- Fast Fourier Transform implementation
- Conversions to and from SI units
- Generic linear algebra structures (vectors and matrices in any dimension)

## Usage examples
```csharp
int[] line = Utils.Mathematics.MathEx.ComputePascalTriangleLine(4); // [1,4,6,4,1]
Complex[] signal = [1, 1, 0, 0];
var fft = new Utils.Mathematics.Fourrier.FastFourrierTransform();
fft.Transform(signal);
var vector = new Utils.Mathematics.LinearAlgebra.Vector<double>([1, 2]);
var identity = Utils.Mathematics.LinearAlgebra.MatrixTransformations.Identity<double>(2);
var result = identity * vector; // [1, 2]
Expression<Func<double, double>> func = x => x * x;
var derivative = (Expression<Func<double, double>>)func.Derivate();
var integrator = new Utils.Mathematics.Expressions.ExpressionIntegration("x");
var simplifier = new Utils.Mathematics.Expressions.ExpressionSimplifier();
var integral = (Expression<Func<double, double>>)simplifier.Simplify(integrator.Integrate(func));
```

```csharp
var converter = Utils.Mathematics.NumberToStringConverter.GetConverter("EN");
string text = converter.Convert(12.34m); // "twelve point thirty four hundredths"
var limit = converter.MaxNumber; // null when unlimited
```

