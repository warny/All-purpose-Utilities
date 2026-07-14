# omy.Utils.Mathematics (advanced math)

`omy.Utils.Mathematics` offers FFTs, symbolic expression transformers, polynomials, and generic linear algebra structures used across the utility family.

## Install
```bash
dotnet add package omy.Utils.Mathematics
```

## Supported frameworks
- net8.0

## Features
- Symbolic expression derivation and integration, including generic numeric transformers.
- Fast Fourier Transform with frequency/magnitude/phase extraction helpers.
- Polynomial arithmetic with canonical form and symbolic calculus.
- Generic vector and matrix structures for linear algebra (arithmetic, inversion, LU decomposition, affine subspaces, geometric transformations).

## Quick usage
```csharp
int[] line = Utils.Mathematics.MathEx.ComputePascalTriangleLine(4); // [1,4,6,4,1]
Complex[] signal = [1, 1, 0, 0];
Utils.Mathematics.Fourier.FastFourierTransform.Transform(signal);
```

## MathEx — extended math utilities

`MathEx` provides generic numerical utilities, rounding helpers, and combinatorial tools.

### `RoundToSignificantDigits`

Rounds a `BigInteger` to a given number of significant digits using standard rounding (≥ 5 rounds up).

```csharp
using System.Numerics;
using Utils.Mathematics;

MathEx.RoundToSignificantDigits(123456789, 3);  // 123000000
MathEx.RoundToSignificantDigits(123456789, 2);  // 120000000
MathEx.RoundToSignificantDigits(123456789, 1);  // 100000000
MathEx.RoundToSignificantDigits(125, 2);        // 130   (5 → rounds up)
MathEx.RoundToSignificantDigits(124, 2);        // 120   (4 → rounds down)
MathEx.RoundToSignificantDigits(-123456789, 3); // -123000000  (sign preserved)
MathEx.RoundToSignificantDigits(0, 3);          // 0
```

Throws `ArgumentOutOfRangeException` when `significantDigits < 1`. Returns `value` unchanged when it already has fewer or equal digits than requested.

### Other rounding helpers

```csharp
MathEx.Round(1234, 100);   // 1200 — nearest multiple of 100
MathEx.Round(1250, 100);   // 1300 — tie rounds up
MathEx.Floor(1234,  100);  // 1200 — greatest multiple ≤ value
MathEx.Ceiling(1201, 100); // 1300 — smallest multiple ≥ value
```

### `Mod` — always-positive modulo

```csharp
MathEx.Mod(-1, 3);  // 2  (unlike C# % which gives -1)
```

### `Gcd` / `Lcm`

```csharp
MathEx.Gcd(12, 8);   // 4
MathEx.Lcm(4, 6);    // 12
```

### `Min` / `Max` / `Clamp` — fixed-set overloads

```csharp
MathEx.Min(3, 1, 4, 1, 5);   // 1
MathEx.Max(3, 1, 4, 1, 5);   // 5
5.Clamp(1, 4);                 // 4
```

## FFT examples

`FastFourierTransform` is a static class that performs an in-place Cooley-Tukey FFT. The input length must be a power of two.

### Forward transform

```csharp
using System.Numerics;
using Utils.Mathematics.Fourier;

Complex[] signal = [1, 1, 0, 0, 0, 0, 0, 0]; // 8 samples
FastFourierTransform.Transform(signal);
// signal now contains the frequency-domain representation
// only signal[0..N/2] carry unique information (Nyquist)
```

### Extract frequency bins

```csharp
double sampleRate = 44100; // Hz
double[] frequencies = signal.GetFrequencies(sampleRate);
for (int i = 0; i < frequencies.Length; i++)
    Console.WriteLine($"{frequencies[i]:F1} Hz  magnitude={signal[i].Magnitude:F3}");
```

### Transform a sub-range

There is no built-in sub-range overload. Extract a power-of-two slice, transform it, and copy it back:

```csharp
Complex[] buffer = new Complex[16];
// ... fill buffer ...
Complex[] subRange = buffer[4..12]; // length 8, a power of two
FastFourierTransform.Transform(subRange);
Array.Copy(subRange, 0, buffer, 4, subRange.Length);
```

## Symbolic expression examples

Extension methods on `LambdaExpression` compute symbolic derivatives and anti-derivatives at compile time using expression trees.

### Derivative

```csharp
using System.Linq.Expressions;
using Utils.Mathematics;

Expression<Func<double, double>> f = x => x * x * x - 2 * x;
LambdaExpression df = f.Derivate(); // 3x² - 2
double value = (double)df.Compile().DynamicInvoke(3.0)!; // 25
```

### Integral (anti-derivative)

```csharp
Expression<Func<double, double>> f = x => 3 * x * x;
LambdaExpression F = f.Integrate(); // x³  (+ C)
double value = (double)F.Compile().DynamicInvoke(2.0)!; // 8
```

### Generic numeric type

```csharp
Expression<Func<float, float>> f = x => x * x + x;
LambdaExpression df = f.Derivate<float>("x"); // 2x + 1
float d = (float)df.Compile().DynamicInvoke(3f)!; // 7
```

## Vector examples

`Vector<T>` is a generic immutable vector. The base constraint is `IFloatingPoint<T>, IRootFunctions<T>` (required for `Norm`/`Normalize`); some operations such as `AngleWith` additionally require `ITrigonometricFunctions<T>`.

### Construction and basic properties

```csharp
using Utils.Mathematics.LinearAlgebra;

var v = new Vector<double>(3.0, 4.0);
Console.WriteLine(v.Norm);      // 5
Console.WriteLine(v.Dimension); // 2
Console.WriteLine(v[0]);        // 3
```

