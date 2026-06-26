# Lexer Inline Actions and Predicates Audit

This document records Phase 0 of the lexer inline actions and predicates roadmap. It is an audit only: it does not define support, does not authorize execution, and does not change parser, lexer, source-generator, or runtime behavior.

## Current implementation state

The current implementation has intentionally narrow lexer embedded-code support:

- Grammar-level `@lexer::header`, `@lexer::members`, and `@lexer::footer` are supported only by the source-generator C# path.
- The grammar-level `@lexer::*` support is limited to combined grammars and lexer grammars.
- Parser grammars with `@lexer::*` remain unsupported because no lexer is generated for that grammar shape.
- `@lexer::members` is injected into the existing generated execution context.
- Injecting `@lexer::members` does not create a separate ANTLR-style lexer runtime type or a separate runtime lexer execution context.
- Lexer inline actions remain unsupported.
- Lexer predicates remain unsupported.
- Runtime discovery classifies lexer actions and lexer predicates as unsupported.
- The source-generator C# path collects executable hooks from parser rules only; lexer rules do not produce executable hooks.
- `Parse(...)` remains conservative and must not execute lexer inline actions, lexer predicates, or lexer hooks.

## Scope of lexer inline actions

Future design work must account for the ANTLR lexer inline action forms below without treating them as currently supported:

```antlr
A : 'a' { OnLex(); } ;
A : 'a' { OnLex(); } 'b' ;
A : ('a' { OnLex(); })+ ;
A : 'a' | 'b' { OnLex(); } ;
```

The audit scope includes these concerns:

- The position of an action inside a lexer rule matters: end-of-rule actions, mid-sequence actions, grouped actions, quantified actions, and alternative-specific actions have different execution points.
- The alternative containing the action must be known so a future implementation can avoid treating actions from non-selected alternatives as executed behavior.
- A stable hook index may be needed if future generated code maps each lexer inline action to generated methods or metadata records.
- The expected execution order must follow the lexer tokenization path, including the order of actions inside a single sequence.
- An action in a sequence such as `'a' { OnLex(); } 'b'` raises ordering questions when later elements fail after the action point.
- An action in an alternative such as `'a' | 'b' { OnLex(); }` is alternative-specific and must not be generalized to the whole rule.
- An action inside a quantifier such as `('a' { OnLex(); })+` may execute zero, one, or many times depending on the matched iterations and future rollback rules.
- An action inside a negation must be considered if the grammar model represents such a placement; this audit does not define how to execute it.
- An action inside a lexer mode must be associated with the active mode and any future mode-stack behavior.
- An action inside a combined grammar must remain distinct from parser inline actions and from grammar-level parser named actions.

Lexer inline actions are not currently executable hooks. Any future support must be explicitly designed, explicitly enabled, and source-generator C# first.

## Scope of lexer predicates

Future design work must account for lexer predicate forms such as:

```antlr
A : { IsEnabled() }? 'a' ;
A : 'a' { IsAllowed() }? ;
A : { IsModeEnabled() }? 'a' | 'b' ;
```

The audit scope includes these concerns:

- Lexer predicates change tokenization decisions; they decide whether a token path is viable.
- Lexer predicates are not equivalent to lexer actions because predicates influence recognition, while actions are side-effecting code after a path reaches an action point.
- Lexer predicates must be designed separately from lexer inline actions.
- Lexer predicates require a lexer-state-aware model that can account for input position, active mode, mode stack, token type decisions, channels, commands, and any future generated execution state.
- Lexer predicates must not be executed by conservative `Parse(...)`.
- Lexer predicates must remain unsupported until a dedicated predicate phase is designed, implemented, tested, and documented.

## Interaction with grammar-level lexer members

Grammar-level `@lexer::members` already exists in the generated-C# compatibility surface. Its content is injected into the existing generated execution context.

Future lexer inline actions or lexer predicates may want to call members declared by `@lexer::members`. That possibility must not be confused with the existence of a separate lexer runtime. The current implementation has no separate runtime lexer context, no separate ANTLR-style lexer class, and no executable lexer hook pipeline.

