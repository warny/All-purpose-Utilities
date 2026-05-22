# ANTLR4 Compatibility Reference

This document lists ANTLR4 grammar features and their support status in Utils.Parser.

For each feature that behaves differently from standard ANTLR4, a **Usage** section explains how to achieve the equivalent result.

This document must be consulted before modifying any grammar-related component, and updated whenever a feature's support status changes.

> Note: this reference is complementary to `docs/parser/Antlr4CompatibilityMatrix.md` (high-level matrix).
> Use this file for implementation-oriented usage guidance when behavior differs from standard ANTLR4.

---

## Supported — full runtime support

These features work as in standard ANTLR4.

| Feature | Notes |
|---|---|
| `grammar Name;` / `lexer grammar Name;` / `parser grammar Name;` | Combined, lexer-only, and parser-only grammars are all supported. |
| Literal matching `'text'` | Exact string match in lexer and parser rules. |
| Character ranges `'a'..'z'` | Range match in lexer rules. |
| Character classes `[a-zA-Z_0-9]` | Set match, including Unicode ranges. |
| Negated character classes `[^...]` | Inverted set match. |
| Wildcard `.` | Matches any single character (lexer) or any single token (parser). |
| Quantifiers `*`, `+`, `?`, `{n,m}` | All quantifier forms are supported. |
| Alternation `a \| b \| c` | Alternatives are tried in declaration order. |
| Grouping `(...)` | Inline groups are supported in lexer and parser rules. |
| Negation `~a` | Supported in lexer and parser rules. |
| `fragment` rules | Fragment rules are never emitted as tokens; they serve as reusable building blocks. |
| Lexer modes — `mode Name;`, `-> pushMode(...)`, `-> popMode`, `-> mode(...)` | Full mode-stack behaviour is implemented. |
| Lexer commands — `-> skip`, `-> more`, `-> channel(...)`, `-> type(...)` | All seven built-in lexer commands are supported. |
| Maximal munch | Longest-match rule wins; ties are broken by declaration order. |
| Panic mode | An unrecognised character emits an `ERROR` token and advances by one character. |
| `options { caseInsensitive = true; }` | Honoured by the lexer engine. |
| Diagnostic codes | Full set of `UP0xxx`–`UP9xxx` and `PARSER0xx` codes. |

---

## Supported — behaviour differs from standard ANTLR4

These features are supported but work differently. Read the **Usage** section before using them.

---

### `superClass` option

**Standard ANTLR4**: `options { superClass = MyBase; }` sets the generated parser or lexer class's base class.

**Utils.Parser**: `superClass` is parsed and stored as metadata in `EffectiveGrammarOptions.ParserSuperClass` / `LexerSuperClass` and in `GrammarExtensionBinding`. It has no effect on class inheritance.

At runtime, the lexer calls **all** registered `ILexerExtension` instances in sequence — there is no automatic dispatch by `superClass` name. The `superClass` value is readable by an extension via `context.Definition.ExtensionBindings`, which lets the extension decide whether to apply its logic to a given grammar. The runtime enforces one constraint: if `ExtensionBindings.Count > 0` (i.e. the grammar declared `superClass`) but no extensions are registered, a validation error is raised.

**Usage** — implement and register a lexer extension; inspect `ExtensionBindings` to filter by grammar if needed:

```csharp
// Grammar declares:  options { superClass = IndentTracker; }

public class IndentTrackerExtension : ILexerExtension
{
    public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
    {
        // Optionally guard by superClass name:
        bool applies = context.Definition.ExtensionBindings
            .Any(b => b.SuperClassName == "IndentTracker");
        if (!applies) return [];

        // Custom token injection logic here.
        return [];
    }

    public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];
    public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
}

var options = new LexerEngineOptions
{
    Extensions = [new IndentTrackerExtension()]
};
var lexer = new LexerEngine(definition, options);
```

The `GrammarExtensionBinding` record exposes `SuperClassName`, the owning grammar's lexer rule names, declared tokens, and declared channels.

---

### Semantic predicates `{ condition }?`

**Standard ANTLR4**: The predicate body is target-language code evaluated inline during parsing.

**Utils.Parser**: Predicates are parsed and stored. Evaluation is delegated to an `ISemanticPredicateEvaluator` registered in `ParserRuntimeFeaturePolicy`. The default policy returns `NotEvaluated`, which does **not** reject the branch — it acts as if the predicate passed.

