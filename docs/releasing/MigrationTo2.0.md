# Migrating the package family to 2.0.0-rc.1

This is a coordinated major-version release candidate. All product-train `omy.Utils*` references should be upgraded together; remove mixed 1.x, 2.0.0, and preview references among them. Internal dependencies between product-train packages are exact at `2.0.0-rc.1`. `omy.Utils.Collections` is the current exception: it is intentionally excluded from the product train and remains independently versioned at `0.0.1` - do not "fix" a mixed `omy.Utils.Collections` reference by pulling it to `2.0.0-rc.1`.

## Families

- **Core:** review the detailed [`omy.Utils` 1.2.1 audit](../api/omy.Utils-1.2.1-to-2.0.0-rc.1.md), including removed expression/number formatting APIs, enumerable overloads, nullability, and embedded DateFormula resources.
- **IO and serialization, Networking, Data, Imaging and fonts, Geography, Mathematics, OData, Dependency injection, Virtual machine, and Number formatting:** recompile against the candidate and review the generated package-specific ApiCompat report. The manifest records the verified latest stable baseline: mostly 1.2.1, OData and OData.Generators 0.0.1, VirtualMachine 0.1.0, and first-candidate baselines for unpublished packages. `omy.Utils.Collections` is not part of this candidate at all - see the note above.
- **Reflection:** compare against the latest published 1.2.1 package and account for the isolated worker behavior already documented in the changelog.
- **Parser:** these packages establish their first public candidate baseline and remain governed by the parser production support contract.
- **Source generators:** reference generators as analyzers with `ReferenceOutputAssembly="false"`; do not reference their implementation assemblies directly.

ApiCompat findings are accepted only for this coordinated major candidate and remain visible in `artifacts/reports/public-api-comparison.*`. Validate application-specific behavior before production deployment.

## Reviewed API compatibility changes

The repository-wide ApiCompat run against verified latest stable packages reports the following accepted major-version incompatibility counts: Core 132; IO 6; XML 1; Net 24; Data 3; Fonts 49; Imaging 7; Geography 23; Reflection 3; Mathematics 19; OData 10; VirtualMachine 7; OData generators 3; IO serialization generators 4; and dependency-injection generators 3. DependencyInjection runtime is binary compatible in the automated comparison. NumberToString and parser packages establish first candidate baselines. `omy.Utils.Collections` is outside the product train's `packages` array entirely, so this gate does not run against it.

Each accepted diagnostic is pinned by diagnostic ID and exact message in `eng/api-breaking-changes/2.0.0.json`. The human-review inventory in [Accepted API breaks](AcceptedApiBreaks.md) groups the exact removed or incompatible surface by package and links every acceptance back to its package section. New diagnostics, stale acceptances, and missing migration anchors all fail the gate. The counts include removed types/members and changed signatures or constraints; they are not behavioral guarantees or rename inference. The package-specific raw reports under `artifacts/api-compat` and structured `public-api-comparison.json` are the authoritative review inputs; consumers must recompile and exercise their own usage.

## Core read-only collections

The numeric classification tables under `Utils.Objects.Types` changed from mutable `Type[]` values
to immutable `IReadOnlyList<Type>` values. Use collection operations directly; for example,
`type.In(Types.Number)` may become `Types.Number.Contains(type)`. If an API specifically requires an
array, materialize an explicit copy with `Types.Number.ToArray()`.

`ConstantNumericAttribute.Values` changed from `double[]` to `IReadOnlyList<double>?`. Use `Count`
instead of `Length`, retain the existing null handling, and call `ToArray()` only when a mutable copy
is genuinely required.

<a id="utils-range-complement"></a>
## Utils.Range: Ranges&lt;T&gt; complement removed

