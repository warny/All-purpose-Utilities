using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{
    /// <summary>
    /// Emits the per-parse execution context that owns generated embedded-code hooks and injected parser members.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="hooks">Collected parser embedded-code hooks.</param>
    /// <param name="lifecycleHooks">Collected rule lifecycle (@init / @after) hooks.</param>
    /// <param name="parserMembers">Parser members blocks to inject verbatim.</param>
    /// <param name="className">Generated grammar class name.</param>
    /// <param name="sourceFileName">Original .g4 file name, used in generated XML documentation.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="embeddedCodeTransformer">Parser embedded-code transformer used for parser members.</param>
    private static void EmitExecutionContext(
        StringBuilder sb,
        IReadOnlyList<EmbeddedCodeHook> hooks,
        IReadOnlyList<LifecycleHook> lifecycleHooks,
        IReadOnlyList<G4GrammarAction> parserMembers,
        string className,
        string sourceFileName,
        G4Grammar grammar,
        IParserEmbeddedCodeTransformer embeddedCodeTransformer)
    {
        var predicates = hooks.Where(static hook => hook.IsPredicate).ToList();
        var actions = hooks.Where(static hook => !hook.IsPredicate).ToList();
        string contextClassName = GetExecutionContextClassName(className);

        sb.AppendLine($"/// <summary>Per-parse execution context for generated embedded C# code compiled from <c>{EscapeXml(sourceFileName)}</c>.</summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Utils.Parser.Generators\", \"0.1.0\")]");
        sb.AppendLine($"internal sealed partial class {contextClassName}");
        sb.AppendLine("{");
        EmitParserMembers(sb, parserMembers, grammar, embeddedCodeTransformer);
        sb.AppendLine("    /// <summary>Creates a copied execution context that can be used by future speculative parser execution paths.</summary>");
        sb.AppendLine("    /// <remarks>The copy follows <c>ParserExecutionContextCopier&lt;TContext&gt;</c> semantics.</remarks>");
        sb.AppendLine($"    /// <returns>A copied <see cref=\"{contextClassName}\"/> instance.</returns>");
        sb.AppendLine($"    internal {contextClassName} Fork()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return global::Utils.Parser.Runtime.ParserExecutionContextCopier<{contextClassName}>.Copy(");
        sb.AppendLine("            this,");
        sb.AppendLine($"            static () => new {contextClassName}());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Copies execution-context state from the supplied source into this instance.</summary>");
        sb.AppendLine("    /// <remarks>This is intended for future commit/restore scenarios and is not invoked automatically by <c>ParserEngine</c>.</remarks>");
        sb.AppendLine($"    /// <param name=\"source\">Source <see cref=\"{contextClassName}\"/> whose state is copied into this instance.</param>");
        sb.AppendLine("    internal void CopyFrom(" + contextClassName + " source)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(source);");
        sb.AppendLine();
        sb.AppendLine($"        global::Utils.Parser.Runtime.ParserExecutionContextCopier<{contextClassName}>.CopyTo(");
        sb.AppendLine("            source,");
        sb.AppendLine("            this);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets the semantic state key for this execution context instance.</summary>");
        sb.AppendLine("    /// <returns>A deterministic parser execution-state key for the current context state.</returns>");
        sb.AppendLine("    internal global::Utils.Parser.Runtime.ParserExecutionStateKey GetExecutionStateKey()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return global::Utils.Parser.Runtime.ParserExecutionContextHasher<{contextClassName}>.GetKey(this);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Last completed child call result captured from a successful child-rule exit; included in managed execution-state snapshots for rollback safety.</summary>");
        sb.AppendLine($"    internal global::Utils.Parser.Runtime.ParserRuleCallResult? _lastChildCallResult;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Pending child-parameter seeds for the next named child rule invocation; included in managed execution-state snapshots so seeds do not leak across failed parser alternatives.</summary>");
        sb.AppendLine($"    internal global::Utils.Parser.Runtime.ParserRuleParameterSeedStore? _pendingChildSeeds;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Current-rule return values for the active frame; included in managed execution-state snapshots so return writes roll back with failed alternatives.</summary>");
        sb.AppendLine("    internal global::Utils.Parser.Runtime.ParserRuleReturnSnapshot? _currentRuleReturnSnapshot;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Immutable assignment/list labeled child-call results for the active frame; included in managed execution-state snapshots for rollback and memoization safety.</summary>");
        sb.AppendLine("    internal global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore _labeledChildCallResults = global::Utils.Parser.Runtime.ParserLabeledRuleCallResultStore.Empty;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Active frame manager bound by <see cref=\"CreateRuntimePolicy\"/>; excluded from execution-state hashing and copying because it is runtime infrastructure, not logical parser state.</summary>");
        sb.AppendLine("    [global::Utils.Parser.Runtime.ParserExecutionStateIgnored]");
        sb.AppendLine($"    private global::Utils.Parser.Runtime.StackParserRuleInvocationFrameManager? _frameManager;");
        sb.AppendLine();
        EmitRuleLocalHelpers(sb);
        EmitRuleReturnHelpers(sb);
        EmitRuleParameterHelpers(sb);
        EmitRuleCallResultHelpers(sb);
        EmitRuleSeedingHelpers(sb);
        sb.AppendLine("    /// <summary>Creates a runtime feature policy bound to this execution context instance.</summary>");
        sb.AppendLine("    /// <param name=\"basePolicy\">Optional policy whose non-embedded-code components are preserved.</param>");
        sb.AppendLine("    /// <returns>A runtime policy whose generated dispatchers call this context instance.</returns>");
        sb.AppendLine("    internal ParserRuntimeFeaturePolicy CreateRuntimePolicy(ParserRuntimeFeaturePolicy? basePolicy = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        var effectiveBase = basePolicy ?? ParserRuntimeFeaturePolicy.Default;");
        sb.AppendLine("        this._frameManager = new global::Utils.Parser.Runtime.StackParserRuleInvocationFrameManager(");
        sb.AppendLine("            onChildCallResult: result => this._lastChildCallResult = result,");
        sb.AppendLine("            onLabeledCallResults: results => this._labeledChildCallResults = results);");
        sb.AppendLine("        return effectiveBase with");
        sb.AppendLine("        {");
        sb.AppendLine("            SemanticPredicateEvaluator = new GeneratedSemanticPredicateEvaluator(this, effectiveBase.SemanticPredicateEvaluator),");
        sb.AppendLine("            ParserActionExecutor = new GeneratedParserActionExecutor(this, effectiveBase.ParserActionExecutor),");
        sb.AppendLine("            ExecutionStateManager = new GeneratedExecutionStateManager(");
        sb.AppendLine("                this,");
        sb.AppendLine("                result => this._frameManager!.SyncCallResultToCurrentFrame(result),");
        sb.AppendLine("                () => this._frameManager!.GetCurrentPendingSeeds(),");
        sb.AppendLine("                seeds => this._frameManager!.SyncPendingSeedsToCurrentFrame(seeds),");
        sb.AppendLine("                () => this._frameManager!.GetCurrentReturnSnapshot(),");
        sb.AppendLine("                snapshot => this._frameManager!.SyncReturnSnapshotToCurrentFrame(snapshot),");
        sb.AppendLine("                () => this._frameManager!.GetCurrentLabeledCallResults(),");
        sb.AppendLine("                results => this._frameManager!.SyncLabeledCallResultsToCurrentFrame(results)),");
        sb.AppendLine("            RuleInvocationFrameManager = this._frameManager,");
        if (lifecycleHooks.Count > 0)
        {
            sb.AppendLine("            RuleLifecycleExecutor = new GeneratedRuleLifecycleExecutor(this),");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        EmitExecutionStateManager(sb, contextClassName);
        EmitSemanticPredicateEvaluator(sb, predicates, contextClassName);
        EmitParserActionExecutor(sb, actions, contextClassName);
        if (lifecycleHooks.Count > 0)
        {
            EmitRuleLifecycleExecutor(sb, lifecycleHooks, contextClassName);
        }

        foreach (var predicate in predicates)
        {
            EmitPredicateHook(sb, predicate);
        }

        foreach (var action in actions)
        {
            EmitActionHook(sb, action);
        }

        foreach (var lifecycleHook in lifecycleHooks)
        {
            EmitLifecycleHookMethod(sb, lifecycleHook);
        }

        sb.AppendLine("}");
    }

}
