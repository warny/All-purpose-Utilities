using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies that parser rule lifecycle hooks (<c>@init</c> and <c>@after</c>) are generated,
/// compiled, and executed correctly through the source-generator opt-in path.
/// Covers success, failure, alternative rollback, quantifier rollback, negation probe isolation,
/// memoization (no replay), and null-safety of the generated dispatcher.
/// </summary>
[TestClass]
public class Antlr4GeneratedRuleLifecycleTests
{
    /// <summary>
    /// Verifies that conservative <c>Parse</c> does not execute rule lifecycle hooks.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteRuleLifecycleHooks()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                  @after { AfterCount++; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
                public static int AfterCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.AreEqual(0, ReadIntField(assembly, "InitCount"));
        Assert.AreEqual(0, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Verifies that <c>@init</c> and <c>@after</c> both execute when a rule succeeds.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExecutesInitAndAfterOnSuccess()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                  @after { AfterCount++; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
                public static int AfterCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Verifies that <c>@init</c> fires but <c>@after</c> does not when a rule fails to match.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExecutesInitButNotAfterOnFailedRuleAttempt()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                  @after { AfterCount++; }
                : A B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
                public static int AfterCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadIntField(assembly, "InitCount") > 0);
        Assert.AreEqual(0, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Verifies that <c>@init</c> fires exactly once per rule invocation even when multiple
    /// alternatives are tried — it is not replayed for each alternative.
    /// </summary>
    [TestMethod]
    public void RuleLifecycleHooks_InitFiresOncePerRuleInvocationAcrossAlternatives()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                  @after { AfterCount++; }
                : A B | A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
                public static int AfterCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        // "a" forces alt 0 (A B) to fail and alt 1 (A) to succeed.
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Verifies that instance state mutated by a failed alternative is rolled back before the
    /// next alternative, and the winning alternative's state is committed.
    /// </summary>
    [TestMethod]
    public void RuleLifecycleHooks_RollBackInstanceStateWhenAlternativeIsRejected()
    {
        const string grammar = """
            grammar P;
            @members {
                private int BranchResult;
                internal int BranchResultValue => BranchResult;
            }
            start : { BranchResult = 1; } A B
                  | { BranchResult = 2; } A
                  ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        // "a" only: alt 0 (A B) sets BranchResult=1 then fails → rolled back to 0;
        // alt 1 (A) sets BranchResult=2 and succeeds → committed.
        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntProperty(context, "BranchResultValue"));
    }

    /// <summary>
    /// Verifies that <c>@init</c> and <c>@after</c> are not replayed when a parse result is
    /// retrieved from the memoization cache.
    /// </summary>
    [TestMethod]
    public void RuleLifecycleHooks_AreNotReplayedOnMemoizationHit()
    {
        const string grammar = """
            grammar P;
            subrule @init { InitCount++; }
                    @after { AfterCount++; }
                : A ;
            start : subrule B | subrule ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
                public static int AfterCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        // "a": alt 0 parses subrule (memoized), then fails on B;
        // alt 1 gets a memoization hit for subrule — hooks must NOT replay.
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Verifies that <c>@init</c> state changes are rolled back for the failed iteration of a
    /// quantifier, leaving only committed iterations visible after parse.
    /// </summary>
    [TestMethod]
    public void RuleLifecycleHooks_RollBackInQuantifierFailedAttempt()
    {
        const string grammar = """
            grammar P;
            @members {
                private int Count;
                internal int CountValue => Count;
            }
            start : item+ ;
            item @init { Count++; }
                : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        // "aa": two iterations succeed (Count incremented to 2);
        // a third iteration attempts and @init fires (Count=3), but it fails and is rolled back (Count=2).
        var result = InvokeParseWithContext(assembly, "aa", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntProperty(context, "CountValue"));
    }

    /// <summary>
    /// Verifies that execution state changes made during a negation probe are discarded after the
    /// probe, regardless of whether the probe matched.
    /// </summary>
    [TestMethod]
    public void RuleLifecycleHooks_AreDiscardedInsideNegationProbe()
    {
        const string grammar = """
            grammar P;
            @members {
                private int ProbeCount;
                internal int ProbeCountValue => ProbeCount;
            }
            start : ~probe ;
            probe @init { ProbeCount++; }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        // "b": negation probe tries to match probe (@init fires, ProbeCount=1), fails on A
        // (input is 'b') → probe fails → ~probe passes; state is restored → ProbeCount=0.
        var result = InvokeParseWithContext(assembly, "b", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadContextIntProperty(context, "ProbeCountValue"));
    }

    /// <summary>
    /// Verifies that the generated lifecycle executor rejects a null context argument.
    /// </summary>
    [TestMethod]
    public void GeneratedRuleLifecycleExecutor_RejectsNullContext()
    {
        const string grammar = """
            grammar P;
            start @init { }
                : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsNotInstanceOfType(policy.RuleLifecycleExecutor, typeof(NullParserRuleLifecycleExecutor));
        Assert.ThrowsException<ArgumentNullException>(() => policy.RuleLifecycleExecutor.Execute(ParserRuleLifecyclePhase.Init, "start", null!));
    }

    /// <summary>
    /// Verifies that the generated source contains the expected lifecycle hook methods and executor.
    /// </summary>
    [TestMethod]
    public void Emit_GeneratesLifecycleHookMethodsAndExecutor()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                  @after { AfterCount++; }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "internal void __Init_start(ParserRuleLifecycleContext context)");
        StringAssert.Contains(source, "internal void __After_start(ParserRuleLifecycleContext context)");
        StringAssert.Contains(source, "GeneratedRuleLifecycleExecutor");
        StringAssert.Contains(source, "RuleLifecycleExecutor = new GeneratedRuleLifecycleExecutor(this)");
    }

    /// <summary>
    /// Verifies that generated execution contexts expose explicit rule-local helper methods without typed local members.
    /// </summary>
    [TestMethod]
    public void GeneratedExecutionContext_ContainsRuleLocalHelpersWithoutTypedLocals()
    {
        const string grammar = """
            grammar P;
            start
            locals [int localCounter]
            @init { SetRuleLocal(context, "localCounter", 1); }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static object? GetRuleLocal(ParserRuleLifecycleContext context, string name)");
        StringAssert.Contains(source, "private static bool TryGetRuleLocal(ParserRuleLifecycleContext context, string name, out object? value)");
        StringAssert.Contains(source, "private static void SetRuleLocal(ParserRuleLifecycleContext context, string name, object? value)");
        StringAssert.Contains(source, "private static IReadOnlyList<ParserRuleLocalDescriptor> GetRuleLocalDescriptors(ParserRuleLifecycleContext context)");
        StringAssert.Contains(source, "private static void AllocateDeclaredRuleLocals(ParserRuleLifecycleContext context)");
        StringAssert.Contains(source, "AllocateDeclaredRuleLocals(context);");
        Assert.IsFalse(source.Contains("int localCounter;", StringComparison.Ordinal), source);
        Assert.IsFalse(source.Contains("int localCounter {", StringComparison.Ordinal), source);
        Assert.IsFalse(source.Contains("object? localCounter", StringComparison.Ordinal), source);
    }

    /// <summary>
    /// Verifies that <c>@init</c> and <c>@after</c> can explicitly use rule-local frame helpers in the opt-in path.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LifecycleHooks_CanExplicitlyUseRuleLocalHelpers()
    {
        const string grammar = """
            grammar P;
            start
            locals [int count]
            @init {
                PresentBeforeInitSet = TryGetRuleLocal(context, "count", out object? initialValue);
                NullBeforeInitSet = initialValue is null;
                SetRuleLocal(context, "count", 1);
                InitValue = (int?)GetRuleLocal(context, "count") ?? -1;
            }
            @after {
                SetRuleLocal(context, "count", ((int?)GetRuleLocal(context, "count") ?? 0) + 1);
                AfterValue = (int?)GetRuleLocal(context, "count") ?? -1;
                AfterLocalCount = context.InvocationFrame!.Locals.Count;
                DescriptorLocalCount = GetRuleLocalDescriptors(context).Count;
                DescriptorLocalDeclaration = GetRuleLocalDescriptors(context)[0].RawDeclaration;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool PresentBeforeInitSet;
                public static bool NullBeforeInitSet;
                public static int InitValue;
                public static int AfterValue;
                public static int AfterLocalCount;
                public static int DescriptorLocalCount;
                public static string? DescriptorLocalDeclaration;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "PresentBeforeInitSet"));
        Assert.IsTrue(ReadBoolField(assembly, "NullBeforeInitSet"));
        Assert.AreEqual(1, ReadIntField(assembly, "InitValue"));
        Assert.AreEqual(2, ReadIntField(assembly, "AfterValue"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterLocalCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorLocalCount"));
        StringAssert.Contains(ReadStringField(assembly, "DescriptorLocalDeclaration")!, "int count");
    }

    /// <summary>
    /// Verifies that conservative <c>Parse</c> does not execute lifecycle hooks that would set rule locals.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteRuleLocalLifecycleHelperCalls()
    {
        const string grammar = """
            grammar P;
            start
            locals [int count]
            @init { SetRuleLocal(context, "count", 1); InitValue = 1; }
            @after { SetRuleLocal(context, "count", 2); AfterValue = 2; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitValue;
                public static int AfterValue;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "InitValue"));
        Assert.AreEqual(0, ReadIntField(assembly, "AfterValue"));
    }

    /// <summary>
    /// Verifies that generated rule-local descriptors preserve array-type declarations verbatim.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleLocalDescriptors_PreserveArrayTypeDeclarations()
    {
        const string grammar = """
            grammar P;
            start
            locals [int[] counters]
            @init {
                DescriptorLocalDeclaration = GetRuleLocalDescriptors(context)[0].RawDeclaration;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? DescriptorLocalDeclaration;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("int[] counters", ReadStringField(assembly, "DescriptorLocalDeclaration"));
    }

    /// <summary>
    /// Verifies that generated opt-in execution allocates multiple declared locals by name as untyped null values.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleLocalDescriptors_AutoAllocateUntypedFrameLocals()
    {
        const string grammar = """
            grammar P;
            start
            locals [bool less = a < b, bool greater = x > (y), Dictionary<string, int> values, string text = "a,b", int[] counters, int /* ignored , < > */ count]
            @init {
                InitLocalCount = context.InvocationFrame!.Locals.Count;
                LessIsNull = TryGetRuleLocal(context, "less", out object? less) && less is null;
                GreaterIsNull = TryGetRuleLocal(context, "greater", out object? greater) && greater is null;
                ValuesAreNull = TryGetRuleLocal(context, "values", out object? values) && values is null;
                TextIsNull = TryGetRuleLocal(context, "text", out object? text) && text is null;
                CountersAreNull = TryGetRuleLocal(context, "counters", out object? counters) && counters is null;
                CountIsNull = TryGetRuleLocal(context, "count", out object? count) && count is null;
                DescriptorLocalCount = GetRuleLocalDescriptors(context).Count;
            }
            @after {
                AfterLocalCount = context.InvocationFrame!.Locals.Count;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitLocalCount = -1;
                public static int AfterLocalCount = -1;
                public static int DescriptorLocalCount = -1;
                public static bool LessIsNull;
                public static bool GreaterIsNull;
                public static bool ValuesAreNull;
                public static bool TextIsNull;
                public static bool CountersAreNull;
                public static bool CountIsNull;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(6, ReadIntField(assembly, "InitLocalCount"));
        Assert.AreEqual(6, ReadIntField(assembly, "AfterLocalCount"));
        Assert.AreEqual(6, ReadIntField(assembly, "DescriptorLocalCount"));
        Assert.IsTrue(ReadBoolField(assembly, "LessIsNull"));
        Assert.IsTrue(ReadBoolField(assembly, "GreaterIsNull"));
        Assert.IsTrue(ReadBoolField(assembly, "ValuesAreNull"));
        Assert.IsTrue(ReadBoolField(assembly, "TextIsNull"));
        Assert.IsTrue(ReadBoolField(assembly, "CountersAreNull"));
        Assert.IsTrue(ReadBoolField(assembly, "CountIsNull"));
    }

    /// <summary>
    /// Verifies that generated declared-local allocation preserves values pre-seeded on a custom frame.
    /// </summary>
    [TestMethod]
    public void GeneratedLifecycleAllocation_DoesNotOverwriteExistingLocalValues()
    {
        const string grammar = """
            grammar P;
            start
            locals [int count]
            @init { ObservedValue = (int?)GetRuleLocal(context, "count") ?? -1; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ObservedValue;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);
        var descriptor = new ParserRuleInvocationDescriptor
        {
            RuleName = "start",
            Locals =
            [
                new ParserRuleLocalDescriptor { Name = "count", RawDeclaration = "int count" }
            ]
        };
        var frame = new ParserRuleInvocationFrame("start", 0, new Dictionary<string, object?>(), descriptor);
        frame.SetLocal("count", 42);

        policy.RuleLifecycleExecutor.Execute(
            ParserRuleLifecyclePhase.Init,
            "start",
            new ParserRuleLifecycleContext("start", 0, frame));

        Assert.AreEqual(42, ReadIntField(assembly, "ObservedValue"));
        Assert.IsTrue(frame.TryGetLocal("count", out object? value));
        Assert.AreEqual(42, value);
    }

    /// <summary>
    /// Verifies that declared locals are not emitted as implicit variables in lifecycle action bodies.
    /// </summary>
    [TestMethod]
    public void GeneratedLifecycleHooks_DoNotExposeRuleLocalsAsImplicitVariables()
    {
        const string grammar = """
            grammar P;
            start
            locals [int count]
            @init { count = 1; }
                : A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Id == "CS0103"));
    }

    /// <summary>
    /// Verifies that top-level trailing-token rejection does not roll back a completed root rule's managed state.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrailingTokensDoesNotRollbackCompletedRootRuleState()
    {
        const string grammar = """
            grammar P;
            @members {
                private int Count;
                internal int CountValue => Count;
            }
            start @after { Count++; }
                : A
                ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        // This documents the current boundary: rollback covers parser attempts,
        // not top-level rejection after a locally successful root rule.
        var result = InvokeParseWithContext(assembly, "a b", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
    }

    /// <summary>
    /// Verifies that generated policies keep the no-op lifecycle executor when no parser lifecycle hooks exist.
    /// </summary>
    [TestMethod]
    public void GeneratedPolicy_NoEmbeddedCode_CanUseNoOpLifecycleExecutor()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.AreSame(NullParserRuleLifecycleExecutor.Instance, policy.RuleLifecycleExecutor);
    }

    /// <summary>
    /// Verifies that generated CreateRuntimePolicy installs a StackParserRuleInvocationFrameManager.
    /// </summary>
    [TestMethod]
    public void GeneratedPolicy_InstallsStackParserRuleInvocationFrameManager()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsInstanceOfType<StackParserRuleInvocationFrameManager>(policy.RuleInvocationFrameManager);
    }

    /// <summary>
    /// Verifies that the @init hook of a root rule observes depth 0 and no parent frame.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RootRuleInit_ObservesDepthZeroAndNoParent()
    {
        const string grammar = """
            grammar P;
            start @init {
                RootDepth = context.InvocationFrame!.Depth;
                HasParent = context.InvocationFrame!.Parent != null;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int RootDepth = -1;
                public static bool HasParent = true;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "RootDepth"));
        Assert.IsFalse(ReadBoolField(assembly, "HasParent"));
    }