`Ranges<T>.Complement(Ranges<T>)` and its `~` operator are removed, and `Ranges<T>` no longer
implements `System.Numerics.IBitwiseOperators<Ranges<T>, Ranges<T>, Ranges<T>>` (a prerequisite for
declaring `~`). Both members were a public stub that unconditionally threw `NotImplementedException`
in 1.x: `Ranges<T>` is only constrained by `IComparable<T>`, and `IRange<T>` carries no notion of a
domain, a minimum/maximum value, or infinite bounds, so a complement relative to "the entire domain"
cannot be computed for an arbitrary `Ranges<T>` — an empty instance would not even have a domain to
recover it from. No internal code or test in the repository called either member. `Union` (`|`),
`Intersect` (`&`), `SymmetricDifference` (`^`), and `Except` (`-`) are unaffected and remain declared
directly on `Ranges<T>` even without the `IBitwiseOperators` interface. `IntRange<T>.Complement()`
and its `~` operator are unaffected: that type's nullable endpoints explicitly represent `±∞`, so its
complement (`Except(FullRange, this)`) is well-defined and unchanged.

<a id="utils-io-serialization-2"></a>
## Utils.IO serialization and stream changes

Version 2.0 intentionally removes the legacy `StreamCopier`, `StreamValidator`, `BaseDecoderStream`, `ReadArray`, and `WriteVariableLengthString` signatures listed in the versioned API-breaking-change manifest. Consumers should migrate to the bounded/configurable overloads described by the current API documentation rather than restore 1.x shims.

`StreamCopier` now validates targets when they are registered, through any constructor, `Add`, `Insert`, or the indexer setter. Null, non-writable, self, and duplicate target references are rejected; duplicate identity is reference-based (`ReferenceEquals`), not `Equals`. After disposal the target list remains inspectable but is frozen: `CanWrite` becomes `false`, `IsReadOnly` becomes `true`, and every mutating member throws `ObjectDisposedException`. Consumers relying on the previous permissive registration or on the post-dispose list being cleared or freely mutable must update accordingly.

`BaseEncoderStream` now validates `maxDataWidth` (must be -1 or positive) and `indent` (must be non-negative) at construction, throwing `ArgumentOutOfRangeException` instead of accepting invalid values or failing later. Wrapping no longer emits a trailing separator/indent after the last line, and padding/filler characters now participate in the configured line width like any other encoded character; consumers matching wrapped output against a fixed string should re-check it. Unsupported `BaseEncoderStream` operations (`Position` setter, `Read`, `Seek`, `SetLength`) now throw `NotSupportedException` instead of `InvalidOperationException`, consistent with the stream's advertised `CanRead`/`CanSeek` capabilities.

`ReaderWriterGenerator` is now an `IIncrementalGenerator`. Generated extension methods no longer use a type's simple name: their deterministic name contains the namespace, containing types, metadata arity, and a stable FNV-1a suffix. Call sites must use the newly generated method name visible in IntelliSense.

Runtime reader converters require an exact result type. A converter returning an interface or base class is not used for a concrete `Read<T>` because it cannot guarantee `T`. Writers may use a base/interface converter; the most specific applicable registration wins and equal-specificity candidates are rejected.

`PartialStream` serializes reads, writes, position changes, seeking, and length changes through one sync/async-compatible gate. Bounds are evaluated while holding that gate, async wait and I/O honor cancellation, failed writes do not advance the logical position, and each operation restores the underlying stream position.

## omy.Utils.Net protocol clients

Use `SmtpPath` and `SmtpMailOptions` for SMTP envelopes, handle `ProtocolResponseException` for negative responses, and recreate a client after `ProtocolSessionPoisonedException`. POP3 STAT sizes are `long`; NNTP `NewNewsAsync` returns message-id strings; and `NextAsync` returns null only for response 421. Prefer the new `TextReader`/`TextWriter` streaming overloads for large payloads.

## NumberToString rule precedence

Version 2.0 removes declaration order as an implicit tie-breaker. Add `priority="100"` (or another intentional signed `xs:int` value) when compatible rules have equal canonical specificity. Ordinal variants and trigger forms select the greatest specificity and then greatest priority. Cumulative variants apply the least specific and lowest-priority rules first.

Programmatic trigger forms must migrate from `(Constraints, To)` tuples to `NumberToStringConverter.TriggerReplacementForm`. `VariantRule` and `OrdinalVariantRule` constructors accept an optional final `priority` argument. Configurations inherited through `baseOn` retain parent priorities; parent and child candidates are validated together and no implicit override occurs.