### Arithmetic operators

```csharp
var a = new Vector<double>(1.0, 2.0, 3.0);
var b = new Vector<double>(4.0, 5.0, 6.0);

Vector<double> sum  = a + b;          // (5, 7, 9)
Vector<double> diff = b - a;          // (3, 3, 3)
Vector<double> scaled = 2.0 * a;      // (2, 4, 6)
double dot = a * b;                   // 32
```

### Normalize

```csharp
var v = new Vector<double>(3.0, 0.0, 4.0);
Vector<double> unit = v.Normalize();  // (0.6, 0, 0.8)
Console.WriteLine(unit.Norm);         // 1
```

### Cross product (n-1 vectors in n dimensions)

```csharp
// Cross product of 2 vectors in 3D space
var u = new Vector<double>(1.0, 0.0, 0.0);
var v = new Vector<double>(0.0, 1.0, 0.0);
Vector<double> normal = Vector<double>.Product(u, v); // (0, 0, 1)
```

### Barycenter

```csharp
var a = new Vector<double>(0.0, 0.0);
var b = new Vector<double>(4.0, 0.0);
var c = new Vector<double>(0.0, 4.0);

var (_, center) = Vector<double>.ComputeBarycenter(a, b, c); // (1.33, 1.33)

// Weighted barycenter
var (_, weighted) = Vector<double>.ComputeBarycenter(
    wp => wp.w, wp => wp.v,
    (w: 1.0, v: a), (w: 2.0, v: b), (w: 3.0, v: c));
```

## Matrix examples

`Matrix<T>` is a generic immutable matrix with base constraint `IFloatingPoint<T>, IRootFunctions<T>`. Supports `+`, `-`, `*` (matrix/scalar/vector), `/` (scalar), inversion, LU decomposition, and determinant. Transformation factories (`MatrixTransformations`) may additionally require `ITrigonometricFunctions<T>`.

### Construction

```csharp
using Utils.Mathematics.LinearAlgebra;

// From a 2D array
var m = new Matrix<double>(new double[,] {
    { 1, 2 },
    { 3, 4 }
});

// From column vectors
var col1 = new Vector<double>(1.0, 3.0);
var col2 = new Vector<double>(2.0, 4.0);
var m2 = new Matrix<double>(col1, col2);
```

### Arithmetic

```csharp
var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
var b = new Matrix<double>(new double[,] { { 5, 6 }, { 7, 8 } });

Matrix<double> sum     = a + b;
Matrix<double> product = a * b;
Matrix<double> scaled  = a * 2.0;

var v = new Vector<double>(1.0, 0.0);
Vector<double> transformed = a * v; // (1, 3)
```

### Determinant, inversion, LU decomposition

```csharp
var m = new Matrix<double>(new double[,] {
    { 2, 1 },
    { 5, 3 }
});

double det = m.Determinant;           // 1
Matrix<double> inv = m.Invert();      // { {3,-1}, {-5,2} }

var (L, U, P) = m.DiagonalizeLU();
// P * m == L * U
```

### Properties

```csharp
Console.WriteLine(m.Rows);          // 2
Console.WriteLine(m.Columns);       // 2
Console.WriteLine(m.IsSquare);      // True
Console.WriteLine(m.IsDiagonal); // False
```

## MatrixTransformations examples

`MatrixTransformations` creates standard transformation matrices in homogeneous coordinates (dimension n+1 for n-dimensional transforms).

### Identity and diagonal

```csharp
using Utils.Mathematics.LinearAlgebra;

Matrix<double> I = MatrixTransformations.Identity<double>(3);        // 3×3 identity
Matrix<double> D = MatrixTransformations.Diagonal<double>(2.0, 3.0); // diag(2,3)
```

### Scaling, translation, rotation

```csharp
// 2D scaling by (2x, 3y) — produces a 3×3 matrix
Matrix<double> scale = MatrixTransformations.Scaling<double>(2.0, 3.0);

// 2D translation by (5, 10) — produces a 3×3 matrix
Matrix<double> trans = MatrixTransformations.Translation<double>(5.0, 10.0);

// 2D rotation by π/4 — produces a 3×3 matrix
Matrix<double> rot = MatrixTransformations.Rotation<double>(Math.PI / 4);

// Chain: scale then rotate then translate
Matrix<double> combined = trans * rot * scale;
```

### Apply a transformation to a point

```csharp
var point = new Vector<double>(1.0, 0.0);
Vector<double> inHomogeneous = point.ToNormalSpace();       // (1, 0, 1)
Vector<double> transformed   = combined * inHomogeneous;
Vector<double> result        = transformed.FromNormalSpace(); // back to 2D
```

### Shear

```csharp
// 2D shear: one angle per off-diagonal pair
Matrix<double> shear = MatrixTransformations.Skew<double>(Math.PI / 6, 0.0);
```

## Line examples

`Line<T>` represents a line in n-dimensional space by a point and a direction vector.

```csharp
using Utils.Mathematics.LinearAlgebra;

var origin    = new Vector<double>(0.0, 0.0, 0.0);
var direction = new Vector<double>(1.0, 0.0, 0.0); // along X axis
var line      = new Line<double>(origin, direction);

var point = new Vector<double>(3.0, 4.0, 0.0);
double dist = line.DistanceTo(point); // 4  (perpendicular distance)
```

## Related packages
- `omy.Utils.Imaging` – leverages math helpers for drawing.
- `omy.Utils` – shared primitives and extensions.
