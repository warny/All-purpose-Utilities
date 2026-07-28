# Production Support Contract for `Utils.Parser 2.0.0-rc.1`

## 1. Purpose and scope

This document is the normative product-support contract for `Utils.Parser 2.0.0-rc.1`. This version is a release candidate: it is suitable for evaluation and controlled production use within the boundaries below, but it does not yet carry the final binary-compatibility promise of `2.0.0`.

`Utils.Parser` implements a tested subset of ANTLR4 grammar ingestion and execution; it is not a drop-in or exhaustive replacement for ANTLR4. The supported scope is grammars owned or reviewed by trusted developers, validated with application-specific tests, and resolved in a controlled environment. Arbitrary grammars or embedded code supplied by untrusted users are excluded.

## 2. Supported surfaces

- **Runtime project compilation:** `Antlr4GrammarProjectCompiler` composes a controlled set of grammar sources, resolves dependencies, and produces a runtime definition.
- **Direct runtime use:** callers can build or convert a `ParserDefinition`, then use `LexerEngine`, `ParserEngine`, or `CompiledGrammar` directly.
- **Roslyn generation:** `Utils.Parser.Generators` parses each configured `.g4` file at build time and emits a local grammar facade. Generated import composition is expressly excluded below.
- **Conservative parsing:** generated `Parse(...)` uses the default conservative policy and does not execute generated embedded-code hooks.
- **Explicit embedded-code parsing:** generated `ParseWithEmbeddedCode(...)` selects the generated-C# opt-in path. Runtime-inline expressions instead require explicit preparation and policy installation.
- **Advanced APIs:** runtime feature policies, rule-call policies, transformers, execution contexts, and observation APIs are supported only under their documented explicit contracts. They are never enabled merely because related metadata exists.

`ParserEngine` remains the final authority for parse success, acceptance, diagnostics, and the produced tree on every runtime surface.

## 3. Normative statuses

- **Stable:** guaranteed for the RC subset, with deterministic behavior covered by tests. RC stabilization commitments in section 9 apply.
- **Supported under option:** executable only after the documented explicit opt-in, policy, transformer, or generation option is selected; the conservative default is unchanged.
- **Experimental:** usable and tested in a bounded form, but its API or detailed behavior may change between release candidates with documentation.
- **Metadata-only:** syntax or information is recognized and preserved, but it has no execution authority. This status never means supported execution.
- **Rejected with diagnostic:** the construct is not accepted for the relevant surface and produces a documented deterministic diagnostic.
- **Preparatory/internal:** infrastructure exists for future work but is not a consumer execution contract.
- **Out of scope for `2.0.0-rc.1`:** no production-support commitment is made for that capability in this RC.

## 4. Support matrix