### ForcedVariants (NTS-04) — additive, with one French output correction

A configured lexical constituent (a time unit, a currency unit/subunit, a fraction
denominator term) can now force grammatical variant dimensions (e.g. gender) on the
numeric fragment it governs, without the caller supplying them — see the
"ForcedVariants" section of `Utils.NumberToString/README.md` for the full precedence
and locality contract. The new public surface is purely additive: the existing
`NumberToStringConverterOptions.TimeUnits` tuple shape and `CurrencyDefinition`'s
existing properties are unchanged; `TimeUnitForcedVariants`, `FractionForcedVariants`,
`CurrencyDefinition.UnitForcedVariants`/`SubunitForcedVariants`, and the new
`ForcedVariantSet` type default to an empty set that is behaviorally identical to
pre-2.0 output. No `AcceptedApiBreaks.md` entry is required.

The one intentional **output change** is a grammatical correction to the built-in
French time-unit configuration: `hour`/`minute`/`second` (all feminine nouns) now
declare `forceVariants="gender=feminin"`. `fr.Convert(new TimeSpan(1, 0, 0))` changes
from `"un heure"` to the grammatically correct `"une heure"`, and `fr.Convert(TimeSpan.FromHours(21))`
changes from `"vingt et un heures"` to `"vingt et une heures"`, without the caller
passing `gender=feminin` explicitly. Callers that previously worked around the defect
by passing `gender=feminin` themselves are unaffected — the explicit variant still
produces the same, now-correct, result. `fr.Convert(1)` and `fr.Convert(21)` (ordinary
cardinals, no time unit involved) remain masculine by default.

`ForcedVariantSet.Create` — introduced in this same rc, not previously published —
takes `params IEnumerable<(string Dimension, string Value)>` rather than an array-typed
`params (string, string)[]`; existing tuple-literal call sites (`Create(("gender", "feminin"))`)
are source-compatible. `ForcedVariantSet` dimension aliases (a language's declared
`localName`, e.g. French `genre`) are now canonicalized to the dimension's canonical
name before use; a forced set that mixed a canonical name and its alias for the same
dimension (previously silently inert) is now rejected deterministically.

Portuguese, Galician, Catalan, and Spanish (`PT`, `GL`, `CA`, `ES`) gain built-in
`Convert(TimeSpan)`/`Convert(TimeOnly)` support (`SupportsTimeConversion` becomes
`true`). PT/GL/CA use the same `forceVariants="gender=..."` pattern on their feminine
`hour` unit; `minute`/`second` remain masculine by default. Spanish additionally
declares a new `form` variant dimension (`standalone`/`attributive`) so its masculine
attributive numeral apocope ("uno"→"un", "veintiuno"→"veintiún") applies correctly to
both count 1 and compound counts (21, 31, …) — see the "Lexical form selection /
ForcedVariants (NTS-05)" note below. These are new capabilities, not behavior changes
to existing output.

### Lexical form selection (NTS-05) — additive

