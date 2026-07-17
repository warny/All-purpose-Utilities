# Utils.Parser Roadmap

## Purpose

Utils.Parser is evolving toward a modern, ANTLR4-like parsing framework and tooling platform through conservative, incremental, and auditable steps.

This roadmap is authoritative for project direction and must be updated whenever meaningful architectural, runtime, metadata, tooling, or public API changes are introduced.

## Public API maturity policy

`Utils.Parser` is currently considered pre-release.

Until an explicit API stabilization milestone is declared:

- public API changes are allowed;
- compatibility preservation is preferred but not mandatory;
- reducing API debt is preferred over preserving accidental contracts;
- API changes must remain explicit, documented, and reviewable.

Public API evolution must not be used to justify changes to runtime authority, parse-tree compatibility, diagnostics format, or unsupported execution semantics.

## Long-term vision

The long-term target is a general parsing framework that can support:

- ANTLR4-like grammar support,
- grammar conversion and compilation,
- code generation,
- syntax highlighting,
- IDE/editor tooling,
- diagnostics tooling,
- parser runtime experimentation.

Long-term research possibilities may eventually include:

- shared lookahead execution,
- continuation-driven parsing,
- reduction of duplicated parsing work,
- forward-only parsing modes,
- potential parallel exploration of alternatives.

These are long-term possibilities only. They are not current runtime features.

## Current runtime philosophy

The current runtime prioritizes:

- determinism,
- auditability,
- compatibility,
- conservative execution,
- observable behavior preservation,
- parse-tree stability,
- diagnostic stability,
- incremental PR discipline.

At this stage, the runtime is intentionally not a speculative graph parser.

## Current runtime state

Current capabilities and responsibilities:

- `ParserEngine` remains the final parse authority.
- `AlternativeScheduler` is the sequential orchestration layer.
- `ScheduledAlternativeExecutor` performs local alternative execution.
- `ParserStateRegistry` manages visited states, completed invocation reuse, and metadata.
- `ActiveParseState` provides local descriptive branch state.
- `ParserLookaheadProbe` and `ParserLookaheadCache` provide conservative shallow lookahead.
- Runtime feature policies are present.
- Shared embedded-code runtime discovery metadata is present for parser semantic predicates and inline parser actions, including explicit unsupported reasons for out-of-scope embedded code.
- Passive runtime observation hooks are available via policy configuration and remain non-authoritative.
- Semantic predicate evaluator abstraction is present.
- Runtime expression-backed semantic predicate evaluator is available as an explicit optional adapter without changing default parser behavior.
- Semantic predicate evaluation now returns structured outcomes so `ParserEngine` can emit fallback `UP1006` or detailed embedded-code diagnostics such as `UP1026` without giving evaluators direct `DiagnosticBag` access.
- Parser action executor abstraction is present.
- Parser execution-state manager abstraction is present as a policy component; the default implementation is no-op, generated policies can expose `Fork()` / `CopyFrom(...)` capture/restore and semantic state keys, and `ParserEngine` uses those keys for completed-result memoization and managed execution-state rollback around parser backtracking attempt boundaries.
- Runtime expression-backed parser action executor is available as an explicit optional adapter without changing default parser behavior or granting actions parse-control authority.
- Continuation metadata is present.
- Shared-prefix metadata is present.
- Parser feature capabilities metadata is present.
- ANTLR4 grammar bootstrap/conversion support is present.
- Syntax colorisation descriptor DTO public contracts are hardened to read-only collection exposure (`IReadOnlyList<T>`), with mutation remaining internal to parser conversion flow.
- Visual Studio syntax colorization descriptor DTO public contracts are hardened to read-only collection exposure (`IReadOnlyList<T>`), with descriptor-population mutation remaining internal to conversion flow.
- `Rule.Kind` is now an immutable part of the public `Rule` record; mutable resolution state is kept inside internal `RuleResolutionBuilder` instances so `RuleResolver` produces final resolved rule projections without mutating public rule objects in place.
- Runtime invariant documentation exists.
- Branch outcome documentation exists.
- Parser tests are organized to reflect runtime contracts.
- Runtime observation and export contract is documented (`docs/parser/RuntimeObservationAndExportContract.md`).
- Diagnostics/observation correlation boundaries are documented (`docs/parser/DiagnosticsObservationCorrelation.md`) as descriptive-only and non-authoritative.
- Source-position contracts are centralized in the shared `Utils.Parser.Source` package: `SourceCodeLocation` / `SourceCodeRange` remain human-readable diagnostic/display locations, while `SourceLocation` / `SourceSpan` preserve runtime text offsets and spans for tokens and parse nodes.
- Source-position contracts are intentionally split between runtime coordinates (`SourceLocation`, `SourceSpan`) and human-readable source coordinates (`SourceCodeLocation`, `SourceCodeRange`). Runtime coordinates carry absolute text offsets for tokens/parser operations, while human-readable coordinates are used for diagnostics/tooling when no canonical source offset is available. These contracts must not be merged without a dedicated design review.
- `ParserRuleInvocationFrame`, `ParserRuleInvocationDescriptor`, `IParserRuleInvocationFrameManager`, and `NullParserRuleInvocationFrameManager` are available as passive rule invocation-frame infrastructure for future rule metadata execution; `ParserEngine` enters and exits frames for actual rule execution, can attach descriptors built from currently represented rule metadata, including preserved raw locals and raw throws/catch/finally metadata, and lifecycle contexts can expose the current frame. Generated C# lifecycle hooks can explicitly use helper methods to read or write that frame's locals store in opt-in paths, but rule parameters, returns, locals, throws/catch/finally metadata, and rule options remain metadata-only unless hook code explicitly stores frame-local values, and no automatic allocation or typed rule-local generation is active.
- `ParserExecutionContextCopier<TContext>` is available as a public runtime helper for parser execution-context snapshot/fork/commit. Generated execution contexts expose it through internal `Fork()`, `CopyFrom(...)`, and `GetExecutionStateKey()` helpers. Generated policies install the generated execution-state manager for generated parser execution contexts, so parser predicates, inline parser actions, and lifecycle hooks share the same state-aware memoization and rollback infrastructure. `ParserEngine` calls `Capture()` and `Restore()` on the state manager during parser backtracking attempt boundaries. Generated policies install a generated rule lifecycle executor only when parser `@init` or `@after` hooks are present; otherwise the base/no-op lifecycle executor is preserved.
- Rule-call argument clauses (`callee[...]`) are preserved as raw metadata on `RuleRef.RawArguments` (outer brackets excluded). Both the runtime ANTLR converter and the source-generator G4 parser preserve the raw text. Reported with `UP1037 RuleCallArgumentsPreservedAsMetadata`. Arguments are not evaluated or bound by default and do not populate invocation-frame parameters unless an explicit rule-call policy requests managed seeds. Generated `Parse(...)` and rule signatures are unchanged. Explicit parameter seeding via helpers or the separate opt-in positional/named literal call policies are the supported mechanisms.
- Rule-call argument text is additionally carried into `ParserRuleCallResult.RawArguments` on the parent frame's last completed child call result via `StackParserRuleInvocationFrameManager.AnnotateLastChildCallRawArguments` (called by `ParserEngine.TryParseRuleRef` after every successful parser-rule call). Generated C# opt-in helpers `TryGetLastRuleCallRawArguments` and `GetLastRuleCallResult(context)?.RawArguments` expose the metadata to parent lifecycle hooks. Call-site metadata is rollback-safe and memoization-safe.

