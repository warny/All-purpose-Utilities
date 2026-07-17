# Utils.Parser — Quality audit (2026-07-10)

Audit of the `Utils.Parser` scope and its associated projects, with a focus on duplications of
code or intent, and on transformation, C# injection, and expression-compilation paths.

**Status (2026-07-10):** The identified execution paths currently pass through
`IParserEmbeddedCodeTransformer`. The dynamic compilation of expressions then goes through
`IExpressionCompiler`. However, C# code injection remains distributed across `GrammarEmitter` and
is not isolated behind a dedicated injection class. The items below are suggestions
not yet addressed unless otherwise noted.

## Embedded code architecture (high priority)

### 1. Introduce a typed representation of the transformed code
**Fixed.** The pipeline now distinguishes between `RawEmbeddedCode` and `TransformedEmbeddedCode` in
`Utils.Parser.Diagnostics.EmbeddedCode`. `EmbeddedCodeSource` exposes the source text as
`RawEmbeddedCode`, the generated hooks keep the raw code in `RawCode`, and their `TransformedCode`
is a `TransformedEmbeddedCode` populated only after the call to `IParserEmbeddedCodeTransformer`.
`GrammarEmitter.TransformEmbeddedCode` and `ExpressionEmbeddedCodePreparer.TransformSource` pass through
`ParserEmbeddedCodeTransformationService.TransformOrThrow`, which runs the transformer, validates the diagnostics, and then returns typed transformed code. C# emission or compilation paths via
`IExpressionCompiler` explicitly consume this type before extracting the final text.

Tests added:

- `ExpressionEmbeddedCodePreparerTests.EmbeddedCodeSource_WhenCreated_ExposesTypedRawCode`;
- `ExpressionEmbeddedCodePreparerTests.PrepareSemanticPredicate_WhenTransformerProvided_CompilerReceivesTransformedCode`;
- `ExpressionEmbeddedCodePreparerTests.PrepareParserAction_WhenNoOpTransformerUsed_CompilerReceivesTextuallyIdenticalTransformedCode`;
- `ExpressionEmbeddedCodePreparerTests.TransformedEmbeddedCode_DoesNotExposePublicConstructorsOrManualResultConversion`;
- `ExpressionEmbeddedCodePreparerTests.ParserEmbeddedCodeTransformationService_WhenRawCodeIsNull_DoesNotInvokeTransformer`;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesParserPredicateAndAction_RawCodeDoesNotAppearInGeneratedHookBodies`;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesLexerHook_RawCodeDoesNotAppearInGeneratedHookBodies`;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_UseTypedRawAndTransformedCodeFields`;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenUntransformedHookIsEmitted_ThrowsBeforeWritingSource`.

### 2. Centralize the transformer call and diagnostic validation
**Fixed.** `ParserEmbeddedCodeTransformationService.TransformOrThrow` is now the single
transformation boundary: it receives the `RawEmbeddedCode`, imposes the raw text in the
`ParserEmbeddedCodeTransformationContext`, calls `IParserEmbeddedCodeTransformer.Transform(...)`
exactly once, validates the result, treats a null diagnostic list as empty, ignores null
diagnostic entries, stops at the first `Error` diagnostic, checks that the transformed code is not
null, and returns only a validated `TransformedEmbeddedCode`.

C# generation and runtime compilation errors go through the same structured model
`ParserEmbeddedCodeTransformationException` defined in the diagnostics package. The exception exposes the path
(generation or runtime compilation), the diagnostic code and message, the location, grammar
name, rule name and available span;
`Utils.Parser.Expressions` maintains its public type by adapting it from this common exception without
losing the inner exception of the transformer. The `Info` and `Warning` diagnostics do not block
transformation and are retained in `TransformedEmbeddedCode.Diagnostics`.

Tests added or consolidated:

- a single transformer call and transmission of the raw context/existing metadata through runtime and
  generator transformation tests;
- rejection of an `Error` diagnostic before runtime compilation;
- rejection of an `Error` diagnostic before the existing generated injection path;
- preservation of `Warning` diagnostics;
- deterministic errors for null result and null code;
- null diagnostics treated as an empty collection;
- preservation of the inner exception when the transformer throws;
- functional architectural test, based on light Roslyn syntactic analysis, scanning the
  invocations rather than full files and prohibiting direct production calls to
  `IParserEmbeddedCodeTransformer.Transform(...)` outside the exact central call in
  `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)`, with regression tests for
  multi-line calls and unexpected calls in the central service file;