New public extension point: `ILexicalFormSelector`, `LexicalFormContext`,
`LexicalFormSet`, `DefaultLexicalFormSelector`, `LexicalFormSelectorConfiguration`,
and `NumberToStringConverter.RegisterLexicalFormSelector`/
`NumberToStringConverterOptions.TimeUnitForms`/`TimeUnitFormSelectors` — lets a
configured time unit choose among more than two named word forms (e.g. a future
Russian "час"/"часа"/"часов") via a selector resolved at most once per distinct
selector type, during configuration loading, never during `Convert(...)`. A unit may
optionally pass selector-specific settings via a structured
`<LexicalFormSelector type="..."><Configuration>...</Configuration></LexicalFormSelector>`
XML element (the core library never interprets `<Configuration>`'s content); units
needing no configuration keep using the concise `formSelector="..."` attribute.
`NumberToStringConverter.TimeUnitForms`/`TimeUnitFormSelectors` report the *effective*
state for every configured time unit (synthesized singular/plural and
`DefaultLexicalFormSelector` for units with no override). Every unit that configures no
selector uses the built-in default (today's exact singular/plural behavior), so no
existing configuration or output changes. See the "Lexical form selection" section of
`Utils.NumberToString/README.md` and `Utils.NumberToString/DONE-2026-08-25(1).md`/
`DONE-2026-08-25(2).md`. Spanish does not use this mechanism — its time-unit fix is a
`ForcedVariants` addition (see above), not a lexical-form-selector one.

<a id="omy-utils-expression-engine-etc-r1b-r1c"></a>
## Expression engine: scalar traversal, faithful reconstruction, side-effect safety and structural equality (ETC R1b/R1c)

These are behavior changes of `ExpressionTransformer`, `ExpressionSimplifier` and `ExpressionComparer`; no public signature
changed. The full record (decisions C-01, C-03, C-04, C-06, findings ETC-03 to ETC-15, measurements) is in
`Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md`.

- **Faithful unary and lambda reconstruction (C-01).** Every transformer, every `ExpressionSimplifier` subclass and the
  protected `CopyExpression`/`ReplaceArguments` helpers now keep a unary operator or conversion `Method`, a typed `Throw`'s
  type, and a lambda's delegate type, name and tail-call flag. A subclass that relied on the former loss (for example a
  custom negation silently becoming the predefined one) observes the original operator again.
- **Newly traversed families (C-03).** Member access (receiver), `TypeIs`/`TypeEqual`, `New` (arguments), `NewArrayInit`/
  `NewArrayBounds` (elements or bounds) are traversed and rebuilt with their metadata; `Default` is an explicit leaf. A
  `PrepareExpression` override is called on those children (in `ExpressionVisitor` order), rules declared on those node types
  receive the prepared children, and `FinalizeExpression` receives a non-empty child list for them. `CopyExpression` applies
  its arity contract to `MemberAccess` (one child for an instance member, none for a static one).
- **Scalar substitution.** `ReplaceArguments` enters member receivers, type tests, creation arguments and array elements;
  invocations of lambdas whose body uses those families can be inlined. Binders (nested lambdas, blocks, catch, switch) are
  unchanged until R2.
- **Side-effect safety (C-06).** The simplifier no longer drops, merges, repeats or reorders an operand that is not proven
  inert: assignments and increments, throws, user methods (only an explicit allow-list of total `Math`/`MathF`/`double`/
  `float` functions is inert), property getters, constructors and allocations, custom operators, invocations, checked
  arithmetic, integer and decimal division, decimal arithmetic (overflow), array access, casts and unknown or extension
  nodes. For example `f(x) + f(x)` keeps both calls, `(v = 5) * 0` keeps the assignment, the decimal sum `2m*x + 3m*x` is no
  longer factored, and a sum keeps such terms in source order after its regrouped inert terms. Results that used to
  rely on those rewrites are now less reduced but evaluate exactly like the source.
- **Structural equality (C-04).** `ExpressionComparer` compares and hashes conditionals, invocations, type tests, defaults,
  creations (constructor, arguments, members) and array creations structurally, with the usual parameter binding; such
  trees, which were always unequal unless they were the same instance, can now be equal keys of a
  `Dictionary<Expression, …>`/`HashSet<Expression>`. Structural equality does not make two allocations or two calls
  interchangeable: the simplifier still keeps both.

## Expression engine: binding scopes, control flow and capture-avoiding substitution (ETC R2)

Behavior changes of `ExpressionTransformer`, `ExpressionSimplifier` and `ExpressionComparer`; no public signature changed.
The full record is in `Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md` (§5, §9 "R2 result").

- **Newly traversed families (C-03 extended).** `Block`, `Try` (with its `CatchBlock`s), `Switch` (with its `SwitchCase`s),
  `Loop`, `Goto` and `Label` are traversed and rebuilt with their metadata (variables and their identity, explicit result
  type, handler test types and variables, filters, finally versus fault, comparison method, case and test order, label
  targets — never recreated —, jump kinds); `RuntimeVariables` keeps its variables (they must stay parameters) and
  `DebugInfo` is an explicit leaf. A `PrepareExpression` override is called on block and catch variables (which must stay
  `ParameterExpression`s, as lambda parameters already had to, otherwise `InvalidCastException`) and on every child, in
  structural order; rules declared on those node types receive the documented positional children, and
  `FinalizeExpression` receives them too. Block and catch bodies go through `PrepareExpression`, not the direct lambda-body
  descent, so a transformer whose `PrepareExpression` is the identity leaves them as they are. `CopyExpression` applies its
  arity contract to `Try`, `Switch`, `Goto`, `Label`, `RuntimeVariables` and `DebugInfo` (an explicit list of the wrong
  length throws `ArgumentException`).
- **Capture-avoiding substitution (ETC-03).** `ReplaceArguments` now replaces free occurrences inside nested lambdas,
  conversion lambdas, blocks, catch handlers, switches, loops, jumps and labels. A binder redeclaring a replaced parameter
  hides it; a binder declaration that a replacement would capture is renamed to a fresh parameter (same type, by-reference
  flag and name). Replacing a runtime variable by anything but another parameter throws `NotSupportedException` (C-07). A
  quoted expression is still never entered (C-05, kept in R4 whatever `SimplifyQuotedExpressions`).
- **Invocation inlining.** Lambda invocations whose bodies contain nested lambdas, blocks, catch handlers or switches can be
  inlined when the analysis proves it safe (same argument eligibility as before: constants, defaults and parameter reads);
  a variable argument read inside a nested closure, after an effect, or bodies with loops, jumps, labels, runtime variables
  or quotes still keep the invocation.
- **Structural equality and hashing.** `ExpressionComparer` compares the families above with a lexical binding model:
  block and catch variables are bound by position (alpha-equivalence), labels by a bijection per lambda body (names never
  count); debug information is not semantic. Two such trees, always unequal before unless they were the same instance, can
  now be equal keys of a `Dictionary<Expression, …>`/`HashSet<Expression>`. Structural equality still does not make two
  blocks, tries, switches or loops interchangeable: the simplifier treats them as observable and keeps every evaluation.
- **Canonical ordering.** Block and catch variables are bound scopes for the simplifier's canonical ordering, so block-local
  terms are ordered by binding position (for example `{ u = …; v = …; v + u }` now simplifies its last statement to `u + v`).
- **Validation of shared DAGs.** The input validation of every expression operation no longer unfolds a shared subtree
  once per use when the node limit is above 65,536: a heavily shared DAG (for example `e = e + e` repeated 40 times) used
  to make a call with `ExpressionResourceLimits.Unlimited` or a very high `MaxNodes` run practically forever; it is now
  decided in linear time (and not validated at all under `Unlimited`, which cannot refuse anything). The default limits,
  the counted quantities (logical occurrences, depth) and the refusal kinds are unchanged; the observed value of such a
  refusal is now the exact (saturated) count, and when a large input exceeds both limits the reported kind may be `Depth`
  where it was `Nodes`.

## Expression engine: initializers and index accesses (ETC R3)

Behavior changes of `ExpressionTransformer`, `ExpressionSimplifier` and `ExpressionComparer`; no public signature changed.
The full record is in `Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md` (§9 "R3 result").

- **Newly traversed families (C-03 extended).** `MemberInit` (with its `MemberAssignment`, `MemberMemberBinding` and
  `MemberListBinding` bindings), `ListInit` (with its `ElementInit`s) and `Index` (array element or indexer) are traversed
  and rebuilt with their metadata: constructor, members, binding kinds, binding and initializer order, the exact `Add`
  method of every initializer (never rediscovered from the argument types), the indexer, the type, and the writability of
  an index access used as an assignment target. A `PrepareExpression` override is called on the creation (which must stay a
  `NewExpression`, otherwise `InvalidCastException`), on every binding value and initializer argument, and on the receiver
  and arguments of an index access, in structural order. Rules declared on those node types and `FinalizeExpression`
  receive the flattened positional children: the creation, then the expressions of the bindings in declaration order (a
  nested member binding contributes its own bindings' expressions, a list binding its initializers' arguments); the
  creation, then the initializers' arguments; the receiver (when present), then the index arguments. A transformer whose
  `PrepareExpression` is the identity leaves these children as they are. `CopyExpression` applies its arity contract to the
  three families: a list of the wrong length, a first child that is not a `NewExpression` of the same type, a value that
  does not fit its member, an argument that does not fit its `Add` method, or an index access that would change type throws
  `ArgumentException`. No constructor, setter, getter, `Add` method or indexer accessor is ever run by the engine.
- **Substitution.** `ReplaceArguments` replaces free occurrences inside binding values, nested bindings, `Add` arguments, and
  the receiver and arguments of index accesses, with the capture-avoiding rules of R2 (these families declare nothing).
- **Invocation inlining.** Lambda invocations whose bodies contain initializations or index accesses can be inlined when the
  analysis proves it safe (same argument eligibility as before). Every setter, getter, `Add` call and indexer accessor
  counts as user code, so a variable argument read after one of them keeps the invocation.
- **Structural equality, hashing and canonical keys.** `ExpressionComparer` compares and hashes the three families
  structurally (creation, bindings in order with their kind and member, initializers in order with their `Add` method,
  indexer, receiver, arguments), with the usual parameter binding; two such trees, always unequal before unless they were
  the same instance, can now be equal keys of a `Dictionary<Expression, …>`/`HashSet<Expression>`. Structural equality does
  not make two initializations or two indexer reads interchangeable: the simplifier treats them as observable and keeps
  every construction, assignment, `Add` call and access, in order.

## Expression engine: dynamic operations, extension nodes, quoted expressions and lambda copies (ETC R4)

Behavior changes of `ExpressionTransformer`, `ExpressionSimplifier` and `ExpressionComparer`, and one new public property,
`ExpressionSimplifier.SimplifyQuotedExpressions`. The full record is in
`Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md` (§9 "R4 result").

- **Quoted expressions are no longer simplified by default (C-05).** Before R4, every simplifier rewrote the lambda inside a
  `Quote` node (`Quote(x => x * 1)` became `Quote(x => x)`), although a quote is data for its consumer (a query provider, a
  translator). A quote is now a strict boundary of `ExpressionSimplifier`: the very same `Quote` instance is kept wherever it
  appears, no rule (built-in or declared by a derived simplifier on `ExpressionType.Quote`) runs on it, and
  `FinalizeExpression` does not receive it. To keep the previous rewriting, opt in when creating the simplifier:

  ```csharp
  var simplifier = new ExpressionSimplifier
  {
      SimplifyQuotedExpressions = true
  };
  ```

  The quoted lambda is then simplified like any lambda (delegate type, name, tail call, parameters, labels and captured
  variables kept). The property is init-only: an instance applies one policy to every call, nested or concurrent. The
  option never changes substitution (`ReplaceArguments` and invocation inlining never enter a quote) nor
  `ExpressionTransformer` subclasses that are not simplifiers, which keep traversing quotes. Because the public
  `ExpressionComparer` simplifies with the default policy, `Equals(Quote(x => x * 1), Quote(x => x))` is now `false`; two
  structurally equal quotes are still equal.
- **Dynamic operations are traversed (C-03 extended).** The arguments of a `DynamicExpression` are prepared in order (a
  `PrepareExpression` override is called on each), passed to rules and `FinalizeExpression` as positional children, and the
  node is rebuilt through `DynamicExpression.Update`: the same binder instance and the exact delegate type are kept; the
  binder is never invoked and no call site is created. `CopyExpression` applies its arity contract (a wrong number of
  children, or an argument that does not fit the delegate, throws `ArgumentException`). `ReplaceArguments` substitutes inside
  the arguments. `ExpressionComparer` compares dynamic operations structurally: the binder by identity (its own `Equals` and
  `GetHashCode` are never called; two binder instances are never equal), the delegate type, then the arguments in order. A
  dynamic operation stays observable for the simplifier: it is never cancelled, merged, dropped or reordered.
- **Extension nodes are strictly opaque.** A node of a class outside the framework expression object model (any user
  subclass of `Expression`) is recognized by type tests only. The engine never reads its `NodeType` or `Type`, never calls
  `CanReduce`, `Reduce`, `VisitChildren`, `Accept`, `Equals`, `GetHashCode` or `ToString`. Consequences for subclasses:
  **no rule runs on an extension node any more**, not even a rule declared on `ExpressionType.Extension` or a wildcard rule
  (rule dispatch had to read the virtual `NodeType`); handle your own node types in a `FinalizeExpression` override, which
  still receives them with an empty child list. `CopyExpression` keeps an extension node for an empty child list and throws
  `ArgumentException` for any other list (it used to ignore the list). `ExpressionComparer` equates an extension node only
  with itself (under binders that bind the same declarations) and hashes it by identity: two distinct instances are never
  equal, and their hash no longer depends on `NodeType`/`Type`. Framework factories rebuilding a supported parent of an
  extension node (for example `Expression.Add` when its other operand changed) still read the node's `Type` and `NodeType`,
  as when the tree was built.
- **`CopyExpression` of a lambda (C-02 completed).** The positional children of a lambda are its parameter declarations, in
  order (the list `FinalizeExpression` receives; the body is never a child). The same declarations keep the node; other
  parameters of the same types and by-reference flags alpha-rename it (capture-avoiding; delegate type, name and tail call
  kept); a wrong length, a node that is not a parameter, another type or by-reference flag, a duplicate, a renaming that would
  capture a free variable of the body, or one that cannot reach an occurrence inside a quote or an extension node throws
  `ArgumentException`. It used to return the lambda unchanged whatever the list.

## Expression engine: closure-audit corrections (after ETC R4)

- **Invocations writing through a value-type parameter are no longer inlined (AUD-01).** A parameter of a structure type is
  a copy of its argument. When the invoked lambda assigns or increments a field or an indexer element of that parameter,
  passes such a field by reference, or calls a mutating method, getter or indexer of a non-`readonly` structure on it, the
  simplifier now keeps the invocation. It used to substitute the argument, so the write reached the caller's variable (or a
  constant's temporary) and `Simplify` changed the result. Reads, `readonly` members, `readonly` structures (every primitive
  numeric type, `decimal`, `DateTime`…) and writes through a reference-type parameter are still inlined.
