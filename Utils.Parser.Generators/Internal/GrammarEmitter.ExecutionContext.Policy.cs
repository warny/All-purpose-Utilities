using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Emits the grammar-specific execution-state manager.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitExecutionStateManager(StringBuilder sb, string contextClassName)
    {
        sb.AppendLine("    /// <summary>Captures and restores generated parser execution-context state manually.</summary>");
        sb.AppendLine("    private sealed class GeneratedExecutionStateManager : IParserExecutionStateManager");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine("        private readonly global::System.Action<global::Utils.Parser.Runtime.ParserRuleCallResult?> _syncCallResult;");
        sb.AppendLine("        private readonly global::System.Func<global::Utils.Parser.Runtime.ParserRuleParameterSeedStore?> _getSeedsFromFrame;");
        sb.AppendLine("        private readonly global::System.Action<global::Utils.Parser.Runtime.ParserRuleParameterSeedStore?> _syncSeedsToFrame;");
        sb.AppendLine("        private readonly global::System.Func<global::Utils.Parser.Runtime.ParserRuleReturnSnapshot?> _getReturnSnapshotFromFrame;");
        sb.AppendLine("        private readonly global::System.Action<global::Utils.Parser.Runtime.ParserRuleReturnSnapshot?> _syncReturnSnapshotToFrame;");
        sb.AppendLine("        private readonly global::System.Func<global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore> _getLabeledResultsFromFrame;");
        sb.AppendLine("        private readonly global::System.Action<global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore?> _syncLabeledResultsToFrame;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated execution-state manager for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedExecutionStateManager({contextClassName} executionContext, global::System.Action<global::Utils.Parser.Runtime.ParserRuleCallResult?> syncCallResult, global::System.Func<global::Utils.Parser.Runtime.ParserRuleParameterSeedStore?> getSeedsFromFrame, global::System.Action<global::Utils.Parser.Runtime.ParserRuleParameterSeedStore?> syncSeedsToFrame, global::System.Func<global::Utils.Parser.Runtime.ParserRuleReturnSnapshot?> getReturnSnapshotFromFrame, global::System.Action<global::Utils.Parser.Runtime.ParserRuleReturnSnapshot?> syncReturnSnapshotToFrame, global::System.Func<global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore> getLabeledResultsFromFrame, global::System.Action<global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore?> syncLabeledResultsToFrame)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext ?? throw new global::System.ArgumentNullException(nameof(executionContext));");
        sb.AppendLine("            _syncCallResult = syncCallResult ?? throw new global::System.ArgumentNullException(nameof(syncCallResult));");
        sb.AppendLine("            _getSeedsFromFrame = getSeedsFromFrame ?? throw new global::System.ArgumentNullException(nameof(getSeedsFromFrame));");
        sb.AppendLine("            _syncSeedsToFrame = syncSeedsToFrame ?? throw new global::System.ArgumentNullException(nameof(syncSeedsToFrame));");
        sb.AppendLine("            _getReturnSnapshotFromFrame = getReturnSnapshotFromFrame ?? throw new global::System.ArgumentNullException(nameof(getReturnSnapshotFromFrame));");
        sb.AppendLine("            _syncReturnSnapshotToFrame = syncReturnSnapshotToFrame ?? throw new global::System.ArgumentNullException(nameof(syncReturnSnapshotToFrame));");
        sb.AppendLine("            _getLabeledResultsFromFrame = getLabeledResultsFromFrame ?? throw new global::System.ArgumentNullException(nameof(getLabeledResultsFromFrame));");
        sb.AppendLine("            _syncLabeledResultsToFrame = syncLabeledResultsToFrame ?? throw new global::System.ArgumentNullException(nameof(syncLabeledResultsToFrame));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Captures the current generated execution-context state after syncing pending seeds and labeled results from the active frame.</summary>");
        sb.AppendLine("        public object Capture()");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext._pendingChildSeeds = _getSeedsFromFrame();");
        sb.AppendLine("            _executionContext._currentRuleReturnSnapshot = _getReturnSnapshotFromFrame();");
        sb.AppendLine("            _executionContext._labeledChildCallResults = _getLabeledResultsFromFrame();");
        sb.AppendLine("            return _executionContext.Fork();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Gets the current generated execution-context state key after syncing pending seeds and labeled results from the active frame.</summary>");
        sb.AppendLine("        public global::Utils.Parser.Runtime.ParserExecutionStateKey GetCurrentStateKey()");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext._pendingChildSeeds = _getSeedsFromFrame();");
        sb.AppendLine("            _executionContext._currentRuleReturnSnapshot = _getReturnSnapshotFromFrame();");
        sb.AppendLine("            _executionContext._labeledChildCallResults = _getLabeledResultsFromFrame();");
        sb.AppendLine("            return _executionContext.GetExecutionStateKey();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Restores the generated execution context from a compatible snapshot, then syncs call results, pending seeds, and labeled results to the current frame.</summary>");
        sb.AppendLine("        public void Restore(object snapshot)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (snapshot is not {contextClassName} contextSnapshot)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new global::System.ArgumentException(");
        sb.AppendLine($"                    \"Snapshot must be a {contextClassName} instance.\",");
        sb.AppendLine("                    nameof(snapshot));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            _executionContext.CopyFrom(contextSnapshot);");
        sb.AppendLine("            _syncCallResult(_executionContext._lastChildCallResult);");
        sb.AppendLine("            _syncSeedsToFrame(_executionContext._pendingChildSeeds);");
        sb.AppendLine("            _syncReturnSnapshotToFrame(_executionContext._currentRuleReturnSnapshot);");
        sb.AppendLine("            _syncLabeledResultsToFrame(_executionContext._labeledChildCallResults);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generated runtime dispatcher by applying the stable hook selection algorithm to descriptor-provided domain data.
    /// </summary>
    private static class EmbeddedHookDispatcherEmitter
    {
        /// <summary>
        /// Writes the dispatcher class, method signature, ordered hook comparisons, hook invocation, success return, and fallback return.
        /// </summary>
        /// <param name="sb">Source builder receiving generated C#.</param>
        /// <param name="hooks">Already ordered hooks selected by the wrapper for one owner and one hook kind.</param>
        /// <param name="contextClassName">Generated execution context class name.</param>
        /// <param name="descriptor">Immutable descriptor for the parser/lexer and predicate/action variation.</param>
        public static void Emit(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> hooks, string contextClassName, EmbeddedHookDispatcherDescriptor descriptor)
        {
            sb.AppendLine($"    /// <summary>{descriptor.ClassSummary}</summary>");
            sb.AppendLine($"    private sealed class {descriptor.ClassName} : {descriptor.InterfaceName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
            sb.AppendLine($"        private readonly {descriptor.InterfaceName} _fallback;");
            sb.AppendLine();
            sb.AppendLine($"        /// <summary>{descriptor.ConstructorSummary}</summary>");
            sb.AppendLine($"        public {descriptor.ClassName}({contextClassName} executionContext, {descriptor.InterfaceName} fallback)");
            sb.AppendLine("        {");
            sb.AppendLine("            _executionContext = executionContext;");
            sb.AppendLine("            _fallback = fallback;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        /// <summary>{descriptor.MethodSummary}</summary>");
            sb.AppendLine($"        public {descriptor.ReturnTypeName} {descriptor.DispatchMethodName}({descriptor.Parameters})");
            sb.AppendLine("        {");
            foreach (var hook in hooks)
            {
                ValidateEmbeddedCodeHook(hook, descriptor.Owner, descriptor.Kind);
                string rawCode = Escape(GetRawEmbeddedCodeText(hook.RawCode));
                sb.AppendLine($"            if (string.Equals({descriptor.ContextParameterName}.Rule.Name, \"{Escape(hook.RuleName)}\", global::System.StringComparison.Ordinal)");
                sb.AppendLine($"                && string.Equals({descriptor.ContextParameterName}.{descriptor.CodePropertyName}, \"{rawCode}\", global::System.StringComparison.Ordinal)");
                sb.AppendLine($"                && {descriptor.ContextParameterName}.AlternativeIndex == {hook.AlternativeIndex}");
                sb.AppendLine($"                && {descriptor.ContextParameterName}.ElementIndex == {hook.ElementIndex})");
                sb.AppendLine("            {");
                foreach (string statement in descriptor.CreateMatchedHookStatements(hook))
                {
                    sb.AppendLine($"                {statement}");
                }
                sb.AppendLine("            }");
                sb.AppendLine();
            }

            sb.AppendLine($"            {descriptor.FallbackStatement}");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Describes the declarative differences between generated parser and lexer runtime dispatchers while keeping one emission algorithm.
    /// </summary>
    private readonly struct EmbeddedHookDispatcherDescriptor
    {
        /// <summary>
        /// Initializes immutable dispatcher descriptor data for one runtime dispatch domain.
        /// </summary>
        private EmbeddedHookDispatcherDescriptor(EmbeddedCodeHookOwner owner, EmbeddedCodeHookKind kind, string className, string interfaceName, string classSummary, string constructorSummary, string methodSummary, string returnTypeName, string dispatchMethodName, string parameters, string contextParameterName, string codePropertyName, string invocationStatementFormat, string successStatement, string fallbackStatement)
        {
            Owner = owner;
            Kind = kind;
            ClassName = className;
            InterfaceName = interfaceName;
            ClassSummary = classSummary;
            ConstructorSummary = constructorSummary;
            MethodSummary = methodSummary;
            ReturnTypeName = returnTypeName;
            DispatchMethodName = dispatchMethodName;
            Parameters = parameters;
            ContextParameterName = contextParameterName;
            CodePropertyName = codePropertyName;
            InvocationStatementFormat = invocationStatementFormat;
            SuccessStatement = successStatement;
            FallbackStatement = fallbackStatement;
        }

        public EmbeddedCodeHookOwner Owner { get; }

        public EmbeddedCodeHookKind Kind { get; }

        public string ClassName { get; }

        public string InterfaceName { get; }

        public string ClassSummary { get; }

        public string ConstructorSummary { get; }

        public string MethodSummary { get; }

        public string ReturnTypeName { get; }

        public string DispatchMethodName { get; }

        public string Parameters { get; }

        public string ContextParameterName { get; }

        public string CodePropertyName { get; }

        public string InvocationStatementFormat { get; }

        public string SuccessStatement { get; }

        public string FallbackStatement { get; }
        /// <summary>
        /// Describes parser semantic predicate dispatching without changing the shared comparison order.
        /// </summary>
        public static readonly EmbeddedHookDispatcherDescriptor ParserPredicate = new(
            EmbeddedCodeHookOwner.Parser,
            EmbeddedCodeHookKind.SemanticPredicate,
            "GeneratedSemanticPredicateEvaluator",
            "ISemanticPredicateEvaluator",
            "Dispatches runtime semantic predicate contexts to generated C# predicate hooks.",
            "Initializes a generated semantic predicate evaluator for one execution context.",
            "Evaluates a generated semantic predicate hook when the runtime context matches exactly.",
            "SemanticPredicateEvaluationOutcome",
            "Evaluate",
            "SemanticPredicateEvaluationContext context",
            "context",
            "PredicateCode",
            "return _executionContext.{0}(context) ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected;",
            string.Empty,
            "return _fallback.Evaluate(context);");

        /// <summary>
        /// Describes parser inline action dispatching, including its void hook call followed by the unchanged executed outcome.
        /// </summary>
        public static readonly EmbeddedHookDispatcherDescriptor ParserAction = new(
            EmbeddedCodeHookOwner.Parser,
            EmbeddedCodeHookKind.InlineAction,
            "GeneratedParserActionExecutor",
            "IParserActionExecutor",
            "Dispatches runtime parser action contexts to generated C# action hooks.",
            "Initializes a generated parser action executor for one execution context.",
            "Executes a generated parser action hook when the runtime context matches exactly.",
            "ParserActionExecutionOutcome",
            "Execute",
            "ParserActionExecutionContext context",
            "context",
            "ActionCode",
            "_executionContext.{0}(context);",
            "return ParserActionExecutionOutcome.Executed;",
            "return _fallback.Execute(context);");

        /// <summary>
        /// Describes lexer semantic predicate dispatching with lexer-specific outcomes represented as immutable data.
        /// </summary>
        public static readonly EmbeddedHookDispatcherDescriptor LexerPredicate = new(
            EmbeddedCodeHookOwner.Lexer,
            EmbeddedCodeHookKind.SemanticPredicate,
            "GeneratedLexerPredicateEvaluator",
            "ILexerPredicateEvaluator",
            "Dispatches lexer predicate contexts to generated C# lexer predicate hooks.",
            "Initializes a generated lexer predicate evaluator for one execution context.",
            "Evaluates a generated lexer predicate hook when the runtime context matches.",
            "LexerPredicateEvaluationOutcome",
            "Evaluate",
            "LexerPredicateEvaluationContext context",
            "context",
            "PredicateCode",
            "return _executionContext.{0}(context) ? LexerPredicateEvaluationOutcome.True : LexerPredicateEvaluationOutcome.False;",
            string.Empty,
            "return _fallback.Evaluate(context);");

        /// <summary>
        /// Describes lexer inline action dispatching, including the separate mutable lexer action result parameter.
        /// </summary>
        public static readonly EmbeddedHookDispatcherDescriptor LexerAction = new(
            EmbeddedCodeHookOwner.Lexer,
            EmbeddedCodeHookKind.InlineAction,
            "GeneratedLexerActionExecutor",
            "ILexerActionExecutor",
            "Dispatches accepted lexer inline actions to generated C# lexer action hooks.",
            "Initializes a generated lexer action executor for one execution context.",
            "Executes a generated lexer action hook when the runtime context matches.",
            "LexerActionExecutionOutcome",
            "Execute",
            "LexerActionExecutionContext context, LexerActionExecutionResult result",
            "context",
            "ActionCode",
            "_executionContext.{0}(context, result);",
            "return LexerActionExecutionOutcome.Executed;",
            "return _fallback.Execute(context, result);");

        /// <summary>
        /// Creates the exact statements emitted when a hook comparison matches.
        /// </summary>
        /// <param name="hook">Hook selected by the shared dispatcher loop.</param>
        /// <returns>Generated C# statements that invoke the hook and, when needed, return the descriptor success outcome.</returns>
        public IEnumerable<string> CreateMatchedHookStatements(EmbeddedCodeHook hook)
        {
            yield return string.Format(global::System.Globalization.CultureInfo.InvariantCulture, InvocationStatementFormat, hook.MethodName);
            if (!string.IsNullOrEmpty(SuccessStatement))
            {
                yield return SuccessStatement;
            }
        }
    }

    /// <summary>
    /// Emits the grammar-specific semantic predicate evaluator through the shared dispatcher emitter.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="predicates">Predicate hooks available to the evaluator.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitSemanticPredicateEvaluator(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> predicates, string contextClassName)
    {
        EmbeddedHookDispatcherEmitter.Emit(sb, predicates, contextClassName, EmbeddedHookDispatcherDescriptor.ParserPredicate);
    }

    /// <summary>
    /// Emits the grammar-specific parser action executor through the shared dispatcher emitter.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="actions">Action hooks available to the executor.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitParserActionExecutor(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> actions, string contextClassName)
    {
        EmbeddedHookDispatcherEmitter.Emit(sb, actions, contextClassName, EmbeddedHookDispatcherDescriptor.ParserAction);
    }

    /// <summary>
    /// Emits normalized transformed embedded C# into a generated hook body through the centralized injector.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="body">Normalized generated embedded-code body.</param>
    /// <param name="indentationLevel">Indentation level applied to generated code lines.</param>
    private static void EmitGeneratedEmbeddedCodeBody(StringBuilder sb, GeneratedEmbeddedCodeBody body, int indentationLevel)
    {
        var injector = new CSharpEmbeddedCodeInjector(sb);
        if (body.Kind == GeneratedEmbeddedCodeBodyKind.Expression)
        {
            injector.InjectReturnExpression(body.Code, indentationLevel);
            return;
        }

        injector.InjectCompleteFragment(body.Code, indentationLevel);
    }

    /// <summary>
    /// Gets raw embedded-code text for generated runtime dispatch comparisons without injecting it as executable C# body.
    /// </summary>
    /// <param name="code">Raw embedded code stored in parser model metadata.</param>
    /// <returns>Raw embedded-code text used as a runtime lookup key.</returns>
    private static string GetRawEmbeddedCodeText(RawEmbeddedCode code)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }

        return code.Text;
    }

    /// <summary>
    /// Emits the generated-C# opt-in rule-call execution policy that binds simple positional arguments before delegating to the caller policy.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    private static void EmitGeneratedRuleCallExecutionPolicy(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>Performs generated-C# opt-in simple positional rule-argument binding, then delegates to the caller-supplied rule-call policy.</summary>");
        sb.AppendLine("    private sealed class GeneratedRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::Utils.Parser.Runtime.IParserRuleCallExecutionPolicy _fallback;");
        sb.AppendLine("        private readonly global::Utils.Parser.Runtime.TypedPositionalLiteralRuleCallExecutionPolicy _positionalBinding = new(global::Utils.Parser.Runtime.ParserRuleCallBindingFailureBehavior.Throw);");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes the generated rule-call policy wrapper.</summary>");
        sb.AppendLine("        public GeneratedRuleCallExecutionPolicy(global::Utils.Parser.Runtime.IParserRuleCallExecutionPolicy fallback)");
        sb.AppendLine("        {");
        sb.AppendLine("            _fallback = fallback ?? throw new global::System.ArgumentNullException(nameof(fallback));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Validates generated rule-call arguments and binds supported positional literals before invoking the fallback policy.</summary>");
        sb.AppendLine("        public void BeforeRuleCall(global::Utils.Parser.Runtime.ParserRuleCallExecutionContext context)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("            if (context.PositionalRawArguments is not null && context.TargetRuleDescriptor is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                int expectedArgumentCount = context.TargetRuleDescriptor!.Parameters.Count;");
        sb.AppendLine("                int actualArgumentCount = context.PositionalRawArguments.Count;");
        sb.AppendLine("                if (actualArgumentCount != expectedArgumentCount)");
        sb.AppendLine("                {");
        sb.AppendLine("                    throw new global::Utils.Parser.Runtime.ParserRuleCallBindingException(");
        sb.AppendLine("                        context.RuleName,");
        sb.AppendLine("                        context.RawArguments,");
        sb.AppendLine("                        $\"Generated-C# automatic rule-call binding requires exactly {expectedArgumentCount} positional argument(s), but received {actualArgumentCount}. Argument count mismatch.\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (context.NamedRawArguments is not null && context.NamedRawArguments.Count > 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    throw new global::Utils.Parser.Runtime.ParserRuleCallBindingException(context.RuleName, context.RawArguments, \"Named rule-call arguments are not supported by generated-C# automatic binding.\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (expectedArgumentCount > 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    _positionalBinding.BeforeRuleCall(context);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            _fallback.BeforeRuleCall(context);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Delegates completed rule-call metadata to the fallback policy.</summary>");
        sb.AppendLine("        public void AfterRuleCall(global::Utils.Parser.Runtime.ParserRuleCallExecutionContext context)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("            _fallback.AfterRuleCall(context);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }


    /// <summary>
    /// Emits the grammar-specific lexer action executor through the shared dispatcher emitter.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerActions">Lexer action hooks available to the executor.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitLexerActionExecutor(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> lexerActions, string contextClassName)
    {
        EmbeddedHookDispatcherEmitter.Emit(sb, lexerActions, contextClassName, EmbeddedHookDispatcherDescriptor.LexerAction);
    }


    /// <summary>
    /// Emits the grammar-specific lexer predicate evaluator through the shared dispatcher emitter.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerPredicates">Lexer predicate hooks available to the evaluator.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitLexerPredicateEvaluator(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> lexerPredicates, string contextClassName)
    {
        EmbeddedHookDispatcherEmitter.Emit(sb, lexerPredicates, contextClassName, EmbeddedHookDispatcherDescriptor.LexerPredicate);
    }

    /// <summary>
    /// Emits a generated C# method for a semantic predicate body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Predicate hook metadata.</param>
    private static void EmitPredicateHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        EmbeddedHookMethodEmitter.Emit(sb, hook, EmbeddedHookMethodDescriptor.ParserPredicate);
    }

    /// <summary>
    /// Emits a generated C# method for an inline parser action body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Action hook metadata.</param>
    private static void EmitActionHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        EmbeddedHookMethodEmitter.Emit(sb, hook, EmbeddedHookMethodDescriptor.ParserAction);
    }


    /// <summary>
    /// Emits a generated C# method for an inline lexer action body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Lexer action hook metadata.</param>
    private static void EmitLexerActionHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        EmbeddedHookMethodEmitter.Emit(sb, hook, EmbeddedHookMethodDescriptor.LexerAction);
    }


    /// <summary>
    /// Emits a generated C# method for an inline lexer predicate body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Lexer predicate hook metadata.</param>
    private static void EmitLexerPredicateHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        EmbeddedHookMethodEmitter.Emit(sb, hook, EmbeddedHookMethodDescriptor.LexerPredicate);
    }

    /// <summary>
    /// Emits generated C# hook methods using one stable algorithm and descriptor-provided variation points.
    /// </summary>
    private static class EmbeddedHookMethodEmitter
    {
        /// <summary>
        /// Emits a complete generated hook method for one parser or lexer embedded-code hook.
        /// </summary>
        /// <param name="sb">Source builder receiving generated C#.</param>
        /// <param name="hook">Embedded-code hook to emit.</param>
        /// <param name="descriptor">Immutable descriptor for the hook family.</param>
        public static void Emit(StringBuilder sb, EmbeddedCodeHook hook, EmbeddedHookMethodDescriptor descriptor)
        {
            ValidateEmbeddedCodeHook(hook, descriptor.Owner, descriptor.Kind);
            GeneratedEmbeddedCodeBody body = descriptor.CreateBody(hook.EmittedCode);

            sb.AppendLine(descriptor.CreateSummary(hook));
            sb.AppendLine($"    private {descriptor.ReturnTypeName} {hook.MethodName}({descriptor.Parameters})");
            sb.AppendLine("    {");
            EmitContextLocals(sb, descriptor.ContextLocalProfile);
            EmitGeneratedEmbeddedCodeBody(sb, body, 2);
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>
        /// Emits parser context locals when required by the selected hook profile.
        /// </summary>
        /// <param name="sb">Source builder receiving generated C#.</param>
        /// <param name="profile">Context-local profile selected by the descriptor.</param>
        private static void EmitContextLocals(StringBuilder sb, EmbeddedHookContextLocalProfile profile)
        {
            switch (profile)
            {
                case EmbeddedHookContextLocalProfile.None:
                    break;
                case EmbeddedHookContextLocalProfile.ParserPredicate:
                    GrammarEmitter.EmitContextLocals(sb, predicate: true);
                    break;
                case EmbeddedHookContextLocalProfile.ParserAction:
                    GrammarEmitter.EmitContextLocals(sb, predicate: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }

    /// <summary>
    /// Identifies the parser context locals emitted before a generated hook body.
    /// </summary>
    private enum EmbeddedHookContextLocalProfile
    {
        /// <summary>No parser context locals are emitted.</summary>
        None,

        /// <summary>Parser predicate locals, including <c>predicateCode</c>, are emitted.</summary>
        ParserPredicate,

        /// <summary>Parser action locals, including <c>actionCode</c>, are emitted.</summary>
        ParserAction
    }

    /// <summary>
    /// Immutable descriptor for one generated embedded-code hook method family.
    /// </summary>
    private readonly struct EmbeddedHookMethodDescriptor
    {
        private readonly Func<TransformedEmbeddedCode, GeneratedEmbeddedCodeBody> _createBody;
        private readonly Func<EmbeddedCodeHook, string> _createSummary;

        /// <summary>Initializes a generated hook method descriptor.</summary>
        private EmbeddedHookMethodDescriptor(EmbeddedCodeHookOwner owner, EmbeddedCodeHookKind kind, string returnTypeName, string parameters, EmbeddedHookContextLocalProfile contextLocalProfile, Func<TransformedEmbeddedCode, GeneratedEmbeddedCodeBody> createBody, Func<EmbeddedCodeHook, string> createSummary)
        {
            Owner = owner;
            Kind = kind;
            ReturnTypeName = returnTypeName;
            Parameters = parameters;
            ContextLocalProfile = contextLocalProfile;
            _createBody = createBody ?? throw new ArgumentNullException(nameof(createBody));
            _createSummary = createSummary ?? throw new ArgumentNullException(nameof(createSummary));
        }

        /// <summary>Gets the parser predicate method descriptor.</summary>
        public static EmbeddedHookMethodDescriptor ParserPredicate { get; } = new(EmbeddedCodeHookOwner.Parser, EmbeddedCodeHookKind.SemanticPredicate, "bool", "SemanticPredicateEvaluationContext context", EmbeddedHookContextLocalProfile.ParserPredicate, GeneratedEmbeddedCodeBody.ForPredicate, static hook => $"    /// <summary>Executes semantic predicate hook for rule <c>{EscapeXml(hook.RuleName)}</c>, alternative {hook.AlternativeIndex}, element {hook.ElementIndex}.</summary>");

        /// <summary>Gets the parser action method descriptor.</summary>
        public static EmbeddedHookMethodDescriptor ParserAction { get; } = new(EmbeddedCodeHookOwner.Parser, EmbeddedCodeHookKind.InlineAction, "void", "ParserActionExecutionContext context", EmbeddedHookContextLocalProfile.ParserAction, GeneratedEmbeddedCodeBody.ForAction, static hook => $"    /// <summary>Executes inline parser action hook for rule <c>{EscapeXml(hook.RuleName)}</c>, alternative {hook.AlternativeIndex}, element {hook.ElementIndex}.</summary>");

        /// <summary>Gets the lexer predicate method descriptor.</summary>
        public static EmbeddedHookMethodDescriptor LexerPredicate { get; } = new(EmbeddedCodeHookOwner.Lexer, EmbeddedCodeHookKind.SemanticPredicate, "bool", "LexerPredicateEvaluationContext context", EmbeddedHookContextLocalProfile.None, GeneratedEmbeddedCodeBody.ForPredicate, static hook => $"    /// <summary>Executes inline lexer predicate hook for rule <c>{EscapeXml(hook.RuleName)}</c>.</summary>");

        /// <summary>Gets the lexer action method descriptor.</summary>
        public static EmbeddedHookMethodDescriptor LexerAction { get; } = new(EmbeddedCodeHookOwner.Lexer, EmbeddedCodeHookKind.InlineAction, "void", "LexerActionExecutionContext context, LexerActionExecutionResult result", EmbeddedHookContextLocalProfile.None, GeneratedEmbeddedCodeBody.ForAction, static hook => $"    /// <summary>Executes inline lexer action hook for rule <c>{EscapeXml(hook.RuleName)}</c>.</summary>");

        /// <summary>Gets the expected hook owner.</summary>
        public EmbeddedCodeHookOwner Owner { get; }

        /// <summary>Gets the expected hook kind.</summary>
        public EmbeddedCodeHookKind Kind { get; }

        /// <summary>Gets the generated method return type name.</summary>
        public string ReturnTypeName { get; }

        /// <summary>Gets the generated method parameter list.</summary>
        public string Parameters { get; }

        /// <summary>Gets the context-local profile emitted before the hook body.</summary>
        public EmbeddedHookContextLocalProfile ContextLocalProfile { get; }

        /// <summary>Creates the generated body wrapper for transformed code.</summary>
        public GeneratedEmbeddedCodeBody CreateBody(TransformedEmbeddedCode code) => _createBody(code);

        /// <summary>Creates the generated XML documentation summary line.</summary>
        public string CreateSummary(EmbeddedCodeHook hook) => _createSummary(hook);
    }

    /// <summary>
    /// Emits the grammar-specific rule lifecycle executor dispatcher.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lifecycleHooks">Lifecycle hooks to dispatch.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitRuleLifecycleExecutor(StringBuilder sb, IReadOnlyList<LifecycleHook> lifecycleHooks, string contextClassName)
    {
        var initHooks = lifecycleHooks.Where(static hook => hook.IsInit).ToList();
        var afterHooks = lifecycleHooks.Where(static hook => !hook.IsInit).ToList();

        sb.AppendLine("    /// <summary>Dispatches rule lifecycle hooks to generated C# <c>@init</c> and <c>@after</c> methods.</summary>");
        sb.AppendLine("    private sealed class GeneratedRuleLifecycleExecutor : IParserRuleLifecycleExecutor");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated rule lifecycle executor for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedRuleLifecycleExecutor({contextClassName} executionContext)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext ?? throw new global::System.ArgumentNullException(nameof(executionContext));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Executes the matching lifecycle hook when the phase and rule name are recognized.</summary>");
        sb.AppendLine("        public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(ruleName);");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine();
        sb.AppendLine("            if (phase == ParserRuleLifecyclePhase.Init)");
        sb.AppendLine("            {");
        sb.AppendLine("                AllocateDeclaredRuleLocals(context);");
        sb.AppendLine("            }");

        if (initHooks.Count > 0)
        {
            sb.AppendLine("            if (phase == ParserRuleLifecyclePhase.Init)");
            sb.AppendLine("            {");
            foreach (var hook in initHooks)
            {
                sb.AppendLine($"                if (string.Equals(ruleName, \"{Escape(hook.RuleName)}\", global::System.StringComparison.Ordinal)) {{ _executionContext.{hook.MethodName}(context); return; }}");
            }
            sb.AppendLine("            }");
        }

        if (afterHooks.Count > 0)
        {
            string elseClause = initHooks.Count > 0 ? "else if" : "if";
            sb.AppendLine($"            {elseClause} (phase == ParserRuleLifecyclePhase.After)");
            sb.AppendLine("            {");
            foreach (var hook in afterHooks)
            {
                sb.AppendLine($"                if (string.Equals(ruleName, \"{Escape(hook.RuleName)}\", global::System.StringComparison.Ordinal)) {{ _executionContext.{hook.MethodName}(context); return; }}");
            }
            sb.AppendLine("            }");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generated C# method for a rule lifecycle hook body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Lifecycle hook metadata.</param>
    private static void EmitLifecycleHookMethod(StringBuilder sb, LifecycleHook hook)
    {
        string phase = hook.IsInit ? "@init" : "@after";
        var body = GeneratedEmbeddedCodeBody.ForAction(hook.Code);

        sb.AppendLine($"    /// <summary>Executes rule lifecycle <c>{phase}</c> hook for rule <c>{EscapeXml(hook.RuleName)}</c>.</summary>");
        sb.AppendLine($"    internal void {hook.MethodName}(ParserRuleLifecycleContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        string ruleName = context.RuleName;");
        sb.AppendLine("        int inputPosition = context.InputPosition;");
        EmitGeneratedEmbeddedCodeBody(sb, body, 2);
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