- generator tests checking that a transformer error blocks the C# emission and that the metadata
  error metadata remains consistent between runtime generation and compilation.

### 3. Create a class dedicated to injecting C# code
**Fixed.** Generated C# injection is now centralized in
`Utils.Parser.Generators.Internal.CSharpEmbeddedCodeInjector`. `GrammarEmitter` retains collection, classification, and transformation-context creation and the call to `TransformEmbeddedCode(...)`, and then passes
only `TransformedEmbeddedCode` to the injector. The injection API accepts neither
`RawEmbeddedCode`, `G4GrammarAction`, `G4EmbeddedAction`, nor a raw content string; the remaining strings are limited to internal markers/descriptors controlled by `CSharpEmbeddedCodeRegion`.

Migrated families:

- parser headers, members, and footers (`@header`, `@members`, `@footer`, `@parser::*`);
- lexer headers, members, and footers (`@lexer::header`, `@lexer::members`, `@lexer::footer`);
- inline parser and lexer actions;
- parser and lexer predicates, with distinction between returned expression and complete fragment;
- parser lifecycle hooks `@init` and `@after`.

The injector carries deterministic normalization of line endings (`\n`, `\r\n`, `\r`), splitting text
into lines, four-space indentation per level, named region markers, and final region
spacing. A trailing newline continues to produce a trailing blank line when the text
is not previously trimmed, in order to preserve the historical behavior of the verbatim regions.

Tests added:

- `CSharpEmbeddedCodeInjectorTests` covers single/multiple lines, line-ending
  normalization, indentation, method bodies, empty lines, empty text, markers,
  final spacing, predicate expressions, action blocks, and typed boundary;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesNamedActions_UsesTransformedMarkedRegions`;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesParserHooks_UsesTransformedHookBodies`;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenTransformerReplacesLexerHooks_UsesTransformedHookBodies`;
- `CSharpEmbeddedCodeInjectorArchitectureTests` adds a functional Roslyn guardrail that prohibits
  direct appends of raw code and limits reads of transformed text outside the injector to uses of
  non-injecting classification explicitly justified.

### 4. Add invariant tests for all embedded code locations
**Fixed.** Invariant coverage is carried by `UtilsTest/Parser/EmbeddedCodeTransformationInvariantTests.cs` with the deterministic spy transformer `RecordingEmbeddedCodeTransformer`.

C# generation locations covered:

- parser `@header`, `@members` and `@footer` (`ParserHeader`, `ParserMembers`, `ParserFooter`);
- lexer `@header`, `@members` and `@footer` (`LexerHeader`, `LexerMembers`, `LexerFooter`);
- semantic inline parser semantic predicate (`SemanticPredicate`);
- inline parser action (`InlineAction`);
- lifecycle hooks `@init` and `@after` (`RuleInit`, `RuleAfter`);
- inline lexer predicate (`LexerSemanticPredicate`);
- inline lexer action (`LexerInlineAction`).

Runtime locations covered:

- parser predicate prepared by `ExpressionEmbeddedCodePreparer` then compiled by `IExpressionCompiler`;
- parser action prepared by `ExpressionEmbeddedCodePreparer` then compiled by `IExpressionCompiler`.

The invariants checked are as follows:

- each expected raw fragment produces exactly one call to `IParserEmbeddedCodeTransformer.Transform(...)`;
- the call contains the `ParserEmbeddedCodeLocation`, the grammar name, the rule name and the available passive metadata;
- the parser parameters, returns, locals, and labels exposed by the current architecture are transmitted to the rule fragments;
- parser fragments are not classified as lexer fragments, and vice versa;
- transformed markers are unique, valid for their C# target, and appear exactly once at the expected injection or compilation point;
- the raw text does not reappear in the generated executable bodies nor in the runtime compiler input;
- the generated predicates cover the expression form and the block form with `return`;
- `@init` and `@after` on the same rule remain two distinct calls, ordered and kept separate;
- an `Error` diagnostic, a transformer exception, a null result or a null transformed code blocks the generated injection;
- an `Error` diagnostic, a transformer exception, a null result or a null transformed code blocks runtime compilation;
- a runtime `Warning` diagnostic maintains simple processing: single transformation and single compilation.

Existing architectural safeguards remain consolidated by:

- `EmbeddedCodeTransformerArchitectureTests`, which limits direct calls to `Transform(...)` to the central service and verifies by Roslyn semantic model that embedded code preparers do not create a second direct path to `IExpressionCompiler.Compile(...)`;
- `CSharpEmbeddedCodeInjectorArchitectureTests`, which verifies that targeted emission methods use `CSharpEmbeddedCodeInjector` and that readings of `TransformedEmbeddedCode.Text` outside the injector remain limited to allowed non-injecting classifications;
- `CSharpEmbeddedCodeInjectorTests`, which locks the injection API to `TransformedEmbeddedCode` and disallows `RawEmbeddedCode` parameters or raw strings.

No production spy injector has been added: the `CSharpEmbeddedCodeInjector` boundary is checked by Roslyn architectural tests and by generated markers.

## Code duplication (medium priority)

### 5. Consolidate parser and lexer named actions
**Fixed.** The six generated named-action families (`parser @header`, `parser @members`,
`parser @footer`, `lexer @header`, `lexer @members`, `lexer @footer`) now go through an
immutable internal descriptor `NamedActionInjectionDescriptor` and the common method
`EmitNamedActionRegion`.

The descriptor only carries the necessary differences between families:

- the `ParserEmbeddedCodeLocation` transmitted to the transformation boundary;
- the `CSharpEmbeddedCodeRegion`, which preserves existing markers, indentation and spacing;
- the selector based on `EmbeddedMembersSupport`, so as not to duplicate the classification
  `@header` / `@parser::header` / `@lexer::header` and equivalents.

The explicit wrappers `EmitParserHeaders`, `EmitParserMembers`, `EmitParserFooters`,
`EmitLexerHeaders`, `EmitLexerMembers` and `EmitLexerFooters` are preserved. They choose
only the appropriate static descriptor and delegate to the common method. The generated C# source
remains unchanged: same emission positions, same regions, same indentation, same empty lines and
same transformer invocation order.

Added or strengthened tests:

- invariant coverage for the order of several fragments of the same named action category;
- Roslyn guardrails checking that the six wrappers delegate to `EmitNamedActionRegion`;
- Roslyn safeguards verifying that wrappers do not transform or inject directly;
- maintaining existing invariants covering the six named `ParserEmbeddedCodeLocation`, the absence of a
  fallback to raw text and exclusive passage by `CSharpEmbeddedCodeInjector`.

Points 6 to 11 remain outside the scope: inline parser/lexer actions, predicates, lifecycle hooks,
recursive traversals, runtime symbols and compilation/preparation of expressions are not consolidated
by this change.

### 6. Merge `EmbeddedCodeHook` and `LexerEmbeddedCodeHook`
**Fixed.** Generated parser and lexer hooks are now represented by a single internal type
`GrammarEmitter.EmbeddedCodeHook`. The old `LexerEmbeddedCodeHook` type has been removed. The common type keeps the raw code in `RawEmbeddedCode RawCode` and the transformed code ready for emission
in `TransformedEmbeddedCode? TransformedCode`, without replacing these typed boundaries with strings.

The domain distinction is explicit thanks to two internal discriminators:

- `EmbeddedCodeHookOwner` distinguishes between `Parser` and `Lexer`;
- `EmbeddedCodeHookKind` distinguishes between `SemanticPredicate` and `InlineAction`.

The four categories are therefore covered without an ambiguous boolean as their primary state: parser predicate,
parser inline action, lexer predicate, and inline lexer action. Construction goes through the factories
`CreateParser(...)` and `CreateLexer(...)`, which validate rule name, raw code, accepted indices (`-1` as historical sentinel or nonnegative value), the generated method name and the
enum values. The transformed result remains absent before the immutable transition, and the single emitter access point
explicitly rejects this state before any source writing.

The parser and lexer producers remain separate: `CollectEmbeddedCodeHooks(...)` keeps the parser
indexing rules, including sequence processing, quantifiers, negations and left
recursion, while `CollectLexerEmbeddedCodeHooks(...)` keeps the existing lexer path. The
recursive paths are not shared in this correction so as not to mask the runtime
differences. The naming conventions remain supported by the producers (`__Predicate...`,
`__Action...`, `__LexerPredicate...`, `__LexerAction...`) and the generated C# source remains unchanged.

Added or strengthened tests:

- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_UseOneTypedHookWithExplicitOwnerAndKind`;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenCreatedThroughFactories_PreserveFourCategories`;
- `Antlr4GeneratedEmbeddedCodeTests.EmbeddedCodeHookTypes_WhenInvalidStateIsCreated_RejectsIt`;
- `Antlr4GeneratedEmbeddedCodeTests.Emit_WhenParserAndLexerHooksShareGrammar_PreservesStableCategoriesAndGeneratedBodies`;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedCodeHooks_UseSingleCommonHookModel`.

