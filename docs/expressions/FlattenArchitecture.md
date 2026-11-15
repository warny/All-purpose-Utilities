# Flatten rules of `ExpressionTransformer`

Status: infrastructure (PR A), used since PR B by the built-in `ExpressionSimplifier` for its linear additive chains (see
[SimplifierFlatten.md](SimplifierFlatten.md)). `ExpressionDerivation<T>` and `ExpressionIntegration<T>` declare no flatten
rule. The measurements of §19 are those of PR A, taken when no shipped transformer declared one.

## 1. Motivation

`System.Linq.Expressions` represents `a + (b + (c - (d + e)))` as nested binary nodes. `ExpressionTransformer` processes
each of them separately: a recursive descent into the operands, the rules of the node, structural comparisons, rebuilds and,
for the simplifier, a canonicalization per level. On long additive or multiplicative chains this costs a stack frame set
per level and, in the simplifier, repeated work on the same sub-chains.

A *flatten rule* asks for the whole chain at once, as an ordered array of operands, before the chain is traversed. The
mechanism is opt-in per transformer type, uses the existing rule plan (no second rule engine), and is available to any
transformer, including user-defined ones.

## 2. Declarations

```csharp
// Simplified forms: one couple, at most one unary operator, bound to the couple (FirstGroup).
[Flatten(ExpressionType.Add, ExpressionType.Subtract, ExpressionType.Negate)]
protected Expression? Additions(FlattenedTerm[] terms) { … }

[Flatten(ExpressionType.Multiply, ExpressionType.Divide)]
protected Expression? Multiplications(Expression source, FlattenedTerm[] terms) { … }

// Advanced form: couples associated by index, unary operators declared one by one with their binding.
[Flatten([ExpressionType.Add, ExpressionType.Multiply], [ExpressionType.Subtract, ExpressionType.Divide])]
[FlattenUnary(ExpressionType.Negate, UnaryOperatorBinding.OperandGroup)]
protected Expression? Both(FlattenedTerm[] terms) { … }
```

### 2.1 The model

The engine knows no operator. A declaration states, for a chain, a **context with two values** (initial or inverted) and how
each declared operator passes it to its operands:

| Role | Operands | Context passed |
|---|---|---|
| direct binary operation | left, right | unchanged, unchanged |
| inverse binary operation | left, right | unchanged, **inverted** |
| unary operator | operand | **inverted** |

Two inversions cancel out. Every operand of the chain becomes a term whose operation is the direct operation of the couple
when the context reaching it is the initial one, its inverse operation otherwise. Nothing else is applied: no
associativity, commutativity, neutral element or reordering is assumed (part A, the structural extraction); what a
declaration means mathematically, and whether a rewriting is valid, belongs to the rules that receive the chain (part B).

Constructors of `FlattenAttribute`:

| Constructor | Declares |
|---|---|
| `Flatten(direct)` | one couple without inverse: every term carries `direct` |
| `Flatten(direct, inverse)` | one couple |
| `Flatten(direct, inverse, unaryOperator)` | one couple and one unary operator bound with `FirstGroup` |
| `Flatten(direct[], inverse[])` | the couples `(direct[i], inverse[i])`; `inverse` may be empty (direct operations alone) |

`FlattenUnaryAttribute(operation, binding)` is repeatable and completes any form; `[Flatten(Add, Subtract)]` with
`[FlattenUnary(Negate, FirstGroup)]` is the same declaration as `[Flatten(Add, Subtract, Negate)]`. The former `params`
constructor (`Flatten(params ExpressionType[])`, never released) is removed: kept beside the new ones, `[Flatten(Subtract,
Add)]` would have silently bound to the two-argument overload and changed meaning.

### 2.2 Couples and unary bindings

The couples of a declaration are **independent chains**: their terms are never mixed, and a node of one couple is an operand
of a chain of another, offered as a chain of its own when the traversal reaches it. Each unary operator has its own binding
(`UnaryOperatorBinding`):

- **`FirstGroup`**: the operator belongs to the first couple of the declaration. It roots a chain of that couple whatever its
  operand (`-x` gives `[Subtract x]` with `[Flatten(Add, Subtract, Negate)]`, the historical result), is crossed by that
  couple's chains only, and is an operand of the chains of the other couples.
