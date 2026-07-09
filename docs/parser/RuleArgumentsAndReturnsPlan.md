# ANTLR rule arguments and returns integration plan

## Purpose

This document records the design boundary for ANTLR-style rule arguments, rule parameters, rule returns, labels, rollback, and generated-C# opt-in integration in `Utils.Parser`.

The current PR is documentation/design only. It must not change `Parse(...)`, must not add target-language C# evaluation to `ParserEngine`, and must not implement automatic `callee[expr]` evaluation or ANTLR-compatible generated parser signatures.

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

## Proposed implementation phases

### Phase A — docs/design only

- Scope: add this design/audit document and align compatibility, matrix, roadmap, and generator README wording.
- Non-goals: no runtime behavior changes, no generated binding, no tests beyond documentation review.
- Expected tests: not run for documentation-only changes unless examples/snippets are made compilable and need verification.
- Docs to update: this document, `docs/parser/INDEX.md`, compatibility docs, roadmap, generator README as needed.
- Merge criteria: boundaries are explicit; no wording implies automatic `callee[expr]` evaluation or typed returns.

### Phase B — generated-C# simple positional argument binding

- Scope: generated-C# opt-in support for simple positional `callee[expr]` binding where expressions are deliberately limited and evaluated in the generated caller context.
- Non-goals: no `Parse(...)` change, no public ANTLR-compatible rule signatures, no arbitrary target-language model, no named binding unless explicitly scoped.
- Expected tests: deterministic generated-C# tests for one-argument and multi-argument success, failed arity, unsupported expression, rollback-safe seeds, and conservative `Parse(...)` unchanged.
- Docs to update: this document, compatibility reference, matrix, generator README, roadmap.
- Merge criteria: all-or-none seed validation; no target-language C# logic in `ParserEngine`; transformer boundaries preserved.

### Phase C — argument edge-case tests

- Scope: harden raw argument splitting, unsupported forms, nested syntax, named/positional separation, duplicate handling, and diagnostics/exception behavior for the selected opt-in path.
- Non-goals: no new semantics outside the Phase B feature envelope.
- Expected tests: deterministic unit tests for nested brackets, strings, escapes, empty/missing arguments, duplicate names, mapper failures, and memoization keys.
- Docs to update: compatibility reference and matrix edge-case notes.
- Merge criteria: edge cases are either supported with tests or rejected/documented deterministically.

### Phase D — returns / labels stabilization

- Scope: stabilize generated-C# helper use around captured returns, assignment/list labels, present-null vs absent semantics, and memoization hashing constraints.
- Non-goals: no typed generated return fields, no bare label variables, no automatic parent return assignment.
- Expected tests: generated-C# opt-in tests for child returns after `@after`, assignment/list label projections, rollback, memoization hit restoration, and unsupported return object behavior.
- Docs to update: embedded-code docs, compatibility reference, matrix, generator README, roadmap.
- Merge criteria: successful child calls snapshot returns consistently and failed alternatives do not leak stale labeled results.

### Phase E — arguments + returns integration

- Scope: integrate generated-C# opt-in argument binding with return and label access so caller-evaluated arguments, child returns, and labels compose predictably.
- Non-goals: no full ANTLR attribute model, no runtime-inline lexer execution, no external side-effect rollback, no conservative `Parse(...)` changes.
- Expected tests: end-to-end generated-C# opt-in scenarios covering argument evaluation before child entry, child parameter visibility, return capture after `@after`, label binding, rollback, and memoization.
- Docs to update: all parser compatibility docs, generator README, roadmap, and this plan's status notes.
- Merge criteria: evaluation order matches the target model and unsupported forms remain deterministic.

## Documentation impact checklist

- `docs/parser/INDEX.md` must link this document.
- `docs/parser/ANTLRCompatibility.md` must distinguish metadata, explicit runtime policies, generated-C# helpers, and unsupported automatic binding.
- `docs/parser/Antlr4CompatibilityMatrix.md` must keep rule parameters, rule arguments, returns, labels, and embedded-code attribute rows aligned with this plan.
- `Utils.Parser/ROADMAP.md` must mention ANTLR rule arguments and returns integration as progressive work.
- `Utils.Parser.Generators/README.md` must not imply helper APIs are automatic ANTLR argument binding.
- PR descriptions for future phases must state whether behavior, diagnostics, runtime metadata, public API shape, or test strategy changed.


### Generated-C# explicit simple positional rule-call binding

Generated parsers can explicitly install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(...)` when generation enables simple positional rule-argument binding. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


## Explicit generated-C# labeled return helper lock-in

Generated-C# parent access to labeled child rule returns is helper-first. Current-rule returns may use the narrow bare `$value` convenience only inside the rule that declares `returns [.. value ..]`, and only through the optional C# transformer in supported parser action locations. Parent rules must inspect completed labeled child calls explicitly with helpers such as `GetRequiredLabeledRuleCallReturn(context, "c", "value")`, `TryGetLabeledRuleCallReturn(context, "c", "value", out object? value)`, `TryGetLabeledRuleCallResult(context, "c", out ParserRuleCallResult? result)`, `GetLabeledRuleCallResults(context, "xs")`, and `GetLabeledRuleCallReturns(context, "xs", "value")`.

Assignment labels (`c=child`) expose the last successful child `ParserRuleCallResult` for that label. List labels (`xs+=child`) expose successful child results in call order. Present-null return values are distinct from missing return keys: required helpers return `null` for present-null values and throw the deterministic parser attribute exception for missing labels or missing return names. Failed alternatives must not leak label state, and memoized child results must restore return values while applying the current successful call-site label. `$c.value`, `$x.value`, `$xs.value`, `$child.value`, and `$rule.value` remain unsupported syntax; a future PR may add `$c.value` only as transformer sugar after this explicit helper behavior is locked. Conservative `Parse(...)` remains unchanged and `ParserEngine` remains target-language-neutral.
