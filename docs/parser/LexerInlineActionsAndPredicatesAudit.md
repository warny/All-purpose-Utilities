# Lexer Inline Actions and Predicates Audit

This document records Phase 0 of the lexer inline actions and predicates roadmap. It is an audit only: it does not define support, does not authorize execution, and does not change parser, lexer, source-generator, or runtime behavior.

## Current implementation state

The current implementation has intentionally narrow lexer embedded-code support:

- Grammar-level `@lexer::header`, `@lexer::members`, and `@lexer::footer` are supported only by the source-generator C# path.
- The grammar-level `@lexer::*` support is limited to combined grammars and lexer grammars.
- Parser grammars with `@lexer::*` remain unsupported because no lexer is generated for that grammar shape.
- `@lexer::members` is injected into the existing generated execution context.
- Injecting `@lexer::members` does not create a separate ANTLR-style lexer runtime type or a separate runtime lexer execution context.
- Simple lexer inline actions are supported only in the generated-C# opt-in path.
- Simple lexer predicates are supported only in the generated-C# opt-in path.
- Generated-C# opt-in regression coverage includes lexer rule references, fragments, simple quantifiers, repeated source text at distinct positions, false-predicate/action ordering, and already-supported lexer commands/modes: `skip`, `channel(...)`, `type(...)`, `more`, `mode(...)`, `pushMode(...)`, and `popMode`.
- Runtime discovery classifies lexer actions and lexer predicates as unsupported.
- The source-generator C# path collects parser executable hooks and limited generated-C# opt-in lexer action/predicate hooks; runtime-inline discovery still treats lexer hooks as non-executable.
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
- Simple lexer predicates now have a dedicated generated-C# opt-in evaluator. Runtime-inline predicate execution, `$...` rewriting, rollback of external effects, and full ANTLR lexer compatibility remain unsupported.

## Interaction with grammar-level lexer members

Grammar-level `@lexer::members` already exists in the generated-C# compatibility surface. Its content is injected into the existing generated execution context.

Future lexer inline actions or lexer predicates may want to call members declared by `@lexer::members`. That possibility must not be confused with the existence of a separate lexer runtime. The current implementation has no separate runtime lexer context, no separate ANTLR-style lexer class, and no executable lexer hook pipeline.

A later design phase must decide whether lexer execution should use:

- the existing generated execution context,
- a lexer-specific sub-context inside that generated context, or
- a separate lexer execution context with explicit copy, restore, and state-key responsibilities.

That decision must be made before enabling lexer inline actions or lexer predicates.

## Modes, channels, commands, and tokenization concerns

The audit accounts for lexer constructs that affect tokenization and records the currently tested generated-C# opt-in boundary for the subset already supported by the runtime:

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

The current generated-C# opt-in regression suite covers the already-supported runtime command/mode subset listed above. It confirms that accepted actions run before accepted commands, predicates reject only the current path, and commands from rejected paths are not applied. Read-only `$type`, `$channel`, and `$mode` actions read passive `LexerActionExecutionContext.TokenType`, `Channel`, and `Mode` values before `type(...)`, `channel(...)`, `mode(...)`, `pushMode(...)`, or `popMode` affect emitted tokens or later tokenization. This does not add advanced ANTLR mode/channel/command semantics beyond what the runtime already supports.

## Rollback and side-effect risks

Lexer inline actions and predicates have boundaries distinct from parser rollback:

- predicates execute during path exploration, but expose no supported mutation result; `false` rejects only the current path and `true` continues it;
- lexer `$...` writes in predicates are forbidden, so a rejected predicate commits no runtime-managed lexer mutation;
- arbitrary effects performed by user predicate code are external and are not promised to roll back;
- action occurrences are collected while matching, but rejected paths never execute them;
- only the accepted token's actions execute, sharing one fresh, acceptance-local `LexerActionExecutionResult`;
- later action assignments replace earlier assignments to the same result property, preserving the tested last-write-wins behavior;
- result requests are applied before accepted commands, which remain authoritative;
- there is no general action buffering, replay, lexer snapshot manager, or external-effect rollback.

Lexer-owned operational state is split by lifetime rather than one persistent bucket. Persistent `LexerEngine` fields cover the current mode, mode stack, `more` accumulation, and associated `more` start position between token recognitions. A tokenization session owns the `TextReaderBuffer` and current input position, emitted-token collection, and extension state for that run. Attempt- and acceptance-local values include best-match data, collected commands and action occurrences, token/chunk construction data, and the accepted token's `LexerActionExecutionResult`. Parser memoization and `IParserExecutionStateManager` do not capture, restore, or hash those lexer categories. Reusing the parser manager or adding a generic `isLexer` switch would conflate two different execution models. A lexer-specific state contract should be considered only in a separate PR backed by a concrete need.

Mutable fields and objects injected through `@lexer::members` belong to the generated execution context. Delayed action execution prevents rejected alternatives from running those actions, but does not make accepted user code transactional. I/O, shared-service mutation, changes to unmanaged objects, later exceptions, `skip`, and eventual parser failure remain outside rollback guarantees.

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
- Simple lexer inline actions are supported only in the generated-C# opt-in path.
- Simple lexer predicates are supported only in the generated-C# opt-in path.
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
- runtime-inline lexer hooks;
- runtime-inline lexer predicates;
- general replay or action buffering;
- rollback of external side effects;
- implicit support for advanced modes, channels, or commands;
- implicit lexer `$...` rewriting;
- a new public API;
- a new compiler API;
- any change to `Parse(...)`.