- **`OperandGroup`**: the operator belongs to the couple its **operand alone** identifies: a crossed binary operator of a
  couple, a `FirstGroup` operator (its couple), or another `OperandGroup` operator whose own operand is decided the same way.
  An operand that identifies no couple (a leaf, a call, an undeclared operator) attaches the operator to none: it roots no
  chain (historical behavior) and is an operand of any chain that reaches it.

With `[Flatten([Add, Multiply], [Subtract, Divide])]` and `[FlattenUnary(Negate, OperandGroup)]`:

| Source | Chains offered |
|---|---|
| `-(a * b)` | `/a /b` |
| `-(a + b)` | `-a -b` |
| `a * -(b / c)` | `*a /b *c` |
| `a * -x` | `*a *(-x)` (not `*a /x`) |
| `a * -(b + c)` | `*a *(-(b + c))`, then `-b -c` (never merged) |
| `-x`, `-(-x)` | none |

The group of a node is a function of its subtree and of the declarations only, so the dispatch is deterministic; the walk
down a prefix of `OperandGroup` operators is iterative and counted (§15).

### 2.3 Validation

Everything is validated once, when the dispatch plan of the transformer type is built (its first construction). An invalid
rule makes the constructor throw `InvalidOperationException` naming the rule:

- no couple; direct and inverse arrays of different lengths;
- a direct or inverse operation that is not a node type of `BinaryExpression`, a unary operator that is not a node type of
  `UnaryExpression`, an unknown binding, a unary operator without an inverse operation;
- an operator declared twice by one rule (in two couples, or by the simplified constructor and by `FlattenUnary`);
- two rules sharing an operator of a couple without declaring the same couple, with the same unary operators and bindings;
- a dispatch that would be ambiguous: a unary operator bound with `FirstGroup` to one couple and declared for any other
  couple (with either binding). A unary operator bound with `OperandGroup` may be shared by several couples and
  configurations, since its operand attaches each node to one couple: an operator does not intrinsically belong to one
  family;
- a `FlattenUnaryAttribute` on a method declaration that carries no `FlattenAttribute`, a static or generic method, a wrong
  parameter list or return type, a method that also carries an `ExpressionSignatureAttribute`, a parameter carrying one.

The shapes accepted are exactly those of the framework nodes (binary or unary); the developer is responsible for the
algebraic meaning of what they declare.

**Inheritance.** `FlattenAttribute` is inherited by overrides. The `FlattenUnaryAttribute`s belong to the method declaration
that carries the effective (most derived) `FlattenAttribute`: an override that declares nothing keeps the whole
configuration of its base, an override that declares a new `FlattenAttribute` replaces it entirely (the unary operators of
the overridden declaration are never merged), and an override that declares only `FlattenUnaryAttribute`s is rejected.
Reflection exposes only the base definition of an override, so the override levels are found by walking the base types,
once, when the plan is built.

### 2.4 Knowledge removed from the engine

| Before | Now |
|---|---|
| `FlattenFamily`, `FamilyOf`, `FlattenGroup.IsAdditive` | none: a group is a declared couple |
| `FlattenOperatorCount`, `FlattenOperatorIndex` (five fixed slots) | an index by node type value, built from the declarations |
| validation of the two families (`Add`/`Subtract`/`Negate`, `Multiply`/`Divide`) | validation of the node shapes (binary/unary) only |
| `Extract`: `Subtract`/`Divide` invert their right operand, `Negate` inverts | each crossed node applies its descriptor (arity, inverted operands) |
| term operations `Add`/`Subtract`/`Multiply`/`Divide` chosen by family | the direct or inverse operation of the couple |
| `IsCrossedByChain`, the root test and `RebuildFlattenedChainCore` by family | one function, the group of a node (§2.2) |

What remains in the engine is the table of the framework node shapes (which node types are those of `BinaryExpression` or
`UnaryExpression`, used to validate the declarations), and `CanFlattenThrough` (§10), a structural condition that reads no
node type. The built-in simplifier keeps its own `Add`/`Subtract` checks: they are business rules of part B.

### 2.5 Rules

- Two signatures: `(FlattenedTerm[] terms)` and `(Expression source, FlattenedTerm[] terms)`; the return type is
  assignable to `Expression`. `source` is the original, untransformed root of the chain.