**Usage** — implement `ISemanticPredicateEvaluator` and pass it via the policy:

```csharp
public class MyPredicateEvaluator : ISemanticPredicateEvaluator
{
    public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
    {
        // context.PredicateCode  — raw predicate text from the .g4 file
        // context.Rule           — current Rule
        // context.InputPosition  — current token index
        // context.AlternativeIndex / ElementIndex — position within the rule

        if (context.PredicateCode == "IsKeyword()")
            return _keywords.Contains(CurrentToken) 
                ? SemanticPredicateEvaluationResult.Satisfied 
                : SemanticPredicateEvaluationResult.Rejected;

        return SemanticPredicateEvaluationResult.NotEvaluated;
    }
}

var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new MyPredicateEvaluator()
};
var parser = new ParserEngine(definition, policy);
```

> **Important**: memoization is keyed by `(rule, input position, precedence)`. Evaluators must be deterministic for identical invocation contexts.

---

### Inline actions `{ code }`

**Standard ANTLR4**: Action code is target-language code executed as a side effect during parsing.

**Utils.Parser**: Actions are parsed and stored. Execution is delegated to an `IParserActionExecutor` registered in `ParserRuntimeFeaturePolicy`. The default policy returns `NotExecuted`.

**Usage** — implement `IParserActionExecutor`:

```csharp
public class MyActionExecutor : IParserActionExecutor
{
    public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
    {
        // context.ActionCode     — raw action text from the .g4 file
        // context.Rule           — current Rule
        // context.InputPosition  — current token index
        // context.AlternativeIndex / ElementIndex — position within the rule

        Console.WriteLine($"Action in rule {context.Rule.Name}: {context.ActionCode}");
        return ParserActionExecutionResult.Executed;
    }
}

var policy = ParserRuntimeFeaturePolicy.Default with
{
    ParserActionExecutor = new MyActionExecutor()
};
var parser = new ParserEngine(definition, policy);
```

> **Important**: actions may fire in branches that are later rejected by backtracking. There is no rollback mechanism. Keep executors side-effect-free or idempotent where possible.

---

### Rule actions `@init { }` and `@after { }`

**Standard ANTLR4**: `@init` runs before the rule body; `@after` runs after.

**Utils.Parser**: Both are parsed and stored in `Rule.InitAction` / `Rule.AfterAction` as raw text. They are not executed automatically. To act on them, inspect the `Rule` model and invoke custom logic at the appropriate parse-tree traversal step using `ParseTreeCompiler<TContext, TResult>`.

---

### Runtime observation

**Standard ANTLR4**: no built-in scheduling observation API.

**Utils.Parser**: a passive, non-authoritative observer can be attached via `ParserRuntimeFeaturePolicy.RuntimeObserver`. It receives `AlternativeRuntimeObservation` records describing scheduler events in deterministic order without affecting parse outcomes.

**Usage**:

```csharp
var recorder = new RuntimeObservationRecorder();
var policy = ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = recorder };
var parser = new ParserEngine(definition, policy);
parser.Parse(tokens);

string text = RuntimeObservationTextWriter.Write(recorder.Observations);
string json  = RuntimeObservationJsonWriter.Write(recorder.Observations);
```

See `docs/parser/RuntimeObservationAndExportContract.md` for the full contract.

---

## Partially supported

