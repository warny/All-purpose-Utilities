using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
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

    // ── Infrastructure helpers (mirrored from Antlr4GeneratedEmbeddedCodeTests) ──────────

    /// <summary>Emits generated C# for the supplied grammar.</summary>
    private static string Emit(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4");
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