- A non-`null` result is the result of the root node. `null` declines (§13).
- The attributes are `sealed` and are a request for a representation, **not a permission**: they never allow a rule to drop,
  repeat, merge or move an evaluation.

## 3. `FlattenedTerm`

```csharp
public readonly record struct FlattenedTerm(ExpressionType Operation, Expression Operand, int SourceIndex);
```

- `Operand` is the **raw** operand: the very node of the source tree, not prepared and not transformed. A shared node
  appears once per occurrence.
- `Operation` is the direct or the inverse operation of the chain's couple, after the context of the crossed operators is
  propagated (§2.1). The first operand has an explicit operation too.
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

Flatten rules are grouped by couple: a rule joins the group of each couple it declares. Rules that share an operator of a
couple must declare the same couple with the same unary operators and bindings, so every binary operator belongs to at most
one group, a chain has one definition per transformer, and it is extracted once whatever the number of rules that receive
it. The plan holds one descriptor per declared node type (indexed by its value): its arity, the operands whose context it
inverts, and its group (or, for an `OperandGroup` operator, the groups that declare it). Nothing reads an attribute after
the plan is built.

A transformer without flatten rules pays one boolean field read per `TransformCore` call (§19).

## 5. Priority and order of invocation

For a transformer that declares flatten rules, `TransformCore(e)` proceeds as follows.

1. `e` is a **chain root** when it belongs to a group (§2.2): a framework `BinaryExpression`/`UnaryExpression` of a declared
   operator, that the transformer accepts to cross (§10), attached to a couple by its declaration or by its operand, and
   that is not a node of a chain being processed after a refusal (§13). Otherwise the node is processed historically.
2. The admission of the chain (`FlattenAdmission`, an engine-internal hook) is asked on the raw root. By default it is the
   effect gate (`AllowsRules`, always open for a user transformer). A transformer of the `Utils` assembly may decide by
   itself which chains its rules treat: the built-in simplifier admits its linear chains only, with an iterative test that
   implies what the gate guarantees (PR B). A chain that is not admitted is not extracted: its root goes to the historical
   path, and the chains below it are offered when the traversal reaches them.
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
recursion on the chain, whatever its depth or shape. A node that belongs to the chain's group is recorded as crossed and its
operands are pushed with the context its descriptor states; any other node becomes a term carrying the operation of the
context that reaches it. The operand of a crossed `OperandGroup` operator decided its group, so it is crossed without
walking its prefix again (the extraction stays linear). The walk reads only framework operator nodes: an operand is never
inspected.

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

Deciding the group of an `OperandGroup` operator walks its prefix of such operators down to the first other node: one
structural visit per node walked. Inside a chain, and in the fallback of a declined chain, each prefix is walked once. A
prefix that identifies no couple (`-(-(…x))`) roots no chain and is processed historically, and each of its levels walks
the rest of it again: quadratic in its depth, bounded by the validated depth (`MaxDepth`, so at most about
`MaxDepth² / 2` = 131,072 structural visits by default) and counted in `MaxStructuralVisits`. No cache is kept. Declarations
without `OperandGroup` operators walk nothing.

## 8. Historical additive declaration

With `[Flatten(Add, Subtract, Negate)]`: a `Subtract` inverts the context of its right operand, a `Negate` inverts it for its
operand, inversions compose.

| Source | Terms |
|---|---|
| `a + (b - (c + d))` | `+a +b -c -d` |
| `a - (b - c)` | `+a -b +c` |
| `-(a + b - c)` | `-a -b +c` |
| `-(-a)` | `+a` |
| `-(a - (b - c))` | `-a +b -c` |

## 9. Historical multiplicative declaration

With `[Flatten(Multiply, Divide)]`: a `Divide` inverts the context of its right operand. These results, and those of §8,
follow from the declarations alone; the same engine gives, for example, `Subtract a`, `Add b`, `Subtract c` for
`a + (b + c)` with `[Flatten(Subtract, Add)]`.

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
- It belongs to the chain's group (§2.2): its node type is declared for that couple, or, for an `OperandGroup` operator, its
  operand identifies that couple. `AddChecked`, `SubtractChecked`, `NegateChecked`, `MultiplyChecked`, `Power` and every
  other operator have their own node types: they are crossed only when declared themselves.
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

