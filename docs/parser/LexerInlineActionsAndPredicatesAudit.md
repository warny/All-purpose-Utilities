# Lexer Inline Actions and Predicates Audit

## Current implementation state

The current lexer-related embedded-code support is intentionally narrow:

- Grammar-level `@lexer::header`, `@lexer::members`, and `@lexer::footer` are supported only by the source-generator C# path.
- That support is limited to combined grammars and lexer grammars.
- Parser grammars that declare `@lexer::*` remain unsupported because no lexer is generated for parser-only grammars.
- `@lexer::members` is injected into the existing generated execution context.
- The `@lexer::members` injection does not create a separate ANTLR-compatible lexer runtime type.
- There is no separate runtime lexer execution context today.
- Lexer inline actions remain unsupported.
- Lexer predicates remain unsupported.
- Runtime discovery classifies lexer actions and lexer predicates as unsupported constructs.
- The source-generator C# path collects executable hooks from parser rules only; lexer rules do not produce executable hooks.
- Generated `Parse(...)` remains conservative and does not execute lexer code.

## Scope of lexer inline actions

Future design work must account for the placement and execution semantics of lexer inline actions before any implementation is attempted. The following examples are representative audit inputs only; they are not supported behavior today:

```antlr
A : 'a' { OnLex(); } ;
A : 'a' { OnLex(); } 'b' ;
A : ('a' { OnLex(); })+ ;
A : 'a' | 'b' { OnLex(); } ;
```

The audit scope includes:

- **Position in the rule**: an action may appear after a token fragment, between token fragments, inside grouped content, or near the end of a lexer rule.
- **Alternative ownership**: an action belongs to the alternative that contains it; the same textual action in different alternatives may need distinct metadata and dispatch identity.
- **Potential hook index**: any future hook model would need stable indexing that distinguishes rule name, alternative, element position, quantifier nesting, mode, and source location without implying execution authority before the feature is enabled.
- **Expected execution order**: actions would need to run in the same tokenization path order as the matched lexer elements, not in parser-rule scheduling order.
- **Actions in sequences**: actions between two lexer elements raise questions about whether the action runs only after the preceding element is accepted and before the following element is attempted.
- **Actions in alternatives**: actions must be associated with the chosen tokenization alternative and must not leak into alternatives that were not selected.
- **Actions in quantifiers**: actions inside `*`, `+`, or `?` content raise repeated-execution, failed-iteration, and partial-match questions.
- **Actions in negation**: if represented by the grammar model, negated lexer content with embedded actions would require explicit design because probes and consumed input may not have the same execution boundary.
- **Actions in lexer modes**: mode-local lexer rules may need mode-aware hook identity and state capture.
- **Actions in combined grammars**: combined grammar support must distinguish lexer-rule actions from parser-rule actions even when both are present in one grammar file.

These cases remain documentation targets only. They do not add executable lexer hooks, source-generator dispatch, diagnostics changes, or runtime behavior.

## Scope of lexer predicates

Future design work must account for lexer predicates separately from lexer inline actions. The following examples are representative audit inputs only; they are not supported behavior today:

```antlr
A : { IsEnabled() }? 'a' ;
A : 'a' { IsAllowed() }? ;
A : { IsModeEnabled() }? 'a' | 'b' ;
```

Lexer predicates require separate treatment because:

- Lexer predicates change tokenization decisions.
- Lexer predicates are not equivalent to lexer actions.
- Lexer predicate execution can decide whether a token alternative is viable before a token is emitted.
- Lexer predicates require a lexer-state-aware model that can describe current input position, mode, token construction state, and any future lexer-managed state.
- Lexer predicates must not be executed by generated `Parse(...)`.
- Lexer predicates must remain unsupported until a dedicated implementation phase designs and documents their semantics.
- Predicate placement matters: a predicate before a token fragment, after a token fragment, or inside one alternative of a multi-alternative lexer rule can affect different tokenization decisions.
- Disabled and enabled paths must be designed explicitly before any opt-in generated-C# support exists.

## Interaction with grammar-level lexer members

`@lexer::members` already exists in the limited source-generator C# compatibility model. Its current behavior is:

