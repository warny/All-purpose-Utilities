# Utils.Expressions.CLike

`Utils.Expressions.CLike` fournit un compilateur d'expressions **C-like** vers des arbres LINQ (`System.Linq.Expressions`).

Le composant principal est `CStyleExpressionCompiler` dans le namespace `Utils.Expressions.CLike.Runtime`.

## À quoi ça sert

- Compiler des expressions textuelles vers des `Expression` .NET.
- Exécuter des expressions dynamiques avec un contexte de symboles.
- Déclarer des fonctions et les réutiliser dans le même source.

## Installation

```bash
dotnet add package omy.Utils.Expressions.CLike
```

## Exemples

### 1) One-liner arithmétique

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
Expression expression = compiler.Compile("1 + 2 * 3");
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 7
```

### 2) One-liner booléen

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
Expression expression = compiler.Compile("3 > 1 && 4 <= 4");
var lambda = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();

bool result = lambda(); // true
```

### 3) Compiler avec des symboles

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
ParameterExpression x = Expression.Parameter(typeof(double), "x");

Expression expression = compiler.Compile("x * 2 + 1", new Dictionary<string, Expression>
{
    ["x"] = x,
});

var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

double result = lambda(4); // 9
```

### 4) Compiler une lambda typée (one-liner)

```csharp
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
Expression<Func<int, int>> expression = compiler.Compile<Func<int, int>>("(x) => x + 1");
Func<int, int> function = expression.Compile();

int result = function(41); // 42
```

### 5) Fonctions déclarées dans le source

```csharp
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
var context = new CStyleCompilerContext();

compiler.CompileSource(
    """
    public double add(double a, double b) { a + b; }
    public double twice(double x) { add(x, x); }
    """,
    context);

var twice = (Func<double, double>)context.Get("twice");
double result = twice(5); // 10
```

### 6) Lambda dans le contexte + appel

```csharp
using System.Linq.Expressions;
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
var context = new CStyleCompilerContext();
context.Set("increment", (Func<double, double>)(x => x + 1));

Expression expression = compiler.Compile("increment(41)", context);
var lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

double result = lambda(); // 42
```

### 7) Structures standard: `if / else`

```csharp
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
var context = new CStyleCompilerContext();

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

### 8) Structures standard: `for`

```csharp
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
var context = new CStyleCompilerContext();

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

### 9) Structures standard: `foreach`

```csharp
using Utils.Expressions.CLike.Runtime;

var compiler = new CStyleExpressionCompiler();
var context = new CStyleCompilerContext();
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

- Le compilateur accepte une syntaxe C-like (opérations arithmétiques, blocs, `if`, `for`, `foreach`, fonctions, etc.).
- Le type final dépend du contexte et des conversions LINQ générées.
- Pour les scénarios avancés, utiliser `CStyleCompilerContext` pour enregistrer symboles et fonctions.