- Extraction: one structural visit per node examined (`ConsumeStructuralVisit`, which also checks the stack), plus one per
  node of a prefix of `OperandGroup` operators walked to decide a group (§7).
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
| `RuleRejected` (`ChainNotAdmitted`) | Detailed | the transformer did not admit the chain (PR B: a chain of the built-in simplifier that is not linear) |
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
- `ExpressionDerivation<T>` and `ExpressionIntegration<T>` declare none. Since PR B the built-in `ExpressionSimplifier`
  declares one **private** rule, which a derived simplifier does not inherit (a plan is built from the methods the concrete
  type sees: the private methods of a base type are not among them).
- New public API: `FlattenAttribute`, `FlattenedTerm`, `ExpressionTransformer.RebuildFlattenedChain`,
  `ExpressionTransformRuleInfo.FlattenedNodeTypes`, `ExpressionTransformEventKind.ChainFlattened`; since PR B,
  `ExpressionRuleRejection.ChainNotAdmitted`; with the declarative model, `FlattenUnaryAttribute` and
  `UnaryOperatorBinding`, and the constructors of §2.1 replace `FlattenAttribute(params ExpressionType[])` (unreleased).
- The declarations of the repository keep their results: `[Flatten(Add, Subtract)]` (the simplifier) and
  `[Flatten(Add, Subtract, Negate)]`, `[Flatten(Multiply, Divide)]` give exactly the terms of §8 and §9.
- `FlattenedNodeTypes` lists the couples in index order, then the unary operator of the simplified constructor, then the
  `FlattenUnaryAttribute` operators by node type value (the metadata order of repeated attributes is not specified).

## 19. Measurements

Machine: i7-12700KF, Windows 11, .NET 9. BenchmarkDotNet 0.14.0, in-process, 8 warmups, 20 iterations, memory diagnoser,
affinity `0x14`, high priority, one process per variant and round. Variants: A = `master` `a925436a`, B = a copy of A's
binaries (A/A control), C = this branch. The test transformer `copy` recurses into every child and copies every node (the
smallest possible work per node, so the most sensitive to the infrastructure); `decline`, `accept` and `extract` are
subclasses of it with flatten rules.

### 19.1 Transformers without flatten rules

Measured in processes that run no flatten transformer (median of the round means):

| Case | A (`master`) | B (A/A) | C (branch) | Allocated |
|---|---|---|---|---|
| `ExpressionSimplifier`, `x * 2 + sin(y)` (3 rounds) | 4.410 µs | +0.6 % | +1.2 % | identical |
| `ExpressionSimplifier`, left sum of 16 parameters (3 rounds) | 1,595 µs | +0.5 % | +1.8 % | identical |
| `ExpressionDerivation<double>`, `x²·sin(x) + 3x − exp(2x)/x` (3 rounds) | 303.2 µs | +1.4 % | +1.7 % | identical |
| `copy`, left chain of 16 operands (2 rounds) | 1.189 / 1.288 µs | — | 1.203 / 1.184 µs | identical |
| `copy`, left chain of 256 operands (2 rounds) | 22.02 / 23.85 µs | — | 22.36 / 21.86 µs | identical |
| `copy`, balanced chain of 256 operands (2 rounds) | 18.96 / 19.06 µs | — | 18.51 / 18.82 µs | identical |

No difference beyond the A/A spread, and identical allocations: the historical path costs one field read.

These rows were measured while the flatten entry called by `TransformCore` was marked `NoInlining`. The attribute was then
removed (review: no demonstrated gain) and the cases measured again, three rounds, A against C, without an A/A control:
simplifier −2.8 % and 0.0 %, derivation +0.2 %, `copy` +1.8 % (16 operands), +0.5 % (left, 256) and +4.0 % (balanced, 256),
allocations identical; the A/A spread of the `copy` cases in the campaigns above is ±7 %. In a mixed process, `copy` on the
left chain of 256 operands took 23.9 µs after the flatten cases, against 22.7 µs alone (+5 %).

