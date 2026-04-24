# Utils.Expressions.CSyntax

`Utils.Expressions.CSyntax` provides a **C-like** expression compiler that targets LINQ expression trees (`System.Linq.Expressions`).

The main component is `CSyntaxExpressionCompiler` in the `Utils.Expressions.CSyntax.Runtime` namespace.

## What it is for

- Compile textual expressions into .NET `Expression` trees.
- Execute dynamic expressions with a symbol context.
- Declare functions and reuse them in the same source.
- Share one context instance across multiple expression compilers.
- Persist runtime symbols (values and static callables) to/from streams.

## Installation

```bash
dotnet add package omy.Utils.Expressions.CSyntax
```

## Examples

### 1) Arithmetic one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
Expression expression = compiler.Compile("1 + 2 * 3");
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 7
```

### 2) Boolean one-liner

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
Expression expression = compiler.Compile("3 > 1 && 4 <= 4");
var lambda = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();

bool result = lambda(); // true
```

### 3) Compile with symbols

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
ParameterExpression x = Expression.Parameter(typeof(double), "x");

Expression expression = compiler.Compile("x * 2 + 1", new Dictionary<string, Expression>
{
    ["x"] = x,
});

var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

double result = lambda(4); // 9
```

### 4) Compile a strongly typed lambda (one-liner)

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
Expression<Func<int, int>> expression = compiler.Compile<Func<int, int>>("(x) => x + 1");
Func<int, int> function = expression.Compile();

int result = function(41); // 42
```

### 5) Functions declared in source

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();

compiler.CompileSource(
    """
    public double add(double a, double b) { a + b; }
    public double twice(double x) { add(x, x); }
    """,
    context);

if (!context.TryGet("twice", out object? twiceSymbol) || twiceSymbol is not Func<double, double> twice)
{
    throw new InvalidOperationException("Unable to resolve function 'twice'.");
}

double result = twice(5d); // 10
```

### 6) Lambda in context + invocation

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();
context.Set("increment", (Func<double, double>)(x => x + 1));

Expression expression = compiler.Compile("increment(41)", context);
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 42
```

### 7) Standard control flow: `if / else`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();

compiler.CompileSource(
    """
    public double abs(double x)
    {
        if (x >= 0) x
        else -x
    }
    """,
    context);

if (!context.TryGet("abs", out object? absSymbol) || absSymbol is not Func<double, double> abs)
{
    throw new InvalidOperationException("Unable to resolve function 'abs'.");
}

double a = abs(3);   // 3
double b = abs(-3);  // 3
```

### 8) Standard control flow: `for`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();

compiler.CompileSource(
    """
    public double sumTo(int n)
    {
        int i = 0;
        double sum = 0;
        for (i = 1; i <= n; i = i + 1) sum = sum + i;
        sum
    }
    """,
    context);

if (!context.TryGet("sumTo", out object? sumToSymbol) || sumToSymbol is not Func<int, double> sumTo)
{
    throw new InvalidOperationException("Unable to resolve function 'sumTo'.");
}

double result = sumTo(4); // 10
```

### 9) Standard control flow: `foreach`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();
context.Set("values", new[] { 1, 2, 3, 4 });

compiler.CompileSource(
    """
    public int sumValues()
    {
        int sum = 0;
        foreach (int item in values) sum = sum + item;
        sum
    }
    """,
    context);

if (!context.TryGet("sumValues", out object? sumValuesSymbol) || sumValuesSymbol is not Func<int> sumValues)
{
    throw new InvalidOperationException("Unable to resolve function 'sumValues'.");
}

int result = sumValues(); // 10
```

### 10) Persist and restore a shared context

```csharp
using Utils.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new ExpressionCompilerContext();
context.Set("add", (Func<int, int, int>)((a, b) => a + b));
compiler.CompileSource("public int twice(int x) { add(x, x); }", context);

using MemoryStream stream = new();
context.WriteToStream(stream);
stream.Position = 0;

ExpressionCompilerContext restored = ExpressionCompilerContext.ReadFromStream(stream);
Expression expression = compiler.Compile("twice(21)", restored);
int result = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile()(); // 42
```

## Notes

- The compiler accepts C-like syntax (arithmetic operations, blocks, `if`, `for`, `foreach`, functions, etc.).
- The final expression type depends on context and generated LINQ conversions.
- For advanced scenarios, use `ExpressionCompilerContext` to register symbols, overloaded callables, and persisted runtime values.