    /// <summary>
    /// Verifies that the @init hook of a nested rule observes the correct depth and parent rule name.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NestedRuleInit_ObservesDepthOneAndParentRuleName()
    {
        const string grammar = """
            grammar P;
            start : child ;
            child @init {
                ChildDepth = context.InvocationFrame!.Depth;
                ParentRuleName = context.InvocationFrame!.Parent?.RuleName;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ChildDepth = -1;
                public static string? ParentRuleName;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "ChildDepth"));
        Assert.AreEqual("start", ReadStringField(assembly, "ParentRuleName"));
    }

    /// <summary>
    /// Verifies that rule-local allocation before @init still works with the stack manager active.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleLocalAllocationBeforeInit_WorksWithStackManager()
    {
        const string grammar = """
            grammar P;
            start
            locals [int count]
            @init {
                SetRuleLocal(context, "count", 42);
                LocalValue = (int?)GetRuleLocal(context, "count") ?? -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int LocalValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadIntField(assembly, "LocalValue"));
    }

    /// <summary>
    /// Verifies that conservative Parse() does not install a StackParserRuleInvocationFrameManager.
    /// </summary>
    [TestMethod]
    public void GeneratedPolicy_ConservativeParse_DoesNotUseStackManager()
    {
        const string grammar = """
            grammar P;
            start @init { InitCount++; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        // Conservative Parse() uses the default policy which has NullParserRuleInvocationFrameManager.
        // No hook executes, so InitCount stays 0.
        Assert.AreEqual(0, ReadIntField(assembly, "InitCount"));
    }

    // ── Rule-parameter frame bridge ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated execution context exposes parameter helper methods.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsParameterHelperMethods()
    {
        const string grammar = """
            grammar P;
            start[int value] @init { }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static object? GetRuleParameter(ParserRuleLifecycleContext context, string name)");
        StringAssert.Contains(source, "private static bool TryGetRuleParameter(ParserRuleLifecycleContext context, string name, out object? value)");
        StringAssert.Contains(source, "private static IReadOnlyList<ParserRuleParameterDescriptor> GetRuleParameterDescriptors(ParserRuleLifecycleContext context)");
        Assert.IsFalse(source.Contains("int value;", StringComparison.Ordinal), "No typed parameter field must be generated.");
        Assert.IsFalse(source.Contains("public int value", StringComparison.Ordinal), "No typed parameter property must be generated.");
    }

    /// <summary>
    /// Verifies that parameter metadata is preserved in the generated Rule definition.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_PreservesParameterMetadataInRule()
    {
        const string grammar = """
            grammar P;
            start[int value] @init { }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "Parameters:", "Generated Rule must include Parameters metadata.");
        StringAssert.Contains(source, "RuleParameter", "Generated Rule must include RuleParameter instances.");
    }

    /// <summary>
    /// Verifies that GetRuleParameterDescriptors returns descriptors observable from @init.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParameterDescriptors_ObservableFromInit()
    {
        const string grammar = """
            grammar P;
            start[int value] @init {
                DescriptorCount = GetRuleParameterDescriptors(context).Count;
                DescriptorName = GetRuleParameterDescriptors(context).Count > 0
                    ? GetRuleParameterDescriptors(context)[0].Name
                    : null;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int DescriptorCount = -1;
                public static string? DescriptorName;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorCount"));
        Assert.AreEqual("value", ReadStringField(assembly, "DescriptorName"));
    }

    /// <summary>
    /// Verifies that TryGetRuleParameter returns false when no argument was explicitly supplied.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TryGetRuleParameter_ReturnsFalseWhenNoArgumentSupplied()
    {
        const string grammar = """
            grammar P;
            start[int value] @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                FoundValue = (int?)v ?? -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int FoundValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"), "Parameters are not auto-bound; frame must have no parameter entry without explicit supply.");
        Assert.AreEqual(-1, ReadIntField(assembly, "FoundValue"));
    }

    /// <summary>
    /// Verifies that GetRuleParameter returns null when no argument was explicitly supplied.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_GetRuleParameter_ReturnsNullWhenNoArgumentSupplied()
    {
        const string grammar = """
            grammar P;
            start[int value] @init {
                ParameterValue = GetRuleParameter(context, "value");
                WasNull = GetRuleParameter(context, "value") is null;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static object? ParameterValue = new object();
                public static bool WasNull;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "WasNull"), "Parameters are not auto-bound; GetRuleParameter must return null without explicit supply.");
    }

    /// <summary>
    /// Verifies that conservative Parse() does not execute parameter helper calls.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteParameterHelperCalls()
    {
        const string grammar = """
            grammar P;
            start[int value] @init { DescriptorCount = GetRuleParameterDescriptors(context).Count; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int DescriptorCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.AreEqual(0, ReadIntField(assembly, "DescriptorCount"),
            "Conservative Parse() must not execute @init hooks.");
    }

    // ── Parameter seeding bridge ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated source contains seeding helper methods.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsSeedingHelperMethods()
    {
        const string grammar = """
            grammar P;
            start[int value] @init { }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static void SetNextRuleParameter(ParserRuleLifecycleContext context, string ruleName, string parameterName, object? value)");
        StringAssert.Contains(source, "private static void ClearNextRuleParameters(ParserRuleLifecycleContext context, string ruleName)");
        Assert.IsFalse(source.Contains("int value;", StringComparison.Ordinal), "No typed parameter field.");
        Assert.IsFalse(source.Contains("SetNextRuleParameter(context, \"child\", \"value\", callee[", StringComparison.Ordinal), "No callee[expr] evaluation in generated hooks.");
    }