| Area | Runtime / project compiler | Generator and `Parse(...)` | Explicit opt-in path | RC status and limits |
|---|---|---|---|---|
| Combined, lexer, and parser grammars; lexer and parser rules | Ingested, resolved, tokenized, and parsed within the tested subset | Local definitions are emitted; `Grammar`, `Tokenize(...)`, and conservative `Parse(...)` execute them | Generated hooks require `ParseWithEmbeddedCode(...)` | **Stable** for controlled, tested local grammars; not exhaustive ANTLR4 parity |
| Literals, groups, alternatives, and quantifiers | Executed by lexer/parser runtime | Emitted and executed conservatively | No opt-in required | **Stable** within documented diagnostics and non-progress guards |
| Fragments, lexer modes, and built-in lexer commands | Fragments, mode stack, `skip`, `more`, `channel`, `type`, `pushMode`, `popMode`, and `mode` execute | Local constructs are emitted and tokenized | Limited lexer hooks use generated C# only | **Stable** for core lexer behavior; embedded lexer code is limited as below |
| Declared channels and `tokens {}` | Names are recognized and preserved; declarations do not create general runtime channel/token semantics | Metadata is emitted with compatibility diagnostics | `-> channel(...)` and `-> type(...)` remain operational commands | **Metadata-only** declarations; unsupported meaning is **rejected with diagnostic** |
| Direct left recursion and precedence / `<assoc=right>` | Seed-and-extend execution with safeguards | Local rules use the same runtime | None | **Experimental**: tested direct shapes only, not all ANTLR transformations or indirect recursion |
| Direct/transitive imports, cycles, collisions, local masking, and local root | Project compiler uses the common deterministic composition plan; cycles/missing/ambiguous inputs diagnose; the entry grammar owns the root | The generator emits the same effective rule selection from the common composition plan | None | Runtime and generator: **Stable** within tested deterministic composition rules |
| `tokenVocab` | Lexer-only dependency visibility is composed when resolver inputs exist | Effective lexer rules, fragments, modes, tokens, and channels are emitted without parser rules | None | Runtime and generator: **Stable** for controlled project inputs |
| Grammar/rule options | `caseInsensitive` executes; supported dependency options participate in compilation; other values may be preserved | Local supported values are emitted | None | `superClass` and unsupported options are **Metadata-only** or **rejected with diagnostic**; no inheritance execution |
| Rule parameters and call arguments | Declarations and raw arguments are preserved; conservative parsing performs no automatic binding | `Parse(...)` preserves metadata and does not bind | Explicit runtime literal policies, helpers, or generated positional binding when `UtilsParserEnableGeneratedRuleArgumentBinding=true` | Metadata by default; bounded literal binding is **Supported under option**; arbitrary expressions and typed ANTLR signatures are out of scope |
| Returns, locals, and rule-reference labels | Descriptors and parser-managed frame/call-result storage exist; no automatic typed semantics | Conservative parsing does not expose or execute them | Generated helpers and the optional C# transformer provide narrow current-rule/labeled-return access | Declarations are **Metadata-only**; helper-based managed state is **Supported under option**; no automatic propagation or typed contexts |
| Parser predicates and inline actions | Preserved; unevaluated predicates do not become authoritative and actions do not execute by default | `Parse(...)` does not execute generated hooks | Runtime-inline prepared expressions or generated C# through `ParseWithEmbeddedCode(...)` | **Supported under option** for documented positions and indexing only; no general ANTLR embedded-code compatibility |
| Parser `@init` and `@after` | Preserved but not executed by the runtime-inline expression path | `Parse(...)` does not execute them | Generated C# lifecycle executor through `ParseWithEmbeddedCode(...)` | **Supported under option** with parser-managed state snapshots |
| Parser named actions | Metadata in runtime paths | Supported C# `@header`, `@parser::header`, `@members`, `@parser::members`, `@footer`, and `@parser::footer` are injected at documented locations; other names diagnose | Compiled as consumer project source, not interpreted by the parser | Limited generated-C# bridge is **Supported under option**; other forms are **Rejected with diagnostic** |
| Lexer embedded code | Runtime-inline preparation does not execute lexer code | `Parse(...)` is conservative | Generated-C# simple predicates/actions and limited `$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos` reads; limited writes to type/channel/mode | **Experimental / Supported under option**; no complete lexer embedded-code model |
| Diagnostics | `ParserEngine` and grammar diagnostics remain authoritative | Roslyn adds generator diagnostics without overriding runtime acceptance | Opt-in compilation may also produce C# or transformation diagnostics | **Stable** where documented; metadata diagnostics do not grant execution authority |
| Generator incrementality | Not applicable | Per-file parsing is reusable; collected project validation/emission may rerun globally | Option changes can regenerate project outputs | **Experimental** performance characteristic, not a guarantee of isolated downstream emission |
| Concurrent execution | A separate `CompiledGrammar` may be created for each concurrent operation; every individual parse remains sequential | The generated static `Grammar` facade caches one `CompiledGrammar`, including one `LexerEngine` and one `ParserEngine`; concurrent `Parse(...)` or `Tokenize(...)` calls on that shared instance are not safe | Callers must synchronize access to the static facade or create a distinct `CompiledGrammar` per concurrent operation; supplied execution contexts and reusable generated policies also carry mutable state and must not be shared concurrently | Parsing-internal parallelism is **out of scope**; no shared runtime engine or mutable execution context has a thread-safety guarantee |
| Rollback and external effects | Parser-managed frame, seed, call-result, and configured execution state participate in documented snapshots | Conservative `Parse(...)` executes no hooks | Generated context snapshots cover managed state at parser backtracking boundaries | Managed rollback is **Supported under option**; external I/O, global/static mutation, and arbitrary object graphs are never fully rolled back |

## 5. Conservative-path guarantees

Generated `Parse(...)` does not execute generated embedded code. An action that was not executed and a predicate that was not evaluated never acquire implicit authority over acceptance. Advanced behavior requires an explicit API, policy, transformer, or build option. During the `2.0.0` RC series, the guaranteed subset will not receive silent parse-tree-shape or diagnostic-contract breaks.