Points 7 to 11 remain outside the scope: runtime symbol construction, general consolidation of
recursive traversals, overall formalization of the pipeline, clarification of the raw/transformed state and
runtime facade documentation are not addressed by this fix.

### 7. Consolidate the construction of expression symbols
**Fixed.** `ExpressionEmbeddedCodePreparer` retains the specialized wrappers
`BuildSemanticPredicateSymbols(...)` and `BuildParserActionSymbols(...)`, but these no longer carry
any classification logic: they delegate directly to the private common method
`BuildRuntimeContextSymbols(ParameterExpression runtimeContext, IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols)`.

The common method creates the dictionary with `StringComparer.Ordinal`, iterates over `supportedSymbols`
exactly once and exposes the four historical symbols without changing their public names:

- `RuleName` -> `ruleName` -> `context.Rule.Name`;
- `InputPosition` -> `inputPosition` -> `context.InputPosition`;
- `AlternativeIndex` -> `alternativeIndex` -> `context.AlternativeIndex`;
- `ElementIndex` -> `elementIndex` -> `context.ElementIndex`.

The duplicate helpers `AddSemanticPredicateSymbol(...)` and `AddParserActionSymbol(...)` have been
deleted. `BuildRuleName(...)` remains isolated, because it explicitly documents the property-access chain
`Rule.Name` shared by both runtime contexts. No public interface or base class has
been added for the `SemanticPredicateEvaluationContext` and `ParserActionExecutionContext` contexts;
the lambda type of each path remains specialized. Constructed expressions always read the
values from the context provided at run time, without capturing values during preparation and
without an additional call to `IExpressionCompiler.Compile(...)`. Unknown values of
`EmbeddedCodeContextSymbol` remain ignored, as in the previous `switch` statements without a `default` branch.

Added or strengthened tests:

- unit matrix covering `PrepareSemanticPredicate` and `PrepareParserAction` for all four
  symbols, an empty dictionary, a single symbol, an unordered subset, and
  unknown enum values;
- inspection of `MemberExpression` to check targeted runtime members without depending on
  `Expression.ToString()`;
- execution tests reusing the same artifact prepared with two different contexts to prove
  runtime reading of values;
- functional Roslyn guardrail checking that wrappers delegate to `BuildRuntimeContextSymbols`,
  that the old `Add...Symbol` helpers no longer exist, only one method classifies
  `EmbeddedCodeContextSymbol`, that lambdas retain their specialized runtime types and that
  no new public contract has been introduced.

Points 8 to 11 remain outside the scope of the production code of this correction: general parser/lexer consolidation, overall pipeline, raw/transformed state clarification and runtime facade documentation remain separate work items.

### 8. Introduce common engines with specialized parser/lexer strategies
**Partially corrected.**

#### 8a. Collecting hooks — fixed

The collection of embedded parser and lexer hooks is now shared by the private common engine
`EmbeddedHookCollector`. This engine implements the creation of the collection, the stable recursive traversal of
nodes `G4Alternation`, `G4Alternative`, `G4Sequence`, `G4Quantifier`, `G4Negation` and
`G4EmbeddedAction`, the accumulation of hooks, and then transforms each hook exactly
once via `TransformEmbeddedCode(...)`.

The real differences remain concentrated in two private strategies:

- `ParserEmbeddedHookCollectionStrategy` enumerates the parser rules, applies the order by priority,
  prepares the traversal roots of direct-left-recursive rules by separating base alternatives and
  recursive tails, preserves historical sentinels `-1`, parser rules for indexing
  alternatives, sequences, quantifiers and negations, `__Predicate` / `__Action` prefixes,
  locations `SemanticPredicate` / `InlineAction`, and the typed factory `EmbeddedCodeHook.CreateParser(...)`;
