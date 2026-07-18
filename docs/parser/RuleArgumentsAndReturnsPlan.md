# ANTLR rule arguments and returns integration plan

## Purpose

This document records the design boundary for ANTLR-style rule arguments, rule parameters, rule returns, labels, rollback, and generated-C# opt-in integration in `Utils.Parser`.

This document is now a durable state and design reference. The explicit literal-binding runtime policy subset is implemented, while `Parse(...)` remains conservative, `ParserEngine` remains target-language neutral, and automatic arbitrary `callee[expr]` evaluation plus ANTLR-compatible generated parser signatures remain out of scope.

The plan separates four surfaces that are easy to confuse:

- runtime standard behavior: conservative `Parse(...)` and the default runtime policy;
- runtime opt-in behavior: explicit `IParserRuleCallExecutionPolicy` implementations installed by callers;
- generated-C# opt-in behavior: generated helper APIs and optional embedded-code hooks used by `ParseWithEmbeddedCode(...)` / generated policies;
- metadata-only behavior: parsed grammar facts that are observable but non-authoritative.

## Current support summary

The project already has several building blocks for future rule argument and return integration:

- rule parameters such as `rule[int x]` are parsed and preserved as metadata;
- rule-call argument clauses such as `callee[...]` are recognized and preserved as raw text;
- `RuleRef.RawArguments` stores call-site argument text without the outer brackets;
- `ParserRuleCallExecutionContext.RawArguments` exposes the raw text to explicit rule-call policies;
- `ParserRuleCallExecutionContext.PositionalRawArguments` exposes syntactic top-level positional slices;
- `ParserRuleCallExecutionContext.NamedRawArguments` exposes syntactic top-level named slices when the whole call-site matches a supported named form;
- `ParserRuleCallResult.RawArguments` carries raw argument metadata on completed child call results;
- `ParserRuleInvocationFrame.Parameters`, `ParserRuleInvocationFrame.Locals`, and `ParserRuleInvocationFrame.Returns` provide untyped parser-managed frame stores;
- `ParserRuleInvocationDescriptor.Parameters`, `ParserRuleInvocationDescriptor.Returns`, and `ParserRuleInvocationDescriptor.Locals` expose passive declaration descriptors;
- `ParserRuleCallResult.Returns` snapshots completed child return values;
- `ParserLabeledRuleCallResultStore` retains assignment/list labeled child call results;
- generated helpers include `SetNextRuleParameter(...)` and `ClearNextRuleParameters(...)`;
- generated helpers include `SetNextRuleParameterFromRawArguments(...)`;
- generated helpers include `SetNextRuleParametersFromRawArguments(...)`;
- generated helpers include `SetNextRuleParametersFromNamedRawArguments(...)`;
- generated helpers include current-rule return helper APIs;
- generated helpers include labeled-rule-call result and return helper APIs.

These pieces do **not** mean full ANTLR compatibility. They are explicit, conservative primitives for metadata visibility, managed state, and opt-in helper use.

## Runtime standard behavior

The default runtime remains conservative:

- `Parse(...)` does not execute generated embedded-code hooks or generated helper logic;
- the default rule-call policy is no-op and does not bind arguments;
- raw arguments are preserved and split syntactically only;
- raw arguments are not evaluated as C#, ANTLR target code, constants, expressions, return reads, label reads, or parameter references;
- declared parameters are not automatically populated by `callee[expr]`;
- declared returns do not create typed ANTLR variables or typed generated members;
- labels store managed `ParserRuleCallResult` snapshots, not ANTLR parser contexts;
- `ParserEngine` remains language-neutral and does not contain target-language C# expression evaluation.

## Runtime opt-in behavior

Runtime opt-in is available only through explicit caller-installed policies. Existing policies include:

- `PositionalLiteralRuleCallExecutionPolicy`;
- `NamedLiteralRuleCallExecutionPolicy`;
- `TypedPositionalLiteralRuleCallExecutionPolicy`;
- `TypedNamedLiteralRuleCallExecutionPolicy`.

Their intended boundary is narrow:

- opt-in only; none of these policies are installed by the default policy;
- simple literals only;
- typed conversion is allowlisted only for typed policies;
- no arbitrary C# evaluation;
- no Roslyn expression evaluation;
- no ANTLR generated method signature changes;
- no automatic binding by default;
- failure behavior is configurable by policy options where exposed;
- any accepted values must be written through the managed pending-seed mechanism rather than through external side effects.