Metadata—including continuations, shared-prefix plans, arguments, returns, locals, labels, imports recorded on a definition, and unsupported options—is descriptive until an expressly documented execution path consumes it.

## 6. Opt-in features

- The **runtime-inline expression path** uses `ExpressionEmbeddedCodePreparer`, an explicit transformer, a caller-supplied expression compiler, prepared registries, and an explicitly installed runtime policy. It covers bounded parser predicates and inline parser actions, not lifecycle or lexer code.
- The **source-generator C# path** emits supported C# hooks and compiles them as part of the consuming project. `ParseWithEmbeddedCode(...)` activates the generated policy; `CreateRuntimePolicy(executionContext, basePolicy)` exposes explicit policy binding.
- **Generated argument binding** is disabled by default. With `UtilsParserEnableGeneratedRuleArgumentBinding=true`, only exact-arity positional simple literals and allowlisted declared types are bound on `ParseWithEmbeddedCode(...)` overloads without `basePolicy`. The `basePolicy` overload preserves the caller's rule-call policy.
- **Lifecycle hooks** `@init` and `@after` execute only in the generated-C# opt-in path.
- **Transactional state** covers parser-managed frame values, pending seeds, call results, and generated execution-context snapshots at documented backtracking boundaries. It does not buffer actions, replay them, deep-clone arbitrary graphs, or undo external effects.

Embedded code remains target-language source, not a claim of general ANTLR4 action compatibility.

## 7. Imports

### Runtime / project compiler

`Antlr4GrammarProjectCompiler` consumes the common composition plan. Tested behavior includes direct and transitive traversal, full-import versus `tokenVocab` visibility, local masking, deterministic imported collisions, diamond deduplication, cycles, missing or ambiguous dependencies, modes, entry-owned options/actions/root, and provenance. Single-file conversion cannot resolve a project dependency without resolver inputs.

### Generator

The generator projects the shared composition plan into an effective emission model. Direct and transitive full imports emit selected parser and lexer rules, fragments, and modes; `tokenVocab` emits lexer declarations only. The entry grammar retains its root, options, and grammar actions. `APU0107` covers uniquely resolved local and imported parser targets and remains silent for unresolved, lexer-only, source-ambiguous, or rule-collision targets.

Generated import composition is **Supported under option** for the deterministic subset described above. Unqualified aliased imports retain the runtime compatibility behavior; qualified `Alias.rule` calls remain unsupported. Per-file parsing is incremental, while graph changes recompute project composition and replace affected generated output without retaining stale declarations.

## 8. Trust model

The RC covers grammars written or reviewed by trusted developers, embedded C# treated exactly like source code in the consuming project, and imports resolved from a controlled environment. It offers no sandbox for hostile grammars or code, no safe evaluation boundary for untrusted C#, and no guarantee against malicious resource consumption or side effects.

## 9. Stability during the RC

- Documented diagnostic identifiers, severities, and trigger conditions are treated as RC contracts.
- The guaranteed subset will not receive silent parse-tree-shape changes.
- Public APIs may still change between release candidates when the change is documented and accompanied by migration notes; final `2.0.0` binary stability is not promised yet.
- Experimental or opt-in behavior will not become enabled implicitly.
- Explicit, tested bug fixes may correct behavior during the RC and will be called out in release notes.

## 10. Non-goals

`2.0.0-rc.1` does not promise exhaustive ANTLR4 compatibility, GLL, adaptive LL, continuation replay, shared-prefix execution, parsing-internal parallelism, async runtime parsing, full rollback of external effects, action buffering, typed ANTLR rule signatures, arbitrary argument expressions, complete lexer embedded code, or a sandbox for untrusted code execution.

## 11. Package-train distribution boundary

The `2.0.0-rc.1` parser packages share one centrally declared version. Runtime package dependencies are `Source -> Diagnostics -> Parser`, with `Antlr4.Common -> Parser`; `Parser.Expressions` depends on `Parser` and `omy.Utils` 1.2.2. The generator is a compiler analyzer package that embeds only its Source, Diagnostics, and Antlr4.Common compiler-host support DLLs. Package-only consumers are restored with an isolated global-package cache and source mapping that resolves every `omy.*` candidate from the local feed. This distribution validation does not broaden the functional compatibility matrix and does not establish reproducibility, performance, trimming/AOT, signing, or transactional multi-package publication guarantees.