- Successful child parser-rule calls now capture immutable return snapshots after child `@after` and can be retained in the parent invocation frame by rule-reference label. Assignment labels use last-successful-result-wins; list labels append successful results in order. The generated C# opt-in path exposes generic assignment/list result and return helpers, including present-null versus absent semantics and ordered missing-key-skipping list projection. Labeled state participates in managed rollback, memoization restoration, and conservative volatile hashing for unsupported return objects; current call-site labels are reapplied after memoized calls. This supports only the later-documented narrow generated-C# read forms; it does not add bare `$x`/`$xs`, implicit variables, automatic return binding, or lexer returns, and conservative `Parse(...)` is unchanged.
- `SetNextRuleParameterFromRawArguments(context, ruleName, parameterName, rawArguments, map)` is a generated C# opt-in helper for explicit user-controlled mapping of raw call-site text into a future child seed; requires a caller-supplied delegate; does not evaluate automatically; null `rawArguments` returns `false`; mapper exceptions propagate; both lifecycle and inline-action overloads are available.
- `ParserRawArgumentSplitter.SplitTopLevel(rawArguments)` is a runtime utility for top-level comma splitting of raw argument text; respects nested `()`, `[]`, `{}` and quoted strings; syntactic only; no evaluation. Exposed through generated C# opt-in helpers `SplitRawArgumentsTopLevel` and `TrySplitLastRuleCallRawArguments` (lifecycle + inline-action overloads).
- `ParserRawArgumentParameterMapping` is a runtime record struct (ParameterName, Index, Map) used with `SetNextRuleParametersFromRawArguments(context, ruleName, rawArgs, params mappings)` for explicit positional multi-argument seeding; validates all indices before seeding; last mapping wins for duplicates; both lifecycle and inline-action overloads generated.
- `ParserRawNamedArgumentSeparatorMode` enum + `ParserRawNamedArgumentSplitter.SplitNamedTopLevel` parse named key–value raw argument forms (`value: 42` / `value = 42`); throws `FormatException` on missing separator or empty key; duplicate keys: last wins. `TrySplitLastRuleCallNamedRawArguments` wraps this. `ParserRawNamedArgumentParameterMapping` record struct (ParameterName, ArgumentName, Map) + `SetNextRuleParametersFromNamedRawArguments` generated helper maps named entries to child seeds; validates all before seeding; missing ArgumentName returns false with no partial seed; lifecycle + inline-action overloads.

Clarifications that must remain true:

- `ParserEngine` owns final parse acceptance.
- `ParserEngine` owns final diagnostics.
- `ParserEngine` owns final parse-tree outcome.
- `AlternativeScheduler` does not own global parse success/failure.
- `ActiveParseState` is not a runtime invocation frame.
- Continuations are metadata-only.
- Shared-prefix infrastructure is metadata-only.
- Runtime observers are descriptive only and do not control scheduling, pruning, parse acceptance, parse-tree outcomes, or diagnostics authority.

## Explicit non-goals

The following must not be introduced prematurely:

- no `ParserEngine2`,
- no undocumented public API break,
- no parse-tree shape break,
- no diagnostic format break,
- no speculative execution,
- no parser graph execution,
- no GLL or adaptive LL runtime,
- no continuation replay,
- no complete ANTLR transactional rollback,
- no semantic-state rollback outside ordinary parser alternative attempts,
- no async runtime,
- no runtime parallelism,
- no action buffering,
- no hidden semantic state,
- no large unreviewable refactors.

## Public API change rules

Public API changes are allowed only when at least one of the following applies:

- remove architectural debt;
- clarify ownership or responsibility boundaries;
- simplify usage patterns;
- eliminate temporary abstractions;
- prepare future stabilized APIs.

Every API-changing PR must include:

- explicit API surface summary;
- migration notes when applicable;
- compatibility impact assessment;
- documentation impact statement;
- tests for public behavior.

## Architectural principles

- Preserve observable behavior unless an API-changing or behavior-changing PR explicitly documents the change.
- Document every new invariant.
- Add invariant tests for runtime changes.
- Prefer small, reviewable PRs.
- Separate documentation-only, test-only, refactor-only, API-changing, and functional PRs.
- Never activate prepared metadata accidentally.
- Keep metadata-only infrastructure visibly metadata-only.
- Avoid wrappers and abstractions unless they remove real ambiguity.
- Do not introduce architecture layers without a clear invariant-driven reason.
- Optimize only after behavior is locked down.
- When parser and lexer paths share a stable algorithm, prefer composition: a common engine owns the invariant algorithm, while a parser- or lexer-specific strategy owns only the true variation points.
- Use small immutable context values for traversal state instead of large mutable state objects shared across recursive calls.
- Prefer immutable descriptors when parser/lexer differences are declarative, such as generated names, context types, signatures, success expressions, fallback expressions, or transformation locations.
- Do not hide parser/lexer differences behind `isLexer` booleans or domain switches spread throughout a common engine.
- Avoid abstract base classes that require most of the algorithm to be overridden.

## Parser/lexer specialization refactor direction

Several generator paths share a similar global algorithm while still carrying real parser/lexer variation points. The composition-based direction remains active and is now implemented for embedded-hook collection and runtime dispatcher emission and generated hook-method emission.

Implemented for embedded-hook collection:

- `EmbeddedHookCollector` owns the common collection algorithm: collection creation, strategy-provided rule/root traversal, recursive traversal of common `G4Content` node shapes, hook accumulation, and ordered transformation through the existing `TransformEmbeddedCode(...)` boundary.
- `IEmbeddedHookCollectionStrategy` defines the parser/lexer variation points used by the collector.
- `ParserEmbeddedHookCollectionStrategy` owns parser rule enumeration, priority ordering, direct-left-recursive root preparation, parser quantifier/negation index semantics, parser method-name prefixes, parser transformation locations, and `EmbeddedCodeHook.CreateParser(...)`.
- `LexerEmbeddedHookCollectionStrategy` owns lexer rule enumeration across the default rule list and extra modes, source-order alternative traversal, lexer quantifier/negation index semantics, lexer method-name prefixes, lexer transformation locations, and `EmbeddedCodeHook.CreateLexer(...)`.
- `HookTraversalPosition` is the immutable value that carries alternative and element indexes, including the historical `-1` sentinels, through recursive traversal.
- Explicit wrappers `CollectEmbeddedCodeHooks(...)` and `CollectLexerEmbeddedCodeHooks(...)` remain as the parser/lexer selection points and delegate to the shared collector.

Invariants now locked by tests and architecture guards:

- parser and lexer hook collection share one recursive collector rather than two duplicated traversals;
- parser and lexer runtime dispatchers share one emitter rather than four duplicated dispatcher bodies;
- parser and lexer generated hook methods share one method emitter rather than four duplicated method bodies;
- parser left recursion remains in parser-specific root preparation;
- lexer modes remain in lexer-specific rule enumeration;
- hook method names, owner/kind categories, indexes, sentinels, source order, priority order, and transformation locations are preserved;
- each hook is transformed once, in final collection order, through the existing centralized transformation boundary;
- no `isLexer` / `isPredicate` parameter, broad inheritance hierarchy, or public strategy/collector/dispatcher/method-emitter API is introduced.

Implemented for runtime dispatcher emission:

- `EmbeddedHookDispatcherEmitter` owns the common generated dispatcher algorithm: generated class and method emission, stable hook iteration, owner/kind validation, rule/code/alternative/element comparisons, hook invocation, success return, and fallback return.
- `EmbeddedHookDispatcherDescriptor` owns immutable declarative differences for parser predicates, parser actions, lexer predicates, and lexer actions, including interfaces, context types, return types, signatures, code-property names, success expressions, invocation arguments, and fallback expressions.
- Explicit wrappers `EmitSemanticPredicateEvaluator(...)`, `EmitParserActionExecutor(...)`, `EmitLexerPredicateEvaluator(...)`, and `EmitLexerActionExecutor(...)` remain as the four readable selection points and delegate to the shared emitter.

Implemented for generated hook-method emission:

- `EmbeddedHookMethodEmitter` owns the common generated hook-method algorithm: owner/kind validation, body classification, XML summary, signature, braces, optional parser context locals, centralized `EmitGeneratedEmbeddedCodeBody(...)`, and final spacing.
- `EmbeddedHookMethodDescriptor` owns immutable declarative differences for parser predicates, parser actions, lexer predicates, and lexer actions, including owner, kind, return type, parameters, XML summary, context-local profile, and `ForPredicate(...)` / `ForAction(...)` body factory.
- Explicit wrappers `EmitPredicateHook(...)`, `EmitActionHook(...)`, `EmitLexerPredicateHook(...)`, and `EmitLexerActionHook(...)` remain as the four readable selection points and delegate to the shared emitter.
- Lifecycle hooks remain separate because they use `LifecycleHook`, `ParserRuleLifecycleContext`, lifecycle phases, `internal` visibility, and lifecycle-specific locals rather than `EmbeddedCodeHookOwner` / `EmbeddedCodeHookKind`.

