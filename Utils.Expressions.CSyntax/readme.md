# Utils.Expressions.CSyntax

`Utils.Expressions.CSyntax` provides a **C-like** expression compiler that targets LINQ expression trees (`System.Linq.Expressions`).

The main component is `CSyntaxExpressionCompiler` in the `Utils.Expressions.CSyntax.Runtime` namespace.

## What it is for

- Compile textual expressions into .NET `Expression` trees.
- Execute dynamic expressions with a symbol context.
- Declare functions and reuse them in the same source.

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
var context = new CSyntaxCompilerContext();

compiler.CompileSource(
    """
    public double add(double a, double b) { a + b; }
    public double twice(double x) { add(x, x); }
    """,
    context);

var twice = (Func<double, double>)context.Get("twice");
double result = twice(5); // 10
```

### 6) Lambda in context + invocation

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new CSyntaxCompilerContext();
context.Set("increment", (Func<double, double>)(x => x + 1));

Expression expression = compiler.Compile("increment(41)", context);
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 42
```

### 7) Standard control flow: `if / else`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new CSyntaxCompilerContext();

compiler.CompileSource(
    """
    public double abs(double x)
    {
        if (x >= 0) x
        else -x
    }
    """,
    context);

var abs = (Func<double, double>)context.Get("abs");
double a = abs(3);   // 3
double b = abs(-3);  // 3
```

### 8) Standard control flow: `for`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new CSyntaxCompilerContext();

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

var sumTo = (Func<int, double>)context.Get("sumTo");
double result = sumTo(4); // 10
```

### 9) Standard control flow: `foreach`

```csharp
using Utils.Expressions.CSyntax.Runtime;

var compiler = new CSyntaxExpressionCompiler();
var context = new CSyntaxCompilerContext();
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

var sumValues = (Func<int>)context.Get("sumValues");
int result = sumValues(); // 10
```

## Notes

- The compiler accepts C-like syntax (arithmetic operations, blocks, `if`, `for`, `foreach`, functions, etc.).
- The final expression type depends on context and generated LINQ conversions.
- For advanced scenarios, use `CSyntaxCompilerContext` to register symbols and functions.