These policies are useful compatibility adapters, not a full ANTLR argument model.

## Literal policy subset status

| Area | Status | Boundary |
| --- | --- | --- |
| Untyped positional literals | Realized | Exact arity by declaration order; declared types and defaults are ignored. |
| Untyped named literals | Realized | Exact ordinal declared-name coverage; argument order is irrelevant; defaults are ignored. |
| Typed positional literals | Realized | Allowlisted conversion with trailing omission satisfied only by supported simple defaults. |
| Typed named literals | Realized | Allowlisted conversion with omitted names satisfied only by supported simple defaults. |
| Default runtime behavior | Explicitly limited | No binding unless a caller installs a policy. |
| Runtime batching | Realized | One atomic managed seed batch after complete validation. |
| Rollback and memoization | Realized | Managed seeds participate in rollback and effective bound values participate in generated state keys. |
| Generated-C# opt-in | Explicitly limited | Generated helper/policy paths may opt in; conservative generated `Parse(...)` remains unchanged. |
| Arbitrary ANTLR/C# semantics | Future separately justified | No expression evaluation, references to parameters/locals/labels/returns, user types, generated typed signatures, automatic returns, or full ANTLR compatibility. |

## Generated-C# opt-in behavior

Generated C# already exposes helper APIs that let embedded code seed parameters manually and inspect call-site metadata. Relevant helpers include:

- `SetNextRuleParameter`;
- `ClearNextRuleParameters`;
- `SetNextRuleParameterFromRawArguments`;
- `SetNextRuleParametersFromRawArguments`;
- `SetNextRuleParametersFromNamedRawArguments`;
- `SplitRawArgumentsTopLevel`;
- `SplitNamedRawArgumentsTopLevel`;
- `TrySplitLastRuleCallRawArguments`;
- `TrySplitLastRuleCallNamedRawArguments`.

These helpers support explicit user-controlled flows such as reading the previous child call's raw arguments, mapping them with caller-provided delegates, and seeding a future child invocation.

They are **not**:

- automatic `callee[expr]` evaluation;
- automatic parameter binding;
- generated method signatures such as `rule(int x)`;
- an ANTLR-compatible public parser API;
- a full ANTLR attribute model;
- permission to move `$...` rewriting into parser core.

ANTLR-style `$...` conveniences remain behind the optional C# `IParserEmbeddedCodeTransformer` path. The no-op/default transformer preserves `$...` text unchanged, and future target-language conveniences must remain transformer-owned.

## Metadata-only behavior

The following grammar/model facts are observable but do not grant execution authority:

- `RawParameters`;
- `RawReturnType`;
- `RawLocals`;
- `ParserRuleParameterDescriptor`;
- `ParserRuleReturnDescriptor`;
- `ParserRuleLocalDescriptor`;
- `RawArguments`;
- `LabelName`;
- `LabelKind`.

Metadata can inform diagnostics, generated helper shape, documentation, tests, and future opt-in paths. It must not be interpreted as permission for implicit execution in conservative parsing.

## Unsupported / non-goals

This plan does not implement or promise:

- full ANTLR compatibility;
- ANTLR generated parser API parity;
- automatic arbitrary target-language expression evaluation;
- automatic public rule signatures;
- automatic `callee[expr]` evaluation;
- automatic parameter binding;
- generated parser signatures compatible with ANTLR;
- full ANTLR attribute model;
- general action buffering / replay;
- rollback of external side effects;
- runtime-inline lexer execution;
- separate runtime lexer;
- target-language C# logic in `ParserEngine`;
- implicit execution in `Parse(...)`.

## Rule parameters

Rule parameter declarations are parsed as passive metadata. Descriptor extraction is lexical and conservative: names are extracted for helper/descriptive purposes, while raw declarations remain available for diagnostics and generated helper context.

Current behavior:

- declaration metadata can be observed through descriptors;
- generated C# helper code can read frame parameter values when a caller or helper has explicitly seeded them;
- pending seeds are managed parser state when the stack frame manager and generated execution state manager are active.

Boundaries:

- parameters are not automatically populated from `callee[...]`;
- parameter declaration types are not automatically compiled into public parser signatures;
- typed policy conversions are limited opt-in adapters, not general C# type checking;
- `$param` conveniences are optional generated-C# transformer behavior only and do not apply to conservative `Parse(...)`.