- `LexerEmbeddedHookCollectionStrategy` lists the lexer rules of `DEFAULT_MODE` then the additional
  modes, keeps source order, keeps historical lexer indexes for alternatives,
  sequences, quantifiers and negations, `__LexerPredicate` / `__LexerAction` prefixes,
  locations `LexerSemanticPredicate` / `LexerInlineAction`, and the typed factory
  `EmbeddedCodeHook.CreateLexer(...)`.

The immutable context `HookTraversalPosition` explicitly carries `AlternativeIndex` and
`ElementIndex`, including `-1` sentinels, without mutable collection or global state. Traversal roots are passed by `HookTraversalRoot`, which leaves left recursion within the specialized parser
scope and the lexer modes at the specialized lexer scope. Readable wrappers
`CollectEmbeddedCodeHooks(...)` and `CollectLexerEmbeddedCodeHooks(...)` are preserved, but they no longer contain a `switch` on `G4Content`, recursion, direct creation of hooks,
direct transformation or indexing logic.

No `isLexer` boolean, no extensive base classes, and no new public APIs have been
introduced. The generated C# source is preserved by existing generated source tests and by
tests of transformation invariants. The Roslyn guardrail
`GrammarEmitterEmbeddedCodeHookCollection_UsesSharedCollectorAndStrategies` checks the delegation of
wrappers, the existence of the engine and strategies, the absence of duplicated paths, the absence of
a `bool isLexer` parameter, centralization of the transformation, use of `CreateParser(...)`
and `CreateLexer(...)`, ownership of left recursion by the parser strategy, ownership of lexer
modes by the lexer strategy, and the presence of the immutable context of indices.

Added or strengthened tests:

- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedCodeHookCollection_UsesSharedCollectorAndStrategies`;
- existing characterization tests `Antlr4GeneratedEmbeddedCodeTests` covering non-recursive parser,
  left recursive parser, lexer, combined grammars, method names, dispatchers, transformed bodies
  and stability of the generated source;
- existing tests `EmbeddedCodeTransformationInvariantTests` covering order, number of calls to
  transform, locations, rule names, raw code and absence of double transformation.

#### 8b. Runtime dispatchers — fixed

The emission of the runtime parser and lexer dispatchers is now shared by the private engine
`EmbeddedHookDispatcherEmitter`. This engine implements the stable algorithm: generated class declaration,
dispatch-method signature, ordered loop on the hooks already selected,
validation of the `Owner` and `Kind` discriminants, comparisons in historical order
`Rule.Name`, raw code (`PredicateCode` or `ActionCode`), `AlternativeIndex`, `ElementIndex`, call of
the hook method, success return and fallback return. No additional sorting is introduced;
the order remains that of the `predicates`, `actions`, `lexerActions` and `lexerPredicates` lists produced by
the existing collection.

Declarative differences are concentrated in the private immutable descriptor
`EmbeddedHookDispatcherDescriptor`, which explicitly exposes the four constant configurations:

- `ParserPredicate` for `GeneratedSemanticPredicateEvaluator` / `ISemanticPredicateEvaluator`,
  `SemanticPredicateEvaluationContext`, `SemanticPredicateEvaluationOutcome`, success
  `Satisfied`/`Rejected` and fallback `_fallback.Evaluate(context)`;
- `ParserAction` for `GeneratedParserActionExecutor` / `IParserActionExecutor`,
  `ParserActionExecutionContext`, `ParserActionExecutionOutcome.Executed` and fallback
  `_fallback.Execute(context)`;
- `LexerPredicate` for `GeneratedLexerPredicateEvaluator` / `ILexerPredicateEvaluator`,
  `LexerPredicateEvaluationContext`, `LexerPredicateEvaluationOutcome.True`/`False` and fallback
  `_fallback.Evaluate(context)`;
- `LexerAction` for `GeneratedLexerActionExecutor` / `ILexerActionExecutor`,
  `LexerActionExecutionContext` with `LexerActionExecutionResult`,
  `LexerActionExecutionOutcome.Executed` and fallback `_fallback.Execute(context, result)`.

The explicit wrappers `EmitSemanticPredicateEvaluator(...)`, `EmitParserActionExecutor(...)`,
`EmitLexerPredicateEvaluator(...)` and `EmitLexerActionExecutor(...)` are kept as explicit parser/lexer and predicate/action entry points; they only select the corresponding descriptor and
delegate to the common engine. No additional behavioral strategies were necessary: the
differences are types, signatures, code properties, success expressions, call
arguments and fallbacks, so they remain described by data. No `isLexer` parameter or
`isPredicate`, no big domain `switch` and no new public API are introduced.

The generated source is preserved: class names, interfaces, signatures, conditions, order of
conditions, calls `__Predicate...`, `__Action...`, `__LexerPredicate...`, `__LexerAction...`, success returns and fallbacks remain the same. The hook methods themselves are not shared and
remain intentionally separate for item 8c.

Added or strengthened tests:

- `Antlr4GeneratedEmbeddedCodeTests.Emit_CombinedEmbeddedCode_GeneratesEquivalentRuntimeDispatchers`
  characterizes the four dispatchers on a grammar combined with several parser and lexer hooks;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedHookDispatchers_UseSharedEmitterAndImmutableDescriptors`
  uses Roslyn to verify the delegation of the four wrappers, the absence of loops and complete implementations
  in the wrappers, the single common emitter, private immutable descriptor, absence of `isLexer` /
  `isPredicate`, the absence of scattered parser/lexer branches in the emitter, validation of the
  discriminants `Owner` and `Kind`, the four explicit configurations, the fallbacks, and preservation of
  separate hook methods for 8c.

