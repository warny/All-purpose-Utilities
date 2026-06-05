using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
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
                MissingBeforeInitSet = TryGetRuleLocal(context, "count", out _);
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
                public static bool MissingBeforeInitSet = true;
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
        Assert.IsFalse(ReadBoolField(assembly, "MissingBeforeInitSet"));
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
    /// Verifies that rule-local descriptor metadata does not pre-populate the frame locals store.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RuleLocalDescriptors_DoNotAutoAllocateFrameLocals()
    {
        const string grammar = """
            grammar P;
            start
            locals [int scratch]
            @init {
                InitLocalCount = context.InvocationFrame!.Locals.Count;
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
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "InitLocalCount"));
        Assert.AreEqual(0, ReadIntField(assembly, "AfterLocalCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "DescriptorLocalCount"));
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