Implemented for the transformation boundary:

- the internal `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)` owns the common,
  target-independent raw-code transformation and result validation step;
- generated C# and runtime expression preparation consume the same validated
  `TransformedEmbeddedCode` boundary while constructing their specialized contexts explicitly;
- `GeneratedEmbeddedCodeBody` and `CSharpEmbeddedCodeInjector` remain generator-only, while
  `ExpressionEmbeddedCodePreparer` and `IExpressionCompiler` retain runtime symbol, lambda, and
  compilation ownership;
- architecture guards prohibit a parallel direct transformer path and prohibit target dependencies
  in the common pipeline.

Completed for the hook phase model:

- collected parser and lexer hooks retain grammar text as `RawEmbeddedCode`;
- the shared pipeline produces validated `TransformedEmbeddedCode` through an explicit immutable transition;
- generated-code targets consume only the transformed phase, and the former ambiguous hook member has been removed.

The [2026-07 embedded-code architecture audit](./DONE-2026-07-17.md) is complete. All technical
findings 1–13, including items 8–11 in this refactor sequence, have been addressed. Item 14 remains
an ongoing documentation maintenance rule for future pipeline changes. Future parser evolution must
be recorded separately rather than appended to the completed audit unless a regression invalidates
one of its conclusions.

Future work must continue to keep the following differences explicit: parser left recursion, alternative priority ordering, lexer modes, quantifier and negation index semantics, generated names, transformation locations, runtime context types, method signatures, success results, and fallback calls. These differences must not be hidden behind a single `isLexer` flag or scattered parser/lexer switches inside a shared engine.

Any future step must preserve generated C# shape, hook order, indexes, transformer invocation count and order, fallback behavior, diagnostics, public API, parser authority, and lexer/runtime semantics. Each step should be delivered as a separate PR or, at minimum, as a separate auditable change.

## Roadmap phases

### Phase 0 — Stabilization and invariant documentation

**Status: mostly complete. Ongoing maintenance required.**

Scope of completed/recently consolidated work:

- runtime memoization invariants,
- runtime state ownership,
- lifecycle contracts,
- branch outcome model,
- parser test suite normalization.

Ongoing expectation:

- keep documentation and tests synchronized with invariant changes.

### Phase 1 — Alternative selection contract hardening

**Status: complete.**

Goal: document and lock the current alternative selection model before changing it.

Scope:

- branch equivalence,
- branch selection,
- pruning eligibility,
- precedence interaction,
- completed-state comparison,
- local outcome prioritization,
- diagnostics around pruning and backtracking.

Current clarification status:

- local alternative comparison order is explicitly documented (longest match, then priority, then declaration order),
- pruning is explicitly documented as orchestration-only and non-syntax-authoritative,
- structural branch equivalence limits are explicitly documented as conservative and non-semantic-proof.
- passive runtime observation is available for tooling/audit visibility and is explicitly non-authoritative (no control over scheduling, pruning, parse acceptance, parse-tree outcomes, or diagnostics authority).
- observation contracts are normalized as immutable descriptive payloads; observer exceptions are isolated from parser execution semantics.
- observation and export contract is formally documented in `docs/parser/RuntimeObservationAndExportContract.md`, covering runtime observation guarantees, export format stability, and non-authoritative boundaries.
- diagnostics/observation correlation boundaries are explicitly documented as optional one-way descriptive identifiers only, with no replay/navigation authority.

Allowed work:

- comments,
- documentation,
- tests,
- minimal helper extraction.

Forbidden work:

- changing selection algorithms,
- changing pruning rules,
- changing precedence behavior,
- introducing selection policies.

### Phase 2 — Diagnostics model consolidation

**Status: complete.**

Goal: make diagnostics ownership and propagation more explicit.

Scope:

- local diagnostics,
- global diagnostics,
- diagnostics emitted during failed branches,
- diagnostics from pruning,
- diagnostics for unsupported ANTLR4 constructs,
- stability of diagnostic codes and format.

Allowed work:

- documentation,
- tests,
- small internal cleanup.

Forbidden work:

- changing diagnostic format,
- changing observable diagnostic behavior without an explicit bug-fix PR.

Current clarification status:

- diagnostics ownership and authority boundaries are explicitly documented,
- local vs global diagnostics lifecycle is explicitly documented,
- orchestration diagnostics (pruning/backtracking) are explicitly separated from engine-authoritative parse diagnostics,
- compatibility diagnostics are explicitly documented as independent from parse success/failure.
- unsupported ANTLR4 lexer-command constructs are now surfaced via explicit deterministic compatibility diagnostics.
- generator/runtime diagnostic parity for `UP1001`-`UP1006` was tightened with parity tests while preserving parsing semantics and recovery determinism.
- shared ANTLR4 prequel DTOs now include shared neutral prequel validation facts in `Utils.Parser.Antlr4.Common`; runtime and generator continue to own conversion into `ParserDiagnostics`, and parsing remains intentionally separate.
- parser diagnostics now use immutable composed value objects (`DiagnosticDetails`, `DiagnosticSpan`, `SourceCodeLocation`, and `SourceCodeRange`) to separate diagnostic content, source offsets, and human-readable source locations while preserving diagnostic ownership and emission semantics.
- `SourceCodeLocation` and `SourceCodeRange` are now hosted by a dedicated shared `Utils.Parser.Source` package so diagnostics, runtime, generators, and future tooling surfaces can share source-location contracts without depending on diagnostics.
- `SourceLocation` and `SourceSpan` were moved into the shared `Utils.Parser.Source` package alongside `SourceCodeLocation` and `SourceCodeRange`, centralizing source-position contracts while keeping token/runtime behavior unchanged.

### Phase 3 — Lookahead contract consolidation

**Status: complete.**

Goal: keep lookahead shallow, conservative, and syntax-oriented while clarifying future options.

Scope:

- `ParserLookaheadProbe`,
- `ParserLookaheadCache`,
- negative shortcut rules,
- epsilon handling,
- parser rule references,
- predicates/actions exclusion,
- case-insensitive matching.

Allowed work:

- tests,
- documentation,
- internal cleanup.

Forbidden work:

- deep parser-rule speculative lookahead,
- evaluating predicates during lookahead,
- executing actions during lookahead.

Current clarification status:

- lookahead ownership boundaries are explicit (`ParserLookaheadProbe` / `ParserLookaheadCache` / engine authority),
- advisory vs authoritative lookahead outcomes are explicitly documented,
- fallback-to-parse behavior is documented for `RequiresParse`, `Unknown`, and ambiguous/epsilon-sensitive outcomes,
- cache semantics are explicit as metadata-only and non-authoritative,
- invariant tests cover conservative behavior for parser-rule references, predicates/actions, and ambiguous shallow outcomes.

### Phase 4 — Shared-prefix metadata maturity

**Status: complete.**

Goal: improve shared-prefix metadata quality without activating execution.

Scope:

- detection,
- plans,
- formatting,
- validation,
- metadata scenarios,
- ambiguity documentation.

Allowed work:

- metadata tests,
- metadata validation,
- documentation.

Current clarification status:

- shared-prefix metadata is explicitly documented as descriptive and non-authoritative,
- metadata ownership and lifecycle boundaries are explicitly documented,
- non-authority guarantees are explicit (no replay, no continuation execution, no branch merging, no semantic-equivalence guarantee),
- future activation prerequisites are documented as boundaries only.

Forbidden work:

- shared-prefix execution,
- branch replay,
- continuation resume,
- action replay.

### Phase 5 — Continuation metadata maturity