#### 8c. Hook methods — fixed

Emission of the four generated hook-method families is now shared by the private engine `EmbeddedHookMethodEmitter`. This engine implements the stable algorithm: typed validation of the hook by
`Owner` and `Kind`, creation of `GeneratedEmbeddedCodeBody`, XML documentation comment, signature, brace
opening, optional preamble of context locals, centralized call to
`EmitGeneratedEmbeddedCodeBody(...)`, closing brace and final empty line. The generated source
is retained: same XML documentation comments, same signatures, same indentation, same injected bodies and
same emission order.

Declarative differences are concentrated in the private immutable descriptor
`EmbeddedHookMethodDescriptor`, which explicitly exposes the four configurations:

- `ParserPredicate` validates `Parser` / `SemanticPredicate`, emits `private bool`, receives
  `SemanticPredicateEvaluationContext context`, uses `GeneratedEmbeddedCodeBody.ForPredicate(...)`
  and keeps the parser predicate locals (`ruleName`, `inputPosition`, `alternativeIndex`,
  `elementIndex`, `predicateCode`);
- `ParserAction` validates `Parser` / `InlineAction`, emits `private void`, receives
  `ParserActionExecutionContext context`, uses `GeneratedEmbeddedCodeBody.ForAction(...)` and
  preserves parser action locals (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`,
  `actionCode`);
- `LexerPredicate` validates `Lexer` / `SemanticPredicate`, emits `private bool`, receives
  `LexerPredicateEvaluationContext context`, uses `GeneratedEmbeddedCodeBody.ForPredicate(...)` and
  receives no parser locals;
- `LexerAction` validates `Lexer` / `InlineAction`, emits `private void`, receives
  `LexerActionExecutionContext context, LexerActionExecutionResult result`, uses
  `GeneratedEmbeddedCodeBody.ForAction(...)`, keeps the mutable parameter `result` and receives no
  parser locals.

The explicit profile `EmbeddedHookContextLocalProfile` limits the preamble to the three real cases
(`None`, `ParserPredicate`, `ParserAction`) without introducing a domain boolean. Wrappers
`EmitPredicateHook(...)`, `EmitActionHook(...)`, `EmitLexerPredicateHook(...)` and
`EmitLexerActionHook(...)` remain the specialized entry points; they only select the
descriptor and delegate to the common engine. Lifecycle hooks remain out of scope: they rely
on `LifecycleHook`, `internal` visibility, `ParserRuleLifecycleContext`, locals and phases
`@init` / `@after`, without `EmbeddedCodeHookOwner` / `EmbeddedCodeHookKind` discriminators.

Added or strengthened tests:

- `Antlr4GeneratedEmbeddedCodeTests.Emit_CombinedEmbeddedCode_GeneratesStableHookMethodBlocks`
  characterizes documentation comments, signatures, parser locals, absence of parser locals on the lexer side,
  expression/block transformation, family ordering and preservation of the lexer parameter `result`;
- `EmbeddedCodeTransformerArchitectureTests.GrammarEmitterEmbeddedHookMethods_UseSharedEmitterAndImmutableDescriptors`
  uses Roslyn to verify the common emitter, the immutable descriptor, the four explicit configurations,
  the delegation of wrappers, the absence of forbidden direct calls in wrappers, typed
  validation, the single call to `EmitGeneratedEmbeddedCodeBody(...)`, the typed distinction
  `ForPredicate(...)` / `ForAction(...)`, explicit local profiles, absence of `isLexer` /
  `isPredicate`, and maintaining lifecycle hooks outside the abstraction.

#### 8d. Global safeguards — fixed

Architectural guardrails now cover collection, runtime dispatchers and hook
methods. They check the absence of reintroduction of parallel algorithms, the absence of domain booleans
`isLexer` / `isPredicate`, the absence of a new public API, the preservation of explicit
wrappers, typed validation and intentional separation of lifecycle hooks.

## Duplicated intent and readability (medium priority)

### 9. Clarify the boundary between preparation, transformation, injection and compilation
**Fixed.** The internal core `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)`, placed
in the `netstandard2.0` shared diagnostics assembly, receives a `RawEmbeddedCode`, an
`IParserEmbeddedCodeTransformer`, a strongly typed `ParserEmbeddedCodeTransformationContext` and the
failure metadata. It calls the transformer exactly once, centralizes validation of arguments, null results, blocking diagnostics, and null transformed code,
then returns a validated `TransformedEmbeddedCode`. The existing public service delegates to the core in order to preserve the API.

The boundary stops strictly at this artifact: it knows nothing about `StringBuilder`, injection, expressions, lambdas, or compilation. The generator constructs its enriched context (rule,
declarations and labels), calls the core, preserves the parser/lexer and predicate/action distinction,
then classifies the result via `GeneratedEmbeddedCodeBody.ForPredicate(...)` or `ForAction(...)` and
delegates exclusively to `CSharpEmbeddedCodeInjector`. The runtime path, limited to predicates and
parser actions, constructs its context in `ExpressionEmbeddedCodePreparer`, calls the same core,
then constructs specialized symbols and a specialized lambda, and keeps the call to
`IExpressionCompiler.Compile(...)`.

The characterization tests cover the four generated categories and the two runtime categories,
exact contexts and raw texts, single invocation, transformation failures, classification,
injection, single compilation, and reuse of artifacts with multiple runtime contexts.
Roslyn guardrail `EmbeddedCodeTransformerArchitectureTests` prohibits parallel direct calls
to the transformer and checks that the core is internal, common to both targets, returns the transformed type
and does not depend on any target details. `CSharpEmbeddedCodeInjectorArchitectureTests` maintains the
injection boundary. No public API or observable behavior was changed.

Point 10 is corrected below. Point 11 (documented status of the runtime facade) remains
explicitly open.

### 10. Explicitly distinguish between raw and transformed states
**Fixed.** The single internal model `EmbeddedCodeHook` is now an immutable record that retains
the collected text in `RawEmbeddedCode RawCode` and exposes separately
`TransformedEmbeddedCode? TransformedCode`. The parser and lexer factories create only the raw
state. The collector then makes an explicit transition with a `with` expression, after the single
call to `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)`; raw code is neither overwritten
nor rebuilt.

The four parser/lexer emitters continue to delegate to `EmbeddedHookMethodEmitter`. Before writing anything
to the `StringBuilder`, this emitter gets the result through the central access point
`RequireTransformedCode(...)`. A hook that is still raw causes a deterministic `InvalidOperationException` that names the affected
method: neither fallback to `RawCode` nor partial source output is possible. `GeneratedEmbeddedCodeBody.ForPredicate(...)` and `ForAction(...)`
thus continue to exclusively receive a validated `TransformedEmbeddedCode`.

The raw code is intentionally retained after transformation for diagnostics, flow audits
and invariant tests. `LifecycleHook` has not been modified: it only represents a state already
transformed and therefore did not carry the same ambiguity. Unit tests characterize the raw
collection, text preservation and emission failure before transformation. Roslyn guardrails
check the types of the two properties, the absence of ambiguous replacement names, the transition by
the common pipeline, the single phase-access point, the typed inputs of
`GeneratedEmbeddedCodeBody` and the absence of `RawCode.Text` reads by the emitters.

This correction is internal to the generator: it modifies neither the generated C# source nor the
runtime behavior, diagnostics, or public APIs.

### 11. Document `ExpressionEmbeddedCodePreparer` as the single runtime compilation facade

**Fixed.** `ExpressionEmbeddedCodePreparer` is now documented as the only supported facade
for runtime compilation of embedded parser code. This scope does not mean that it is the only
global user of `IExpressionCompiler`: the compiler remains a generic component injected by
the caller and directed by the facade for this specific preparation pipeline.

The facade supports only `SemanticPredicate` and `ParserInlineAction` for the
`RuntimeInlineExpression` target. The full path is explicit: `EmbeddedCodeSource`, validation of the
category and target, `EmbeddedCodeTransformationPipeline.TransformAndValidate(...)`,
`TransformedEmbeddedCode`, construction of symbol expressions linked to the runtime context, a single call
to `IExpressionCompiler.Compile(...)`, then specialized CLR lambda. Predicates produce a
`Func<SemanticPredicateEvaluationContext, bool>` wrapped in
`PreparedExpressionSemanticPredicate`; actions produce a
`Action<ParserActionExecutionContext>` wrapped in `PreparedExpressionParserAction`.

The results remain normalized to `Unsupported`, `PreservedNotCompiled`, `CompilationFailed` or
`Success`. Lexer hooks, grammar actions, `@init`, `@after` and other categories outside the parser
runtime-inline path remain unsupported. The facade does not generate C#, does not replace the source
generator and does not execute or capture runtime values during preparation.

The characterization tests check the transformed text, the runtime symbols, the single invocation of the
transformer and compiler, the absence of calls to these dependencies for categories or targets
not supported, and the absence of execution during preparation. A semantic Roslyn guardrail locks
the existing public interface and public members, the shared pipeline, the common compiler field, the
two artifact constructions and specialized lambdas, the independence of the C# generator and
the absence of a second facade or a pipeline combining transformation, compilation and artifacts.
No behavior, diagnostic, prepared artifact, or public API was added or changed.

## Safeguards and controls (low priority)

### 12. Add static check against direct injection of raw code

**Fixed.** `CSharpEmbeddedCodeInjectorArchitectureTests` uses Roslyn semantic analysis to
disallow direct writes of `RawEmbeddedCode.Text`, `TransformedEmbeddedCode.Text` and
`SourceText` to `Append` or `AppendLine` outside `CSharpEmbeddedCodeInjector`.

The guard also tracks assignments to local variables to prohibit
alias workarounds. The only read of transformed text allowed outside the injector is the
non-injecting classification done in `GeneratedEmbeddedCodeBody.ForPredicate`.

The safeguard remains deliberately targeted and audit-friendly. If the generation paths become
more indirect, a more general flow analysis could be considered without compromising the
current boundary.

### 13. Standardize transformation exceptions

**Fixed.** C# generation and runtime compilation paths rely on
`Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException`.

The common exception exposes the diagnostic code and message, the transformation path, the
`ParserEmbeddedCodeLocation`, grammar name, rule name, span and optional inner
exception. `ParserEmbeddedCodeTransformationService.TransformOrThrow` uses this for blocking
diagnostics, transformer exceptions, null results and null transformed codes.

`Utils.Parser.Expressions.ParserEmbeddedCodeTransformationException` derives from the common type to
preserve the Expressions project's public API without losing structured metadata. The historical constructor taking only one message is retained for compatibility, but the production
paths use the structured form.

### 14. Check documentation after each pipeline refactoring
Any change to the behavior or boundaries of the embedded code must be reflected in the
documents imposed by `AGENTS.md`, in particular:

- `Utils.Parser/ROADMAP.md`;
- `docs/parser/ANTLRCompatibility.md`;
- `docs/parser/EmbeddedCodeExecutionModel.md`;
- `docs/parser/EmbeddedCodeTransactionalState.md`;
- `docs/parser/Antlr4CompatibilityMatrix.md`;
- `Utils.Parser.Generators/README.md`.

For each PR, explicitly document which files were updated or why no
update was necessary.