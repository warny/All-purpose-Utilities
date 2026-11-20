# Utils.Expressions.VBSyntax

`Utils.Expressions.VBSyntax` provides a **VB-like** expression compiler that targets LINQ expression trees (`System.Linq.Expressions`).

The main component is `VBSyntaxExpressionCompiler` in the `Utils.Expressions.VBSyntax.Runtime` namespace.

## What it is for

- Compile textual VB-like expressions into .NET `Expression` trees.
- Execute dynamic expressions with a symbol context.
- Declare `Function` and `Sub` procedures and reuse them in the same source.

## Installation

First published as `2.0.0-rc.1`; the current candidate is `2.0.0-rc.2`. No stable release exists yet,
so `dotnet add package` requires an explicit version (NuGet does not install a prerelease by default):

```bash
dotnet add package omy.Utils.Expressions.VBSyntax --version 2.0.0-rc.2
```

## Examples

### 1) Arithmetic one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.CompileExpression("1 + 2 * 3");
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 7
```

Recommended pattern for a self-contained expression: `Compile<TDelegate>` compiles straight to a real delegate, skipping the manual `Expression.Lambda(...).Compile()` step:

```csharp
Func<double> lambda = compiler.Compile<Func<double>>("1 + 2 * 3");

double result = lambda(); // 7
```

### 2) Power operator

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.CompileExpression("2.0 ^ 10");
var lambda = Expression.Lambda<Func<double>>(expression).Compile();

double result = lambda(); // 1024
```

### 3) Boolean one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.CompileExpression("3 > 1 AndAlso 4 <= 4");
var lambda = Expression.Lambda<Func<bool>>(expression).Compile();

bool result = lambda(); // True
```

### 4) Compile with symbols

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
ParameterExpression x = Expression.Parameter(typeof(double), "x");

Expression expression = compiler.CompileExpression("x * 2 + 1", new Dictionary<string, Expression>
{
    ["x"] = x,
});

var lambda = Expression.Lambda<Func<double, double>>(
    Expression.Convert(expression, typeof(double)), x).Compile();

double result = lambda(4); // 9
```

### 5) Compile with a runtime context

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();
context.Set("pi", 3.14159);