- **Dynamic operations no longer block lambda copies (AUD-02).** The free-variable walk treated a `DynamicExpression` as an
  opaque extension node: `CopyExpression` refused to alpha-rename a lambda whose body holds a dynamic operation, and the
  substitution renamed declarations a replacement holding one did not mention. The walk now enters the arguments of a
  dynamic operation (never its binder).
- **Factoring a difference by 1 no longer loops (AUD-03, S5 P17).** `Simplify(c * (y - 1))`, `c * (1 - y)` with `c` other
  than ±1, `(1 - y) / (1 / y)` and `a * x + (1 - a) * x` used to fail with a stack overflow, then (since the resource limits)
  with an `ExpressionResourceLimitException` of kind `Stack`. They now return a simplified tree. The forms that held a
  spurious factor 1 change: `y - 1` gives `(0 - 1) + y` (like `y - 2` gives `(0 - 2) + y`) instead of `(y - 1) * 1`,
  `1 - y` gives `1 + (0 - y)` instead of `(1 - y) * 1`; the values are unchanged.

## Expression engine: injectable transformation tracing (additive)

New public API in `Utils.Expressions`, without behavior change for code that does not use it: `ExpressionTransformTracing`
(`Begin(tracer, detail)` opens a disposable `ExpressionTraceScope`), `IExpressionTransformTracer`,
`ExpressionTransformEvent` and its enumerations, `ExpressionTransformRuleInfo`, the bounded `ExpressionTraceCollector`,
`ExpressionTraceFormatter`, `ExpressionTraceExporter` (indented text, JSON) and `ExpressionTraceCycleAnalyzer`. The full
record is in `Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md` (§9 "Engine tracing").