| Feature | Limitation |
|---|---|
| Direct left recursion | Detected at resolution time and handled with a seed-and-extend loop, but not equivalent to all ANTLR4 left-recursive shapes. Emits `LeftRecursivePrecedencePartiallySupported` where applicable. |
| Precedence predicates `precpred(_ctx, N)` | Regex-based extraction. Falls back to precedence `0` if the level cannot be parsed. Only recognised in direct left-recursive rules. |
| Right-associativity `<assoc=right>` | Parsed and applied during left-recursive extension. Only meaningful within direct left-recursive rules; subject to the same partial-parity limits as left recursion. |
| Labels — `e=expr` and `ids+=ID` | Applied when the labelled item is a `RuleRef`. Ignored on literals and other non-reference items. |
| `import` | Fully resolved when grammars are compiled as a project set (`Antlr4GrammarProjectCompiler`). Single-file compilation emits `ImportParsedButNotResolved`. |
| `options { tokenVocab = MyLexer; }` | Dependency loading depends on available resolver inputs at compilation time. |
| Unknown grammar options (`visitor`, `listener`, `contextSuperClass`, …) | Parsed and preserved as raw option metadata, but rejected with `UP1021 UnsupportedAntlrOptionIgnored`. Recognised options that do not trigger this diagnostic are: `tokenVocab`, `superClass`, `caseInsensitive`, and `language`. |
| Lexer commands | Only the seven built-in commands are accepted. Any unknown command name is rejected with `UnsupportedLexerCommand`. |
| `tokens { }` block | Recognised, stored in `GrammarExtensionBinding.DeclaredTokens`, and reported explicitly with `UP1002 TokensBlockIgnored`. Not mapped to runtime token definitions. |
| `channels { }` block | Recognised, stored in `GrammarExtensionBinding.DeclaredChannels`, and reported explicitly with `UP1003 ChannelsBlockIgnored`. Not mapped to runtime channel semantics beyond `-> channel(...)` command support. |

---

## Parsed and stored — no runtime semantics

These constructs are recognised without error but produce no runtime effect.

| Construct | Stored where | Runtime behaviour |
|---|---|---|
| Rule parameters `rule[int x]` | `Rule.Parameters` as raw text | No argument passing, no typed binding, no invocation frame. |
| Rule returns `returns [int x]` | `Rule.ReturnType` as raw text | Recognized, ignored by runtime semantics, and reported with `UP1023 RuleReturnsIgnored`. |
| `locals [...]` | Parsed, discarded | No runtime semantics. |
| `throws ExceptionType` | Parsed, discarded | No runtime semantics. |
| `catch [...] {...}` / `finally {...}` | Parsed, discarded | No runtime semantics. |
| Grammar-level actions `@header`, `@members`, etc. | `ParserDefinition.Actions` | Metadata only; not executed. |
| Other rule actions (not `@init`/`@after`) | Parsed, discarded | `ActionIgnored` diagnostic emitted. |

---

## Runtime metadata boundary

Continuation metadata descriptors are internal runtime metadata.
They are prepared after grammar resolution.
They are not ANTLR grammar constructs.
They are preserved/normalized as descriptive metadata only.
They are never executed, replayed, or resumed.

---

## Not supported — intentional exclusions

These capabilities are outside the current runtime model by design. Attempting to use them produces explicit diagnostics or has no effect.

| Capability | Diagnostic / behaviour |
|---|---|
| Indirect left recursion | Error `UP0xxx` — explicitly unsupported. Rewrite as direct recursion or factor out the common prefix. |
| Adaptive LL / GLL prediction | Not implemented. The runtime uses sequential backtracking with memoization. |
| Error recovery (resync, token insertion/deletion) | No recovery strategy. `ParserEngine.Parse()` returns `ErrorNode` on failure. |
| Speculative parsing / continuation replay | Not implemented. Continuation metadata exists but is descriptive only. |
| Parse-forest generation | A single parse tree is produced. |
| Async or parallel parsing | Not implemented. |
| Target-language action execution engines | Actions are stored as raw text strings. Execution requires a custom `IParserActionExecutor`. |
| `superClass` class inheritance (generated code) | `superClass` is repurposed as an extension-binding key. See the **Usage** section above. |

---

## Diagnostics quick reference

| Prefix | Severity | Meaning |
|---|---|---|
| `UP0xxx` | Error | Blocking — unresolved rules, grammar violations, import failures |
| `UP1xxx` | Warning | Compatibility behavior that is recognized and ignored / partially normalized (e.g. `UP1002` tokens block ignored, `UP1003` channels block ignored, `UP1020` lexer command rejected, `UP1021` option ignored, `UP1023` rule returns ignored) |
| `UP5xxx` | Warning | Best-effort recovery warnings (trailing tokens, ambiguity) |
| `UP8xxx` | Info | Informational runtime events |
| `UP9xxx` | Debug | Detailed execution traces |
| `PARSER0xx` | Warning/Info | Runtime safety guards (cycle detection, non-progressive loop termination) |
| `APU0xxx` | Error/Warning | Source-generator diagnostics (Roslyn pipeline) |

Full descriptor table: `ParserDiagnostics.All`.