A later design phase must decide whether lexer execution should use:

- the existing generated execution context,
- a lexer-specific sub-context inside that generated context, or
- a separate lexer execution context with explicit copy, restore, and state-key responsibilities.

That decision must be made before enabling lexer inline actions or lexer predicates.

## Modes, channels, commands, and tokenization concerns

The audit must account for lexer constructs that affect tokenization, without promising support for executing inline lexer code with those constructs:

- lexer modes;
- lexer commands;
- channels;
- `skip`;
- `more`;
- `type`;
- `pushMode`, `popMode`, and `mode`;
- interactions between inline actions and command execution;
- interactions between predicates and command or mode decisions;
- ordering between ANTLR commands and target-language actions.

This document does not add support for advanced mode, channel, command, or inline execution semantics. It only records the cases that a future design must review.

## Rollback and side-effect risks

Lexer inline actions and predicates introduce risks that are separate from parser rollback:

- A lexer action could run during a tokenization attempt that is later abandoned.
- External side effects from a lexer action may not be rollback-safe.
- Parser rollback and lexer rollback are different concerns and must not be conflated.
- Parser memoization currently does not imply that a future lexer execution state is captured, restored, or hashed correctly.
- A future design may require a separate lexer state model for input position, mode stack, command effects, pending token decisions, and generated execution state.
- There is currently no general action buffering or replay mechanism for lexer actions.
- There is currently no general rollback mechanism for external side effects.

Any future implementation must state which side effects are parser-managed, lexer-managed, or external and non-rollback-safe.

## Source-generator path versus runtime-inline path

Any future support should start as an explicitly enabled source-generator C# feature. It must not make default runtime parsing execute lexer code.

The runtime-inline expression path remains separate. Lexer execution must not add C# target-language logic to the parser core, `ParserEngine`, `ParserRuntimeFeaturePolicy`, runtime frames, or the grammar model. The generator must not depend on a Roslyn semantic model for deciding lexer action or predicate behavior. This audit does not introduce a new compiler API.

ANTLR-style lexer `$...` rewriting must remain optional and must stay behind an explicit embedded-code transformer. No implicit lexer `$...` rewriting should be added as part of the parser core, source-generator core, or runtime-inline path.

## Proposed future PR sequence

The sequence below is indicative and non-binding:

```text
PR A — audit documentation only
PR B — metadata/indexing tests only, if needed
PR C — lexer context design documentation
PR D — source-generator opt-in lexer inline actions, no predicates
PR E — tests and docs for lexer inline actions
PR F — lexer predicates design
PR G — source-generator opt-in lexer predicates
```

Each future PR should restate whether it is documentation-only, test-only, behavior-changing, or API-changing.

## Existing tests and future tests

Existing test families to preserve include:

- `@lexer::*` supported in lexer grammars.
- `@lexer::*` supported in combined grammars.
- Parser grammars with `@lexer::*` remain unsupported.
- Lexer inline actions remain unsupported.
- Lexer predicates remain unsupported.
- Generated source does not contain executable lexer hooks.

Possible future test families include:

- stable indexing for lexer actions;
- lexer modes;
- quantifiers;
- alternatives;
- predicates before a token fragment;
- predicates after a token fragment;
- disabled lexer execution paths;
- enabled lexer execution paths after explicit opt-in;
- absence of lexer-code execution through conservative `Parse(...)`.

## Non-goals

This audit explicitly excludes:

- complete ANTLR compatibility;
- a separate runtime lexer in this PR;
- executable lexer hooks in this PR;
- executable lexer predicates in this PR;
- general replay or action buffering;
- rollback of external side effects;
- implicit support for advanced modes, channels, or commands;
- implicit lexer `$...` rewriting;
- a new public API;
- a new compiler API;
- any change to `Parse(...)`.


> Lexer inline actions: simple source-generator C# lexer inline actions are now supported only through the explicit opt-in generated path. `Parse(...)` remains conservative; lexer predicates, lexer `$...` rewriting, lexer modes/channels/commands action semantics, runtime-inline lexer execution, a separate runtime lexer, and external side-effect rollback remain unsupported.
