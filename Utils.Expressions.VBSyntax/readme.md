# Utils.Expressions.VBSyntax

`Utils.Expressions.VBSyntax` provides a **VB-like** expression compiler that targets LINQ expression trees (`System.Linq.Expressions`).

The main component is `VBSyntaxExpressionCompiler` in the `Utils.Expressions.VBSyntax.Runtime` namespace.

## What it is for

- Compile textual VB-like expressions into .NET `Expression` trees.
- Execute dynamic expressions with a symbol context.
- Declare `Function` and `Sub` procedures and reuse them in the same source.

## Installation

```bash
dotnet add package omy.Utils.Expressions.VBSyntax
```

## Examples

### 1) Arithmetic one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.Compile("1 + 2 * 3");
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 7
```

### 2) Power operator

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.Compile("2.0 ^ 10");
var lambda = Expression.Lambda<Func<double>>(expression).Compile();

double result = lambda(); // 1024
```

### 3) Boolean one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
Expression expression = compiler.Compile("3 > 1 AndAlso 4 <= 4");
var lambda = Expression.Lambda<Func<bool>>(expression).Compile();

bool result = lambda(); // True
```

### 4) Compile with symbols

```csharp
using System.Linq.Expressions;
using Utils.Expressions.VBSyntax.Runtime;

var compiler = new VBSyntaxExpressionCompiler();
ParameterExpression x = Expression.Parameter(typeof(double), "x");

Expression expression = compiler.Compile("x * 2 + 1", new Dictionary<string, Expression>
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

Expression expression = compiler.Compile("pi * 2", context);
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

Expression expression = compiler.Compile("greeting & \", World!\"", context);
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

Expression expression = compiler.Compile("increment(41)", context);
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
Expression expression = compiler.Compile("New System.Text.StringBuilder()");
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
- **Keyword operator disambiguation** — logical keyword operators are extracted using regex patterns with word-boundary anchors (`\b`). This guarantees that `OrElse` is never misidentified as `Or`, and `AndAlso` is never split into `And` + `Also`, even when multiple chained operators appear in a single expression.