**Status: complete.**

Goal: clarify and mature continuation metadata as descriptive infrastructure only.

Scope:

- continuation identity,
- continuation formatting,
- ownership,
- relation to `ActiveParseState`,
- relation to registry metadata,
- preparation-layer extraction outside scheduler,
- scheduler transport-only boundaries.

Current clarification status:

- structural extraction moved to preparation layer,
- scheduler consumes descriptors only,
- `ParserContinuationFactory` no longer traverses grammar structures,
- continuation metadata remains descriptive-only and non-authoritative,
- ownership boundaries are documented and aligned with runtime behavior.

Allowed work:

- metadata tests,
- documentation consistency,
- simplification.

Forbidden work:

- continuation replay,
- runtime resume,
- invocation frame model.

Completed in PR #234:

- move continuation creation out of scheduler,
- extract continuation descriptors in preparation layer,
- remove scheduler metadata generation responsibility.

### Phase 6 — ANTLR4 compatibility expansion

**Status: in progress.**

Current clarification status:

- converter now emits explicit diagnostics for unsupported grammar options instead of implicit acceptance,
- `tokens` / `channels` prequel constructs now emit deterministic partial-support diagnostics during conversion,
- labels targeting non-rule-reference elements now emit deterministic compatibility diagnostics instead of being accepted silently,
- compatibility documentation is aligned with explicit parsed/normalized/rejected boundaries,
- rule `locals [...]` clauses now emit deterministic explicit compatibility diagnostics (`UP1008 RuleLocalsIgnored`) instead of generic silent metadata discard; their raw declarations and lexical names are preserved as passive runtime metadata, and the generated C# opt-in lifecycle path allocates missing declared names as untyped `null` entries before `@init`. Explicit `context.InvocationFrame` helper access remains required; typed local generation, implicit action variables, and conservative `Parse(...)` allocation remain unsupported.
- rule `throws`, `catch`, and `finally` exception metadata now emits deterministic explicit compatibility diagnostics (`UP1023 RuleExceptionMetadataIgnored`) while preserving raw passive runtime metadata without changing parser exception behavior or executing handlers.
- semantic predicate default-policy behavior is explicitly documented as runtime-policy-driven (`ISemanticPredicateEvaluator`) with deterministic `UP1006` coverage, and precedence predicates are documented separately as non-generic predicate evaluation flow.
- embedded ANTLR code execution model is documented as a future-safe boundary between source-generation C# and runtime-inline expression-compilation paths, including multi-project responsibilities and the shared target of preparing executable artifacts before parsing.
- shared embedded-code diagnostic taxonomy is defined for future runtime, generator, and tooling paths without enabling execution.
- parser action execution now returns structured outcomes so `ParserEngine` can emit fallback `UP1005` or detailed embedded-code diagnostics without giving executors direct `DiagnosticBag` access.
- generator/runtime parity characterization tests now document shared supported grammar facts and known metadata divergences without changing runtime, diagnostics, parse-tree shape, or scheduler behavior.
- generator-side AST now preserves grammar prequel metadata (`import`, `tokens`, `channels`, and grammar-level actions including scoped targets) for parity/audit visibility while keeping runtime behavior and emitted C# unchanged.
- generator-side AST now preserves rule lifecycle prequel metadata (`@init` / `@after`) for parity/audit visibility while keeping runtime behavior and emitted C# unchanged.
- shared ANTLR prequel metadata model extraction is complete for options/imports/actions/tokens/channels through mapper-only DTO sharing; runtime and generator parsing remain intentionally duplicated.
- a shared netstandard2.0 ANTLR prequel DTO project was introduced, while runtime and generator parsing remain intentionally separate.
- runtime advanced-configuration public API was consolidated so `ParserRuntimeFeaturePolicy` is the single explicit runtime feature-entry point for semantic predicates, parser actions, and runtime observers.
- current expression-backed predicate/action adapters are documented as useful intermediate runtime adapters with opportunistic compilation, not as the final prepare-before-parse architecture.
- public embedded-code preparation boundary contracts now model raw source, target path, contextual symbols, preparation status, diagnostics metadata, and path-specific artifacts without changing default runtime behavior. `Utils.Parser.Expressions` now provides an expression-backed preparer, an explicit registry builder for parser-model validating predicates and inline parser actions, registry-backed runtime adapters for prepared artifacts, and an opt-in runtime policy builder that assembles those components; automatic default `ParserEngine` wiring remains unimplemented.
- `Utils.Parser.Generators` now implements the separate source-generator C# path for parser semantic predicates and inline parser actions by emitting generated hook methods, generated runtime policy dispatchers, and an explicit `ParseWithEmbeddedCode(...)` helper; generated predicate hooks support expression bodies and statement blocks with `return`, generated action hooks support multi-statement and multi-line bodies, generated parser headers can inject ordinary C# header content such as `using` directives, generated parser footers can inject trailing C# source near the end of the generated file, generated `Parse(...)` remains conservative by default, and hook dispatch is aligned with the runtime indexes covered by generated Roslyn + `ParserEngine` tests.
- embedded-code preparation/generation contracts are available.
- expression-backed preparation, prepared artifact registry/adapters, parser-definition registry builder, and prepared runtime policy builder are available for the runtime-inline expression opt-in path.
- generated C# hooks, generated hook dispatch hardening, shared runtime metadata alignment, cross-path regression coverage, generated C# body support, generated execution contexts with fresh default `ParseWithEmbeddedCode(string)` creation, limited parser `@header` / `@parser::header` source-file injection with generator warning `UP1035`, limited parser `@members` / `@parser::members` execution-context injection with generator warning `UP1031`, limited parser `@footer` / `@parser::footer` trailing source injection with generator warning `UP1036`, and generator warning `UP1029` for visible unsupported embedded-code constructs are available for parser semantic predicates and inline parser actions.
- `ParserExecutionContextCopier<TContext>` is available as a preparatory runtime copy primitive for generated execution-context snapshot/fork/commit work, and generated execution contexts now expose internal `Fork()`, `CopyFrom(...)`, and `GetExecutionStateKey()` helpers. `[ParserExecutionStateIgnored]` is available to mark instance fields that must be excluded from copying and hashing (e.g. infrastructure fields such as `_frameManager` on generated contexts). `IParserExecutionStateManager` is available through required `ParserRuntimeFeaturePolicy.ExecutionStateManager`; the default manager is no-op and returns `ParserExecutionStateKey.Stateless`, generated policies install a manager backed by `Fork()` / `CopyFrom(...)` and generated state hashing, and callers that directly instantiate `ParserRuntimeFeaturePolicy` must provide the required manager explicitly. `ParserEngine` uses current state keys for completed-result memoization, stores post-rule snapshots in completed results for memoization-hit restoration, and captures/restores managed parser execution state around parser backtracking attempt boundaries. `IParserRuleLifecycleExecutor` is now available through required `ParserRuntimeFeaturePolicy.RuleLifecycleExecutor`; generated policies install a lifecycle executor for grammars that declare `@init` or `@after`; the no-op executor is used for grammars without lifecycle hooks. `IParserRuleInvocationFrameManager` is also available through required `ParserRuntimeFeaturePolicy.RuleInvocationFrameManager`; the default manager is no-op/passive, invocation frames can carry passive descriptors for represented rule metadata, and descriptors do not activate rule parameter, return, local, rule option, or exception metadata execution. This is not complete ANTLR transactional semantics and is not wired into action buffering or external side-effect rollback.
- Parser rule invocation frames now expose explicit `Parent` and `Depth` properties, and `StackParserRuleInvocationFrameManager` is available as a stack-aware manager that tracks the active call chain. Generated C# opt-in runtime policies (`CreateRuntimePolicy`) now install `StackParserRuleInvocationFrameManager` automatically; lifecycle hooks in `ParseWithEmbeddedCode(...)` can observe `context.InvocationFrame.Parent` and `context.InvocationFrame.Depth`. The frame stack is implicitly rollback-aware: the engine's `try/finally` structure ensures every `ParseRule` call's frame is exited regardless of success, so failed alternatives, quantifier iterations, negation probes, and memoization hits cannot leave stale frames. The conservative `Parse(...)` policy continues to use `NullParserRuleInvocationFrameManager.Instance`. Rule parameters remain metadata-only; `StackParserRuleInvocationFrameManager` is preparatory infrastructure only for future return propagation and argument support.
- Generated C# execution contexts now expose explicit rule-return frame helpers (`GetRuleReturn`, `TryGetRuleReturn`, `SetRuleReturn`, `GetRuleReturnDescriptors`) alongside the existing rule-local helpers. Generated `@init` and `@after` lifecycle hooks can explicitly read and write untyped return entries on the active invocation frame. Returns are not auto-allocated, not typed, not exposed as implicit variables, not propagated to caller frames, and `$rule.value` is supported only as a generated embedded-code read-only helper rewrite. `UP1007 RuleReturnsIgnored` continues to fire with updated wording. Generated `Parse(...)` remains conservative; `ParseWithEmbeddedCode(...)` is the opt-in path. Rule parameters and exception metadata remain non-executable.
- Explicit child-rule parameter seeding infrastructure is available. `ParserRuleParameterSeedStore` captures pending parameter seeds per rule name; seeds are stored on the caller frame, consumed and copied into the matching child frame when `StackParserRuleInvocationFrameManager.Enter` is called, and are rollback-safe — the managed execution-state snapshot includes seed state (synced via `GetCurrentPendingSeeds`/`SyncPendingSeedsToCurrentFrame` before each `Capture()`/`GetCurrentStateKey()` call and after `Restore()`). Generated C# execution contexts expose `SetNextRuleParameter(context, ruleName, parameterName, value)` and `ClearNextRuleParameters(context, ruleName)` helpers in both lifecycle-hook form (accepting `ParserRuleLifecycleContext`) and inline-action form (accepting `ParserActionExecutionContext`, routing through the instance `_frameManager` field). Both overloads are rollback-safe. This is not ANTLR-compatible argument passing; `callee[expr]` is not evaluated; `$param` is supported only as a generated C# read-only current-rule typed helper access; generated parser signatures are unchanged.
- `ParserRuleParameterDescriptor.Name` now contains the lexical parameter name (e.g. `value` for `rule[int value]`) extracted using the same top-level comma split and final-identifier strategy as local and return descriptors. Raw declarations are preserved verbatim. Generated C# execution contexts expose `GetRuleParameter(context, name)`, `TryGetRuleParameter(context, name, out value)`, and `GetRuleParameterDescriptors(context)` helpers. The source-generator's internal G4 parser now preserves raw rule parameter metadata and emits it in the generated `Rule(...)` definition. Parameters are not auto-bound; rule call arguments are not evaluated or passed; generated parser signatures are unchanged; `$param` is supported only as a generated C# read-only current-rule typed helper access.
- `ParserRuleReturnDescriptor.Name` now contains the lexical return name (e.g. `value` for `returns [int value]`) extracted without C# parsing or type inference, using the same top-level comma split strategy as local descriptors. Multiple returns in a single `returns [...]` clause are split into individual descriptors. Raw declarations are preserved verbatim.
- `ParserRuleCallResult` is available as an immutable passive snapshot of return values from a successfully completed child rule invocation. `StackParserRuleInvocationFrameManager` captures the snapshot before the post-rule execution-state snapshot is taken (via `PrepareCallResultForSnapshot`, called by `ParserEngine` before `Capture()`) so that memoization hits correctly restore the call result alongside the rest of the execution context. The call result is stored on the parent frame's `LastCompletedChildCall` and propagated to the managed execution-state snapshot via a callback, enabling rollback-safe behavior: failed alternatives clear stale results on the parent frame, and memoization hits restore the correct child result. Generated C# execution contexts expose `GetLastRuleCallResult(context)` and `TryGetLastRuleCallReturn(context, returnName, out value)` helpers for explicit access. `labeled rule-reference return access remains available only through explicit helper APIs; `$child.value`, `$c.value`, and `$rule.value` are not generated conveniences; typed returns and automatic assignment to parent return dictionaries are not supported.
- Parser rule-reference label metadata is now preserved and exposed end-to-end. `RuleRef.Label` (type `RuleLabel`) carries the label name and additive flag; `RuleRef.LabelName` and `RuleRef.LabelKind` (type `ParserRuleReferenceLabelKind`: `None`, `Assignment`, `List`) derive from it. `ParserRuleCallResult.LabelName` and `ParserRuleCallResult.LabelKind` expose the call-site label after `ParserEngine.TryParseRuleRef` calls `IParserRuleInvocationFrameManager.AnnotateLastChildCallLabel` on every successful child rule completion. The source-generator's internal G4 parser now captures `x=child` and `xs+=child` label prefixes and stores them on `G4RuleRef.LabelName` / `G4RuleRef.LabelIsAdditive`; `GrammarEmitter` emits `Label: new RuleLabel(...)` when a label is present so that generated `BuildDefinition()` definitions include label metadata. Label metadata composes with `callee[...]` raw argument metadata: both can coexist on the same `RuleRef`. Labels are metadata-only: no `$x`, `$x.value`, `$xs`, implicit variables, typed label fields/properties, automatic parse-node storage, automatic return access, automatic binding, automatic argument evaluation, automatic parameter seeding, or generated parser method signatures are added. Labels on non-rule-reference elements continue to emit diagnostic `UP1022 LabelOnNonRuleReferenceIgnored`. Label metadata is included in `ParserRuleCallResult.GetParserExecutionStateHash()` for correct rollback-safe memoization. Generated C# opt-in code can inspect labels explicitly via `GetLastRuleCallResult(context)?.LabelName` and `?.LabelKind`. Conservative `Parse(...)` remains conservative; hooks do not execute and label metadata is not exposed.
- Explicit parser rule-call execution policy infrastructure is available through `IParserRuleCallExecutionPolicy`, `NullParserRuleCallExecutionPolicy`, and `ParserRuleCallExecutionContext`. `ParserEngine` invokes `BeforeRuleCall(...)`, parses the child, applies current-call-site raw argument and label annotations, and then invokes `AfterRuleCall(...)` with success/failure and the completed tracked call result when available, including right-hand self-references dispatched through direct-left-recursive tails. `ParserRuntimeFeaturePolicy.Default` installs the no-op policy. Generated C# preserves a custom rule-call policy from `basePolicy` through `CreateRuntimePolicy(executionContext, basePolicy)` and the new backward-compatible `ParseWithEmbeddedCode(input, executionContext, basePolicy)` overload. This remains an explicit extension point only: no `callee[...]` evaluation, automatic positional/named binding, automatic seed, `$param`, `$x`, `$x.value`, `$rule.value`, typed generated variables, or label-backed parse-node storage is implemented. Policy external side effects are not automatically rolled back; current-call-site result metadata remains rollback/memoization safe.
- the remaining embedded-code work is explicit: lexer predicate/action design; transactional action rollback/buffering design; deeper alignment between the generator `G4Grammar` collector and `EmbeddedCodeRuntimeDiscovery`; and a broader ANTLR corpus.