> Lexer inline actions and predicates: simple source-generator C# lexer inline actions and simple lexer predicates are now supported only through the explicit opt-in generated path. `Parse(...)` remains conservative. Predicates run during lexer matching and reject only the current path; actions run after token acceptance and before accepted lexer commands are applied. Commands from rejected paths are not applied. `AlternativeIndex` and `ElementIndex` identify the source hook location and are reused across quantified iterations. The tested command/mode boundary covers `skip`, `channel(...)`, `type(...)`, `more`, `mode(...)`, and `pushMode(...)`/`popMode`. Lexer `$...` rewriting is limited to generated-C# opt-in inline action reads (`$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos`) and simple `$type = ...`/`$channel = ...`/`$mode = ...` statement writes; lexer predicate attributes, runtime-inline lexer execution, a separate runtime lexer, complete ANTLR embedded-code command/mode semantics, generalized action buffering/replay, general lexer rollback, and external side-effect rollback remain unsupported.


## Lexer `$...` rewrite audit note

The generated-C# opt-in path has an optional C# transformer-only lexer attribute rewrite aligned with parser attribute rewriting. `EmbeddedLexerAttributeRewriter` rewrites inline lexer action reads of `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos` into helper calls in the generated execution context. `$text` is rewritten to `GetRequiredLexerText(context)` and reads passive `LexerActionExecutionContext.Text`: the accepted token/chunk text available to the action context. The tested boundary locks that actions reached before or after fragments, inside fragments, or through lexer rule references receive context-level accepted text rather than a guaranteed fragment-local slice. `skip`, `type(...)`, and `channel(...)` actions read `$text` before the accepted command is applied. `more` actions read the current accepted chunk before accumulation; in the current runtime, `M : 'm' { First = $text; } -> more ; A : 'a' { Second = $text; } ;` over `ma` yields `First == "m"` and `Second == "a"`. `$type`, `$channel`, and `$mode` rewrite to `GetRequiredLexerType(context)`, `GetRequiredLexerChannel(context)`, and `GetRequiredLexerMode(context)` and read `LexerActionExecutionContext.TokenType`, `Channel`, and `Mode`. `$line` rewrites to `GetRequiredLexerLine(context)` and reads `LexerActionExecutionContext.Line`; `$pos` rewrites to `GetRequiredLexerPos(context)` and reads `LexerActionExecutionContext.Column`. These values are passive action-context values available before commands execute: `type(...)` does not retag the value read by `$type`, `channel(...)` does not hide the value read by `$channel`, and mode commands affect later tokenization after `$mode` reads the accepting mode for the current token/chunk. `$line` and `$pos` come from `SourceSpan.Line` and `SourceSpan.Column` at the accepted token/chunk start; `$pos` is a 1-based runtime column, not full ANTLR `charPositionInLine` compatibility. Fragment and lexer-rule-reference actions currently read the accepted outer token context metadata. Lexer predicates reject all `$...` attributes with deterministic diagnostics rather than compiling raw invalid C#. The default no-op transformer still preserves source unchanged, and runtime-inline lexer actions/predicates remain unsupported. `$index`, `$int`, `$token`, `$start`, `$stop`, `$ctx`, and `$input` remain deferred.

## Lexer `$type` / `$channel` / `$mode` write edge-case stabilization

The generated-C# opt-in path supports only simple lexer action write statements for `$type`, `$channel`, and `$mode`: identifier or string assignments such as `$type = IDENTIFIER;`, `$type = "IDENTIFIER";`, `$channel = HIDDEN;`, `$channel = "HIDDEN";`, `$mode = SECOND;`, and `$mode = "SECOND";`. `EmbeddedLexerAttributeRewriter` rewrites those statements to `SetLexerType(result, "...")`, `SetLexerChannel(result, "...")`, or `SetLexerMode(result, "...")`. Generated hooks receive `LexerActionExecutionContext context` and `LexerActionExecutionResult result`; they do not mutate `Token` directly. The helpers write only `LexerActionExecutionResult.TokenType`, `LexerActionExecutionResult.Channel`, and `LexerActionExecutionResult.Mode`.

`LexerEngine` constructs the accepted token, executes accepted lexer actions, applies `result.TokenType`, `result.Channel`, and `result.Mode`, and then executes lexer commands. This locks the write-before-command contract: action writes happen first, commands remain authoritative, `type(...)` can override a previous `$type = ...`, `channel(...)` can override a previous `$channel = ...`, `mode(...)` can override a previous `$mode = ...`, `skip` remains authoritative over emission, and `more` keeps the existing chunking semantics for intermediate and final chunks. `$mode = ...` replaces the current mode like `mode(...)`; it does not push or pop modes.

Reads remain context-passive even when a write appears earlier in the same body. `$type`, `$channel`, and `$mode` read `LexerActionExecutionContext.TokenType`, `LexerActionExecutionContext.Channel`, and `LexerActionExecutionContext.Mode`; they do not read back pending `LexerActionExecutionResult` mutations. Regression coverage stabilizes last-write-wins, `$type` plus `$channel`/`$mode` in the same action, multiple actions in one accepted token, contradictory writes, fragments, lexer rule references, quantifiers, rejected alternatives, whitespace and newline variations, identifier/string/escaped-string values, command override, direct `Token.RuleName`/`Token.Channel` observation and following lexer mode behavior, and deterministic diagnostics for unsupported complex forms.

Unsupported forms remain unsupported and diagnostic-only: `$text = ...`, `$line = ...`, `$pos = ...`, compound/coalescing/increment writes such as `$mode += ...`, `$type += ...`, `$channel ??= ...`, `$mode++`, `$type++`, `$channel++`, dotted/chained writes, expression writes, embedded assignment expressions, `ref`/`out`, lexer `$...` in predicates, runtime-inline lexer execution, separate runtime lexer execution, general lexer action buffering/replay, general lexer rollback, and full ANTLR lexer compatibility.