- A scope is ambient per execution context (`AsyncLocal`): it traces the operations started inside it, on this flow and in
  the tasks it starts, never concurrent operations elsewhere, and never through a field of a shared simplifier. It is read
  once per outermost operation; an operation running when the scope is disposed keeps reporting to it until it returns.
- Tracing never changes a result, the work performed (transformation and structural visits are identical) or the
  exception thrown. One visible difference: an exception leaving a **traced** outermost operation is rethrown with
  `ExceptionDispatchInfo`, so its stack trace carries the "End of stack trace from previous location" marker.
- A tracer that throws is detached (`ExpressionTraceScope.TracerException`), and a tracer cannot call the engine from its
  callback (the call throws `InvalidOperationException` before it starts, which detaches the tracer). Export and analyze
  the events outside the callback.
- Events whose delivery is deferred (the stack was too short to call the tracer, typically while a stack refusal unwinds)
  wait in a bounded list; beyond it they are dropped and counted on `ExpressionTraceScope.UndeliveredEvents`, leaving a gap
  in the sequence numbers. The resource refusal and the root failure are always kept.

## Expression engine: simplification policies consumed (S5 P18 Architecture C)

`ExpressionSimplifier.Options` (Architecture B) had no effect; it is now consumed by the zero and one identities. No public
signature changed, and the default policy (`ExpressionSimplifierOptions.Default`, or any equal value) gives exactly the previous
results, so code that does not set `Options` is unaffected, and so is `ExpressionComparer.Default`.