Expression expression = compiler.CompileExpression("pi * 2", context);
var lambda = Expression.Lambda<Func<double>>(
    Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 6.28318
```

### 6) String concatenation with `&`

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();
context.Set("greeting", "Hello");

Expression expression = compiler.CompileExpression("greeting & \", World!\"", context);
var lambda = Expression.Lambda<Func<string>>(expression).Compile();

string result = lambda(); // "Hello, World!"
```

### 7) Functions declared in source

```csharp
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();

compiler.CompileSource(
    """
    Public Function Twice(x As Integer) As Integer
        Return x * 2
    End Function
    """,
    context);

var twice = (Func<int, int>)context.Get("Twice");
int result = twice(5); // 10
```

### 8) Lambda in context + invocation

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();
context.Set("increment", (Func<double, double>)(x => x + 1));

Expression expression = compiler.CompileExpression("increment(41)", context);
var lambda = Expression.Lambda<Func<double>>(
    Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 42
```

### 9) Object creation

```csharp
using System.Linq.Expressions;
using System.Text;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.CompileExpression("New System.Text.StringBuilder()");
var lambda = Expression.Lambda<Func<object>>(
    Expression.Convert(expression, typeof(object))).Compile();

object result = lambda(); // StringBuilder instance
```

### 10) Standard control flow: `If / ElseIf / Else / End If`

```csharp
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();

compiler.CompileSource(
    """
    Public Function Abs(x As Double) As Double
        If x >= 0 Then
            Return x
        Else
            Return -x
        End If
    End Function
    """,
    context);

var abs = (Func<double, double>)context.Get("Abs");
double a = abs(3);   // 3
double b = abs(-3);  // 3
```

### 11) Standard control flow: `For … To … Next`

```csharp
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();

compiler.CompileSource(
    """
    Public Function SumTo(n As Integer) As Integer
        Dim sum As Integer = 0
        For i = 1 To n
            sum = sum + i
        Next i
        Return sum
    End Function
    """,
    context);

var sumTo = (Func<int, int>)context.Get("SumTo");
int result = sumTo(4); // 10
```

### 12) Standard control flow: `For Each … In … Next`

```csharp
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
var context = new VBSyntaxCompilerContext();
context.Set("values", new[] { 1, 2, 3, 4 });

compiler.CompileSource(
    """
    Public Function SumValues() As Integer
        Dim sum As Integer = 0
        For Each item As Integer In values
            sum = sum + item
        Next item
        Return sum
    End Function
    """,
    context);

var sumValues = (Func<int>)context.Get("SumValues");
int result = sumValues(); // 10
```

## Notes

- The compiler accepts VB-like syntax: arithmetic, logical (`And`/`AndAlso`, `Or`/`OrElse`, `Not`, `Xor`), comparison, string concatenation (`&`), and the power operator (`^`).
- Keywords are case-sensitive (e.g. `True`, `False`, `AndAlso`, `Integer`).
- The `=` operator means equality in expressions and assignment in statements; the compiler infers intent from whether the left-hand side is writeable.
- Public `Function`/`Sub` declarations are registered as delegates in the `VBSyntaxCompilerContext` and can be retrieved with `context.Get(name)`.
- For advanced scenarios, use `VBSyntaxCompilerContext` to pre-register symbols and delegates that the compiled code can call.
- **Operators come from the parse tree** — every binary operator chain (`OrElse`/`Or`/`Xor`, `AndAlso`/`And`, `=`/`<>`, relational, `&`, `<<`/`>>`, `+`/`-`, `*`/`/`/`Mod`, `^`) and the unary `Not` and `-` operators are read from the operator tokens of the parse tree, never from the source text. Chains fold left-associatively (`a - b - c` is `(a - b) - c`, `a * b / c` is `(a * b) / c`), `^` folds right-associatively (`a ^ b ^ c` is `a ^ (b ^ c)`), a parenthesized operand keeps its own operators, and an identifier or literal starting with `Not` (such as `Nothing`) is not negated.
- **Numeric promotion** — binary arithmetic (`+`, `-`, `*`, `Mod`) and comparisons (`=`, `<>`, `<`, `<=`, `>`, `>=`) promote both operands, symmetrically, to the operand type of higher rank in `Byte < Int16 < Int32 < Int64 < Single < Double` (`10 - x` with a `Double` `x` is a `Double` subtraction; the operand order does not matter). The promotion is applied to each binary operation of a chain, which stays left-associative. This is a deliberate compiler policy, not the full VB.NET widening table: promoting `Int64` (or large `Int32` values to `Single`) to a floating-point type can lose integer precision. `Byte` arithmetic between two `Byte` operands is computed and returned in `Int32`, like C# (`250 + 10` on two `Byte` values is the `Int32` `260`). Integral overflow wraps (unchecked), as for every integral operation of the compiler.
- **Division** — for the six ranked types, `/` is a real division: its result is `Double` for every pair of promoted types (`3 / 4` is `0.75`, `10 / 2` is `5.0`), with IEEE 754 results for division by zero (`Infinity`, `NaN`). Integer division `\` is not supported by the grammar. `/` is therefore not universally real: two operands of one same unranked type (see below) keep the division of that type, so `UInt32 / UInt32` is still an integral division.
- **Other numeric types** — `SByte`, `UInt16`, `UInt32`, `UInt64` and `Decimal` are not ranked: an operation on two operands of the same such type keeps its previous behavior (for example `Decimal / Decimal` and integral `UInt32 / UInt32`), and mixing such a type with any other numeric type throws `NotSupportedException` instead of converting silently; convert one operand explicitly.
- **Nullable operands** — when an operand is `Nullable<T>`, the operation is lifted to the nullable common type, whichever side it is on; nullability is never discarded and no default value is substituted. Arithmetic with a null operand produces null. Comparisons return a non-nullable `Boolean` with the lifted semantics of each operator: `=` is `True` when both operands are null and `False` when only one is; `<>` is its negation (a null operand compared with a value is `True`, two nulls are `False`); `<`, `<=`, `>`, `>=` are `False` as soon as an operand is null, including `null <= null`.
- **Unary operators and shifts** are not promoted: `-`, `Not` and `<<` / `>>` keep their operand types (the shift count must be `Int32`). Integer literals are `Int32` (a literal outside the `Int32` range is rejected), literals with a decimal point or an exponent are `Double`, and type suffixes are accepted but ignored.

[Versioned API documentation](https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.2/)
