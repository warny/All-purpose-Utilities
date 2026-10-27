# AGENTS Guidelines — `Utils` project

This file applies to the `Utils` project (everything under `Utils/`). Every rule of the repository-level
[`AGENTS.md`](../AGENTS.md) applies here unchanged, **except** the local exception below. Where the two files differ,
this file takes precedence for `Utils/`, and only on the point it explicitly states.

## Local exception — node-type dispatch `switch` statements

The repository rule says that large `switch` statements (more than **10 cases** or **30 lines**) must be replaced by a
`Dictionary<case, method>` or `Dictionary<case, class>`.

In `Utils`, that rule does **not** apply to a `switch` (statement or expression) whose only purpose is to dispatch on:

- an `ExpressionType` value;
- an expression node class (`BinaryExpression`, `MethodCallExpression`, `LambdaExpression`, …), including type patterns;
- a structural category of expression nodes (for example the node families of `ExpressionTransformer`,
  `ExpressionComparer`, `ExpressionCanonicalOrder` or the simplifier memo keys).

Such a dispatch `switch` may have more than ten cases, provided that it stays a dispatch table:

```csharp
ExpressionType.Add => PrepareBinary(expression),
ExpressionType.Call => PrepareMethodCall(expression),
ExpressionType.Lambda => PrepareLambda(expression),
```

### Criteria

- Every branch is a short expression, a direct return or a call to a specialized method.
- No significant logic is written inside the `switch`; complex branches are extracted into named methods.
- The whole `switch` stays easy to read and to maintain.
- When the order of the cases matters (type patterns over a class hierarchy, a default case), the dispatch order stays
  deterministic and is documented.
- The exception does **not** cover large `switch` statements unrelated to expression node types; those still follow the
  repository rule.

A dictionary of handlers remains an acceptable design when measurements or architectural reasons justify it (for
example extensibility, or a dispatch shown to be faster), but it is not mandatory for node-type dispatch.