Embedded-code transactional-state sequence (reference architecture: `docs/parser/EmbeddedCodeTransactionalState.md`):

1. Add parser execution-state manager contract and semantic memoization key. **Complete.**
2. Carry semantic-state snapshots through ordinary scheduled alternatives. **Complete.**
3. Apply transactional state to left-recursive extensions. **Complete.**
4. Apply transactional state to quantifier attempts. **Complete.**
5. Isolate negation probes. **Complete.**
6. Add parser rule lifecycle hooks for `@init` / `@after`. **Complete for source-generator C# opt-in.**
7. Design lexer embedded-code state separately.

- Steps 1–6 are complete for the source-generator C# opt-in path. Managed execution-state rollback is active for all parser backtracking attempt boundaries. Lifecycle hooks fire through the configured `RuleLifecycleExecutor`; grammars without hooks use the no-op executor. Generated policies install the generated execution-state manager for generated parser execution contexts, while the generated rule lifecycle executor is installed only for grammars that declare `@init` or `@after`. Action buffering, replay, top-level parse-rejection rollback for caller-supplied contexts, and external side-effect rollback are not implemented. Completed-result memoization is semantic-state-aware, memoization hits restore stored post-rule state snapshots, and restored parser attempts restore the state key before later cache lookups.
- Unsupported grammar actions and automatic default execution remain not done and must not be documented as complete. Grammar-level `@lexer::*`, simple lexer inline actions, and simple lexer predicates now have limited generated-C# opt-in support only.

