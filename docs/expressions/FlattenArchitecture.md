# Flatten rules of `ExpressionTransformer`

Status: infrastructure (PR A). No shipped transformer declares a flatten rule: `ExpressionSimplifier`,
`ExpressionDerivation<T>` and `ExpressionIntegration<T>` are unchanged.

## 1. Motivation

`System.Linq.Expressions` represents `a + (b + (c - (d + e)))` as nested binary nodes. `ExpressionTransformer` processes
each of them separately: a recursive descent into the operands, the rules of the node, structural comparisons, rebuilds and,
for the simplifier, a canonicalization per level. On long additive or multiplicative chains this costs a stack frame set
per level and, in the simplifier, repeated work on the same sub-chains.

A *flatten rule* asks for the whole chain at once, as an ordered array of operands, before the chain is traversed. The
mechanism is opt-in per transformer type, uses the existing rule plan (no second rule engine), and is available to any
transformer, including user-defined ones.

## 2. `FlattenAttribute`

```csharp
[Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
protected Expression? Additions(FlattenedTerm[] terms) { … }

[Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
protected Expression? Multiplications(Expression source, FlattenedTerm[] terms) { … }
```

- The declared operators are the ones the chain may **cross**. Two families exist: additive (`Add`, `Subtract`, `Negate`)
  and multiplicative (`Multiply`, `Divide`). A rule declares operators of one family, possibly a subset (`[Flatten(Add)]`:
  a subtraction is then an operand).
- Two signatures: `(FlattenedTerm[] terms)` and `(Expression source, FlattenedTerm[] terms)`; the return type is
  assignable to `Expression`. `source` is the original, untransformed root of the chain.
- A non-`null` result is the result of the root node. `null` declines (§13).
- The attribute is `sealed`, inherited by overrides, and is a request for a representation, **not a permission**: it never
  allows a rule to drop, repeat, merge or move an evaluation.

Everything is validated once, when the dispatch plan of the transformer type is built (its first construction). An invalid
rule makes the constructor throw `InvalidOperationException` naming the rule: no operator, an operator outside the two
families, both families in one rule, a static or generic method, a wrong parameter list or return type, a method that also
carries an `ExpressionSignatureAttribute`, a parameter carrying one, or two rules whose operator sets overlap without being
equal.

## 3. `FlattenedTerm`

```csharp
public readonly record struct FlattenedTerm(ExpressionType Operation, Expression Operand, int SourceIndex);
```

- `Operand` is the **raw** operand: the very node of the source tree, not prepared and not transformed. A shared node
  appears once per occurrence.
- `Operation` is `Add`/`Subtract` (additive chain) or `Multiply`/`Divide` (multiplicative chain), after the signs and
  inversions of the crossed operators are propagated. The first operand has an explicit operation too.
- `SourceIndex` is the position of the operand in the source, left to right, from 0.

Each rule receives its own copy of the array.

## 4. Discovery and indexing

`BuildPlan` scans the instance methods of the transformer type once, as before. A method carrying `FlattenAttribute` becomes
a rule of kind `InvocationKind.Flattened` in the same `TransformPlan`; the methods carrying an
`ExpressionSignatureAttribute` are collected in the same order as before, into the same per-node-type buckets.

`FlattenAttribute` does **not** derive from `ExpressionSignatureAttribute`. A derived attribute would be read by
`GetCustomAttributes<ExpressionSignatureAttribute>`, would not be one of the four known-safe attribute types and would
therefore become a candidate of every node-type bucket, with a single `ExpressionType` and a `Match` meaningless for a
chain; the trace identity, the audited-rule cache and the bucket index all assume one operator per rule. A separate rule
kind in the same plan leaves all of that untouched.

Flatten rules are grouped by operator set. Rules that share an operator must declare exactly the same set, so every
operator belongs to at most one group, a chain has one definition per transformer, and it is extracted once whatever the
number of rules that receive it. The plan indexes the group by operator (five slots). Nothing reads an attribute after the
plan is built.

A transformer without flatten rules pays one boolean field read per `TransformCore` call (§19).

## 5. Priority and order of invocation

For a transformer that declares flatten rules, `TransformCore(e)` proceeds as follows.

