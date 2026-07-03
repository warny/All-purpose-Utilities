using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    /// Emits the grammar-specific semantic predicate evaluator.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="predicates">Predicate hooks available to the evaluator.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitSemanticPredicateEvaluator(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> predicates, string contextClassName)
    {
        sb.AppendLine("    /// <summary>Dispatches runtime semantic predicate contexts to generated C# predicate hooks.</summary>");
        sb.AppendLine("    private sealed class GeneratedSemanticPredicateEvaluator : ISemanticPredicateEvaluator");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine("        private readonly ISemanticPredicateEvaluator _fallback;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated semantic predicate evaluator for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedSemanticPredicateEvaluator({contextClassName} executionContext, ISemanticPredicateEvaluator fallback)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext;");
        sb.AppendLine("            _fallback = fallback;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Evaluates a generated semantic predicate hook when the runtime context matches exactly.</summary>");
        sb.AppendLine("        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)");
        sb.AppendLine("        {");
        foreach (var predicate in predicates)
        {
            sb.AppendLine($"            if (string.Equals(context.Rule.Name, \"{Escape(predicate.RuleName)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(context.PredicateCode, \"{Escape(predicate.Code)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && context.AlternativeIndex == {predicate.AlternativeIndex}");
            sb.AppendLine($"                && context.ElementIndex == {predicate.ElementIndex})");
            sb.AppendLine("            {");
            sb.AppendLine($"                return _executionContext.{predicate.MethodName}(context) ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            return _fallback.Evaluate(context);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the grammar-specific parser action executor.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="actions">Action hooks available to the executor.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitParserActionExecutor(StringBuilder sb, IReadOnlyList<EmbeddedCodeHook> actions, string contextClassName)
    {
        sb.AppendLine("    /// <summary>Dispatches runtime parser action contexts to generated C# action hooks.</summary>");
        sb.AppendLine("    private sealed class GeneratedParserActionExecutor : IParserActionExecutor");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine("        private readonly IParserActionExecutor _fallback;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated parser action executor for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedParserActionExecutor({contextClassName} executionContext, IParserActionExecutor fallback)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext;");
        sb.AppendLine("            _fallback = fallback;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Executes a generated parser action hook when the runtime context matches exactly.</summary>");
        sb.AppendLine("        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)");
        sb.AppendLine("        {");
        foreach (var action in actions)
        {
            sb.AppendLine($"            if (string.Equals(context.Rule.Name, \"{Escape(action.RuleName)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(context.ActionCode, \"{Escape(action.Code)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && context.AlternativeIndex == {action.AlternativeIndex}");
            sb.AppendLine($"                && context.ElementIndex == {action.ElementIndex})");
            sb.AppendLine("            {");
            sb.AppendLine($"                _executionContext.{action.MethodName}(context);");
            sb.AppendLine("                return ParserActionExecutionOutcome.Executed;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            return _fallback.Execute(context);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
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
        sb.AppendLine("                if (context.NamedRawArguments is not null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    throw new global::Utils.Parser.Runtime.ParserRuleCallBindingException(context.RuleName, context.RawArguments, \"Named rule-call arguments are not supported by generated-C# automatic binding.\");");
        sb.AppendLine("                }");
        sb.AppendLine();
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
    /// Emits the grammar-specific lexer action executor.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerActions">Lexer action hooks available to the executor.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitLexerActionExecutor(StringBuilder sb, IReadOnlyList<LexerEmbeddedCodeHook> lexerActions, string contextClassName)
    {
        sb.AppendLine("    /// <summary>Dispatches accepted lexer inline actions to generated C# lexer action hooks.</summary>");
        sb.AppendLine("    private sealed class GeneratedLexerActionExecutor : ILexerActionExecutor");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine("        private readonly ILexerActionExecutor _fallback;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated lexer action executor for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedLexerActionExecutor({contextClassName} executionContext, ILexerActionExecutor fallback)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext;");
        sb.AppendLine("            _fallback = fallback;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Executes a generated lexer action hook when the runtime context matches.</summary>");
        sb.AppendLine("        public LexerActionExecutionOutcome Execute(LexerActionExecutionContext context, LexerActionExecutionResult result)");
        sb.AppendLine("        {");
        foreach (var action in lexerActions)
        {
            sb.AppendLine($"            if (string.Equals(context.Rule.Name, \"{Escape(action.RuleName)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(context.ActionCode, \"{Escape(action.Code)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && context.AlternativeIndex == {action.AlternativeIndex}");
            sb.AppendLine($"                && context.ElementIndex == {action.ElementIndex})");
            sb.AppendLine("            {");
            sb.AppendLine($"                _executionContext.{action.MethodName}(context, result);");
            sb.AppendLine("                return LexerActionExecutionOutcome.Executed;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            return _fallback.Execute(context, result);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }


    /// <summary>
    /// Emits the grammar-specific lexer predicate evaluator.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerPredicates">Lexer predicate hooks available to the evaluator.</param>
    /// <param name="contextClassName">Generated execution context class name.</param>
    private static void EmitLexerPredicateEvaluator(StringBuilder sb, IReadOnlyList<LexerEmbeddedCodeHook> lexerPredicates, string contextClassName)
    {
        sb.AppendLine("    /// <summary>Dispatches lexer predicate contexts to generated C# lexer predicate hooks.</summary>");
        sb.AppendLine("    private sealed class GeneratedLexerPredicateEvaluator : ILexerPredicateEvaluator");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {contextClassName} _executionContext;");
        sb.AppendLine("        private readonly ILexerPredicateEvaluator _fallback;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Initializes a generated lexer predicate evaluator for one execution context.</summary>");
        sb.AppendLine($"        public GeneratedLexerPredicateEvaluator({contextClassName} executionContext, ILexerPredicateEvaluator fallback)");
        sb.AppendLine("        {");
        sb.AppendLine("            _executionContext = executionContext;");
        sb.AppendLine("            _fallback = fallback;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Evaluates a generated lexer predicate hook when the runtime context matches.</summary>");
        sb.AppendLine("        public LexerPredicateEvaluationOutcome Evaluate(LexerPredicateEvaluationContext context)");
        sb.AppendLine("        {");
        foreach (var predicate in lexerPredicates)
        {
            sb.AppendLine($"            if (string.Equals(context.Rule.Name, \"{Escape(predicate.RuleName)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && string.Equals(context.PredicateCode, \"{Escape(predicate.Code)}\", global::System.StringComparison.Ordinal)");
            sb.AppendLine($"                && context.AlternativeIndex == {predicate.AlternativeIndex}");
            sb.AppendLine($"                && context.ElementIndex == {predicate.ElementIndex})");
            sb.AppendLine("            {");
            sb.AppendLine($"                return _executionContext.{predicate.MethodName}(context) ? LexerPredicateEvaluationOutcome.True : LexerPredicateEvaluationOutcome.False;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            return _fallback.Evaluate(context);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generated C# method for a semantic predicate body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Predicate hook metadata.</param>
    private static void EmitPredicateHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        var body = GeneratedEmbeddedCodeBody.ForPredicate(hook.EmittedCode);

        sb.AppendLine($"    /// <summary>Executes semantic predicate hook for rule <c>{EscapeXml(hook.RuleName)}</c>, alternative {hook.AlternativeIndex}, element {hook.ElementIndex}.</summary>");
        sb.AppendLine($"    private bool {hook.MethodName}(SemanticPredicateEvaluationContext context)");
        sb.AppendLine("    {");
        EmitContextLocals(sb, predicate: true);
        EmitGeneratedEmbeddedCodeBody(sb, body, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generated C# method for an inline parser action body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Action hook metadata.</param>
    private static void EmitActionHook(StringBuilder sb, EmbeddedCodeHook hook)
    {
        var body = GeneratedEmbeddedCodeBody.ForAction(hook.EmittedCode);

        sb.AppendLine($"    /// <summary>Executes inline parser action hook for rule <c>{EscapeXml(hook.RuleName)}</c>, alternative {hook.AlternativeIndex}, element {hook.ElementIndex}.</summary>");
        sb.AppendLine($"    private void {hook.MethodName}(ParserActionExecutionContext context)");
        sb.AppendLine("    {");
        EmitContextLocals(sb, predicate: false);
        EmitGeneratedEmbeddedCodeBody(sb, body, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }


    /// <summary>
    /// Emits a generated C# method for an inline lexer action body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Lexer action hook metadata.</param>
    private static void EmitLexerActionHook(StringBuilder sb, LexerEmbeddedCodeHook hook)
    {
        var body = GeneratedEmbeddedCodeBody.ForAction(hook.EmittedCode);

        sb.AppendLine($"    /// <summary>Executes inline lexer action hook for rule <c>{EscapeXml(hook.RuleName)}</c>.</summary>");
        sb.AppendLine($"    private void {hook.MethodName}(LexerActionExecutionContext context, LexerActionExecutionResult result)");
        sb.AppendLine("    {");
        EmitGeneratedEmbeddedCodeBody(sb, body, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }


    /// <summary>
    /// Emits a generated C# method for an inline lexer predicate body.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hook">Lexer predicate hook metadata.</param>
    private static void EmitLexerPredicateHook(StringBuilder sb, LexerEmbeddedCodeHook hook)
    {
        var body = GeneratedEmbeddedCodeBody.ForPredicate(hook.EmittedCode);

        sb.AppendLine($"    /// <summary>Executes inline lexer predicate hook for rule <c>{EscapeXml(hook.RuleName)}</c>.</summary>");
        sb.AppendLine($"    private bool {hook.MethodName}(LexerPredicateEvaluationContext context)");
        sb.AppendLine("    {");
        EmitGeneratedEmbeddedCodeBody(sb, body, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
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
        EmitGeneratedEmbeddedCodeBody(sb, body, "        ");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