## Rule-call arguments

Rule-call arguments are call-site metadata first. The raw text in `callee[...]` is preserved, can be syntactically split, and can be carried into completed child call results.

Current behavior:

- raw argument text is preserved without outer brackets;
- positional splitting is top-level syntactic splitting only;
- named splitting supports the documented named separators and last-wins duplicate raw names;
- generated helper APIs can map raw slices with caller-provided delegates;
- explicit rule-call policies can seed simple literal values.

Boundaries:

- no automatic expression evaluation;
- no automatic caller-context evaluation before child entry;
- no automatic binding to child parameters;
- no general support for arguments that read labels, returns, locals, parameters, or arbitrary target code.

## Rule returns

Rule returns are represented as metadata and parser-managed untyped frame state.

Current behavior:

- return declarations are preserved as raw metadata and descriptor entries;
- generated C# helper APIs can explicitly read/write the active frame's return store;
- successful child rule calls can snapshot return values into `ParserRuleCallResult.Returns`;
- labeled child result helpers can read captured return values from assignment/list labels;
- present-null return values and missing return keys remain distinct.

Boundaries:

- returns are not auto-allocated as typed ANTLR variables;
- returns are not public generated method return values;
- parent rules do not receive automatic return assignments;
- dotted writes such as `$rule.value = ...` remain unsupported;
- any `$...` return convenience remains optional generated-C# transformer behavior.

## Labels and child call results

Rule-reference labels are preserved as metadata and can bind successful child call results into managed stores.

Current behavior:

- assignment labels retain the last successful `ParserRuleCallResult`;
- list labels append successful results in order;
- result snapshots include returns, raw arguments, and label metadata;
- memoized child calls can be annotated with the current call-site label before label binding;
- generated C# helper APIs can inspect labeled result and return collections.

Boundaries:

- labels are not ANTLR parser contexts;
- no bare `$x` / `$xs` variables are created by parser core;
- no typed label fields/properties are generated;
- no automatic return binding occurs merely because a label exists;
- lexer labels and lexer returns remain out of scope.

## Rollback and memoization

Rollback support is limited to parser-managed state:

- pending child parameter seeds are rollback-safe when the managed execution-state mechanism is active;
- current-rule returns can be snapshotted and restored through managed execution-state capture/restore;
- completed child call results include returns, raw arguments, and labels;
- labeled results are stored as immutable snapshots;
- memoization keys can include deterministic parser-managed state;
- return objects that are not deterministically hashable must remain conservative for memoization, typically forcing volatile/non-reusable state rather than unsafe reuse;
- external side effects are not rolled back.

This is not general ANTLR transactional execution. There is no action buffering/replay layer and no external side-effect rollback.

## Evaluation order target model

A future functional PR may add a generated-C# opt-in path for `callee[expr]`. The target model is:

1. parse the call site exactly as today;
2. before entering `callee`, evaluate argument expressions in the caller context;
3. prepare a complete batch of parameter seeds;
4. validate arity, names, and unsupported cases before writing seeds;
5. write seeds atomically;
6. enter `callee`;
7. expose parameters through the callee frame;
8. capture returns after success and `@after`;
9. annotate the child call result with raw arguments and the call-site label;
10. bind the result into assignment/list labels when applicable.

This model is for a future generated-C# opt-in implementation only. It is not the default runtime behavior and is not a `Parse(...)` feature.

## Implementation status groups

### Completed milestones

- Documentation and compatibility boundaries are established in this document, the compatibility reference, the compatibility matrix, the roadmap, and the generator README.
- Runtime metadata preservation for rule parameters, returns, locals, labels, and raw call arguments is implemented.
- Explicit runtime literal policies cover untyped positional, untyped named, typed positional, and typed named binding.
- Generated-C# simple positional literal binding is implemented only for its documented opt-in path; it keeps exact arity, limited literal conversion, one atomic seed batch, conservative generated `Parse(...)`, and unchanged public parser method signatures.
- Raw argument splitters, named argument splitting, duplicate/empty/unsupported forms, mapper failure behavior, policy exceptions, rollback, and state-key edge cases are covered by deterministic tests across the runtime and generated-C# suites.
- Current-rule returns and labeled child result/return helpers are stabilized for the explicit generated-C# helper surface, including present-null versus absent semantics, rollback, and memoization restoration boundaries.