- The member text is injected into the existing generated execution context.
- The injected members can be available to generated C# code that shares that execution context.
- The injection is not a separate lexer runtime, a separate lexer object, or a complete ANTLR target-language compatibility layer.

Future lexer inline actions or lexer predicates may want to call members declared in `@lexer::members`. That possibility must be designed without confusing the existing execution context with a dedicated lexer runtime. A later design phase must decide whether lexer execution can safely use the existing generated execution context, requires a lexer sub-context, or requires a separate lexer-specific state object.

## Modes, channels, commands, and tokenization concerns

The audit must account for ANTLR lexer constructs that can affect tokenization, without promising support for them. Relevant constructs include:

- lexer modes;
- lexer commands;
- channels;
- `skip`;
- `more`;
- `type`;
- `pushMode`, `popMode`, and `mode`;
- interaction between inline actions and lexer commands;
- interaction between predicates and lexer commands;
- ordering between ANTLR commands and target-language actions.

These constructs are not implemented by this audit. The existence of this checklist does not imply support for advanced modes, channels, commands, or complete ANTLR lexer semantics.

## Rollback and side-effect risks

Lexer execution has rollback and side-effect risks that are distinct from parser rollback:

- A lexer action could run during a tokenization attempt that is later abandoned.
- External side effects from target-language code are not rollback-safe by default.
- Parser rollback and lexer rollback must not be treated as the same mechanism.
- Parser memoization does not necessarily capture future lexer state, token-construction state, mode stack state, or side effects.
- A future implementation may require a separate lexer state model with copy, hash, rollback, and restore semantics.
- There is currently no general action buffering or replay system.
- Failed tokenization paths must not leave observable state changes unless a future design explicitly documents that risk and opt-in behavior.

Any future support must preserve the current conservative default and avoid presenting external target-language side effects as rollback-safe.

## Source-generator path versus runtime-inline path

Any future support should preserve the existing architecture boundaries:

- Future lexer inline action support, if implemented, should begin as source-generator C# opt-in work.
- The runtime-inline expression path remains separate.
- Parser core components must not gain C# target-language parsing, rewriting, or semantic logic.
- There must be no target-language-specific lexer execution logic in the parser core.
- The generator must not depend on a Roslyn semantic model for these features.
- No new compiler API should be introduced for this audit or as an implicit prerequisite.
- Lexer `$...` rewriting must remain optional and must be placed behind an explicit transformer.
- No implicit lexer `$...` rewriting should be added to core parser, runtime, or generator logic.

## Proposed future PR sequence

The following sequence is indicative only and does not commit the project to implementation or to complete ANTLR compatibility:

```text
PR A — audit documentation only
PR B — metadata/indexing tests only, if needed
PR C — lexer context design documentation
PR D — source-generator opt-in lexer inline actions, no predicates
PR E — tests and docs for lexer inline actions
PR F — lexer predicates design
PR G — source-generator opt-in lexer predicates
```

Each implementation phase should independently document behavior, diagnostics, rollback boundaries, opt-in requirements, and tests before changing runtime or generator behavior.

## Existing tests and future tests

Existing test families to preserve include:

- `@lexer::*` supported in lexer grammars.
- `@lexer::*` supported in combined grammars.
- Parser grammar plus `@lexer::*` remains unsupported.
- Lexer inline actions remain unsupported.
- Lexer predicates remain unsupported.
- Generated source contains no executable lexer hooks.

Possible future test families include:

- stable indexing for lexer actions;
- lexer modes;
- quantifiers;
- alternatives;
- predicates before a token fragment;
- predicates after a token fragment;
- disabled path behavior;
- enabled path behavior behind an explicit opt-in;
- absence of lexer-code execution through `Parse(...)`.

Future tests should remain deterministic and should not imply execution support before a dedicated implementation phase exists.

## Non-goals

This audit explicitly does not include:

- complete ANTLR compatibility;
- a separate runtime lexer in this PR;
- executable lexer hooks;
- executable lexer predicates;
- a general replay or action-buffering system;
- rollback of external side effects;
- implicit support for advanced modes, channels, or commands;
- lexer `$...` rewriting;
- a new public API;
- a new compiler API;
- any change to generated `Parse(...)` behavior.