- Under any other policy, `F() * 1`, `F() / 1`, `0 - F()`, `F() ^ 1`, `F() * -1` and similar identities apply even when the
  operand is not inert (they change no evaluation), and `t * 0`, `0 * t`, `0 / t`, `t ^ 0`, `0 ^ t`, `1 ^ t` drop `t` when the
  policy lets its evaluation be discarded (for example a partial integer division without arithmetic preservation, a
  `[Deterministic]` method under `TrustDeclarations` without user-code preservation). Unknown code and side effects are never
  dropped. Code that set a non-default `Options` while it had no effect now gets these rewrites.
- An integral or `decimal` division of a non-inert operand by a constant zero no longer throws while simplifying under such a
  policy (it is left to the evaluation); under the default it never reached the rule.
- Rules declared by a derived simplifier are unaffected (they keep the historical admission).

<a id="omy-utils-fonts-2"></a>
## Utils.Fonts hostile-font parsing hardening

`TrueTypeFont.ParseFont`/`ParseFontAsync` now accept an optional `TrueTypeFontParsingOptions`
governing a `FontValidationMode` (`Strict`, the default, or `Permissive`), explicit resource limits
(font/table size, table count, `cmap` subtable count, composite-glyph depth/component/point
budgets), and stream-ownership (`LeaveOpen`). Strict mode rejects any structural anomaly by throwing
`FontParseException`; permissive mode records `FontDiagnostic`s on `TrueTypeFont.Diagnostics` and
continues whenever doing so remains memory-safe -- resource-limit violations always throw in both
modes. Fonts that previously loaded silently despite duplicate table tags, checksum mismatches,
malformed `cmap` subtables, or out-of-range composite glyph references now fail fast by default;
pass `FontValidationMode.Permissive` to restore a best-effort parse. `TrueTypeFontParsingOptions.MaximumFontBytes`
defaults to 64 MiB and cannot be configured above `uint.MaxValue` (4 GiB) -- SFNT table
offsets/lengths are themselves unsigned 32-bit fields, so this library never supports a larger font
regardless of this setting; a value above that ceiling throws `ArgumentOutOfRangeException`
immediately when constructing the options.

Several `short`-typed members were widened to their correct unsigned/wider wire type and are 2.0
breaks requiring recompilation: see the "Second audit pass" entry under
[`omy.Utils.Fonts`](AcceptedApiBreaks.md#omy-utils-fonts) for the full list, including the
`GlyphCompound.getGlyphIndex` → `GetGlyphIndex` rename and the `CmapTable.CMaps`/
`GlyphCompound.Instructions` immutability changes. This also includes `CmapSubtable.PlatformID` and
`PlatformSpecificID`, which now use `ushort` to match their unsigned 16-bit wire fields.

The static `FontSupport` name, charset, and encoding tables now expose `IReadOnlyList<string>` or
`IReadOnlyList<int>` rather than mutable arrays. Continue to use indexing and enumeration, replace
`Length` with `Count`, and make any required mutable copy explicit, for example:

```csharp
var copy = FontSupport.StdNames.ToArray();
```