**A process that mixes both kinds of transformers.** In the three-round campaign (with the `NoInlining` entry) where the same process also ran the flatten
subclasses of `copy`, the `copy` cases were 5 % to 28 % slower than on `master` (A/A: −7 % to +1 %), with identical
allocations, and the simplifier and derivation cases 3 % slower (A/A: −1.5 % to −3.6 %). This is not a cost of the added
code: the same binaries measured alone show no difference (table above), and with dynamic PGO disabled
(`DOTNET_TieredPGO=0`) `copy` takes the same time alone (37.6 / 36.8 µs) and after the flatten cases (37.6 / 36.2 µs). The
engine methods shared by all transformers are compiled once per process, from the profile of whatever ran first; a process
that runs flatten transformers before historical ones on a hot path can therefore see the historical ones slower.

### 19.2 Opt-in transformers

Against `copy` on the same tree, same process, three rounds (median; the rounds agree within a few points, except `accept` on 16 operands, −3 % to −16 %):

| Tree | `decline` (rules always return `null`) | `accept` (transform each operand, faithful rebuild) | `extract` (the rule returns a constant) |
|---|---|---|---|
| left, 16 operands | +56 % time, +70 % allocated | −10 %, +100 % | −73 %, −30 % |
| left, 256 | +55 %, +60 % | −35 %, +74 % | −82 %, −40 % |
| right, 256 | +57 %, +36 % | −32 %, +26 % | −81 %, −64 % |
| balanced, 256 | +55 %, +38 % | −23 %, +29 % | −78 %, −62 % |
| multiplicative, 256 | +62 %, +60 % | −33 %, +74 % | −81 %, −40 % |

- **A declined chain is not free.** It pays the extraction (lists of terms, one array) and the rule invocation on top of the
  unchanged historical traversal: about +55 % time on a transformer that does almost nothing per node. The absolute cost is
  about 55 ns and 80 B per operand; it weighs less on a transformer whose rules do real work. A transformer should declare
  a flatten rule only for chains it mostly accepts.
- **An accepted chain** is faster than the historical traversal (no per-level rule dispatch, preparation or finalization) but
  allocates more in this benchmark, because the rule builds an operand array and `RebuildFlattenedChain` extracts the chain
  a second time.
- **Extraction alone** costs about a fifth of the historical traversal.

### 19.3 Growth (exact counts)

For every shape (left, right, balanced, alternating `+`/`−`, multiplicative, shared DAG) and every size from 8 to 4,096
operands (DAG: 3 to 12 levels, up to 8,191 logical nodes), with `n` operands:

| Mode | Transformation visits | Structural visits | Flatten invocations | `PrepareExpression` calls | `FinalizeExpression` calls |
|---|---|---|---|---|---|
| `copy` (historical) | `2n − 1` | none | — | `2n − 2` | `2n − 1` |
| `decline` | `2n` | `2n − 1` | 1 | `2n − 2` | `2n − 1` |
| `accept` | `n + 1` | `4n − 2` | 1 | 0 | `n` |
| `extract` | 1 | `2n − 1` | 1 | 0 | 0 |

One flatten invocation per chain in every case, including the shared DAG. The result of `decline` and `accept` has the same
`2n − 1` nodes as the source. Stack: a chain of 100,000 operands of every shape is extracted, given to its rule and rebuilt on
a 256 KiB thread (security tests); the same chain, declined, ends with the controlled `Stack` refusal of the historical
recursion.

### 19.4 Not measured

The one-time cost of building the plan of a transformer type (one more reflection scan, for static methods, and an
attribute test per method) was not measured.

## 20. Future uses (not part of PR A)

- **Simplifier (PR B, done for the linear additive chains).** See [SimplifierFlatten.md](SimplifierFlatten.md): one private
  additive rule on `Add` and `Subtract`, the default `CanFlattenThrough`, exact compatibility of the results. Open: the
  chains that are not linear (repeated terms, calls, powers, negations, subtracted groups), the multiplicative chains.
- **Derivation.** Term-wise derivation of a sum with `RebuildFlattenedChain`; n-ary product rule with prefix and suffix
  products (sharing `Expression` objects does not share evaluations).
- **Integration.** Term-wise integration of sums; extraction of the factors that are constant with respect to the
  integration parameter. No general product rule.
- C3 on the flattened representation, advanced regrouping and automatic factoring are out of scope until the architecture
  is validated.
