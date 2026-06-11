using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies that generated ANTLR4 embedded parser code is emitted as C# hooks,
/// compiled by Roslyn, and executed by <see cref="ParserEngine"/> through a generated runtime policy.
/// </summary>
[TestClass]
public class Antlr4GeneratedEmbeddedCodeTests
{
    /// <summary>
    /// Ensures a generated <c>true</c> predicate hook is compiled and allows parsing to succeed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateTrue_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { true }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private bool __Predicate_start_0_0_0");
        StringAssert.Contains(source, "GeneratedSemanticPredicateEvaluator");

        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures a generated <c>false</c> predicate hook is executed and rejects the branch through the parser engine.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateFalse_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : { false }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures generated predicate hooks expose the documented contextual symbols.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateContextualSymbols_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 && ruleName == "start" && alternativeIndex == 0 && elementIndex == 0 }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures expression-bodied predicate hooks can use multiple contextual symbols and parse successfully.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateExpressionBody_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 && ruleName == "start" }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures block-bodied predicate hooks keep local variables and return statements as C# statements.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateBlockWithReturn_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : {
                var isStart = inputPosition == 0;
                return isStart && ruleName == "start";
            }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures one-line predicate statement blocks with conditional returns compile and parse through generated hooks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateOneLineConditionalReturn_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { if (inputPosition == 0) return true; return false; }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures predicate expressions containing return as part of an identifier stay expression-bodied.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateReturnIdentifier_DoesNotUseBlockBody()
    {
        const string grammar = """
            grammar P;
            start : { returnValue == true }? A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static bool returnValue = true;
            }
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures block-bodied predicate hooks can reject parsing through a generated runtime policy.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateBlockWithFalseReturn_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : {
                var blocked = true;
                return !blocked;
            }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures inline parser actions can call user code supplied in another partial class.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineAction_ExecutesUserPartialMethod()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private void __Action_start_0_0_0");
        StringAssert.Contains(source, "GeneratedParserActionExecutor");

        var assembly = CompileGeneratedSource(source, userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures the existing default parse helper remains conservative and does not execute generated action hooks.
    /// </summary>
    [TestMethod]
    public void Parse_DefaultParse_DoesNotExecuteGeneratedInlineAction()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(0, ReadActionCount(assembly));

        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");
        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures <c>@members</c> is injected into the per-parse execution context and can be called by an inline action.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_MembersAction_InjectsMembersIntoExecutionContext()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                }

                internal int CountValue => Count;
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal sealed partial class PExecutionContext");
        StringAssert.Contains(source, "private int Count;");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);
        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
    }

    /// <summary>
    /// Ensures unscoped <c>@header</c> can inject C# using directives consumed by generated parser members and actions.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_HeaderUsing_AllowsMembersAndActionsToReferenceImportedType()
    {
        const string grammar = """
            grammar P;

            @header {
                using System.Text;
            }

            @members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            start : { var builder = new StringBuilder(); builder.Append("a"); TextValue = builder.ToString(); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "// <auto-generated-parser-header>");
        StringAssert.Contains(source, "using System.Text;");
        StringAssert.Contains(source, "var builder = new StringBuilder();");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        var defaultResult = InvokeParse(assembly, "Parse", "a");
        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        var embeddedResult = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual("a", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures scoped <c>@parser::header</c> can inject C# using directives consumed by generated parser members and actions.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParserHeaderUsing_AllowsMembersAndActionsToReferenceImportedType()
    {
        const string grammar = """
            grammar P;

            @parser::header {
                using System.Text;
            }

            @parser::members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            start : { var builder = new StringBuilder(); builder.Append("a"); TextValue = builder.ToString(); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual("a", ReadContextStringProperty(context, "Text"));
    }


    /// <summary>
    /// Ensures <c>@footer</c> can inject a trailing helper type that generated members and inline actions reference explicitly.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FooterHelperType_AllowsMembersAndActionsToReferenceTrailingType()
    {
        const string grammar = """
            grammar P;

            @members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            @footer {
                internal static class ParserFooterHelper
                {
                    internal static string Read() => "footer";
                }
            }

            start : { TextValue = ParserFooterHelper.Read(); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "// <auto-generated-parser-footer>");
        StringAssert.Contains(source, "internal static class ParserFooterHelper");
        Assert.IsTrue(
            source.IndexOf("internal static class ParserFooterHelper", StringComparison.Ordinal)
                > source.IndexOf("internal sealed partial class PExecutionContext", StringComparison.Ordinal),
            source);

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        var defaultResult = InvokeParse(assembly, "Parse", "a");
        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        var embeddedResult = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual("footer", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures scoped <c>@parser::footer</c> can inject the same trailing helper type shape as unscoped parser footer blocks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParserFooterHelperType_AllowsMembersAndActionsToReferenceTrailingType()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            @parser::footer {
                internal static class ParserFooterHelper
                {
                    internal static string Read() => "parser-footer";
                }
            }

            start : { TextValue = ParserFooterHelper.Read(); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual("parser-footer", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures explicit generated execution contexts keep instance state isolated across parses.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExplicitExecutionContexts_IsolateMembersState()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                }

                internal int CountValue => Count;
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var firstContext = CreateExecutionContext(assembly);
        var secondContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", firstContext);
        InvokeParseWithContext(assembly, "a", secondContext);
        InvokeParseWithContext(assembly, "a", firstContext);

        Assert.AreEqual(2, ReadContextIntProperty(firstContext, "CountValue"));
        Assert.AreEqual(1, ReadContextIntProperty(secondContext, "CountValue"));
    }

    /// <summary>
    /// Ensures generated execution contexts expose an internal <c>Fork</c> helper that returns the context type.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_IsGeneratedWithContextReturnType()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                internal int CountValue => Count;
            }

            start : A { Count++; } ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal PExecutionContext Fork()");
        StringAssert.Contains(source, "global::Utils.Parser.Runtime.ParserExecutionContextCopier<PExecutionContext>.Copy(");

        var assembly = CompileGeneratedSource(source);
        var contextType = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var fork = contextType.GetMethod("Fork", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(fork);
        Assert.AreEqual(contextType, fork.ReturnType);
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> copies scalar and mutable collection state from the source context.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_CopiesCurrentState()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var fork = InvokeFork(context);

        Assert.AreNotSame(context, fork);
        Assert.AreEqual(ReadContextIntProperty(context, "CountValue"), ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(context, "ItemValues"), ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> structurally copies mutable collections instead of sharing collection instances.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_IsolatesMutableCollections()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var fork = InvokeFork(context);

        Assert.AreNotSame(ReadContextObjectProperty(context, "MutableItems"), ReadContextObjectProperty(fork, "MutableItems"));

        InvokeParseWithContext(assembly, "a", fork);

        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
        Assert.AreEqual(2, ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(new[] { "a" }, ReadContextStringItems(context, "ItemValues"));
        CollectionAssert.AreEqual(new[] { "a", "a" }, ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated <c>CopyFrom</c> replaces target state with a structural copy of source state.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CopyFrom_ReplacesStateAndIsolatesCollections()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var source = CreateExecutionContext(assembly);
        var target = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", source);
        InvokeParseWithContext(assembly, "a", source);
        InvokeParseWithContext(assembly, "a", target);

        InvokeCopyFrom(target, source);

        Assert.AreEqual(ReadContextIntProperty(source, "CountValue"), ReadContextIntProperty(target, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(source, "ItemValues"), ReadContextStringItems(target, "ItemValues"));
        Assert.AreNotSame(ReadContextObjectProperty(source, "MutableItems"), ReadContextObjectProperty(target, "MutableItems"));

        InvokeParseWithContext(assembly, "a", target);

        Assert.AreEqual(2, ReadContextIntProperty(source, "CountValue"));
        Assert.AreEqual(3, ReadContextIntProperty(target, "CountValue"));
        CollectionAssert.AreEqual(new[] { "a", "a" }, ReadContextStringItems(source, "ItemValues"));
        CollectionAssert.AreEqual(new[] { "a", "a", "a" }, ReadContextStringItems(target, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated runtime policies install an execution-state manager that can manually capture and restore context state.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;
            }

            start @init { Count++; } : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal global::Utils.Parser.Runtime.ParserExecutionStateKey GetExecutionStateKey()");
        StringAssert.Contains(source, "return _executionContext.GetExecutionStateKey();");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var policy = InvokeCreateRuntimePolicy(assembly, context);
        Assert.IsNotNull(policy.ExecutionStateManager);
        var firstKey = policy.ExecutionStateManager.GetCurrentStateKey();

        var snapshot = policy.ExecutionStateManager.Capture();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(context.GetType(), snapshot.GetType());

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual(2, ReadContextIntProperty(context, "CountValue"));
        Assert.AreNotEqual(firstKey, policy.ExecutionStateManager.GetCurrentStateKey());

        policy.ExecutionStateManager.Restore(snapshot);
        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
        Assert.AreEqual(firstKey, policy.ExecutionStateManager.GetCurrentStateKey());
    }

    /// <summary>
    /// Ensures a grammar with only inline actions and no lifecycle hooks wires a generated execution-state manager into the runtime policy.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_InlineActionWithoutLifecycleHook_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;
            }

            start : { Count++; } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsNotInstanceOfType<NullParserExecutionStateManager>(policy.ExecutionStateManager);
    }

    /// <summary>
    /// Ensures a grammar with only semantic predicates (no inline actions, no lifecycle hooks) also wires a generated execution-state manager into the runtime policy.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_PredicateOnlyGrammar_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Guard;
                public void Allow() => Guard = 1;
            }

            start : { Guard != 0 }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsNotInstanceOfType<NullParserExecutionStateManager>(policy.ExecutionStateManager);
    }

    /// <summary>
    /// Ensures generated execution-state managers reject snapshots from another type.
    /// </summary>
    [TestMethod]
    public void GeneratedExecutionStateManager_RestoreRejectsWrongSnapshotType()
    {
        const string grammar = """
            grammar P;
            start @init { } : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.ThrowsException<ArgumentException>(() => policy.ExecutionStateManager.Restore(new object()));
    }

    /// <summary>
    /// Ensures generated <c>CopyFrom</c> validates the supplied source context.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CopyFrom_RejectsNullSource()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);
        var copyFrom = context.GetType().GetMethod("CopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var exception = Assert.ThrowsException<TargetInvocationException>(() => copyFrom.Invoke(context, [null]));

        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> delegates to <c>ParserExecutionContextCopier&lt;TContext&gt;.Copy</c>, preserving <see cref="ICloneable"/> precedence after parser rollback snapshots have also used the same path.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_UsesCloneableContextWhenAvailable()
    {
        const string userPartial = """
            using System;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext : ICloneable
            {
                public int CloneCallCount { get; private set; }

                public object Clone()
                {
                    CloneCallCount++;
                    var clone = new PExecutionContext();
                    clone.CopyFrom(this);
                    return clone;
                }
            }
            """;
        var assembly = CompileGeneratedSource(EmitCopyGrammar(), userPartial);
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var cloneCallCountBeforeManualFork = ReadContextIntProperty(context, "CloneCallCount");
        var fork = InvokeFork(context);

        Assert.AreEqual(cloneCallCountBeforeManualFork + 1, ReadContextIntProperty(context, "CloneCallCount"));
        Assert.AreEqual(ReadContextIntProperty(context, "CountValue"), ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(context, "ItemValues"), ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures the default embedded-code parse helper creates a fresh generated execution context for each call.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_DefaultOverload_CreatesFreshExecutionContextEachCall()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                internal static readonly System.Collections.Generic.List<int> ObservedCounts = new();

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                    ObservedCounts.Add(Count);
                }
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));

        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        CollectionAssert.AreEqual(new[] { 1, 1 }, ReadContextObservedCounts(assembly));
    }

    /// <summary>
    /// Ensures the generated facade does not expose a policy helper that creates and captures a hidden execution context.
    /// </summary>
    [TestMethod]
    public void CreateRuntimePolicy_WithoutExecutionContext_IsNotGenerated()
    {
        const string grammar = """
            grammar P;
            start : { true }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "CreateRuntimePolicy(PExecutionContext executionContext, ParserRuntimeFeaturePolicy? basePolicy = null)");
        Assert.IsFalse(source.Contains("public static ParserRuntimeFeaturePolicy CreateRuntimePolicy(ParserRuntimeFeaturePolicy? basePolicy = null)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("new PExecutionContext().CreateRuntimePolicy(basePolicy)", StringComparison.Ordinal));

        var assembly = CompileGeneratedSource(source);
        var facadeType = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var policyMethods = facadeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == "CreateRuntimePolicy")
            .ToArray();

        Assert.AreEqual(1, policyMethods.Length);
        var parameters = policyMethods[0].GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual(contextType, parameters[0].ParameterType);
        Assert.AreEqual(typeof(ParserRuntimeFeaturePolicy), policyMethods[0].ReturnType);
        Assert.AreEqual(typeof(ParserRuntimeFeaturePolicy), parameters[1].ParameterType);
        Assert.IsTrue(parameters[1].HasDefaultValue);
    }

    /// <summary>
    /// Ensures the generated opt-in parse overload preserves a custom rule-call execution policy
    /// and exposes current raw argument and label metadata without changing conservative Parse behavior.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_BasePolicy_ObservesRuleCallMetadata()
    {
        const string grammar = """
            grammar P;
            start : item=child[42] ;
            child : A ;
            A : 'a' ;
            """;
        string source = Emit(grammar);
        StringAssert.Contains(source, "ParseWithEmbeddedCode([global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof(P))] string input, PExecutionContext executionContext, ParserRuntimeFeaturePolicy basePolicy)");
        var assembly = CompileGeneratedSource(source);
        var executionContext = CreateExecutionContext(assembly);
        var callPolicy = new GeneratedRecordingRuleCallPolicy();
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = callPolicy,
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var before = callPolicy.Events.Single(item => item.Phase == "before" && item.Context.RuleName == "child").Context;
        var after = callPolicy.Events.Single(item => item.Phase == "after" && item.Context.RuleName == "child").Context;
        Assert.AreEqual("42", before.RawArguments);
        Assert.AreEqual("item", before.LabelName);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, before.LabelKind);
        Assert.IsTrue(after.Succeeded);
        Assert.IsNotNull(after.CompletedCallResult);
        Assert.AreEqual("42", after.CompletedCallResult.RawArguments);
        Assert.AreEqual("item", after.CompletedCallResult.LabelName);

        callPolicy.Events.Clear();
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", "a"), typeof(ErrorNode));
        Assert.AreEqual(0, callPolicy.Events.Count, "Conservative Parse() must not use the opt-in custom policy.");
    }

    /// <summary>
    /// Ensures generated embedded-code parsing accepts an explicitly installed typed positional policy through <c>basePolicy</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPositionalPolicy_BindsConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[42] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)42, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures generated embedded-code parsing accepts an explicitly installed typed named policy through <c>basePolicy</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedNamedPolicy_BindsConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[value: 42] ;
            child[long value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42L, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures typed IgnoreCall leaves an incompatible value absent while typed Throw reports the conversion failure.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_IncompatibleValueHonorsFailureBehavior()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool Found { get; private set; }
            }
            start : child["hello"] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var ignoredContext = CreateExecutionContext(assembly);
        var ignoredPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode ignoredResult = InvokeParseWithContextAndPolicy(assembly, "a", ignoredContext, ignoredPolicy);

        Assert.IsNotInstanceOfType(ignoredResult, typeof(ErrorNode));
        Assert.AreEqual(false, ReadContextObjectProperty(ignoredContext, "Found"));

        var throwingContext = CreateExecutionContext(assembly);
        var throwingPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw),
        };
        TargetInvocationException invocationException = Assert.ThrowsException<TargetInvocationException>(() =>
            InvokeParseWithContextAndPolicy(assembly, "a", throwingContext, throwingPolicy));
        Assert.IsInstanceOfType<ParserRuleCallBindingException>(invocationException.InnerException);
    }

    /// <summary>
    /// Ensures nullable null is retained as a present seed in generated execution state.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_NullableNullIsPresent()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool Found { get; private set; }
                public object? Seen { get; private set; } = 1;
            }
            start : child[null] ;
            child[int? value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(true, ReadContextObjectProperty(executionContext, "Found"));
        Assert.IsNull(ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures generated rollback and memoization use converted positional seed values from the current call site.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPositionalPolicy_RollbackUsesSuccessfulConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[1] B | child[2] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures memoization keys use the converted effective value rather than the original literal source form.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_EquivalentConvertedValuesShareMemoizedResult()
    {
        const string grammar = """
            grammar P;
            @members {
                public static int InitCount;
            }
            start : child[1] B | child[1.0] ;
            child[double value]
            @init {
                InitCount++;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
    }

    /// <summary>
    /// Ensures generated rollback and memoization use converted named seed values from the current call site.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedNamedPolicy_RollbackUsesSuccessfulConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[value: 1] B | child[value: 2] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures predicates can call instance members injected through <c>@members</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_Predicate_CallsInjectedInstanceMember()
    {
        const string grammar = """
            grammar P;

            @members {
                private bool Allow() => true;
            }

            start : { Allow() }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures predicate instance state belongs to the supplied generated execution context.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateState_UsesSuppliedExecutionContext()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                private bool Allow()
                {
                    Count++;
                    return true;
                }

                internal int CountValue => Count;
            }

            start : { Allow() }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var firstContext = CreateExecutionContext(assembly);
        var secondContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", firstContext);
        InvokeParseWithContext(assembly, "a", secondContext);

        Assert.IsTrue(ReadContextIntProperty(firstContext, "CountValue") > 0);
        Assert.IsTrue(ReadContextIntProperty(secondContext, "CountValue") > 0);
    }

    /// <summary>
    /// Ensures invalid C# injected through <c>@members</c> remains a Roslyn compilation error.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidMembersCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;

            @members {
                not valid csharp
            }

            start : A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures member-name collisions in injected <c>@members</c> remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_MembersHookNameCollision_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;

            @members {
                private void __Action_start_0_0_0(ParserActionExecutionContext context) { }
            }

            start : { } A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures multi-statement inline parser action hooks execute each generated C# statement.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineActionMultiStatement_ExecutesAllStatements()
    {
        const string grammar = """
            grammar P;
            start : {
                OnBefore(context);
                OnAfter(context);
            } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int BeforeCount;
                public static int AfterCount;

                private void OnBefore(ParserActionExecutionContext context)
                {
                    BeforeCount++;
                }

                private void OnAfter(ParserActionExecutionContext context)
                {
                    AfterCount++;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var defaultResult = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "BeforeCount"));
        Assert.AreEqual(0, ReadIntField(assembly, "AfterCount"));

        var embeddedResult = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "BeforeCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Ensures multi-line inline parser action hooks can declare local variables and pass them to user code.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineActionWithLocalVariable_ExecutesWithGeneratedLocals()
    {
        const string grammar = """
            grammar P;
            start : {
                var name = ruleName;
                OnAction(context, name);
            } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static string? ActionName;

                private void OnAction(ParserActionExecutionContext context, string name)
                {
                    ActionName = name;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("start", ReadStringField(assembly, "ActionName"));
    }

    /// <summary>
    /// Ensures duplicate embedded source text in different sequence positions dispatches through distinct hooks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_DuplicateActionSourceText_UsesPositionSpecificHooks()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A { OnAction(context); } B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 || context.ElementIndex == 2 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_0_0");
        StringAssert.Contains(source, "__Action_start_0_2_1");

        var assembly = CompileGeneratedSource(source, userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures a semantic predicate that is the only item in an alternative uses the runtime single-item element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SinglePredicateAlternative_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : { false }? ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Predicate_start_0_m1_0");

        var assembly = CompileGeneratedSource(source);

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", string.Empty), typeof(ErrorNode));
        Assert.IsInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", string.Empty), typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures an inline action that is the only item in an alternative dispatches and executes.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SingleActionAlternative_ExecutesAction()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == -1 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_m1_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", string.Empty), typeof(ErrorNode));
        Assert.AreEqual(0, ReadActionCount(assembly));

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", string.Empty), typeof(ErrorNode));
        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures an inline action inside a quantifier uses the runtime inner element index rather than the parent sequence index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_QuantifierInlineAction_ExecutesAction()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnAction(context); } B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 ? 1 : 100;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "abb");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures a predicate inside a quantifier is evaluated with the runtime inner element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_QuantifierPredicate_EvaluatesPredicate()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnPredicate(context) }? B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.InputPosition == 1 && context.ElementIndex == 0;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadPredicateCount(assembly) > 0);
    }

    /// <summary>
    /// Ensures equal action source text in separate alternatives dispatches by alternative index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_AlternativesWithSameActionSource_DispatchesByAlternativeIndex()
    {
        const string grammar = """
            grammar P;
            start
                : { OnAction(context); } A
                | { OnAction(context); } B
                ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.AlternativeIndex == 0 ? 1 : 10;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_0_0");
        StringAssert.Contains(source, "__Action_start_1_0_1");

        var assembly = CompileGeneratedSource(source, userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "a"), typeof(ErrorNode));
        Assert.AreEqual(1, ReadActionCount(assembly));

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "b"), typeof(ErrorNode));
        Assert.AreEqual(11, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures a predicate inside negation dispatches with the runtime probe index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NegationPredicate_DispatchesWithRuntimeIndex()
    {
        const string grammar = """
            grammar P;
            start : ~({ OnPredicate(context) }? A) ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.ElementIndex == 0;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "b");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadPredicateCount(assembly));
    }

    /// <summary>
    /// Ensures generated hooks in a direct-left-recursive tail use the runtime tail element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveTailAction_DispatchesWithRuntimeTailIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : INT
                | expr { OnAction(context); } PLUS INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_expr_0_0_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures generated predicates in a direct-left-recursive tail use the runtime tail element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveTailPredicate_DispatchesWithRuntimeTailIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : INT
                | expr { OnPredicate(context) }? PLUS INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.ElementIndex == 0;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Predicate_expr_0_0_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadPredicateCount(assembly) > 0);
    }

    /// <summary>
    /// Ensures a generated helper resolves direct-left-recursive metadata before dispatching a base alternative after a recursive alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveBaseAfterRecursiveAlternative_UsesResolvedAlternativeIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : expr PLUS INT
                | { false }? INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "new CompiledGrammar(Build(), executionContext.CreateRuntimePolicy())");
        StringAssert.Contains(source, "__Predicate_expr_0_0_0");

        var assembly = CompileGeneratedSource(source);

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", "1"), typeof(ErrorNode));
        Assert.IsInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "1"), typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures invalid embedded C# remains a Roslyn compilation error in the source-generator path.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidPredicateCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { not valid }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("not", StringComparison.Ordinal)));
    }


    /// <summary>
    /// Ensures predicate statement blocks without a return remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_PredicateBlockWithoutReturn_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : {
                var isStart = inputPosition == 0;
            }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures predicate blocks that return a non-boolean value remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_PredicateReturnWrongType_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { return "not bool"; }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("string", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Ensures invalid inline action C# remains a Roslyn compilation error in the source-generator path.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidActionCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { not valid ; } A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("not", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Ensures generated hook names remain aligned with shared runtime discovery metadata for representative parser shapes.
    /// </summary>
    [TestMethod]
    public void Emit_GeneratedHooks_MatchSharedRuntimeDiscoveryIndexes_ForParserShapes()
    {
        var singlePredicate = new ValidatingPredicate("true");
        var singleAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var sequenceAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var quantifierPredicate = new ValidatingPredicate("OnPredicate(context)");
        var negationPredicate = new ValidatingPredicate("OnPredicate(context)");
        var duplicateFirst = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var duplicateSecond = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var leftRecursiveBasePredicate = new ValidatingPredicate("false");
        var leftRecursiveTailAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);

        var cases = new[]
        {
            (
                Grammar: """
                    grammar P;
                    start : { true }? ;
                    A : 'a' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, singlePredicate)]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : { OnAction(context); } ;
                    A : 'a' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, singleAction)]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : A { OnAction(context); } B ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([new RuleRef("A"), sequenceAction, new RuleRef("B")]))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : A ({ OnPredicate(context) }? B)* ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([new RuleRef("A"), new Quantifier(new Sequence([quantifierPredicate, new RuleRef("B")]), 0, null)]))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : ~({ OnPredicate(context) }? A) ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Negation(new Sequence([negationPredicate, new RuleRef("A")])))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start
                        : { OnAction(context); } A
                        | { OnAction(context); } B
                        ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([
                    new Alternative(0, Associativity.Left, new Sequence([duplicateFirst, new RuleRef("A")])),
                    new Alternative(1, Associativity.Left, new Sequence([duplicateSecond, new RuleRef("B")]))
                ]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    expr
                        : expr PLUS INT
                        | { false }? INT
                        ;
                    INT : [0-9]+ ;
                    PLUS : '+' ;
                    """,
                Definition: CreateGeneratedParityLeftRecursiveBaseDefinition(leftRecursiveBasePredicate)),
            (
                Grammar: """
                    grammar P;
                    expr
                        : INT
                        | expr { OnAction(context); } PLUS INT
                        ;
                    INT : [0-9]+ ;
                    PLUS : '+' ;
                    """,
                Definition: CreateGeneratedParityLeftRecursiveTailDefinition(leftRecursiveTailAction))
        };

        foreach (var testCase in cases)
        {
            AssertGeneratedHooksMatchDiscovery(testCase.Grammar, testCase.Definition);
        }
    }

    /// <summary>
    /// Ensures generated hook dispatch metadata remains aligned with shared ParserDefinition runtime discovery metadata.
    /// </summary>
    [TestMethod]
    public void Emit_InlineActionHook_UsesSharedRuntimeDiscoveryIndexes()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnAction(context); } B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        var action = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var parserRule = new Rule(
            "start",
            0,
            false,
            new Alternation([new Alternative(0, Associativity.Left, new Sequence([
                new RuleRef("A"),
                new Quantifier(new Sequence([action, new RuleRef("B")]), 0, null)
            ]))]),
            Kind: RuleKind.Parser);
        var definition = new ParserDefinition("P", GrammarType.Combined, null, [], [], [], [parserRule], parserRule);

        var entry = EmbeddedCodeRuntimeDiscovery.Discover(definition).ExecutableEntries.Single();
        string generatedSource = Emit(grammar);
        string expectedHookName = $"__Action_{entry.RuleName}_{entry.AlternativeIndex}_{entry.ElementIndex}_0";

        Assert.AreEqual(EmbeddedCodeKind.ParserInlineAction, entry.Kind);
        Assert.AreEqual(0, entry.AlternativeIndex);
        Assert.AreEqual(0, entry.ElementIndex);
        StringAssert.Contains(generatedSource, expectedHookName);
    }


    /// <summary>
    /// Asserts generated hook names for all runtime-executable entries discovered from a hand-built parser definition.
    /// </summary>
    /// <param name="grammarText">ANTLR grammar text emitted by the production generator.</param>
    /// <param name="definition">Equivalent parser definition inspected by shared runtime discovery.</param>
    private static void AssertGeneratedHooksMatchDiscovery(string grammarText, ParserDefinition definition)
    {
        string generatedSource = Emit(grammarText);
        var entries = EmbeddedCodeRuntimeDiscovery.Discover(definition).ExecutableEntries;
        var ordinalsByKind = new Dictionary<EmbeddedCodeKind, int>();

        foreach (var entry in entries)
        {
            int ordinal = ordinalsByKind.TryGetValue(entry.Kind, out int current) ? current : 0;
            ordinalsByKind[entry.Kind] = ordinal + 1;
            string prefix = entry.Kind == EmbeddedCodeKind.SemanticPredicate ? "__Predicate" : "__Action";
            string elementIndex = entry.ElementIndex?.ToString() ?? "m1";
            string expectedHookName = $"{prefix}_{entry.RuleName}_{entry.AlternativeIndex}_{elementIndex}_{ordinal}";

            StringAssert.Contains(generatedSource, expectedHookName);
        }
    }

    /// <summary>
    /// Creates a parser definition used by generated-hook parity tests.
    /// </summary>
    /// <param name="rootRule">Root parser rule.</param>
    /// <returns>A parser definition containing the supplied root rule.</returns>
    private static ParserDefinition CreateGeneratedParityDefinition(Rule rootRule) =>
        new("P", GrammarType.Combined, null, [], [], [], [rootRule], rootRule);

    /// <summary>
    /// Creates a direct-left-recursive parser definition whose base alternative follows a recursive alternative.
    /// </summary>
    /// <param name="predicate">Predicate contained in the base alternative.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveBaseDefinition(ValidatingPredicate predicate)
    {
        var recursiveAlternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("expr"), new RuleRef("PLUS"), new RuleRef("INT")]));
        var baseAlternative = new Alternative(1, Associativity.Left, new Sequence([predicate, new RuleRef("INT")]));
        var rule = new Rule("expr", 0, false, new Alternation([recursiveAlternative, baseAlternative]), Kind: RuleKind.Parser);
        return CreateGeneratedParityLeftRecursiveDefinition(rule, [baseAlternative], [recursiveAlternative]);
    }

    /// <summary>
    /// Creates a direct-left-recursive parser definition with an executable tail action.
    /// </summary>
    /// <param name="action">Action contained in the recursive tail.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveTailDefinition(EmbeddedAction action)
    {
        var baseAlternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("INT")]));
        var recursiveAlternative = new Alternative(1, Associativity.Left, new Sequence([new RuleRef("expr"), action, new RuleRef("PLUS"), new RuleRef("INT")]));
        var rule = new Rule("expr", 0, false, new Alternation([baseAlternative, recursiveAlternative]), Kind: RuleKind.Parser);
        return CreateGeneratedParityLeftRecursiveDefinition(rule, [baseAlternative], [recursiveAlternative]);
    }

    /// <summary>
    /// Creates a parser definition with direct-left-recursive metadata populated.
    /// </summary>
    /// <param name="rule">Left-recursive parser rule.</param>
    /// <param name="baseAlternatives">Resolved base alternatives.</param>
    /// <param name="recursiveAlternatives">Resolved recursive alternatives.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveDefinition(
        Rule rule,
        IReadOnlyList<Alternative> baseAlternatives,
        IReadOnlyList<Alternative> recursiveAlternatives) =>
        new ParserDefinition("P", GrammarType.Combined, null, [], [], [], [rule], rule)
        {
            LeftRecursiveRules = new Dictionary<string, LeftRecursiveRuleInfo>
            {
                [rule.Name] = new()
                {
                    Rule = rule,
                    BaseAlternatives = baseAlternatives,
                    RecursiveAlternatives = recursiveAlternatives
                }
            }
        };

    /// <summary>
    /// Emits generated C# for the supplied grammar using the production grammar emitter.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar source.</param>
    /// <returns>Generated C# source.</returns>
    private static string Emit(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4");
    }

    /// <summary>
    /// Emits the shared grammar used to verify generated execution-context copy helpers.
    /// </summary>
    /// <returns>Generated C# source for a grammar with scalar and mutable collection context state.</returns>
    private static string EmitCopyGrammar()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;

                private List<string> Items = new();
                public IReadOnlyList<string> ItemValues => Items;
                public List<string> MutableItems => Items;
            }

            start : A { Count++; Items.Add("a"); } ;
            A : 'a' ;
            """;

        return Emit(grammar);
    }

    /// <summary>
    /// Compiles generated C# and optional user partial source, then loads the resulting in-memory assembly.
    /// </summary>
    /// <param name="generatedSource">Generated grammar source.</param>
    /// <param name="additionalSource">Optional user source compiled with the generated source.</param>
    /// <returns>Loaded test assembly.</returns>
    private static Assembly CompileGeneratedSource(string generatedSource, string? additionalSource = null)
    {
        var result = CompileGeneratedSourceExpectingFailure(generatedSource, additionalSource);
        if (!result.Success)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics));
        }

        result.AssemblyStream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(result.AssemblyStream);
    }

    /// <summary>
    /// Compiles generated C# and returns raw Roslyn diagnostics without asserting success.
    /// </summary>
    /// <param name="generatedSource">Generated grammar source.</param>
    /// <param name="additionalSource">Optional user source compiled with the generated source.</param>
    /// <returns>Compilation output and diagnostics.</returns>
    private static CompilationResult CompileGeneratedSourceExpectingFailure(string generatedSource, string? additionalSource = null)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(generatedSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), path: "P.g.cs")
        };

        if (additionalSource is not null)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(additionalSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), path: "P.User.cs"));
        }

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratedEmbeddedCodeTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        return new CompilationResult(emitResult.Success, stream, emitResult.Diagnostics);
    }

    /// <summary>
    /// Builds metadata references from trusted platform assemblies plus parser assemblies used by generated code.
    /// </summary>
    /// <returns>Roslyn metadata references.</returns>
    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is not null)
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                paths.Add(path);
            }
        }

        AddAssemblyPath(paths, typeof(ParserEngine).Assembly);
        AddAssemblyPath(paths, typeof(CompiledGrammar).Assembly);
        AddAssemblyPath(paths, typeof(object).Assembly);
        AddAssemblyPath(paths, typeof(Enumerable).Assembly);

        return paths.Select(static path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    /// <summary>
    /// Adds an assembly location to the reference path set when available.
    /// </summary>
    /// <param name="paths">Reference path set to update.</param>
    /// <param name="assembly">Assembly to add.</param>
    private static void AddAssemblyPath(HashSet<string> paths, Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            paths.Add(assembly.Location);
        }
    }

    /// <summary>
    /// Invokes a generated parse helper by reflection on the internal generated class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="methodName">Parse helper method name.</param>
    /// <param name="input">Input text to parse.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParse(Assembly assembly, string methodName, string input)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string));
        return (ParseNode)method.Invoke(null, [input])!;
    }

    /// <summary>
    /// Reads the test action counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current action count.</returns>
    private static int ReadActionCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField("ActionCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads the test predicate counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current predicate count.</returns>
    private static int ReadPredicateCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField("PredicateCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads a named integer field from the generated test partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="fieldName">Public static integer field name.</param>
    /// <returns>Current integer field value.</returns>
    private static int ReadIntField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads a named string field from the generated test partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="fieldName">Public static string field name.</param>
    /// <returns>Current string field value.</returns>
    private static string? ReadStringField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (string?)field.GetValue(null);
    }

    /// <summary>
    /// Creates a generated execution context instance by reflection.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated execution context.</param>
    /// <returns>A new generated execution context instance.</returns>
    private static object CreateExecutionContext(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }


    /// <summary>
    /// Invokes the generated facade helper that creates a runtime policy for an explicit execution context.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="executionContext">Execution context instance to bind to the policy.</param>
    /// <returns>The generated runtime policy bound to the supplied execution context.</returns>
    private static ParserRuntimeFeaturePolicy InvokeCreateRuntimePolicy(Assembly assembly, object executionContext)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "CreateRuntimePolicy"
                && method.GetParameters() is [{ ParameterType: var executionContextType }, { ParameterType: var basePolicyType }]
                && executionContextType == contextType
                && basePolicyType == typeof(ParserRuntimeFeaturePolicy));
        return (ParserRuntimeFeaturePolicy)method.Invoke(null, [executionContext, null])!;
    }

    /// <summary>
    /// Invokes the generated embedded-code parse overload that accepts an explicit execution context.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="input">Input text to parse.</param>
    /// <param name="executionContext">Execution context instance to pass to the generated overload.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParseWithContext(Assembly assembly, string input, object executionContext)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is [{ ParameterType: var inputType }, { ParameterType: var executionContextType }]
                && inputType == typeof(string)
                && executionContextType == contextType);
        return (ParseNode)method.Invoke(null, [input, executionContext])!;
    }

    /// <summary>
    /// Invokes the generated embedded-code parse overload that accepts an execution context and base policy.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="input">Input text to parse.</param>
    /// <param name="executionContext">Execution context instance to pass to the generated overload.</param>
    /// <param name="basePolicy">Base runtime policy whose custom rule-call policy should be preserved.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParseWithContextAndPolicy(
        Assembly assembly,
        string input,
        object executionContext,
        ParserRuntimeFeaturePolicy basePolicy)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is
                [
                    { ParameterType: var inputType },
                    { ParameterType: var executionContextType },
                    { ParameterType: var policyType },
                ]
                && inputType == typeof(string)
                && executionContextType == contextType
                && policyType == typeof(ParserRuntimeFeaturePolicy));
        return (ParseNode)method.Invoke(null, [input, executionContext, basePolicy])!;
    }

    /// <summary>
    /// Invokes the generated internal <c>Fork</c> helper on an execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to fork.</param>
    /// <returns>The copied execution context returned by <c>Fork</c>.</returns>
    private static object InvokeFork(object executionContext)
    {
        var method = executionContext.GetType().GetMethod("Fork", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return method.Invoke(executionContext, [])!;
    }

    /// <summary>
    /// Invokes the generated internal <c>CopyFrom</c> helper on an execution context instance.
    /// </summary>
    /// <param name="target">Execution context instance that receives copied state.</param>
    /// <param name="source">Execution context instance that provides copied state.</param>
    private static void InvokeCopyFrom(object target, object source)
    {
        var method = target.GetType().GetMethod("CopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(target, [source]);
    }

    /// <summary>
    /// Reads a named integer property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The integer property value.</returns>
    private static int ReadContextIntProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (int)property.GetValue(executionContext)!;
    }

    /// <summary>
    /// Reads a named object property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The property value.</returns>
    private static object ReadContextObjectProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return property.GetValue(executionContext)!;
    }

    /// <summary>
    /// Reads a named string property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The string property value.</returns>
    private static string? ReadContextStringProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (string?)property.GetValue(executionContext);
    }

    /// <summary>
    /// Reads a named string collection property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The string collection values as an array.</returns>
    private static string[] ReadContextStringItems(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var values = (System.Collections.Generic.IEnumerable<string>)property.GetValue(executionContext)!;
        return values.ToArray();
    }

    /// <summary>
    /// Reads static observed action counts from the generated execution context type.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated execution context.</param>
    /// <returns>Observed action counts.</returns>
    private static int[] ReadContextObservedCounts(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField("ObservedCounts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var counts = (System.Collections.Generic.IEnumerable<int>)field.GetValue(null)!;
        return counts.ToArray();
    }

    /// <summary>
    /// Records rule-call callbacks observed through a generated opt-in runtime policy.
    /// </summary>
    private sealed class GeneratedRecordingRuleCallPolicy : IParserRuleCallExecutionPolicy
    {
        /// <summary>
        /// Gets callback events in invocation order.
        /// </summary>
        public List<(string Phase, ParserRuleCallExecutionContext Context)> Events { get; } = [];

        /// <summary>
        /// Records a before-call callback.
        /// </summary>
        /// <param name="context">Current rule-call context.</param>
        public void BeforeRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("before", context));
        }

        /// <summary>
        /// Records an after-call callback.
        /// </summary>
        /// <param name="context">Completed rule-call context.</param>
        public void AfterRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("after", context));
        }
    }

    /// <summary>
    /// Captures Roslyn compilation output for generated embedded-code tests.
    /// </summary>
    /// <param name="Success">Whether compilation succeeded.</param>
    /// <param name="AssemblyStream">Emitted assembly stream.</param>
    /// <param name="Diagnostics">Roslyn diagnostics reported during compilation.</param>
    private sealed record CompilationResult(bool Success, MemoryStream AssemblyStream, IReadOnlyList<Diagnostic> Diagnostics);
}