1. `e` is a **chain root** when it is a framework `BinaryExpression`/`UnaryExpression` whose operator a group declares,
   that the transformer accepts to cross (§10), and that is not a node of a chain being processed after a refusal (§13).
   Otherwise the node is processed historically.
2. The effect gate (`AllowsRules`, an engine-internal hook, always open for a user transformer) is asked on the raw root. A
   refusal leaves the node to the historical path.
3. The chain is extracted (§6) and given to the rules of its group **in declaration order** (the order of
   `Type.GetMethods`, as for signature rules).
4. The first non-`null` result is the result of `e`: no signature rule and no `FinalizeExpression` runs on the chain.
5. If every rule declines, the chain is processed historically (§13).

So the flatten rules of a chain run **before** every signature rule of every node of the chain, and before any operand is
prepared. This order is forced by the purpose (signature rules need prepared children) and concerns only transformers that
opted in.

A chain is rooted at the first of its nodes reached by `TransformCore`. A rule that accepts a chain decides what it
transforms next; a nested chain (inside a call, a lambda, an operator that is not crossed) is a chain of its own, offered
when the traversal reaches it.

## 6. Extraction

`FlattenedChain.Extract` walks the raw tree from the root with an explicit work stack (pre-order, left operand first): no
recursion on the chain, whatever its depth or shape. A node of a declared operator that the transformer accepts to cross is
recorded as crossed and its operands are pushed; any other node becomes a term. The walk reads only framework operator
nodes: an operand is never inspected.

## 7. Complexity

For a chain of `n` operands made of binary operators (`2n − 1` nodes):

| | Cost |
|---|---|
| Extraction | `2n − 1` nodes examined, each once; O(n) time and memory |
| Structural budget | `2n − 1` visits |
| Transformation budget | 1 visit for the chain |
| Stack | constant |
| Faithful reconstruction (§12) | O(n), iterative; one more extraction |
| Fallback after a refusal (§13) | the historical traversal, plus a constant per crossed node (the positional link) |

These bounds cover the infrastructure only. A rule that sorts, compares or re-transforms its terms adds its own cost, and a
declined chain keeps the historical cost, including its recursion depth.

## 8. Additive semantics

A `Subtract` inverts the sign of its right operand; a `Negate` inverts the sign of its operand; signs compose.

| Source | Terms |
|---|---|
| `a + (b - (c + d))` | `+a +b -c -d` |
| `a - (b - c)` | `+a -b +c` |
| `-(a + b - c)` | `-a -b +c` |
| `-(-a)` | `+a` |

## 9. Multiplicative semantics

A `Divide` inverts its right operand.

| Source | Terms |
|---|---|
| `a / (b / c)` | `*a /b *c` |
| `(a / b) * c` | `*a /b *c` |
| `(a * b) / (c * d)` | `*a *b /c /d` |

This is a **symbolic** view. It does not state that another grouping computes the same value: integer division truncates,
floating-point rounding and overflow depend on the association, and intermediate results can become infinite or zero. The
two first rows have the same terms and different results in general. Extraction and the permission to regroup are
separate: a rule that only inspects the chain, or that transforms the operands and keeps the grouping (§12), needs no
proof; a rule that reassociates must establish validity for the operand types itself.

## 10. Boundaries

The chain crosses a node only when all of the following hold; otherwise the node is an operand, kept intact.

- It is a framework `BinaryExpression` or `UnaryExpression` (type tests only; an extension node is never read).
- Its node type is declared by the group. `AddChecked`, `SubtractChecked`, `NegateChecked`, `MultiplyChecked`, `Power` and
  every other operator have their own node types and cannot be declared.
- `CanFlattenThrough(node)` accepts it. The default accepts only an ordinary intrinsic operator: no operator method (so no
  user-defined operator and no `decimal`, whose operators are methods) and a result type that is, by reference, one of
  `double`, `float`, `int`, `uint`, `long`, `ulong`, `short`, `ushort` (so no lifted/nullable operator and no user `Type`
  object).