Goal: progressively improve ANTLR4 grammar compatibility.

Scope may include:

- grammar parsing coverage,
- lexer commands,
- parser commands,
- labels,
- predicates,
- actions,
- parameters and returns,
- imports,
- `tokenVocab`,
- modes,
- options,
- compatibility matrix updates.

Important constraint:

- runtime support must stay clearly separated from metadata preservation.
- the C# source-generator path and runtime-inline `IExpressionCompiler` path must remain separate; the runtime core must stay conservative and language-neutral.

Allowed work:

- converter support,
- diagnostics for unsupported constructs,
- compatibility matrix updates,
- tests.

Forbidden work:

- pretending unsupported runtime semantics are supported,
- silently ignoring meaningful constructs without diagnostics.

### Phase 7 — Code generation and tooling

**Status: in progress.**

Goal: move toward tooling capabilities once runtime behavior is stable.

Current status includes an explicit prepared expression registry builder and runtime policy builder in `Utils.Parser.Expressions` for callers that opt into the runtime-inline preparation path. The registry builder consumes shared runtime discovery metadata for parser predicates/actions, and the policy builder assembles the preparer, registry builder, no-compile adapters, and `ParserRuntimeFeaturePolicy` without increasing ANTLR support by default. `Utils.Parser.Generators` also has an explicit generated C# opt-in path for parser predicates/actions through generated execution contexts, generated instance hooks, generated dispatchers, explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, and `ParseWithEmbeddedCode(...)`; generated `Parse(...)` remains conservative, and only `ParseWithEmbeddedCode(string)` creates a fresh context implicitly.

Scope:

- grammar-derived metadata,
- code generation experiments,
- syntax highlighting,
- Visual Studio / IDE support,
- parse-tree tooling,
- diagnostics tooling.

Allowed work:

- tooling layers,
- non-runtime infrastructure,
- generated artifacts when isolated.

Forbidden work:

- destabilizing `ParserEngine`,
- tying IDE tooling to unstable runtime internals.

Current clarification status:

- tooling direction now distinguishes implemented C# source-generator embedded-code hooks from runtime-inline expression preparation; generator hook execution is explicit through generated policy helpers, supports tested parser predicate expression/block bodies and parser action statement bodies, and is not `IExpressionCompiler`-backed.
- embedded-code preparation contracts provide a source-generator/runtime-inline boundary; source-generator C# hooks for parser predicates/actions are implemented, while automatic runtime-inline model preparation from `ParserEngine` remains unimplemented. Explicit registry-backed prepared expression runtime adapters and an opt-in prepared runtime policy builder are available through `ParserRuntimeFeaturePolicy` without changing default runtime behavior.
- runtime trace analysis abstractions are available as tooling-only, read-only, deterministic consumers of passive observations/exports,
- analysis outputs are explicitly descriptive and non-authoritative (no replay, no runtime ownership transfer, no parser/diagnostics authority transfer).
- end-to-end runtime-observation/export/analysis usage guidance is documented as illustrative tooling only, with explicit non-framework/non-authoritative boundaries.

### Phase 8 — Future runtime research gates

**Status: not started.**

Goal: define prerequisites before any major runtime evolution.

Potential future research areas:

- shared lookahead execution,
- duplicated-work reduction,
- continuation-driven parsing,
- forward-only parsing,
- parallel alternative exploration.

These are not approved runtime features today.

Before any such work, require:

- complete invariant documentation,
- explicit design document,
- dedicated tests,
- migration plan,
- rollback/side-effect policy,
- semantic predicate policy,
- parser action policy,
- diagnostic compatibility plan,
- parse-tree compatibility plan,
- small experimental branch or prototype when needed.

## Required update policy

This roadmap must be updated when any PR changes:

- runtime behavior,
- parser scheduling,
- memoization,
- diagnostics,
- parse-tree shape,
- metadata semantics,
- ANTLR4 compatibility,
- parser feature capabilities,
- public API shape,
- runtime policies,
- test coverage strategy,
- tooling direction.

Roadmap updates should be in the same PR unless there is a clearly stated reason not to.

## Definition of done for future runtime PRs

Future runtime PRs should include:

- behavior summary,
- invariant impact,
- compatibility impact,
- diagnostics impact,
- parse-tree impact,
- public API impact,
- tests,
- documentation updates,
- roadmap update when direction changes.

## Current safety summary

The runtime currently remains conservative and deterministic. Metadata-rich infrastructure, the execution-context copy helper, and generated context copy helpers exist, but they are not replay authority. Managed semantic-state rollback exists for parser backtracking attempt boundaries, but not for top-level parse rejection after final validation failures. No replay, complete ANTLR transactional rollback, graph execution, async parsing, or parallel parsing exists today. Public APIs may evolve while the project remains pre-release; runtime execution guarantees remain conservative.

### Incremental parser rule-call execution policy

**Status: in progress.**

- `PositionalLiteralRuleCallExecutionPolicy` and the separate `NamedLiteralRuleCallExecutionPolicy` are available as explicit opt-ins while the default remains metadata-only; they are not combined automatically.
- The positional policy binds exact-arity values by declaration order. The named policy consumes the existing `NamedRawArguments` split for `name: literal` and `name = literal`, matches exact ordinal names independently of order, and requires exact declared-name coverage. Duplicate raw names inherit splitter last-wins semantics. The untyped policies continue to ignore defaults and require exact arity/coverage. Partial untyped binding, mixed syntax, declared-type validation/conversion in untyped policies, and arbitrary expressions remain unsupported.
- Binding validates the full call and applies all values through one rollback-managed, all-or-none pending child seed batch; the frame-manager contract forbids partial acceptance and reports when the complete batch cannot be retained. Generated state-aware memoization deterministically includes the supported literal values (`null`, `bool`, `int`, `long`, `double`, `string`, and `char`) so different bound values cannot reuse stale child state. Historical arbitrary explicit seeds remain accepted; non-hashable objects conservatively force volatile state keys instead of aborting parsing or claiming safe reuse.
- Typed validation is available only through the separate `TypedPositionalLiteralRuleCallExecutionPolicy` and `TypedNamedLiteralRuleCallExecutionPolicy`. They may consume conservatively preserved simple-literal parameter defaults: positional omission is trailing-only, named omission may occur in any order, explicit values override defaults, and complete effective values precede one atomic seed batch. General optional/default expressions, return/label semantics, policy composition, and broader expression semantics remain future separately reviewed phases. Lexer execution and ANTLR-complete argument semantics remain out of scope.


### Explicit typed literal rule-call binding

- Separate typed positional and named policies are available as explicit opt-ins; the default and existing untyped policies are unchanged.
- `ParserRuleParameterDescriptor.RawType` preserves the conservative `type name` declaration prefix without arbitrary C# type resolution; `RawDefaultValue` passively preserves a top-level `= defaultText` suffix.
- The closed allowlist covers built-in Boolean, integral, floating, decimal, character, string, and object aliases plus exact canonical `System.*` names and one nullable suffix.
- Conversion is checked and culture invariant: integral ranges are enforced, integral-to-floating and double-to-float require exact preservation, floating-to-integral and double-to-decimal are rejected, and text conversion is limited to exact string/char forms.
- Explicit arguments and only the defaults required for omitted parameters pass through the simple-literal parser and typed converter; complete validation and conversion precede one atomic managed seed batch. Positional omission is trailing-only, named omission may occur in any order, and explicit values override defaults. Generated rollback and memoization use converted effective values; runtime numeric types remain distinct, while different source forms may share a key after conversion to the same target value.
- Arbitrary type resolution, user-defined types, arrays, generics, enums, general/default expression evaluation, parameter references, constants, Roslyn conversion, mixed binding, policy composition, generated typed signatures, `$param` write/chained forms, returns, labels, and lexer execution remain out of scope.