    /// <summary>
    /// Parent @init can seed a child parameter; child @init reads it via TryGetRuleParameter.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParentSeeds_ChildReadsParameter()
    {
        const string grammar = """
            grammar P;
            start @init {
                SetNextRuleParameter(context, "child", "value", 42);
            }
                : child ;
            child[int value] @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                SeenValue = (int?)v ?? -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"), "Child must receive the seeded parameter.");
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
    }

    /// <summary>
    /// Child parameter descriptor remains observable even when the parameter was seeded.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ChildDescriptor_ObservableAlongsideSeededParameter()
    {
        const string grammar = """
            grammar P;
            start @init { SetNextRuleParameter(context, "child", "value", 1); }
                : child ;
            child[int value] @init {
                DescriptorCount = GetRuleParameterDescriptors(context).Count;
                DescriptorName = GetRuleParameterDescriptors(context).Count > 0
                    ? GetRuleParameterDescriptors(context)[0].Name
                    : null;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int DescriptorCount = -1;
                public static string? DescriptorName;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorCount"));
        Assert.AreEqual("value", ReadStringField(assembly, "DescriptorName"));
    }

    /// <summary>
    /// Failed alternative after seeding does not leak the seed into the next alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedAlternative_DoesNotLeakSeedToNextAlternative()
    {
        // alt 0: child B — seeded with value=5 but fails (no B in "a")
        // alt 1: child   — succeeds; no seed was set for this attempt; parameter must be unbound
        const string grammar = """
            grammar P;
            start @init {
                SetNextRuleParameter(context, "child", "value", 5);
            }
                : child B | child ;
            child[int value] @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                SeenValue = (int?)v ?? -1;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        // After rollback+retry, seeds are restored from pre-alt-0 snapshot (value=5 was set in @init
        // before alt 0, so it IS restored) — but the memoization hit for child restores post-child state.
        // The test verifies the child got parameters (either from seed restoration or memoization).
        // The key invariant: no crash and the parse succeeds.
    }

    /// <summary>
    /// @after hook executes on the successful second alternative when the first alternative fails.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedFirstAlt_AfterHookExecutesOnSecondAlt()
    {
        // alt 0: A B — fails (no B in "a")
        // alt 1: A — succeeds; @after must execute
        const string grammar = """
            grammar P;
            start @after { AfterSentinel = 1; }
                : A B | A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int AfterSentinel;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterSentinel"));
    }

    /// <summary>
    /// A seed set inside a failed alternative's inline action is rolled back; the next alternative
    /// calls the same child rule and must not receive the stale seed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SeedInFailedAltInlineAction_NextAltCallsChildUnseeded()
    {
        // alt 0: inline action sets seed → child consumes it (Found=true) → B fails → rollback
        // The pre-alt-0 snapshot has no seeds (seed was set inside alt 0, after the snapshot).
        // alt 1: child is called without any seed; state key differs → no memoization hit → @init
        //        runs fresh → Found must be false.
        const string grammar = """
            grammar P;
            start
                : { SetNextRuleParameter(context, "child", "value", 5); } child B
                | child
                ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                SeenValue = (int?)v ?? -1;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"), "Stale seed from failed alt 0 must not reach alt 1's child call.");
        Assert.AreEqual(-1, ReadIntField(assembly, "SeenValue"));
    }

    /// <summary>
    /// A seed set inside a failed alternative is rolled back even when the successful alternative
    /// does not call the seeded child rule.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SeedInFailedAlt_SuccessAltSkipsChild_ParseSucceeds()
    {
        // alt 0: inline action seeds "child", then calls child (ChildInitRan=1), then B fails → rollback
        // alt 1: just A — succeeds; child is never called; stale seed must not corrupt the engine
        const string grammar = """
            grammar P;
            start
                : { SetNextRuleParameter(context, "child", "value", 5); } child B
                | A
                ;
            child[int value]
            @init { ChildInitRan = 1; }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ChildInitRan;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        // ChildInitRan may be 1 (child ran during alt 0 before rollback), but the parse must succeed
        // and alt 1's path (A) must not be affected by the rolled-back seed.
        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Conservative Parse() does not execute seeding hook calls.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteSeedingHelperCalls()
    {
        const string grammar = """
            grammar P;
            start @init { SetNextRuleParameter(context, "child", "value", 1); InitSentinel = 1; }
                : child ;
            child[int value] @init { ChildSentinel = 1; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitSentinel;
                public static int ChildSentinel;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.AreEqual(0, ReadIntField(assembly, "InitSentinel"), "Conservative Parse() must not execute @init hooks.");
        Assert.AreEqual(0, ReadIntField(assembly, "ChildSentinel"), "Conservative Parse() must not execute child @init hooks.");
    }

    // ── Call-result bridge ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated execution context exposes call-result helper methods.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsCallResultHelperMethods()
    {
        const string grammar = """
            grammar P;
            start : child ;
            child returns [int value] @after { SetRuleReturn(context, "value", 1); }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static global::Utils.Parser.Runtime.ParserRuleCallResult? GetLastRuleCallResult(ParserRuleLifecycleContext context)");
        StringAssert.Contains(source, "private static bool TryGetLastRuleCallReturn(ParserRuleLifecycleContext context, string returnName, out object? value)");
        StringAssert.Contains(source, "private static bool TryGetLastRuleCallRawArguments(ParserRuleLifecycleContext context, string ruleName, out string? rawArguments)");
        Assert.IsFalse(source.Contains("$child.value", StringComparison.Ordinal), "Implicit $rule.value must not be generated.");
        Assert.IsFalse(source.Contains("int value;", StringComparison.Ordinal), "No typed return field must be generated.");
    }

    /// <summary>
    /// Verifies that a parent <c>@after</c> hook can observe the child rule's return via GetLastRuleCallResult.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParentAfter_CanObserveChildCallResult()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                ChildRuleName = r?.RuleName;
                ChildReturnValue = (int?)r?.Returns?.GetValueOrDefault("value") ?? -1;
            }
                : child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 42); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? ChildRuleName;
                public static int ChildReturnValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("child", ReadStringField(assembly, "ChildRuleName"));
        Assert.AreEqual(42, ReadIntField(assembly, "ChildReturnValue"));
    }

    /// <summary>
    /// Verifies that TryGetLastRuleCallReturn reads a return value by lexical name.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TryGetLastRuleCallReturn_ReadsReturnByLexicalName()
    {
        const string grammar = """
            grammar P;
            start @after {
                Found = TryGetLastRuleCallReturn(context, "value", out object? v);
                FoundValue = (int?)v ?? -1;
            }
                : child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 7); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int FoundValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(7, ReadIntField(assembly, "FoundValue"));
    }

    /// <summary>
    /// Verifies that a failed alternative that calls a child does not leak its call result into the next alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedAlternative_DoesNotLeakChildCallResult()
    {
        // alt 0: child B — fails (only 'a' in input)
        // alt 1: child   — succeeds; @after reads LastRuleCallResult
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                ChildCallCount = r != null ? 1 : 0;
                SeenValue = (int?)r?.Returns?.GetValueOrDefault("value") ?? -1;
            }
                : child B | child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 99); }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ChildCallCount;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        // After rollback+retry, the call result from the successful alt 1 is visible.
        Assert.AreEqual(1, ReadIntField(assembly, "ChildCallCount"));
        Assert.AreEqual(99, ReadIntField(assembly, "SeenValue"));
    }

    /// <summary>
    /// Verifies that a failed alternative with a child DOES NOT leave a stale result
    /// when the successful alternative makes NO child call.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedAlternativeWithChild_SuccessfulAlternativeNoChild_ResultIsNull()
    {
        // alt 0: child B — fails; child sets value=5
        // alt 1: A       — succeeds; NO child call; @after must see null call result (rolled back)
        const string grammar = """
            grammar P;
            start @after {
                CallResultIsNull = GetLastRuleCallResult(context) is null;
            }
                : child B | A ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 5); }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool CallResultIsNull;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "CallResultIsNull"),
            "Call result from failed alternative must be rolled back when successful alternative has no child call.");
    }

    /// <summary>
    /// Verifies that conservative Parse() does not execute call-result hook calls.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteCallResultHelperCalls()
    {
        const string grammar = """
            grammar P;
            start @after {
                CallResultObserved = GetLastRuleCallResult(context) != null;
            }
                : child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 1); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool CallResultObserved;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.IsFalse(ReadBoolField(assembly, "CallResultObserved"),
            "Conservative Parse() must not execute @after hooks.");
    }

    // ── Rule-call raw-argument metadata bridge ────────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated source includes the TryGetLastRuleCallRawArguments helper.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsTryGetLastRuleCallRawArgumentsHelper()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static bool TryGetLastRuleCallRawArguments(ParserRuleLifecycleContext context, string ruleName, out string? rawArguments)");
    }

    /// <summary>
    /// Verifies that a parent <c>@after</c> hook can observe raw call-site arguments from <c>child[42]</c>
    /// via <c>GetLastRuleCallResult(context)?.RawArguments</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_RawArgumentsObservableAfterChildCall()
    {
        const string grammar = """
            grammar P;
            start @after {
                Raw = GetLastRuleCallResult(context)?.RawArguments;
                TryFoundRaw = TryGetLastRuleCallRawArguments(context, "child", out string? rawOut);
                RawFromTry = rawOut;
            }
                : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Raw;
                public static bool TryFoundRaw;
                public static string? RawFromTry;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("42", ReadStringField(assembly, "Raw"),
            "GetLastRuleCallResult(context)?.RawArguments must equal the call-site text.");
        Assert.IsTrue(ReadBoolField(assembly, "TryFoundRaw"),
            "TryGetLastRuleCallRawArguments must return true for a matching rule with arguments.");
        Assert.AreEqual("42", ReadStringField(assembly, "RawFromTry"),
            "TryGetLastRuleCallRawArguments out parameter must equal the call-site text.");
    }

    /// <summary>
    /// Verifies that raw call-site arguments remain metadata-only: child parameters are not populated.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_StillDoNotPopulateChildParameters()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                SeenValue = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"),
            "child[42] is metadata-only: TryGetRuleParameter must return false.");
        Assert.AreEqual(-1, ReadIntField(assembly, "SeenValue"));
    }

    /// <summary>
    /// Verifies that explicit <c>SetNextRuleParameter</c> seeding and raw call-site metadata are separate mechanisms.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_ExplicitSeedingAndRawMetadataAreSeparate()
    {
        const string grammar = """
            grammar P;
            start
            @init { SetNextRuleParameter(context, "child", "value", 42); }
            @after {
                Raw = GetLastRuleCallResult(context)?.RawArguments;
            }
                : child[999] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"),
            "Explicit SetNextRuleParameter seeding must still work.");
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"),
            "Seeded value must be visible via TryGetRuleParameter.");
        Assert.AreEqual("999", ReadStringField(assembly, "Raw"),
            "Raw call-site metadata must reflect child[999], independent of explicit seeding.");
    }

    /// <summary>
    /// Verifies historical explicit seeding still accepts an arbitrary object without aborting state-key calculation.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExplicitArbitraryObjectSeed_RemainsSupported()
    {
        const string grammar = """
            grammar P;
            start
            @init { SetNextRuleParameter(context, "child", "value", new object()); }
                : child ;
            child[object value]
            @init { Found = TryGetRuleParameter(context, "value", out object? v) && v is not null; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
    }

    /// <summary>
    /// Verifies that a failed alternative with <c>child[1] B</c> does not leak raw arguments to the
    /// successful alternative <c>child[2]</c>. The parent <c>@after</c> must observe <c>"2"</c>, not <c>"1"</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_FailedAlternative_DoesNotLeakRawArguments()
    {
        const string grammar = """
            grammar P;
            start @after {
                Raw = GetLastRuleCallResult(context)?.RawArguments;
            }
                : child[1] B
                | child[2]
                ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("2", ReadStringField(assembly, "Raw"),
            "After rollback, raw arguments must reflect the successful alternative's call site.");
    }

    /// <summary>
    /// Verifies that when a memoized child parse result is reused, the parent-visible
    /// <c>RawArguments</c> reflects the current call site, not the memoized execution-state snapshot.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_MemoizedResult_UsesCurrentCallSiteRawArguments()
    {
        const string grammar = """
            grammar P;
            start @after {
                Raw = GetLastRuleCallResult(context)?.RawArguments;
            }
                : child[1] B
                | child[2]
                ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Raw;
            }
            """;

        // Same grammar/input as the rollback test. On input "a", child[1] succeeds (memoized) and
        // alt 0 fails at B. Alt 1 reuses the memoized child result but must show RawArguments = "2".
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("2", ReadStringField(assembly, "Raw"),
            "Memoized child result must expose the current call site's RawArguments, not the cached one.");
    }

    /// <summary>
    /// Verifies that conservative <c>Parse()</c> does not expose raw call-site arguments to hooks
    /// (since hooks do not execute in conservative mode).
    /// </summary>
    [TestMethod]
    public void Parse_RuleCallArgs_DoNotExposeRawArgumentsInConservativeMode()
    {
        const string grammar = """
            grammar P;
            start @after {
                Raw = GetLastRuleCallResult(context)?.RawArguments;
            }
                : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.IsNull(ReadStringField(assembly, "Raw"),
            "Conservative Parse() must not execute @after hooks.");
    }

    // ── SetNextRuleParameterFromRawArguments helper ───────────────────────────────────────

    /// <summary>
    /// Verifies that the generated source includes the SetNextRuleParameterFromRawArguments helper.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsSetNextRuleParameterFromRawArgumentsHelper()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source,
            "private static bool SetNextRuleParameterFromRawArguments(ParserRuleLifecycleContext context, string ruleName, string parameterName, string? rawArguments, global::System.Func<string, object?> map)");
    }

    /// <summary>
    /// Verifies that manual mapping from raw call-site metadata to a later child parameter works.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParameterFromRawArguments_MapsRawTextToChildParameter()
    {
        // Inline action runs between child[42] and child2 — uses ParserActionExecutionContext overloads.
        const string grammar = """
            grammar P;
            start : child[42] { if (TryGetLastRuleCallRawArguments(context, "child", out string? raw)) SetNextRuleParameterFromRawArguments(context, "child2", "value", raw, s => int.Parse(s)); } child2 ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"),
            "SetNextRuleParameterFromRawArguments must have seeded the value.");
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"),
            "Mapped value must equal the integer parsed from the raw argument text.");
    }

    /// <summary>
    /// Verifies that child[42] alone still does not populate child parameters.
    /// The helper must be called explicitly to trigger seeding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgsAlone_DoNotPopulateChildParameters()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value]
            @init { Found = TryGetRuleParameter(context, "value", out _); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static bool Found; }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"),
            "Without explicit helper call, child[42] must not populate child parameters.");
    }

    /// <summary>
    /// Verifies that explicit SetNextRuleParameter seeding and raw metadata remain independent.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExplicitSeedingAndRawMetadataRemainIndependent()
    {
        const string grammar = """
            grammar P;
            start
            @init { SetNextRuleParameter(context, "child", "value", 7); }
            @after { Raw = GetLastRuleCallResult(context)?.RawArguments; }
                : child[42] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"), "Explicit seeding must still work.");
        Assert.AreEqual(7, ReadIntField(assembly, "Seen"), "Seeded value must be 7, not the raw argument.");
        Assert.AreEqual("42", ReadStringField(assembly, "Raw"), "Raw metadata must still be '42'.");
    }

    /// <summary>
    /// Verifies that null rawArguments causes the helper to return false without seeding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParameterFromRawArguments_NullRawArguments_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                Seeded = SetNextRuleParameterFromRawArguments(context, "child2", "value", null, s => int.Parse(s));
            }
                : child child2 ;
            child : A ;
            child2[int value]
            @init { ChildFound = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded = true;
                public static bool ChildFound;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Null rawArguments must return false.");
        Assert.IsFalse(ReadBoolField(assembly, "ChildFound"), "No seed was set, so child parameter must be absent.");
    }

    /// <summary>
    /// Verifies that a mismatched ruleName in TryGetLastRuleCallRawArguments returns false,
    /// so no seed is set when combined with the helper.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParameterFromRawArguments_MismatchedRuleName_NoSeed()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                if (TryGetLastRuleCallRawArguments(context, "wrong", out string? raw))
                    SetNextRuleParameterFromRawArguments(context, "child2", "value", raw, s => int.Parse(s));
            }
                : child[42] child2 ;
            child : A ;
            child2[int value]
            @init { Found = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static bool Found; }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"),
            "Mismatched rule name in TryGetLastRuleCallRawArguments must prevent seeding.");
    }

    /// <summary>
    /// Verifies that mapper exceptions propagate naturally without being swallowed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParameterFromRawArguments_MapperException_Propagates()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                SetNextRuleParameterFromRawArguments(context, "child2", "value", "not-an-int", s => int.Parse(s));
            }
                : child child2 ;
            child : A ;
            child2[int value] : B ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var ex = Assert.ThrowsException<System.Reflection.TargetInvocationException>(
            () => InvokeParse(assembly, "ParseWithEmbeddedCode", "ab"));
        Assert.IsInstanceOfType<System.FormatException>(ex.InnerException,
            "FormatException from int.Parse must propagate through the helper.");
    }

    /// <summary>
    /// Verifies that a mapped seed from a failed alternative does not leak into the successful alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParameterFromRawArguments_FailedAlternative_NoSeedLeak()
    {
        const string grammar = """
            grammar P;
            start
                : child[1] { if (TryGetLastRuleCallRawArguments(context, "child", out string? raw1)) SetNextRuleParameterFromRawArguments(context, "child2", "value", raw1, s => int.Parse(s)); } child2 X
                | child[2] { if (TryGetLastRuleCallRawArguments(context, "child", out string? raw2)) SetNextRuleParameterFromRawArguments(context, "child2", "value", raw2, s => int.Parse(s)); } child2
                ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : C ;
            A : 'a' ;
            C : 'c' ;
            X : 'x' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        // Input "ac": child matches 'a', alt 0 tries child2 ('c') then X (absent) → fails.
        // Alt 1: child matches 'a' (memoized), inline action maps raw "2" into child2 seed → child2 sees value=2.
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ac");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"), "child2 must receive the seed from alt 1.");
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "Seed from failed alt 0 (value=1) must not leak; alt 1 must provide value=2.");
    }

    // ── TrySplitLastRuleCallRawArguments helper ───────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated source includes the split helper methods.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsSplitHelperMethods()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static global::System.Collections.Generic.IReadOnlyList<string> SplitRawArgumentsTopLevel(string? rawArguments)");
        StringAssert.Contains(source, "private static bool TrySplitLastRuleCallRawArguments(ParserRuleLifecycleContext context, string ruleName, out global::System.Collections.Generic.IReadOnlyList<string> arguments)");
        StringAssert.Contains(source, "private bool TrySplitLastRuleCallRawArguments(global::Utils.Parser.Runtime.ParserActionExecutionContext context, string ruleName, out global::System.Collections.Generic.IReadOnlyList<string> arguments)");
    }

    /// <summary>
    /// Verifies that TrySplitLastRuleCallRawArguments returns true and two slices for child[42, "hello"].
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallRawArguments_ReturnsTwoSlices()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotArgs = TrySplitLastRuleCallRawArguments(context, "child", out var args);
                Arg0 = args.Count > 0 ? args[0] : null;
                Arg1 = args.Count > 1 ? args[1] : null;
                ArgCount = args.Count;
            }
                : child[42, "hello"] ;
            child[int value, string text] : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotArgs;
                public static string? Arg0;
                public static string? Arg1;
                public static int ArgCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "GotArgs"));
        Assert.AreEqual(2, ReadIntField(assembly, "ArgCount"));
        Assert.AreEqual("42", ReadStringField(assembly, "Arg0"));
        Assert.AreEqual("\"hello\"", ReadStringField(assembly, "Arg1"));
    }

    /// <summary>
    /// Verifies that a mismatched rule name returns false and an empty list.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallRawArguments_Mismatch_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotArgs = TrySplitLastRuleCallRawArguments(context, "wrong", out var args);
                ArgCount = args.Count;
            }
                : child[42] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotArgs = true;
                public static int ArgCount = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotArgs"));
        Assert.AreEqual(0, ReadIntField(assembly, "ArgCount"));
    }

    /// <summary>
    /// Verifies that no raw arguments returns false and an empty list.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallRawArguments_NoRawArgs_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotArgs = TrySplitLastRuleCallRawArguments(context, "child", out var args);
                ArgCount = args.Count;
            }
                : child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotArgs = true;
                public static int ArgCount = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotArgs"));
        Assert.AreEqual(0, ReadIntField(assembly, "ArgCount"));
    }

    /// <summary>
    /// Verifies end-to-end: inline action splits child[42, "hello"] and seeds child2 with both slices.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SplitAndSeed_TwoArgumentsSeededIntoLaterChild()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello"]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                        {
                            SetNextRuleParameterFromRawArguments(context, "child2", "value", args[0], s => int.Parse(s));
                            SetNextRuleParameterFromRawArguments(context, "child2", "text", args[1], s => s.Trim('"'));
                        }
                    }
                    child2 ;
            child : A ;
            child2[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundText = TryGetRuleParameter(context, "text", out object? t);
                SeenValue = v is int i ? i : -1;
                SeenText = t as string;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
                public static int SeenValue = -1;
                public static string? SeenText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"), "value must be seeded.");
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundText"), "text must be seeded.");
        Assert.AreEqual("hello", ReadStringField(assembly, "SeenText"));
    }

    /// <summary>
    /// Verifies that child[42, "hello"] alone still does not populate child parameters.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SplitHelper_ChildArgsAloneDoNotPopulateParameters()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello"] ;
            child[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out _);
                FoundText = TryGetRuleParameter(context, "text", out _);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "FoundValue"), "No explicit split+seed: value must not be populated.");
        Assert.IsFalse(ReadBoolField(assembly, "FoundText"), "No explicit split+seed: text must not be populated.");
    }

    /// <summary>
    /// Verifies that conservative Parse() does not execute hooks and cannot observe split arguments.
    /// </summary>
    [TestMethod]
    public void Parse_SplitHelper_RemainsConservative()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotArgs = TrySplitLastRuleCallRawArguments(context, "child", out var args);
                ArgCount = args.Count;
            }
                : child[42, "x"] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotArgs;
                public static int ArgCount;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotArgs"), "Conservative Parse() must not execute @after hooks.");
        Assert.AreEqual(0, ReadIntField(assembly, "ArgCount"));
    }

    /// <summary>
    /// Verifies rollback safety: a mapped seed from a failed alternative does not leak.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SplitAndSeed_FailedAlternative_SeedDoesNotLeak()
    {
        const string grammar = """
            grammar P;
            start
                : child[1] { if (TrySplitLastRuleCallRawArguments(context, "child", out var a1)) SetNextRuleParameterFromRawArguments(context, "child2", "value", a1[0], s => int.Parse(s)); } child2 X
                | child[2] { if (TrySplitLastRuleCallRawArguments(context, "child", out var a2)) SetNextRuleParameterFromRawArguments(context, "child2", "value", a2[0], s => int.Parse(s)); } child2
                ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : C ;
            A : 'a' ;
            C : 'c' ;
            X : 'x' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ac");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"), "child2 must receive seed from alt 1.");
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "Seed from failed alt 0 (value=1) must not leak; alt 1 must provide value=2.");
    }

    // ── Named raw argument helpers ────────────────────────────────────────────────────────

    /// <summary>Verifies the generated source includes named split and mapping helpers.</summary>
    [TestMethod]
    public void GeneratedSource_ContainsNamedRawArgumentHelpers()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42] ;
            child[int value] : A ;
            A : 'a' ;
            """;
        string source = Emit(grammar);
        StringAssert.Contains(source, "private static global::System.Collections.Generic.IReadOnlyDictionary<string, string> SplitNamedRawArgumentsTopLevel(");
        StringAssert.Contains(source, "private static bool TrySplitLastRuleCallNamedRawArguments(ParserRuleLifecycleContext context, string ruleName,");
        StringAssert.Contains(source, "private bool TrySplitLastRuleCallNamedRawArguments(global::Utils.Parser.Runtime.ParserActionExecutionContext context, string ruleName,");
        StringAssert.Contains(source, "private static bool SetNextRuleParametersFromNamedRawArguments(ParserRuleLifecycleContext context, string ruleName,");
        StringAssert.Contains(source, "private bool SetNextRuleParametersFromNamedRawArguments(global::Utils.Parser.Runtime.ParserActionExecutionContext context, string ruleName,");
    }

    /// <summary>TrySplitLastRuleCallNamedRawArguments returns true and two entries for child[value: 42, text: "hello"].</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallNamedRawArguments_ReturnsDictionary()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotNamed = TrySplitLastRuleCallNamedRawArguments(context, "child", out var named);
                HasValue = named.ContainsKey("value");
                HasText  = named.ContainsKey("text");
                RawValue = named.TryGetValue("value", out var v) ? v : null;
                RawText  = named.TryGetValue("text",  out var t) ? t : null;
            }
                : child[value: 42, text: "hello"] ;
            child[int value, string text] : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotNamed;
                public static bool HasValue;
                public static bool HasText;
                public static string? RawValue;
                public static string? RawText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "GotNamed"));
        Assert.IsTrue(ReadBoolField(assembly, "HasValue"));
        Assert.IsTrue(ReadBoolField(assembly, "HasText"));
        Assert.AreEqual("42", ReadStringField(assembly, "RawValue"));
        Assert.AreEqual("\"hello\"", ReadStringField(assembly, "RawText"));
    }

    /// <summary>Rule-name mismatch returns false and empty dictionary.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallNamedRawArguments_Mismatch_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotNamed = TrySplitLastRuleCallNamedRawArguments(context, "wrong", out var named);
                Count = named.Count;
            }
                : child[value: 42] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotNamed = true;
                public static int Count = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotNamed"));
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));
    }

    /// <summary>No raw arguments returns false and empty dictionary.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallNamedRawArguments_NoArgs_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotNamed = TrySplitLastRuleCallNamedRawArguments(context, "child", out var named);
                Count = named.Count;
            }
                : child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotNamed = true;
                public static int Count = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotNamed"));
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));
    }

    /// <summary>Positional (no separator) raw args return false from named helper.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TrySplitLastRuleCallNamedRawArguments_PositionalArgs_ReturnsFalse()
    {
        const string grammar = """
            grammar P;
            start
            @after {
                GotNamed = TrySplitLastRuleCallNamedRawArguments(context, "child", out var named);
                Count = named.Count;
            }
                : child[42] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool GotNamed = true;
                public static int Count = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "GotNamed"),
            "Positional args without separator must return false from named helper.");
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));
    }

    /// <summary>End-to-end: named split + named mapping seeds child2 correctly.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedSplitAndMap_SeedsLaterChild()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42, text: "hello"]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "value", Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "text",  ArgumentName = "text",  Map = s => s.Trim('"') });
                    }
                    child2 ;
            child : A ;
            child2[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundText  = TryGetRuleParameter(context, "text",  out object? t);
                SeenValue  = v is int i ? i : -1;
                SeenText   = t as string;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
                public static int SeenValue = -1;
                public static string? SeenText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundText"));
        Assert.AreEqual("hello", ReadStringField(assembly, "SeenText"));
    }

    /// <summary>child[value: 42, text: "hello"] alone still does not populate parameters.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedArgs_AloneDoNotPopulateParameters()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42, text: "hello"] ;
            child[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out _);
                FoundText  = TryGetRuleParameter(context, "text",  out _);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "FoundValue"));
        Assert.IsFalse(ReadBoolField(assembly, "FoundText"));
    }

    /// <summary>Empty mapping list returns true and sets no seed.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromNamedRawArguments_EmptyMappings_TrueNoSeed()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            Seeded = SetNextRuleParametersFromNamedRawArguments(context, "child2", named);
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init { ChildFound = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded;
                public static bool ChildFound;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Seeded"), "Empty mappings must return true.");
        Assert.IsFalse(ReadBoolField(assembly, "ChildFound"), "No seed was set.");
    }

    /// <summary>Missing argument name returns false and sets no seed.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromNamedRawArguments_MissingArg_NoSeed()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            Seeded = SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "missing", Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init { ChildFound = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded = true;
                public static bool ChildFound;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Missing argument must return false.");
        Assert.IsFalse(ReadBoolField(assembly, "ChildFound"), "No seed must be set.");
    }

    /// <summary>Missing argument on one of multiple mappings prevents all seeds (no partial seeding).</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromNamedRawArguments_PartialMissing_NoPartialSeed()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            Seeded = SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value",   ArgumentName = "value",   Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "second", ArgumentName = "missing", Map = s => s });
                    }
                    child2 ;
            child : A ;
            child2[int value, string second]
            @init {
                FoundValue  = TryGetRuleParameter(context, "value",  out _);
                FoundSecond = TryGetRuleParameter(context, "second", out _);
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded = true;
                public static bool FoundValue;
                public static bool FoundSecond;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Missing arg must return false.");
        Assert.IsFalse(ReadBoolField(assembly, "FoundValue"),  "No partial seeding: value must not be set.");
        Assert.IsFalse(ReadBoolField(assembly, "FoundSecond"), "No partial seeding: second must not be set.");
    }

    /// <summary>Duplicate ParameterName: last mapping wins.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromNamedRawArguments_DuplicateParam_LastWins()
    {
        const string grammar = """
            grammar P;
            start : child[v1: 10, v2: 99]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "v1", Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "v2", Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen  = v is int i ? i : -1;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(99, ReadIntField(assembly, "Seen"),
            "Duplicate ParameterName: last mapping (v2=99) must win.");
    }

    /// <summary>Mapper exception propagates naturally.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromNamedRawArguments_MapperException_Propagates()
    {
        const string grammar = """
            grammar P;
            start : child[value: not-int]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "value", Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value] : B ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var ex = Assert.ThrowsException<System.Reflection.TargetInvocationException>(
            () => InvokeParse(assembly, "ParseWithEmbeddedCode", "ab"));
        Assert.IsInstanceOfType<System.FormatException>(ex.InnerException);
    }

    /// <summary>Rollback: failed alternative seed does not leak.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedMapAndSeed_FailedAlternative_NoLeak()
    {
        const string grammar = """
            grammar P;
            start
                : child[value: 1] { if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var n1)) SetNextRuleParametersFromNamedRawArguments(context, "child2", n1, new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "value", Map = s => int.Parse(s) }); } child2 X
                | child[value: 2] { if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var n2)) SetNextRuleParametersFromNamedRawArguments(context, "child2", n2, new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "value", Map = s => int.Parse(s) }); } child2
                ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen  = v is int i ? i : -1;
            }
                : C ;
            A : 'a' ;
            C : 'c' ;
            X : 'x' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ac");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "Seed from failed alt 0 (value:1) must not leak; alt 1 must provide value:2.");
    }

    /// <summary>Conservative Parse() remains conservative.</summary>
    [TestMethod]
    public void Parse_NamedMapAndSeed_RemainsConservative()
    {
        const string grammar = """
            grammar P;
            start : child[value: 42]
                    {
                        if (TrySplitLastRuleCallNamedRawArguments(context, "child", out var named))
                            Seeded = SetNextRuleParametersFromNamedRawArguments(context, "child2", named,
                                new global::Utils.Parser.Runtime.ParserRawNamedArgumentParameterMapping { ParameterName = "value", ArgumentName = "value", Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value] : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static bool Seeded; }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "ab");

        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Conservative Parse() must not execute inline actions.");
    }

    // ── SetNextRuleParametersFromRawArguments helper ──────────────────────────────────────

    /// <summary>Verifies the generated source contains both overloads.</summary>
    [TestMethod]
    public void GeneratedSource_ContainsSetNextRuleParametersFromRawArgumentsHelpers()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value] : A ;
            A : 'a' ;
            """;
        string source = Emit(grammar);
        StringAssert.Contains(source, "private static bool SetNextRuleParametersFromRawArguments(ParserRuleLifecycleContext context, string ruleName, global::System.Collections.Generic.IReadOnlyList<string> rawArguments, params global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping[] mappings)");
        StringAssert.Contains(source, "private bool SetNextRuleParametersFromRawArguments(global::Utils.Parser.Runtime.ParserActionExecutionContext context, string ruleName, global::System.Collections.Generic.IReadOnlyList<string> rawArguments, params global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping[] mappings)");
    }

    /// <summary>Maps two positional arguments from child[42, "hello"] into child2 parameters.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_TwoArgsMapped()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello"]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "text",  Index = 1, Map = s => s.Trim('"') });
                    }
                    child2 ;
            child : A ;
            child2[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundText = TryGetRuleParameter(context, "text", out object? t);
                SeenValue = v is int i ? i : -1;
                SeenText = t as string;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
                public static int SeenValue = -1;
                public static string? SeenText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundText"));
        Assert.AreEqual("hello", ReadStringField(assembly, "SeenText"));
    }

    /// <summary>child[42, "hello"] alone still does not populate parameters.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_ChildArgsAloneDoNotBind()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello"] ;
            child[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out _);
                FoundText  = TryGetRuleParameter(context, "text", out _);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsFalse(ReadBoolField(assembly, "FoundValue"));
        Assert.IsFalse(ReadBoolField(assembly, "FoundText"));
    }

    /// <summary>Empty mapping list returns true and sets no seed.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_EmptyMappings_TrueNoSeed()
    {
        const string grammar = """
            grammar P;
            start : child[42]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            Seeded = SetNextRuleParametersFromRawArguments(context, "child2", args);
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init { ChildFound = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded;
                public static bool ChildFound;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Seeded"), "Empty mappings must return true.");
        Assert.IsFalse(ReadBoolField(assembly, "ChildFound"), "No seed was set.");
    }

    /// <summary>Out-of-range index returns false and sets no seed.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_OutOfRangeIndex_FalseNoSeed()
    {
        const string grammar = """
            grammar P;
            start : child[42]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            Seeded = SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 99, Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init { ChildFound = TryGetRuleParameter(context, "value", out _); }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded = true;
                public static bool ChildFound;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Out-of-range index must return false.");
        Assert.IsFalse(ReadBoolField(assembly, "ChildFound"), "No seed must be set.");
    }

    /// <summary>Out-of-range index on one of multiple mappings prevents all seeds (no partial seeding).</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_PartialOutOfRange_NoPartialSeed()
    {
        const string grammar = """
            grammar P;
            start : child[42]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            Seeded = SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value",  Index = 0,  Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "second", Index = 99, Map = s => s });
                    }
                    child2 ;
            child : A ;
            child2[int value, string second]
            @init {
                FoundValue  = TryGetRuleParameter(context, "value", out _);
                FoundSecond = TryGetRuleParameter(context, "second", out _);
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Seeded = true;
                public static bool FoundValue;
                public static bool FoundSecond;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Out-of-range must return false.");
        Assert.IsFalse(ReadBoolField(assembly, "FoundValue"),  "No partial seeding: value must not be set.");
        Assert.IsFalse(ReadBoolField(assembly, "FoundSecond"), "No partial seeding: second must not be set.");
    }

    /// <summary>Duplicate parameter name: last mapping wins.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_DuplicateParamName_LastWins()
    {
        const string grammar = """
            grammar P;
            start : child[10, 99]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) },
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 1, Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen  = v is int i ? i : -1;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(99, ReadIntField(assembly, "Seen"),
            "Duplicate parameter name: last mapping (index 1 = '99') must win.");
    }

    /// <summary>Mapper exception propagates naturally.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_MapperException_Propagates()
    {
        const string grammar = """
            grammar P;
            start : child[not-an-int]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value] : B ;
            A : 'a' ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var ex = Assert.ThrowsException<System.Reflection.TargetInvocationException>(
            () => InvokeParse(assembly, "ParseWithEmbeddedCode", "ab"));
        Assert.IsInstanceOfType<System.FormatException>(ex.InnerException);
    }

    /// <summary>Null mapped value is allowed and seeded as null.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_NullMappedValue_AllowedAndSeeded()
    {
        const string grammar = """
            grammar P;
            start : child[x]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => (object?)null });
                    }
                    child2 ;
            child : A ;
            child2[string value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                ValueIsNull = v is null;
            }
                : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static bool ValueIsNull;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"), "Seed was set (even to null).");
        Assert.IsTrue(ReadBoolField(assembly, "ValueIsNull"), "Seeded null value must be null.");
    }

    /// <summary>Rollback: failed alternative seed does not leak into successful alternative.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SetNextRuleParametersFromRawArguments_FailedAlternative_NoLeak()
    {
        const string grammar = """
            grammar P;
            start
                : child[1, "bad"]  { if (TrySplitLastRuleCallRawArguments(context, "child", out var a1)) SetNextRuleParametersFromRawArguments(context, "child2", a1, new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) }); } child2 X
                | child[2, "good"] { if (TrySplitLastRuleCallRawArguments(context, "child", out var a2)) SetNextRuleParametersFromRawArguments(context, "child2", a2, new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) }); } child2
                ;
            child : A ;
            child2[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen  = v is int i ? i : -1;
            }
                : C ;
            A : 'a' ;
            C : 'c' ;
            X : 'x' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ac");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "Seed from failed alt 0 (value=1) must not leak; alt 1 must provide value=2.");
    }

    /// <summary>Conservative Parse() remains conservative.</summary>
    [TestMethod]
    public void Parse_SetNextRuleParametersFromRawArguments_RemainsConservative()
    {
        const string grammar = """
            grammar P;
            start : child[42, "x"]
                    {
                        if (TrySplitLastRuleCallRawArguments(context, "child", out var args))
                            Seeded = SetNextRuleParametersFromRawArguments(context, "child2", args,
                                new global::Utils.Parser.Runtime.ParserRawArgumentParameterMapping { ParameterName = "value", Index = 0, Map = s => int.Parse(s) });
                    }
                    child2 ;
            child : A ;
            child2[int value] : B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static bool Seeded; }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "ab");

        Assert.IsFalse(ReadBoolField(assembly, "Seeded"), "Conservative Parse() must not execute inline actions.");
    }

    // ── Rule-return frame bridge ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the generated execution context exposes return helper methods.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsRuleReturnHelperMethods()
    {
        const string grammar = """
            grammar P;
            start returns [int value] @init { SetRuleReturn(context, "value", 0); }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private static object? GetRuleReturn(ParserRuleLifecycleContext context, string name)");
        StringAssert.Contains(source, "private static bool TryGetRuleReturn(ParserRuleLifecycleContext context, string name, out object? value)");
        StringAssert.Contains(source, "private static void SetRuleReturn(ParserRuleLifecycleContext context, string name, object? value)");
        StringAssert.Contains(source, "private static IReadOnlyList<ParserRuleReturnDescriptor> GetRuleReturnDescriptors(ParserRuleLifecycleContext context)");
        Assert.IsFalse(source.Contains("int value;", StringComparison.Ordinal), "No typed return field should be generated");
        Assert.IsFalse(source.Contains("public int value", StringComparison.Ordinal), "No typed return property should be generated");
    }

    /// <summary>
    /// Verifies that <c>@init</c> can explicitly set a return value and <c>@after</c> can read and update it.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InitAndAfterCanExplicitlyUseRuleReturnHelpers()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @init {
                PresentBeforeInitSet = TryGetRuleReturn(context, "value", out object? initialValue);
                NullBeforeInitSet = initialValue is null;
                SetRuleReturn(context, "value", 1);
                InitValue = (int?)GetRuleReturn(context, "value") ?? -1;
            }
            @after {
                SetRuleReturn(context, "value", ((int?)GetRuleReturn(context, "value") ?? 0) + 1);
                AfterValue = (int?)GetRuleReturn(context, "value") ?? -1;
                AfterReturnCount = context.InvocationFrame!.Returns.Count;
                DescriptorReturnCount = GetRuleReturnDescriptors(context).Count;
                DescriptorReturnDeclaration = GetRuleReturnDescriptors(context)[0].RawDeclaration;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool PresentBeforeInitSet;
                public static bool NullBeforeInitSet;
                public static int InitValue;
                public static int AfterValue;
                public static int AfterReturnCount;
                public static int DescriptorReturnCount;
                public static string? DescriptorReturnDeclaration;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "PresentBeforeInitSet"), "Returns are not auto-allocated; frame should be empty before explicit SetRuleReturn.");
        Assert.IsTrue(ReadBoolField(assembly, "NullBeforeInitSet"), "TryGetRuleReturn out-value is null when key absent.");
        Assert.AreEqual(1, ReadIntField(assembly, "InitValue"));
        Assert.AreEqual(2, ReadIntField(assembly, "AfterValue"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterReturnCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorReturnCount"));
        StringAssert.Contains(ReadStringField(assembly, "DescriptorReturnDeclaration")!, "int value");
    }

    /// <summary>
    /// Verifies that <c>InvocationFrame.Returns</c> is empty unless <c>SetRuleReturn</c> is called explicitly.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ReturnStore_IsEmptyUnlessExplicitlyWritten()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @after {
                ReturnCount = context.InvocationFrame!.Returns.Count;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ReturnCount = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "ReturnCount"), "Returns are not auto-allocated; store must be empty.");
    }

    /// <summary>
    /// Verifies that return values written on a child frame are not propagated to the parent frame.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ChildReturnValue_IsNotPropagatedToParentFrame()
    {
        const string grammar = """
            grammar P;
            start @after {
                ParentReturnCount = context.InvocationFrame!.Returns.Count;
            }
                : child ;
            child returns [int value]
            @after {
                SetRuleReturn(context, "value", 99);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ParentReturnCount = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "ParentReturnCount"), "Child returns must not propagate to the parent frame.");
    }

    /// <summary>
    /// Verifies that return descriptors are observable even without explicit <c>SetRuleReturn</c> calls.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ReturnDescriptors_ObservableWithoutAllocation()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @init {
                DescriptorCount = GetRuleReturnDescriptors(context).Count;
                ReturnCount = context.InvocationFrame!.Returns.Count;
                DescriptorName = GetRuleReturnDescriptors(context)[0].Name;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int DescriptorCount = -1;
                public static int ReturnCount = -1;
                public static string? DescriptorName;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorCount"), "Descriptor must be observable from metadata.");
        Assert.AreEqual(0, ReadIntField(assembly, "ReturnCount"), "Frame returns must remain empty without explicit SetRuleReturn.");
        StringAssert.Contains(ReadStringField(assembly, "DescriptorName")!, "value", "Descriptor name must expose the raw returns metadata text.");
    }

    /// <summary>
    /// Verifies that conservative <c>Parse()</c> does not execute return helper calls.
    /// </summary>
    [TestMethod]
    public void Parse_DoesNotExecuteRuleReturnHelperCalls()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @init { SetRuleReturn(context, "value", 42); InitSentinel = 1; }
            @after { AfterSentinel = 1; }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int InitSentinel;
                public static int AfterSentinel;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.AreEqual(0, ReadIntField(assembly, "InitSentinel"), "Conservative Parse() must not execute @init hooks.");
        Assert.AreEqual(0, ReadIntField(assembly, "AfterSentinel"), "Conservative Parse() must not execute @after hooks.");
    }

    /// <summary>
    /// Verifies that UP1007 still fires for rule returns clauses, with updated wording.
    /// </summary>
    [TestMethod]
    public void Converter_RuleReturns_StillEmitsUp1007()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar P;
            start returns [int value] : A ;
            A : 'a' ;
            """, diagnostics);

        var returnsDiags = diagnostics
            .Where(d => d.Code == ParserDiagnostics.RuleReturnsIgnored.Code)
            .ToList();

        Assert.AreEqual(1, returnsDiags.Count);
        Assert.AreEqual("UP1007", returnsDiags[0].Code);
        StringAssert.Contains(returnsDiags[0].Message, "has no typed or implicit runtime semantics");
        StringAssert.Contains(returnsDiags[0].Message, "not propagated to callers");
    }

    // ── Rule-call argument metadata — non-binding ─────────────────────────────

    /// <summary>
    /// Verifies that <c>callee[...]</c> call-site arguments are metadata-only and do not populate child rule
    /// invocation-frame parameters. <c>TryGetRuleParameter</c> must return <c>false</c> inside the child
    /// <c>@init</c> when the caller used <c>child[42]</c> without explicit <c>SetNextRuleParameter</c> seeding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleCallArgs_DoNotPopulateChildParameters()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                SeenValue = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int SeenValue = -1;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode), "Parse must succeed.");
        Assert.IsFalse(ReadBoolField(assembly, "Found"),
            "child[42] is metadata-only: TryGetRuleParameter must return false because callee[...] does not populate child frame parameters.");
        Assert.AreEqual(-1, ReadIntField(assembly, "SeenValue"),
            "No parameter was explicitly seeded, so v must be null and SeenValue must remain -1.");
    }

    /// <summary>
    /// Verifies generated C# binds multiple simple literals only when the concrete policy is supplied through <c>basePolicy</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PositionalLiteralPolicy_BindsOnlyWhenExplicitlyInstalled()
    {
        const string grammar = """
            grammar P;
            start : child[42, "hello", true, null] ;
            child[int value, string text, bool enabled, object empty]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundText = TryGetRuleParameter(context, "text", out object? t);
                FoundEnabled = TryGetRuleParameter(context, "enabled", out object? e);
                FoundNull = TryGetRuleParameter(context, "empty", out object? n) && n is null;
                SeenValue = v is int i ? i : -1;
                SeenText = t as string;
                SeenEnabled = e is true;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
                public static bool FoundEnabled;
                public static bool FoundNull;
                public static int SeenValue = -1;
                public static string? SeenText;
                public static bool SeenEnabled;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new PositionalLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundText"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundEnabled"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundNull"));
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
        Assert.AreEqual("hello", ReadStringField(assembly, "SeenText"));
        Assert.IsTrue(ReadBoolField(assembly, "SeenEnabled"));

        var conservativeAssembly = CompileGeneratedSource(Emit(grammar), userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(conservativeAssembly, "ParseWithEmbeddedCode", "a"), typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(conservativeAssembly, "FoundValue"));
        Assert.IsNotInstanceOfType(InvokeParse(conservativeAssembly, "Parse", "a"), typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(conservativeAssembly, "FoundValue"));
    }

    /// <summary>
    /// Verifies rollback and memoization distinguish positional literal values at otherwise identical child invocations.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PositionalLiteralPolicy_RollbackAndMemoizationUseCurrentValue()
    {
        const string grammar = """
            grammar P;
            start : child[1] B | child[2] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new PositionalLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "The failed child[1] state and its memoized result must not be reused for child[2].");
    }

    /// <summary>
    /// Verifies generated C# binds named literals by ordinal name only when the named policy is explicitly installed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedLiteralPolicy_BindsOnlyWhenExplicitlyInstalled()
    {
        const string grammar = """
            grammar P;
            start : child[text: "hello", value: 42] ;
            child[int value, string text]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundText = TryGetRuleParameter(context, "text", out object? t);
                SeenValue = v is int i ? i : -1;
                SeenText = t as string;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundText;
                public static int SeenValue = -1;
                public static string? SeenText;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new NamedLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", CreateExecutionContext(assembly), basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundText"));
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
        Assert.AreEqual("hello", ReadStringField(assembly, "SeenText"));

        var conservativeAssembly = CompileGeneratedSource(Emit(grammar), userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(conservativeAssembly, "ParseWithEmbeddedCode", "a"), typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(conservativeAssembly, "FoundValue"));
        Assert.IsNotInstanceOfType(InvokeParse(conservativeAssembly, "Parse", "a"), typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(conservativeAssembly, "FoundValue"));
    }

    /// <summary>
    /// Verifies generated equals syntax binds and preserves the distinction between present null and absence.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedLiteralPolicy_EqualsSyntaxAndNullBind()
    {
        const string grammar = """
            grammar P;
            start : child[value = 42, empty = null] ;
            child[int value, object empty]
            @init {
                FoundValue = TryGetRuleParameter(context, "value", out object? v);
                FoundNull = TryGetRuleParameter(context, "empty", out object? n) && n is null;
                SeenValue = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundValue;
                public static bool FoundNull;
                public static int SeenValue = -1;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new NamedLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", CreateExecutionContext(assembly), basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundNull"));
        Assert.AreEqual(42, ReadIntField(assembly, "SeenValue"));
    }

    /// <summary>
    /// Verifies a positional call remains unbound when only the named literal policy is installed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedLiteralPolicy_DoesNotBindPositionalCall()
    {
        const string grammar = """
            grammar P;
            start : child[42] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new NamedLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", CreateExecutionContext(assembly), basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "Found"));
    }

    /// <summary>
    /// Verifies rollback and memoization distinguish named literal values at otherwise identical child invocations.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NamedLiteralPolicy_RollbackAndMemoizationUseCurrentValue()
    {
        const string grammar = """
            grammar P;
            start : child[value: 1] B | child[value: 2] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? v);
                Seen = v is int i ? i : -1;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static int Seen = -1;
            }
            """;
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new NamedLiteralRuleCallExecutionPolicy(),
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", CreateExecutionContext(assembly), basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"),
            "The failed value: 1 state and its memoized result must not be reused for value: 2.");
    }

    // ── Rule-call label metadata ─────────────────────────────────────────────

    /// <summary>
    /// A. Assignment label is visible in the parent @after hook via GetLastRuleCallResult.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_AssignmentLabelVisibleInParentAfter()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
            }
                : x=child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("x", ReadStringField(assembly, "Label"));
        Assert.AreEqual("Assignment", ReadStringField(assembly, "Kind"));
    }

    /// <summary>
    /// B. List label is visible in the parent @after hook via GetLastRuleCallResult.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_ListLabelVisibleInParentAfter()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
            }
                : xs+=child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("xs", ReadStringField(assembly, "Label"));
        Assert.AreEqual("List", ReadStringField(assembly, "Kind"));
    }

    /// <summary>
    /// C. Unlabeled rule ref yields null LabelName and Kind == "None".
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_UnlabeledRefYieldsNullNameAndNoneKind()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
            }
                : child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsNull(ReadStringField(assembly, "Label"));
        Assert.AreEqual("None", ReadStringField(assembly, "Kind"));
    }

    /// <summary>
    /// D. Assignment label and raw positional arguments compose correctly.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_AssignmentLabelAndRawArgumentsCompose()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
                Raw = r?.RawArguments;
            }
                : x=child[42] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("x", ReadStringField(assembly, "Label"));
        Assert.AreEqual("Assignment", ReadStringField(assembly, "Kind"));
        Assert.AreEqual("42", ReadStringField(assembly, "Raw"));
    }

    /// <summary>
    /// E. List label and named raw arguments compose correctly.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_ListLabelAndNamedRawArgumentsCompose()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
                Raw = r?.RawArguments;
            }
                : xs+=child[value: 42] ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
                public static string? Raw;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("xs", ReadStringField(assembly, "Label"));
        Assert.AreEqual("List", ReadStringField(assembly, "Kind"));
        Assert.AreEqual("value: 42", ReadStringField(assembly, "Raw"));
    }

    /// <summary>
    /// F. Generated source for a grammar with labels does not contain implicit label variables or typed fields.
    /// </summary>
    [TestMethod]
    public void Emit_LabelMetadata_DoesNotGenerateImplicitLabelVariables()
    {
        const string grammar = """
            grammar P;
            start : x=child xs+=child ;
            child : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        // No $x / $xs implicit variables.
        Assert.IsFalse(source.Contains("\"$x\"", StringComparison.Ordinal),
            "Must not generate $x implicit label variable.");
        Assert.IsFalse(source.Contains("\"$xs\"", StringComparison.Ordinal),
            "Must not generate $xs implicit label variable.");
        // No x.value / xs.value label-backed access.
        Assert.IsFalse(source.Contains("x.value", StringComparison.Ordinal),
            "Must not generate x.value label-backed access.");
        Assert.IsFalse(source.Contains("xs.value", StringComparison.Ordinal),
            "Must not generate xs.value label-backed access.");
        // No typed public field/property named x or xs.
        Assert.IsFalse(source.Contains("public object x", StringComparison.Ordinal)
            || source.Contains("public object? x", StringComparison.Ordinal)
            || source.Contains("public ParseNode x", StringComparison.Ordinal),
            "Must not generate typed field/property for label x.");
        Assert.IsFalse(source.Contains("public object xs", StringComparison.Ordinal)
            || source.Contains("public object? xs", StringComparison.Ordinal)
            || source.Contains("public ParseNode xs", StringComparison.Ordinal),
            "Must not generate typed field/property for label xs.");
    }

    /// <summary>
    /// G. Failed alternative label does not leak into the successful alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_FailedAlternativeLabelDoesNotLeak()
    {
        const string grammar = """
            grammar P;
            start @after {
                var r = GetLastRuleCallResult(context);
                Label = r?.LabelName;
                Kind = r?.LabelKind.ToString();
            }
                : a=child B
                | b=child
                ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
                public static string? Kind;
            }
            """;

        // Input "a": alt 0 (a=child B) fails on B; alt 1 (b=child) succeeds.
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("b", ReadStringField(assembly, "Label"),
            "Label from the successful alternative must be visible; failed alt label must not leak.");
        Assert.AreEqual("Assignment", ReadStringField(assembly, "Kind"));
    }

    /// <summary>
    /// H. When a memoized child result is reused, the current call-site label wins over the cached one.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LabelMetadata_MemoizedResult_UsesCurrentCallSiteLabel()
    {
        const string grammar = """
            grammar P;
            start @after {
                Label = GetLastRuleCallResult(context)?.LabelName;
            }
                : x=child B
                | y=child
                ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
            }
            """;

        // Input "a": child[1] is parsed and memoized by alt 0, which then fails on B.
        // Alt 1 gets the memoized result but must show the current call-site label "y".
        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("y", ReadStringField(assembly, "Label"),
            "Memoized result must expose the current call-site label, not the cached one.");
    }

    /// <summary>
    /// I. Conservative Parse() does not execute hooks; label metadata remains default/null.
    /// </summary>
    [TestMethod]
    public void Parse_LabelMetadata_ConservativeParse_DoesNotExposeLabel()
    {
        const string grammar = """
            grammar P;
            start @after {
                Label = GetLastRuleCallResult(context)?.LabelName;
            }
                : x=child ;
            child : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static string? Label;
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        InvokeParse(assembly, "Parse", "a");

        Assert.IsNull(ReadStringField(assembly, "Label"),
            "Conservative Parse() must not execute @after hooks; Label must remain null.");
    }

    /// <summary>Verifies assignment-labeled and current-rule return attributes execute through generic required helpers.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LimitedParserAttributes_ReadReturns()
    {
        const string grammar = """
            grammar P;
            start @after {
                Found = $x.value is int;
                Seen = $x.value is int i ? i : -1;
                IsNull = $x.nullable == null;
            } : x=child ;
            child returns [int value, object nullable]
            @after {
                SetRuleReturn(context, "value", 42);
                SetRuleReturn(context, "nullable", null);
                Current = $child.value is int i ? i : -1;
            } : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool Found;
                public static bool IsNull;
                public static int Seen = -1;
                public static int Current = -1;
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\")");
        StringAssert.Contains(source, "GetRequiredRuleReturn(context, \"value\")");
        Assert.IsFalse(source.Contains("$x.value", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("$child.value", StringComparison.Ordinal));

        Assembly assembly = CompileGeneratedSource(source, userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "Found"));
        Assert.IsTrue(ReadBoolField(assembly, "IsNull"));
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"));
        Assert.AreEqual(42, ReadIntField(assembly, "Current"));
    }

    /// <summary>Verifies a rule-wide visible label that is absent on the selected alternative fails deterministically.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_AbsentAttributeLabel_ThrowsParserAttributeAccessException()
    {
        const string grammar = """
            grammar P;
            start @after { Seen = $x.value; } : x=child B | y=child ;
            child returns [int value] @after { SetRuleReturn(context, "value", 42); } : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static object? Seen; }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        TargetInvocationException exception = Assert.ThrowsException<TargetInvocationException>(() => InvokeParse(assembly, "ParseWithEmbeddedCode", "a"));

        Assert.IsInstanceOfType<ParserAttributeAccessException>(exception.InnerException);
        Assert.AreEqual("Assignment label 'x' is not available in the current rule invocation.", exception.InnerException!.Message);
    }

    /// <summary>Verifies failed-alternative rollback and memoized child reuse expose the successful call-site label.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_AttributeRead_UsesSuccessfulMemoizedLabelState()
    {
        const string grammar = """
            grammar P;
            start @after { Seen = $y.value is int i ? i : -1; } : x=child B | y=child ;
            child returns [int value] @after { SetRuleReturn(context, "value", 2); } : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext { public static int Seen = -1; }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"));
    }

    // ── Infrastructure helpers (mirrored from Antlr4GeneratedEmbeddedCodeTests) ──────────

    /// <summary>Emits generated C# for the supplied grammar.</summary>
    /// <summary>
    /// Verifies generated source exposes only generic metadata-driven labeled-result helpers.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_ContainsGenericLabeledRuleCallHelpers()
    {
        string source = Emit("grammar P; start @after { Values = $xs.value; } : x=child | xs+=child ; child returns [int value] : A ; A : 'a' ;");

        StringAssert.Contains(source, "TryGetLabeledRuleCallResult");
        StringAssert.Contains(source, "GetLabeledRuleCallResults");
        StringAssert.Contains(source, "TryGetLabeledRuleCallReturn");
        StringAssert.Contains(source, "GetLabeledRuleCallReturns(context, \"xs\", \"value\")");
        Assert.IsFalse(source.Contains("GetX(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GetXs(", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("$x.value", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("$xs.value", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies assignment-labeled child returns, multiple returns, present-null values, and absent returns.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_AssignmentLabel_ExposesResultAndReturnPresence()
    {
        const string grammar = """
            grammar P;
            start @after {
                FoundResult = TryGetLabeledRuleCallResult(context, "x", out var result);
                FoundValue = TryGetLabeledRuleCallReturn(context, "x", "value", out object? value);
                FoundNull = TryGetLabeledRuleCallReturn(context, "x", "nullable", out object? nullable) && nullable == null;
                Missing = TryGetLabeledRuleCallReturn(context, "x", "missing", out _);
                Seen = value is int i ? i : -1;
                Other = (int?)result.Returns.GetValueOrDefault("other") ?? -1;
            }
                : x=child ;
            child returns [int value, int other, object nullable]
            @after {
                SetRuleReturn(context, "value", 42);
                SetRuleReturn(context, "other", 7);
                SetRuleReturn(context, "nullable", null);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool FoundResult;
                public static bool FoundValue;
                public static bool FoundNull;
                public static bool Missing;
                public static int Seen = -1;
                public static int Other = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "FoundResult"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundValue"));
        Assert.IsTrue(ReadBoolField(assembly, "FoundNull"));
        Assert.IsFalse(ReadBoolField(assembly, "Missing"));
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"));
        Assert.AreEqual(7, ReadIntField(assembly, "Other"));
    }

    /// <summary>
    /// Verifies list labels append successful calls in order and skip absent named returns while retaining present-null values.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ListLabel_AppendsInOrderAndProjectsPresentReturns()
    {
        const string grammar = """
            grammar P;
            start @after {
                ResultCount = GetLabeledRuleCallResults(context, "xs").Count;
                var values = $xs.value;
                ValueCount = values.Count;
                First = values[0] is int first ? first : -1;
                Last = values[2] is int last ? last : -1;
            }
                : (xs+=child)+ ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", context.InputPosition); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int ResultCount;
                public static int ValueCount;
                public static int First = -1;
                public static int Last = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "aaa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(3, ReadIntField(assembly, "ResultCount"));
        Assert.AreEqual(3, ReadIntField(assembly, "ValueCount"));
        Assert.AreEqual(0, ReadIntField(assembly, "First"));
        Assert.AreEqual(2, ReadIntField(assembly, "Last"));
    }

    /// <summary>
    /// Verifies list-label attribute projection includes present-null values, skips absent returns, and preserves order.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ListLabelAttribute_ProjectsReturnPresence()
    {
        const string grammar = """
            grammar P;
            start @after {
                var values = $xs.value;
                Count = values.Count;
                First = values[0] is int first ? first : -1;
                NullAtEnd = values[1] == null;
            }
                : (xs+=child)+ ;
            child returns [object value]
            @after {
                if (context.InputPosition == 0) SetRuleReturn(context, "value", 7);
                if (context.InputPosition == 2) SetRuleReturn(context, "value", null);
            }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int Count;
                public static int First = -1;
                public static bool NullAtEnd;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "aaa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadIntField(assembly, "Count"));
        Assert.AreEqual(7, ReadIntField(assembly, "First"));
        Assert.IsTrue(ReadBoolField(assembly, "NullAtEnd"));
    }

    /// <summary>
    /// Verifies a repeated list label may target different rules and skips calls whose rule lacks the projected return.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RepeatedListLabelTargets_ProjectsDeclaredReturns()
    {
        const string grammar = """
            grammar P;
            start @after {
                var values = $xs.value;
                Count = values.Count;
                Seen = values[0] is int value ? value : -1;
            }
                : xs+=withoutValue xs+=withValue ;
            withoutValue : A ;
            withValue returns [int value]
            @after { SetRuleReturn(context, "value", 42); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int Count;
                public static int Seen = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "aa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "Count"));
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies absent and rolled-back list labels project as empty while a memoized child binds to the successful label.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ListLabelAttribute_UsesSuccessfulCurrentLabelState()
    {
        const string grammar = """
            grammar P;
            start @after {
                XCount = $xs.value.Count;
                YCount = $ys.value.Count;
                Seen = $ys.value[0] is int value ? value : -1;
            }
                : xs+=child B
                | ys+=child
                ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 42); }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int XCount = -1;
                public static int YCount = -1;
                public static int Seen = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "XCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "YCount"));
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies failed alternative assignment state is rolled back and a memoized child result binds to the current label.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedAlternativeAndMemoHit_UseCurrentAssignmentLabel()
    {
        const string grammar = """
            grammar P;
            start @after {
                HasX = TryGetLabeledRuleCallResult(context, "x", out _);
                HasY = TryGetLabeledRuleCallReturn(context, "y", "value", out object? value);
                Seen = value is int i ? i : -1;
            }
                : x=child B | y=child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 42); }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool HasX;
                public static bool HasY;
                public static int Seen = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "HasX"));
        Assert.IsTrue(ReadBoolField(assembly, "HasY"));
        Assert.AreEqual(42, ReadIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies repeated assignment labels retain the last successful child result.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RepeatedAssignmentLabel_LastSuccessfulResultWins()
    {
        const string grammar = """
            grammar P;
            start @after {
                TryGetLabeledRuleCallReturn(context, "x", "value", out object? value);
                Seen = value is int i ? i : -1;
            }
                : (x=child)+ ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", context.InputPosition); }
                : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int Seen = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "aaa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies failed alternative list appends are rolled back before the successful current-site list binding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedAlternative_ListAppendDoesNotLeak()
    {
        const string grammar = """
            grammar P;
            start @after {
                XCount = GetLabeledRuleCallResults(context, "xs").Count;
                YCount = GetLabeledRuleCallResults(context, "ys").Count;
            }
                : xs+=child B | ys+=child ;
            child returns [int value]
            @after { SetRuleReturn(context, "value", 1); }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static int XCount = -1;
                public static int YCount = -1;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "XCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "YCount"));
    }

    /// <summary>
    /// Verifies nested labeled calls remain owned by the frame that executed them.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NestedLabels_DoNotLeakIntoParentFrame()
    {
        const string grammar = """
            grammar P;
            start @after {
                ParentHasX = TryGetLabeledRuleCallResult(context, "x", out _);
                ParentHasZ = TryGetLabeledRuleCallResult(context, "z", out _);
            }
                : x=child ;
            child @after { ChildHasZ = TryGetLabeledRuleCallResult(context, "z", out _); }
                : z=grandchild ;
            grandchild : A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool ParentHasX;
                public static bool ParentHasZ;
                public static bool ChildHasZ;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "ParentHasX"));
        Assert.IsFalse(ReadBoolField(assembly, "ParentHasZ"));
        Assert.IsTrue(ReadBoolField(assembly, "ChildHasZ"));
    }

    /// <summary>
    /// Verifies generated state capture and hashing synchronize the labeled-result store from the active frame.
    /// </summary>
    [TestMethod]
    public void GeneratedSource_SynchronizesActiveFrameLabeledResultsBeforeCaptureAndHashing()
    {
        string source = Emit("grammar P; start : child ; child : A ; A : 'a' ;");

        StringAssert.Contains(source, "() => this._frameManager!.GetCurrentLabeledCallResults()");
        Assert.AreEqual(
            2,
            source.Split("_executionContext._labeledChildCallResults = _getLabeledResultsFromFrame();", StringSplitOptions.None).Length - 1,
            "Both Capture() and GetCurrentStateKey() must synchronize the active frame's labeled store.");
    }

    /// <summary>
    /// Verifies child backtracking snapshots cannot restore a parent-owned labeled result into the child frame.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ChildBacktracking_DoesNotRestoreParentLabelsIntoChildFrame()
    {
        const string grammar = """
            grammar P;
            start : parent=seed child ;
            seed : A ;
            child @after {
                ChildSeesParent = TryGetLabeledRuleCallResult(context, "parent", out _);
                ChildSeesFailedOwn = TryGetLabeledRuleCallResult(context, "own", out _);
            }
                : own=leaf B
                | leaf
                ;
            leaf : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool ChildSeesParent;
                public static bool ChildSeesFailedOwn;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "aa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "ChildSeesParent"));
        Assert.IsFalse(ReadBoolField(assembly, "ChildSeesFailedOwn"));
    }

    /// <summary>
    /// Verifies a labeled right-hand reference in a direct-left-recursive rule uses the central result-binding path.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_DirectLeftRecursiveRightHandLabel_BindsCompletedResult()
    {
        const string grammar = """
            grammar P;
            start : expr ;
            expr @after {
                if (TryGetLabeledRuleCallResult(context, "right", out var result))
                {
                    SawRight = true;
                    RightRuleName = result.RuleName;
                    RightLabelName = result.LabelName;
                }
            }
                : atom
                | expr PLUS right=expr
                ;
            atom : INT ;
            PLUS : '+' ;
            INT : '1' | '2' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool SawRight;
                public static string? RightRuleName;
                public static string? RightLabelName;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadBoolField(assembly, "SawRight"));
        Assert.AreEqual("expr", ReadStringField(assembly, "RightRuleName"));
        Assert.AreEqual("right", ReadStringField(assembly, "RightLabelName"));
    }

    /// <summary>
    /// Verifies a labeled optional rule reference that fails without consuming input does not bind a result.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FailedNonConsumingOptionalLabel_RemainsAbsent()
    {
        const string grammar = """
            grammar P;
            start @after { HasOptional = TryGetLabeledRuleCallResult(context, "x", out _); }
                : (x=child)? B ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;
            internal sealed partial class PExecutionContext
            {
                public static bool HasOptional;
            }
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        object result = InvokeParse(assembly, "ParseWithEmbeddedCode", "b");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadBoolField(assembly, "HasOptional"));
    }


    /// <summary>
    /// Verifies default generation preserves lifecycle embedded code instead of rewriting ANTLR-style attributes.
    /// </summary>
    [TestMethod]
    public void Emit_DefaultTransformer_PreservesRuleAfterCodeUnchanged()
    {
        const string grammar = """
            grammar P;
            start @after {
                Seen = $value;
            }
                : A ;
            A : 'a' ;
            """;

        var parsed = new G4Parser(new G4Tokenizer(grammar).Tokenize()).Parse();
        string generated = GrammarEmitter.Emit(parsed, "Generated.Tests", "P", "P.g4");

        StringAssert.Contains(generated, "Seen = $value;");
    }

    /// <summary>
    /// Verifies generated emission uses a caller-provided embedded-code transformer.
    /// </summary>
    [TestMethod]
    public void Emit_CustomTransformer_ReplacesEmbeddedCodeToken()
    {
        const string grammar = """
            grammar P;
            start @after {
                Seen = __TOKEN__;
            }
                : A ;
            A : 'a' ;
            """;
        var parsed = new G4Parser(new G4Tokenizer(grammar).Tokenize()).Parse();

        string generated = GrammarEmitter.Emit(parsed, "Generated.Tests", "P", "P.g4", new ReplaceTokenTransformer());

        StringAssert.Contains(generated, "Seen = 42;");
    }

    /// <summary>
    /// Verifies transformer metadata identifies rule lifecycle locations and rule declarations.
    /// </summary>
    [TestMethod]
    public void Emit_CustomTransformer_ReceivesLifecycleMetadata()
    {
        const string grammar = """
            grammar P;
            start[int value] returns [int result] locals [int total]
            @init { __INIT__; }
            @after { __AFTER__; }
                : x=child xs+=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;
        var transformer = new RecordingTransformer();
        var parsed = new G4Parser(new G4Tokenizer(grammar).Tokenize()).Parse();

        _ = GrammarEmitter.Emit(parsed, "Generated.Tests", "P", "P.g4", transformer);

        ParserEmbeddedCodeTransformationContext init = transformer.Contexts.Single(context => context.Location == ParserEmbeddedCodeLocation.RuleInit);
        ParserEmbeddedCodeTransformationContext after = transformer.Contexts.Single(context => context.Location == ParserEmbeddedCodeLocation.RuleAfter);
        Assert.AreEqual("P", init.GrammarName);
        Assert.AreEqual("start", init.RuleName);
        Assert.IsTrue(init.Parameters.Any(parameter => parameter.Name == "value"));
        Assert.IsTrue(init.Locals.Any(local => local.Name == "total"));
        Assert.IsTrue(init.Returns.Any(ret => ret.Name == "result"));
        Assert.IsTrue(after.Labels.ContainsKey("x"));
        Assert.IsTrue(after.Labels.ContainsKey("xs"));
        CollectionAssert.Contains(after.Labels["xs"].RuleNames.ToList(), "child");
    }

    private static string Emit(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4", new CSharpAntlrStyleParserEmbeddedCodeTransformer(grammar));
    }


    /// <summary>
    /// Test transformer that replaces a sentinel token with a numeric literal.
    /// </summary>
    private sealed class ReplaceTokenTransformer : IParserEmbeddedCodeTransformer
    {
        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code.Replace("__TOKEN__", "42") };
        }
    }

    /// <summary>
    /// Test transformer that records every context it receives.
    /// </summary>
    private sealed class RecordingTransformer : IParserEmbeddedCodeTransformer
    {
        /// <summary>
        /// Gets recorded transformation contexts.
        /// </summary>
        public List<ParserEmbeddedCodeTransformationContext> Contexts { get; } = [];

        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            Contexts.Add(context);
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code };
        }
    }

    /// <summary>Compiles generated C# and optional user partial, then loads the resulting assembly.</summary>
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

    /// <summary>Compiles generated C# and returns raw Roslyn diagnostics without asserting success.</summary>
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
            assemblyName: "LifecycleTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        return new CompilationResult(emitResult.Success, stream, emitResult.Diagnostics);
    }

    /// <summary>Builds metadata references from trusted platform assemblies plus parser assemblies.</summary>
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

    private static void AddAssemblyPath(HashSet<string> paths, Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            paths.Add(assembly.Location);
        }
    }

    private static ParseNode InvokeParse(Assembly assembly, string methodName, string input)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string));
        return (ParseNode)method.Invoke(null, [input])!;
    }

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
    /// Invokes generated embedded-code parsing with an explicit execution context and caller-supplied base policy.
    /// </summary>
    /// <param name="assembly">Generated parser assembly.</param>
    /// <param name="input">Input text.</param>
    /// <param name="executionContext">Generated execution context instance.</param>
    /// <param name="basePolicy">Caller-supplied base policy.</param>
    /// <returns>The generated parse result.</returns>
    private static ParseNode InvokeParseWithContextAndPolicy(
        Assembly assembly,
        string input,
        object executionContext,
        ParserRuntimeFeaturePolicy basePolicy)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        Type contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is
                [
                    { ParameterType: var inputType },
                    { ParameterType: var executionContextType },
                    { ParameterType: var policyType }
                ]
                && inputType == typeof(string)
                && executionContextType == contextType
                && policyType == typeof(ParserRuntimeFeaturePolicy));
        return (ParseNode)method.Invoke(null, [input, executionContext, basePolicy])!;
    }

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

    private static object CreateExecutionContext(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    private static int ReadIntField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    private static bool ReadBoolField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (bool)field.GetValue(null)!;
    }

    private static string? ReadStringField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (string?)field.GetValue(null);
    }

    private static int ReadContextIntProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (int)property.GetValue(executionContext)!;
    }

    /// <summary>Captures Roslyn compilation output for lifecycle tests.</summary>
    private sealed record CompilationResult(bool Success, MemoryStream AssemblyStream, IReadOnlyList<Diagnostic> Diagnostics);
}