| Node inside a chain | Default behavior |
|---|---|
| user-defined operator, `decimal` operator | operand |
| lifted (nullable) operator | operand |
| checked operator, `Power`, conversion | operand |
| method call, invocation, member access, conditional | operand |
| lambda, quote, block, try | operand (its content is reached only by the historical traversal, which opens the scope) |
| extension node | operand, never read |
| integer division | crossed (the terms are symbolic, §9) |
| shared sub-tree | crossed or operand like any node, once per occurrence |

`CanFlattenThrough` is `internal virtual`: only a transformer of the `Utils` assembly can widen it (the simplifier may reuse
its `IsOrdinaryBinaryArithmetic`/`IsOrdinaryUnaryNegate` classification in PR B). It grants the structural crossing only,
never a permission to reassociate. There is no public extension point at this stage.

## 11. Provenance

The engine keeps, for each extracted chain, the root, every crossed node (with its node type, operator method, result type
and lifting, which are the node's own) and their pre-order positions, hence the original grouping. This structure
(`FlattenedChain`) is internal; the public surface is `FlattenedTerm[]` and the source node.

## 12. Faithful reconstruction

```csharp
protected Expression RebuildFlattenedChain(Expression source, IReadOnlyList<Expression> operands)
```

Rebuilds the chain rooted at `source` with one new operand per term, in `SourceIndex` order: the same operator nodes
(`MakeBinary`/`MakeUnary` with the original method, lifting and type) and the **same parentheses**. With the original
operands it returns `source` itself. It is iterative and re-extracts the chain (counted, §15). A rule that transforms each
operand and keeps the association, for example a term-wise derivation, uses it instead of rebuilding from the signs, which
would lose the distinction between `(a / b) * c` and `a / (b / c)`.

`source` must be the root of a chain of the transformer (`ArgumentException` otherwise, and for a wrong operand count); an
operand whose type does not fit its operator fails in the expression factory.

## 13. Declining (`null`)

When every rule of the group returns `null`, the transformer processes the root exactly as it does without flatten rules:
`PrepareExpression` on the children, the signature rules, `FinalizeExpression`, in the historical order, with the historical
recursion. The fallback does not extract the chain again from its own nodes, and it identifies them **by position, not by
reference**: when a node of the declined chain prepares a child that the chain also crosses, the engine leaves a link (per
thread, naming the transformer and the child) that the first `TransformCore` call on that child, made during its
`PrepareExpression`, consumes; that call continues the declined chain. The link is restored in a `finally`. No table of
nodes is kept.

Consequences:

- **Nothing is prepared twice.** The engine prepares no operand before or during the flatten rules, so the fallback is the
  first and only preparation. Exceptions and user-code calls of the preparation are not moved.
- **The rules run once per chain**, not once per level.
- `PrepareExpression` keeps its meaning for every transformer: it is not replaced by a transformation of the inner nodes
  (in `ExpressionDerivation`, `PrepareExpression` does not differentiate).
- The depth gain exists only for a chain that a rule accepts.
- A chain inside an operand of the declined chain is still offered when the traversal reaches it, **including another
  occurrence of a node object the declined chain crosses**: in `s + Abs(s)` with `s = a + b`, the `s` under the call is a
  chain of its own, exactly as a structurally equal but distinct node would be. The result never depends on whether a
  sub-tree is shared. (A node object crossed twice by the same chain, `s + s`, is part of that chain at both positions.)
- A `PrepareExpression` override that does not call `TransformCore` on the child it receives simply leaves the link unused.
- Invoking a flatten rule is opt-in behavior in itself: a rule with side effects has them even when it declines.

A rule that accepts is responsible for its operands (it calls `TransformCore` on those it wants transformed, in the order it
chooses). A rule must not call `TransformCore(source)`: the root would be offered to it again.

## 14. Preparation of the operands

None by the infrastructure (decision recorded for this PR): terms are raw. A memoized preparation of the leaves was
rejected because it would add state and could change the order of transformations.

## 15. Budgets and stack guards

- Extraction: one structural visit per node examined (`ConsumeStructuralVisit`, which also checks the stack).
- A chain given to its rules: one transformation visit, counted before the first rule.
- `RebuildFlattenedChain`: one more extraction. Called outside an operation, it starts one with the transformer's limits.
- The fallback counts its historical visits; a declined chain therefore costs the historical traversal plus one
  transformation visit and `2n − 1` structural visits, once.
- `MaxDepth` and `MaxNodes` are validated when the operation starts (`ExpressionShapeValidator`, before any rule): a chain
  deeper than the limit is still refused, even if a flatten rule would have accepted it without recursion. Two different
  quantities must not be confused: the **depth the budget accepts** (`MaxDepth`, 512 by default) is unchanged by flatten
  rules; what an accepted chain reduces is the **execution stack actually used** by the transformation. Flatten rules do not
  raise the first; they only make the second independent of the chain's depth.
- `ExpressionResourceLimitException` and `InsufficientExecutionStackException` are never caught, never wrapped when they
  cross a flatten rule, and never turned into a result.

The budgets of an opt-in transformer are therefore **not** identical to those of the same transformer without flatten rules:
it can reach a limit earlier. A transformer without flatten rules has identical counters.

## 16. Tracing

Inside a tracing scope, for a transformer that declares flatten rules:

| Event | Level | Meaning |
|---|---|---|
| `ChainFlattened` (new kind) | Detailed | the chain rooted at `Input` was extracted |
| `RuleCandidate` | Detailed | a flatten rule is about to receive the chain |
| `RuleApplied` | Standard | the rule returned a result; the treatment's exit reports `RuleApplied` |
| `RuleRejected` (`Declined`) | Detailed | the rule returned `null` |
| `RuleRejected` (`EffectGate`) | Detailed | the effect gate refused the root |
| `TransformFailed` | Standard | an exception left the treatment; `Rule` is the flatten rule that was running |

`ExpressionTransformRuleInfo.FlattenedNodeTypes` lists the operators of a flatten rule (`DeclaredNodeType` is the first
one); flatten rules are numbered separately in `DeclarationIndex`. Tracing adds no call to user code and changes no
decision. No event changes for a transformer without flatten rules.

## 17. Security

- Extraction runs no user code: only framework operator nodes are read (`NodeType`, `Method` compared with `null`, `Type`
  compared by reference). Extension nodes (ETC R4) and custom reflection objects (A10-B) are operands that are never
  inspected; an operator carrying a user `MethodInfo` is never crossed.
- A chain never crosses a lexical scope (lambda, conversion lambda, block, catch) or a quote.
- Operator methods, `Type` objects and operand references are preserved by the reconstruction.
- No global mutable state: the plan is immutable per transformer type; the fallback link is one per-thread value, restored
  in `finally`.

## 18. Compatibility

- A transformer type without flatten rules is unchanged: same rules, order, exceptions, preparations, counters and traces.
- A plan belongs to a concrete type. A subclass inherits the flatten rules of its base; a subclass that adds one opts in for
  itself only.
- `ExpressionSimplifier`, `ExpressionDerivation<T>` and `ExpressionIntegration<T>` declare none.
- New public API: `FlattenAttribute`, `FlattenedTerm`, `ExpressionTransformer.RebuildFlattenedChain`,
  `ExpressionTransformRuleInfo.FlattenedNodeTypes`, `ExpressionTransformEventKind.ChainFlattened`.

## 19. Measurements

See §19.1–19.3 (filled from the benchmark campaign of the PR).

## 20. Future uses (not part of PR A)

- **Simplifier (PR B).** One additive rule that reproduces the existing regrouping (`CollectAdditiveTerms`,
  `AggregateAdditiveConstants`, `CanonicalizeAdditiveExpression`), with `CanFlattenThrough` overridden from the simplifier's
  ordinary-arithmetic classification, under exact compatibility of the default mode. The prepared-operand entry
  (`TransformCoreWithPreparedOperands`) does not go through the flatten path and must be reviewed there.
- **Derivation.** Term-wise derivation of a sum with `RebuildFlattenedChain`; n-ary product rule with prefix and suffix
  products (sharing `Expression` objects does not share evaluations).
- **Integration.** Term-wise integration of sums; extraction of the factors that are constant with respect to the
  integration parameter. No general product rule.
- C3 on the flattened representation, advanced regrouping and automatic factoring are out of scope until the architecture
  is validated.