## Optional generated-C# ANTLR-style convenience transformer

**Status: complete.**

- ANTLR-style `$...` forms are no longer parser/generator core behavior. They belong behind an explicit target-language transformer such as a C# compatibility transformer.
- The optional C# transformer may rewrite current-rule `$param`, `$local`, and declared bare `$returnName` reads/writes to generated helper calls. With `NoOpParserEmbeddedCodeTransformer`, those forms remain unchanged target-language text.
- The optional transformer remains conservative: it creates no implicit variables or typed fields/properties, keeps generated `Parse(...)` conservative, and emits diagnostics for unsafe forms. Supported generated-C# convenience forms are intentionally narrow: bare `$value` for declared current-rule returns, `$c.value`/`$x.value` for assignment-label child return reads, and `$xs.value` for list-label child return projections. `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare label objects, label writes, `@init` label-return reads, semantic-predicate label-return reads, token attributes, lexer attributes, typed parser contexts, public ANTLR-style parser methods, and general ANTLR attribute compatibility remain unsupported. These limitations are isolated from parser core behavior.
- Existing explicit helper APIs remain the recommended direct C# style for no-transformer embedded code.

## Embedded-code transformation boundary

**Status: complete.**

The audit sequence represented by items 8 through 11 is complete. Parser and lexer generated-code
transformation share the upstream pipeline, and the raw (`RawEmbeddedCode`) and validated transformed
(`TransformedEmbeddedCode`) phases are explicit. Generated C# and runtime-inline expressions remain
separate targets after that common boundary: generated code continues through
`GeneratedEmbeddedCodeBody` and `CSharpEmbeddedCodeInjector`, while
`ExpressionEmbeddedCodePreparer` is the supported runtime parser embedded-code preparation facade.

`IExpressionCompiler` remains caller-supplied and independently reusable; the facade coordinates it
only for prepared runtime-inline parser predicates and actions. A semantic Roslyn architecture guard
prevents another production component from combining the shared transformation pipeline, expression
compilation, specialized embedded-code lambdas, and prepared parser artifacts. This consolidation is
documentation and test enforcement only: no public API, diagnostics, generated source, or runtime
behavior changed.

Parser embedded-code handling is centered on preservation plus an explicit `IParserEmbeddedCodeTransformer` extension point. Embedded grammar text is carried as `RawEmbeddedCode`, transformed through `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)`, and consumed by emitters or expression compilers only as `TransformedEmbeddedCode`. The default transformer is no-op, `$...` rewriting is no longer a core parser/generator responsibility, and dynamic expression-backed preparation transforms code before using the existing compiler/preparer mechanism. Future target-language transformers must remain isolated from parser runtime authority and must not introduce a second compiler abstraction.

### Optional full embedded-code transformer

- A fuller C# transformer may be implemented later, but it must live behind `IParserEmbeddedCodeTransformer`.
- It must remain optional and target-language-specific.
- It must not drive parser core development or add C# parsing/rewriting to `GrammarEmitter`, `ParserEngine`, runtime frame classes, or source generator core logic.
- It must keep dynamic execution on the existing compiler/preparer path rather than introducing a parallel compiler API.


### Embedded-code transformer boundary update

ANTLR-style current-rule local writes are now documented as an optional C# transformer convenience only. The parser/generator core continues to treat embedded code as raw target-language text by default; no-op generation preserves `$local = ...` unchanged, while richer conveniences remain isolated behind `IParserEmbeddedCodeTransformer`.


### Optional C# transformer current-rule return writes

**Status: in progress.**

ANTLR-style current-rule return writes are recognized only by the optional C# ANTLR-style transformer for rule `@after` code and inline parser actions. The no-op/default transformer preserves `$returnName = ...` unchanged. The transformer supports bare declared current-rule return attributes and emits typed `SetRequiredRuleReturn<T>` / `GetRequiredRuleReturn<T>` helper calls for assignment, compound assignment, and standalone increment/decrement statements. Runtime helpers write parser-managed frame return state, successful child rule completions snapshot those returns into `ParserRuleCallResult`, failed alternatives roll return writes back, last successful write wins, present-null is distinct from missing, and returns are not auto-initialized. This does not complete broader ANTLR return execution semantics: dotted `$rule.returnName = ...`, parameter writes, label writes, token/lexer writes, `ref`/`out`, predicates, and `@init` writes remain unsupported or read-only.


Parser and lexer grammar-level named-action support is source-generator C# only. In parser or combined grammars, unscoped `@header` / `@members` / `@footer` are treated as parser compatibility blocks, and scoped `@parser::header` / `@parser::members` / `@parser::footer` are equivalent parser compatibility blocks. They emit parser header code, generated execution-context members, or deterministic trailing parser source in grammar source order, and they still produce compatibility warnings (`UP1035`, `UP1031`, or `UP1036`) because invalid C# remains a Roslyn responsibility. Scoped lexer named actions (`@lexer::header`, `@lexer::members`, `@lexer::footer`) mirror the same limited injection model in combined or lexer grammars only with dedicated lexer markers; parser-only grammars keep them unsupported because no lexer is generated; lexer members are emitted into the existing generated execution context and do not create a separate ANTLR lexer runtime type. Parser named actions in lexer grammars are invalid for this generator, and unscoped `@header`, `@members`, and `@footer` are not parser compatibility blocks in lexer grammars. Unsupported named actions, unknown lexer/parser action names such as `@lexer::custom` or `@parser::custom`, and unknown scopes such as `@tree::members` produce deterministic `UP1029` diagnostics and are not silently injected. The default/no-op transformer preserves named-action content unchanged; optional transformer behavior remains opt-in, and `$...` current-rule attribute rewriting is intentionally limited to parser actions/lifecycle code, not parser or lexer header/member/footer content. Parser and lexer members can be called from generated inline parser actions and supported `@init`/`@after` lifecycle hooks, and simple lexer inline actions and simple lexer predicates are available in the explicit generated-C# opt-in path. Lexer predicates are evaluated during lexer matching, while lexer actions still execute only after token acceptance.


### Lexer inline actions and predicates roadmap

**Status: mostly complete. Ongoing maintenance required.**

This section records the consolidated state after the generated-C# lexer inline action and predicate work. It is no longer a future roadmap for simple generated-C# lexer actions or simple generated-C# lexer predicates; those explicit opt-in paths are implemented within the bounded limits below.

This state must stay aligned with the existing in-progress sections:

- Phase 6 — ANTLR4 compatibility expansion.
- Phase 7 — Code generation and tooling.
- Embedded-code transformation boundary.
- Incremental parser rule-call execution policy, which remains parser-only and does not include lexer execution.

Current state:

