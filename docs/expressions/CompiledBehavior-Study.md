# Keeping the analyzed behavior of a compiled lambda — design study

Status: study only, nothing implemented. Requested with the A-10 fix (2026-11-04); not part of Architectures B or C.

## 1. Goal and non-goals

When a `LambdaExpression` is compiled, classify its behavior once (`ExpressionBehaviorAnalysis`) and keep the result with the
delegate, so that later analyses meeting that delegate can reuse it instead of classifying an invocation of it as unknown code.

Non-goals: verifying compiled code, promoting an analysis to a `[Deterministic]` declaration, trusting a delegate because its
`Method` looks like one already analyzed, or changing any simplification before an explicit policy (Architecture B/C).

## 2. `Analyze(lambda)` versus `AnalyzeInvocation(lambda)` (point E)

`Analyze(lambda)` classifies *creating* the delegate: `Allocates`, whatever the body does. `AnalyzeInvocation(lambda)`
classifies *one call* of it: the body, its parameters local. A compiled delegate is a callable value, so the stored behavior is
the invocation behavior. The creation behavior is irrelevant once the delegate exists.

## 3. Options

### A. `CompiledExpression<TDelegate>` (recommended, public)

```csharp
public sealed class CompiledExpression<TDelegate> where TDelegate : Delegate
{
    public Expression<TDelegate> Source { get; }          // the tree actually compiled (after any finalization)
    public TDelegate Delegate { get; }
    public ExpressionBehavior Behavior { get; }           // AnalyzeInvocation(Source), computed once
    public ExpressionTrustPolicy Policy { get; }          // the analysis policy in force (verified or declarations trusted)
    public IReadOnlyList<MethodInfo> Declarations { get; } // the [Deterministic] methods the behavior relies on, if any
}

public static class ExpressionCompilation
{
    public static CompiledExpression<TDelegate> Compile<TDelegate>(Expression<TDelegate> lambda, CompiledExpressionOptions? options = null)
        where TDelegate : Delegate;
}
```

Explicit, self-describing, no global state. The caller decides where the result travels. `Declarations` makes the provenance
of the guarantees auditable (point A of the brief): `ReliesOnDeclarations` says *that* it relies on declarations; this list says
*on which*.

### B. Registry by delegate identity (recommended, internal, fed by A)

`ConditionalWeakTable<Delegate, CompiledBehaviorRecord>` filled by `ExpressionCompilation.Compile`, keyed by the delegate
**instance** (reference identity, as `ConditionalWeakTable` does), never by `Delegate.Method`. The record lives exactly as long
as the delegate and keeps `Source` alive for that time.

Why identity and not `Method`: every compiled lambda's `Method` is an anonymously hosted dynamic method. Two delegates can share
it with different closures, `Delegate.CreateDelegate` can bind it to another target, and `Delegate.Combine` builds a new
multicast instance. None of them is registered, so none inherits a behavior.

### C. Emitting annotated methods (rejected)

.NET does not offer `LambdaExpression.CompileToMethod`. A `DynamicMethod` cannot carry custom attributes. Emitting into a
collectible `AssemblyBuilder` would work only for closed lambdas (no hoisted closure), would cost an assembly per batch, and,
above all, would turn an analysis result into a `[Deterministic]` *declaration*, which the brief forbids. Rejected.

## 4. Validity (point D)

The record describes the code, not the values:

- **Captured mutable state.** Closures read hoisted variables and captured objects. The analysis already reports these reads
  as `ReadsMutableState` (and writes to captured locals as `SideEffects`). The record stays valid when those values change:
  determinism is "same state, same outcome", and reproducibility is not claimed.
- **Argument state.** Reference-typed arguments may change between calls; a declared method receiving one already reports
  `ReadsMutableState`.
- **Tree changes.** Expression trees are immutable; the record holds the exact `Source` that was compiled. A modified tree is a
  new tree, compiled into a new delegate with its own record.
- **Rebuilt delegates.** A delegate rebuilt by `CreateDelegate`, `Combine` or a second `Compile` is a new instance: no record,
  unknown code.
- **Metadata.** Reflection metadata and attributes of runtime methods do not change at run time, except under hot reload
  (`MetadataUpdateHandler`), which should clear the registry. Custom reflection objects are unknown code (A-10), so they never
  produce a stable trusted record.
- **Policy.** The record stores the policy it was computed under. A consumer with a stricter policy widens a
  declaration-based record to unknown code (`ExpressionBehaviorPermissions.Effective`), never the reverse.
- **No promotion.** An `Unknown` part stays unknown; a declaration-based part keeps `ReliesOnDeclaration`. Nothing is ever
  converted into `[Deterministic]`.

## 5. Future consumers (point F)

- **`ExpressionBehaviorAnalysis`.** On `Invoke(Constant(d), args)` and `Call(Constant(d), d.GetType().GetMethod("Invoke"), args)`
  with `d` registered: behavior = record ∪ arguments, instead of unknown code. Any other delegate stays unknown.
- **`ExpressionSimplifier`** (Architecture C only, behind an explicit option): such an invocation could become discardable or
  mergeable under the permissions of `ExpressionBehaviorPermissions`, which are local (no side effect may run between the
  merged occurrences).
- **`InvocationInliningAnalysis`.** Inlining `Invoke(Constant(d), args)` by substituting `Source` is valid only for a *closed*
  lambda (no hoisted variable, no quote, no free parameter): otherwise the compiled closure binds variables the source tree would
  rebind to the enclosing scope. The record would carry an `IsClosed` flag; only then may inlining be considered.
- **`ExpressionComparer`.** No change. Delegates in constants compare by reference; equal records never make two delegates
  equal.
- **Architecture B/C.** The record is one more source of verified behavior. It lets annotated or analyzed helpers be simplified
  without trusting unrelated user code.

## 6. Cost estimate

From the P18-A measurements: about 40 ns per analysis plus 12–25 ns per node, so about 25 µs for a 1,000-node lambda.
`LambdaExpression.Compile` costs tens of microseconds to milliseconds for such trees, so the analysis adds roughly 5–20 % to a
compilation and nothing to an invocation. A `ConditionalWeakTable` insertion costs about 100 ns and a lookup 20–50 ns. Memory:
one record per delegate (about 64 bytes plus the declaration list); it keeps the source tree alive as long as the delegate,
which matters for callers that drop the tree after compiling. Option A without B costs nothing beyond the analysis.

## 7. Recommendation

Implement A as the public entry point, with B as an internal registry fed only by A. Consult B only from
`ExpressionBehaviorAnalysis`, and only for invocations of a registered delegate instance. Leave the simplifier, the comparer and
the inlining analysis unchanged until Architecture B/C defines the policies. Reject C.