### Current bounded behavior

- Runtime literal binding is opt-in only and never selected by the default runtime policy.
- Generated-C# binding is opt-in only and limited to the documented generated policy/helper path; runtime-inline `Parse(...)` remains metadata-only.
- Accepted literal-policy calls validate the complete call before submitting one atomic managed pending-seed batch.
- Unsupported syntax and conversion cases fail before mutation, either silently in `IgnoreCall` mode or through `ParserRuleCallBindingException` in `Throw` mode.
- Return and label support remains explicit helper-based state, not ANTLR-compatible generated context fields or public rule signatures.

### Future separately designed extensions

- General argument and return integration remains future work when it requires arbitrary expression evaluation, parameter/return/local/label references inside argument clauses, mixed positional/named syntax, policy composition, user-defined types, arrays, generics, enums, Roslyn/general C# resolution, generated typed rule signatures, automatic parent-return propagation, lexer execution, or full ANTLR compatibility.
- Any future extension must update this document, `docs/parser/ANTLRCompatibility.md`, `docs/parser/Antlr4CompatibilityMatrix.md`, `Utils.Parser/ROADMAP.md`, `Utils.Parser.Generators/README.md`, and `docs/parser/INDEX.md` when materially changed.
- Future PR descriptions must state whether behavior, diagnostics, runtime metadata, public API shape, or test strategy changed.


### Generated-C# explicit simple positional rule-call binding

Generated parsers install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(input)` and `ParseWithEmbeddedCode(input, executionContext)` when generation enables simple positional rule-argument binding. The overload `ParseWithEmbeddedCode(input, executionContext, basePolicy)` deliberately calls the generated policy factory with automatic argument binding disabled so the caller-supplied `RuleCallExecutionPolicy` wins unchanged. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path. This generated wrapper is distinct from the reusable runtime policies (`PositionalLiteralRuleCallExecutionPolicy`, `NamedLiteralRuleCallExecutionPolicy`, `TypedPositionalLiteralRuleCallExecutionPolicy`, and `TypedNamedLiteralRuleCallExecutionPolicy`) that callers can install explicitly through a runtime policy.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


## Explicit generated-C# labeled return helper lock-in

Generated-C# parent access to labeled child rule returns is helper-first. Current-rule returns may use the narrow bare `$value` convenience only inside the rule that declares `returns [.. value ..]`, and only through the optional C# transformer in supported parser action locations. Parent rules may inspect completed assignment-labeled child calls with narrow `$c.value` sugar in generated-C# inline parser actions and `@after`, or explicitly with helpers such as `GetRequiredLabeledRuleCallReturn(context, "c", "value")`, `TryGetLabeledRuleCallReturn(context, "c", "value", out object? value)`, `TryGetLabeledRuleCallResult(context, "c", out ParserRuleCallResult? result)`, `GetLabeledRuleCallResults(context, "xs")`, and `GetLabeledRuleCallReturns(context, "xs", "value")`.

Assignment labels (`c=child`) expose the last successful child `ParserRuleCallResult` for that label. List labels (`xs+=child`) expose successful child results in call order. Present-null return values are distinct from missing return keys: required helpers return `null` for present-null values and throw the deterministic parser attribute exception for missing labels or missing return names. Failed alternatives must not leak label state, and memoized child results must restore return values while applying the current successful call-site label. `$c.value`/`$x.value` are supported as assignment-label transformer sugar in generated-C# inline parser actions and `@after`. `$xs.value` is supported separately as read-only list-label projection sugar only when `xs` is a visible `xs+=child` parser-rule list label and every referenced target rule declares `value`; it rewrites to `GetLabeledRuleCallReturns(context, "xs", "value")`, so ordinary C# member access such as `$xs.value.Count` works after the root projection rewrite. `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare `$c`/`$xs` label objects, writes to `$c.value` or `$xs.value`, `@init` label-return reads, semantic-predicate label-return reads, token attributes such as `$t.text`, lexer attributes, typed parser contexts, public ANTLR-style parser rule methods, and general ANTLR attribute compatibility remain unsupported syntax. Conservative `Parse(...)` remains unchanged and `ParserEngine` remains target-language-neutral.