- Grammar-level `@lexer::header`, `@lexer::members`, and `@lexer::footer` are supported only by the source-generator C# path.
- That support applies only to combined grammars and lexer grammars; parser grammars that declare `@lexer::*` remain unsupported because no lexer is generated.
- `@lexer::members` is injected into the existing generated execution context and does not create a separate ANTLR lexer runtime type.
- Simple lexer inline actions such as `A : 'a' { OnLex(); } ;` are supported only by generated-C# opt-in policies. They execute after the owning token is accepted and before accepted lexer commands are applied; conservative `Parse(...)` does not execute them. The tested boundary includes actions reached through lexer rule references and fragments, actions in simple quantifiers, duplicate action source text at distinct positions, and interaction with already-supported lexer commands/modes: `skip`, `channel(...)`, `type(...)`, `more`, `mode(...)`, `pushMode(...)`, and `popMode`.
- Simple lexer predicates such as `A : { IsEnabled() }? 'a' ;` are supported only by generated-C# opt-in policies. They are evaluated during lexer matching and can reject only the current rule or alternative path; conservative `Parse(...)` does not execute them. The tested boundary includes predicates reached through lexer rule references and fragments, predicates in simple quantifiers, duplicate predicate shapes at distinct positions, and false predicates preventing later accepted-token actions on that path.
- Runtime discovery still classifies lexer actions and lexer predicates as unsupported for runtime-inline execution. Only generated-C# opt-in policies make simple lexer actions and predicates executable. Lexer `AlternativeIndex` and `ElementIndex` identify the source hook location; quantified iterations do not receive a public iteration index.
- The source-generator C# path now collects parser hooks and limited generated-C# opt-in lexer action/predicate hooks.
- The generated-C# opt-in lexer attribute surface is consolidated for the current bounded subset. Inline lexer actions may read `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos`; `$line`/`$pos` expose the accepted token/chunk start `SourceSpan.Line`/`SourceSpan.Column`, with `$pos` documented as this runtime's 1-based column rather than full ANTLR `charPositionInLine` compatibility. Inline lexer actions may write only simple `$type = ...`, `$channel = ...`, and `$mode = ...` statements with identifier or string values. Those writes flow through `LexerActionExecutionResult`, are applied before lexer commands, and are covered by edge-case tests for last-write-wins, same-action type/channel/mode writes, fragments, lexer rule references, quantifiers, rejected alternatives, `more`, command override, passive read-context behavior, and deterministic diagnostics for complex unsupported forms.
- Future/non-goal lexer semantics remain explicit: `$text = ...`, `$line = ...`, `$pos = ...`, complex `$mode` writes, `$index`, `$int`, `$token`, `$start`, `$stop`, `$ctx`, `$input`, lexer predicate `$...`, runtime-inline lexer execution, a separate runtime lexer, full ANTLR lexer compatibility, general lexer action replay/buffering, and general lexer rollback are unsupported.

Completed phases:

- Phase 0: audit and documentation — completed.
- Phase 1: lexer hook metadata audit — completed.
- Phase 2: lexer context design — completed for bounded generated-C# opt-in.
- Phase 3: generated-C# lexer inline actions — completed for simple hooks and bounded lexer attributes.
- Phase 4: generated-C# lexer predicates — completed for simple predicates without `$...` attribute rewriting.
- Phase 5: optional C# lexer transformer — completed for bounded action reads `$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos` and simple writes `$type`, `$channel`, `$mode`.

Remaining future work:

- richer ANTLR lexer attribute compatibility;
- lexer predicate `$...` support, if ever designed;
- full target-language lexer semantics;
- runtime-inline lexer execution;
- separate runtime lexer;
- general action buffering/replay;
- general lexer rollback.

Required invariants for any future work:

- The default parser runtime remains conservative.
- `Parse(...)` must not start executing lexer code.
- Any future lexer-code execution must be explicitly opt-in.
- Lexer actions must not be mixed with parser actions.
- Lexer predicates remain separate from lexer actions because predicates change tokenization decisions.
- No lexer `$...` rewriting may be introduced outside an explicit transformer.
- No C# target-language logic may be added to `ParserEngine`, `ParserRuntimeFeaturePolicy`, runtime frames, or the core parser model.
- Unsupported diagnostics must remain deterministic until a feature is intentionally implemented and documented.

Non-goals:

- No promise of complete ANTLR compatibility.
- No general replay or action-buffering system.
- No rollback of external side effects.
- No implicit support for advanced ANTLR target-language modes, channels, commands, or lexer actions beyond explicitly designed increments.
- No Roslyn semantic model in the generator.
- No new compiler API.
- No change to the default parser runtime.


### ANTLR rule arguments and returns integration

**Status: in progress.**

Current work is design/documentation only. The integration path is progressive: preserve metadata and explicit helper boundaries first, then add narrowly scoped generated-C# opt-in argument binding only in future PRs. Conservative `Parse(...)` must remain unchanged, `ParserEngine` must stay target-language neutral, and `$...` conveniences must remain isolated behind `IParserEmbeddedCodeTransformer`. See `docs/parser/RuleArgumentsAndReturnsPlan.md` for the phase plan covering parameters, call arguments, returns, labels, rollback, memoization, and unsupported ANTLR compatibility boundaries.

## Recent documentation note: generated-C# lexer attributes

**Status: mostly complete. Ongoing maintenance required.**

The generated-C# opt-in embedded-code path has a bounded lexer `$...` rewrite architecture through the optional C# transformer and `EmbeddedLexerAttributeRewriter`. Inline lexer actions support read-only `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos` helpers. `$text` reads `LexerActionExecutionContext.Text` via `GetRequiredLexerText(context)` and is locked by tests as accepted token/chunk context text: fragments and lexer rule references receive context-level accepted text, `skip`/`type(...)`/`channel(...)` read before commands apply, and `more` reads the current accepted chunk before final accumulation. `$type`, `$channel`, and `$mode` read `LexerActionExecutionContext.TokenType`, `Channel`, and `Mode` through `GetRequiredLexerType(context)`, `GetRequiredLexerChannel(context)`, and `GetRequiredLexerMode(context)`. `$line` and `$pos` read `LexerActionExecutionContext.Line` and `Column` through `GetRequiredLexerLine(context)` and `GetRequiredLexerPos(context)`; they reflect the accepted token/chunk start `SourceSpan.Line` and 1-based `SourceSpan.Column`, not complete ANTLR `charPositionInLine` compatibility. These passive values are available after accepting the token/chunk and before lexer commands apply, so `type(...)`, `channel(...)`, `mode(...)`, `pushMode(...)`, and `popMode` do not alter what the current action reads. Fragment and lexer-rule-reference actions read the available accepted outer token metadata. Simple `$type = ...`, `$channel = ...`, and `$mode = ...` statement writes are supported only in generated-C# opt-in actions through `LexerActionExecutionResult.TokenType`, `LexerActionExecutionResult.Channel`, and `LexerActionExecutionResult.Mode`; `LexerEngine` applies those requested token and mode mutations before commands run, so commands remain authoritative. Edge-case tests cover last-write-wins, same-action type/channel/mode writes, multiple actions, passive read-context behavior, fragments, lexer rule references, quantifiers, rejected alternatives, `more`, command override, and diagnostics for unsupported complex forms. Lexer predicate attributes, `$text`/`$line`/`$pos` writes, compound/coalescing/increment writes, dotted/chained writes, expression writes, `$index`, `$int`, `$token`, `$start`, `$stop`, `$ctx`, and `$input` remain unsupported with deterministic diagnostics. Future work must keep runtime-inline lexer execution, a separate runtime lexer, general action replay/rollback, and full ANTLR lexer attribute compatibility out of scope until a broader design exists.


### Generated-C# explicit simple positional rule-call binding

Generated parsers can explicitly install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(...)` when generation enables simple positional rule-argument binding. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


### Helper-first labeled child return stabilization

Generated-C# labeled child return access is currently stabilized through explicit helpers over existing invocation-frame `ParserRuleCallResult` storage. Current-rule returns may use bare `$value` only in the declaring rule through the optional C# transformer, while parent access to `c=child` and `xs+=child` returns remains `GetRequiredLabeledRuleCallReturn`, `TryGetLabeledRuleCallReturn`, `TryGetLabeledRuleCallResult`, `GetLabeledRuleCallResults`, and `GetLabeledRuleCallReturns`. Present-null values, missing labels, missing return names, list-label ordering, rollback, and memoized call-site label rebinding are covered before any syntax sugar. Assignment-label `$c.value`/`$x.value` sugar and list-label `$xs.value` projection sugar are supported only in generated-C# inline parser actions and `@after`; list sugar rewrites to `GetLabeledRuleCallReturns(context, "xs", "value")` and remains read-only. `$child.value`, `$rule.value`, `$ctx`, `$xs.ctx`, typed parser contexts, and public ANTLR-style parser rule methods remain out of scope; any future sugar must stay inside `IParserEmbeddedCodeTransformer` and must not move C# logic into `ParserEngine`.
