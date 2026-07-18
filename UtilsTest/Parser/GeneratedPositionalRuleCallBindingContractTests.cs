using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Locks the generated-C# positional literal rule-call binding contract end to end.
/// </summary>
[TestClass]
public sealed class GeneratedPositionalRuleCallBindingContractTests
{
    /// <summary>
    /// Verifies that the no-context generated-C# overload installs automatic positional binding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InputOnly_BindsGeneratedPositionalLiteralAutomatically()
    {
        const string grammar = """
            grammar P;
            @members { public static int Seen; }
            start : child[42] ;
            child[int value]
            @init { Seen = GetRequiredRuleParameter<int>(context, "value"); }
                : A ;
            A : 'a';
            """;

        Assembly assembly = CompileGeneratedSource(Emit(grammar));

        ParseNode result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadStaticIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies that the explicit-context generated-C# overload installs automatic positional binding.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExplicitContext_BindsGeneratedPositionalLiteralAutomatically()
    {
        Assembly assembly = CompileGeneratedSource(Emit(SingleIntGrammar()));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntField(context, "Seen"));
    }

    /// <summary>
    /// Verifies that conservative Parse parses syntax without executing lifecycle hooks or binding parameters.
    /// </summary>
    [TestMethod]
    public void Parse_RemainsConservativeAndDoesNotExecuteGeneratedBindingOrHooks()
    {
        const string grammar = """
            grammar P;
            @members { public static int HookCount; public static int Seen; }
            start : child[42] ;
            child[int value]
            @init {
                HookCount++;
                Seen = GetRequiredRuleParameter<int>(context, "value");
            }
                : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));

        ParseNode result = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadStaticIntField(assembly, "HookCount"));
        Assert.AreEqual(0, ReadStaticIntField(assembly, "Seen"));
    }

    /// <summary>
    /// Verifies that the overload accepting a base policy preserves the caller's rule-call policy.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_BasePolicy_PreservesCallerPolicyWithoutGeneratedBindingComposition()
    {
        const string grammar = """
            grammar P;
            @members { public int Seen; }
            start : x=child[42] ;
            child[int value]
            @init { Seen = GetRequiredRuleParameter<int>(context, "value"); }
                : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);
        var recorder = new RecordingRuleCallPolicy(seedValue: 7);
        ParserRuntimeFeaturePolicy basePolicy = ParserRuntimeFeaturePolicy.Default with { RuleCallExecutionPolicy = recorder };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", context, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, recorder.BeforeCalls.Count);
        Assert.AreEqual(1, recorder.AfterCalls.Count);
        Assert.AreEqual(7, ReadContextIntField(context, "Seen"));
        Assert.AreEqual("42", recorder.BeforeCalls[0].RawArguments);
        Assert.AreEqual("x", recorder.BeforeCalls[0].LabelName);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, recorder.BeforeCalls[0].LabelKind);
        Assert.IsTrue(recorder.AfterCalls[0].Succeeded);
        Assert.IsNotNull(recorder.AfterCalls[0].CompletedCallResult);
    }

    /// <summary>
    /// Verifies multi-parameter binding and present-null semantics.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_MultipleParameters_BindsNamesTypesAndExplicitNull()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Number;
                public string? Text;
                public bool Flag;
                public bool HasNothing;
                public object? Nothing;
            }
            start : child[1, "text", true, null] ;
            child[int number, string text, bool flag, object nothing]
            @init {
                Number = GetRequiredRuleParameter<int>(context, "number");
                Text = GetRequiredRuleParameter<string>(context, "text");
                Flag = GetRequiredRuleParameter<bool>(context, "flag");
                HasNothing = TryGetRuleParameter(context, "nothing", out object? nothingValue);
                Nothing = nothingValue;
            }
                : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntField(context, "Number"));
        Assert.AreEqual("text", ReadContextStringField(context, "Text"));
        Assert.IsTrue(ReadContextBoolField(context, "Flag"));
        Assert.IsTrue(ReadContextBoolField(context, "HasNothing"));
        Assert.IsNull(ReadContextField(context, "Nothing"));
    }

    /// <summary>
    /// Verifies representative successful conversions delegated by the generated wrapper.
    /// </summary>
    [DataTestMethod]
    [DataRow("42", "int", "int", 42)]
    [DataRow("42", "byte", "byte", (byte)42)]
    [DataRow("1", "double", "double", 1.0d)]
    [DataRow("null", "object", "null", null)]
    [DataRow("\"text\"", "string", "string", "text")]
    [DataRow("'x'", "char", "char", 'x')]
    public void ParseWithEmbeddedCode_TypedConversion_UsesGeneratedWrapperContract(string rawArgument, string declaredType, string readKind, object? expected)
    {
        string grammar = $$"""
            grammar P;
            @members { public object? Seen; }
            start : child[{{rawArgument}}] ;
            child[{{declaredType}} value]
            @init { Seen = GetRequiredRuleParameter<{{declaredType}}>(context, "value"); }
                : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(expected, ReadContextField(context, "Seen"), readKind);
    }

    /// <summary>
    /// Verifies representative conversion failures reject before child entry.
    /// </summary>
    [DataTestMethod]
    [DataRow("300", "byte", "value")]
    [DataRow("\"text\"", "int", "value")]
    [DataRow("null", "int", "value")]
    [DataRow("42", "CustomType", "value")]
    public void ParseWithEmbeddedCode_TypedConversionFailures_RejectBeforeChildEntry(string rawArgument, string declaredType, string parameterName)
    {
        string grammar = $$"""
            grammar P;
            @members { public int Entered; }
            start : child[{{rawArgument}}] ;
            child[{{declaredType}} {{parameterName}}]
            @init { Entered++; }
                : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParserRuleCallBindingException exception = AssertBindingException(() => InvokeParseWithContext(assembly, "a", context));

        Assert.AreEqual("child", exception.RuleName);
        Assert.AreEqual(rawArgument, exception.RawArguments);
        Assert.AreEqual(parameterName, exception.ParameterName);
        Assert.AreEqual(declaredType, exception.DeclaredType);
        Assert.AreEqual(0, ReadContextIntField(context, "Entered"));
    }

    /// <summary>
    /// Verifies exact generated arity validation and default omission rejection.
    /// </summary>
    [DataTestMethod]
    [DataRow("child[int value] : A ;", "child[]", "", 1, 0)]
    [DataRow("child[int value] : A ;", "child[1, 2]", "1, 2", 1, 2)]
    [DataRow("child[int value = 5] : A ;", "child[]", "", 1, 0)]
    public void ParseWithEmbeddedCode_ArityMismatch_ThrowsStructuredBindingException(string childDeclaration, string call, string rawArguments, int expected, int actual)
    {
        string grammar = $$"""
            grammar P;
            start : {{call}} ;
            {{childDeclaration}}
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParserRuleCallBindingException exception = AssertBindingException(() => InvokeParseWithContext(assembly, "a", context));

        Assert.AreEqual("child", exception.RuleName);
        Assert.AreEqual(rawArguments, exception.RawArguments);
        StringAssert.Contains(exception.Message, $"requires exactly {expected} positional argument(s), but received {actual}");
    }

    /// <summary>
    /// Verifies zero-parameter callees accept both absent and explicit empty argument clauses.
    /// </summary>
    [DataTestMethod]
    [DataRow("child")]
    [DataRow("child[]")]
    public void ParseWithEmbeddedCode_ZeroParameterRule_AllowsAbsentOrEmptyArgumentClause(string call)
    {
        string grammar = $$"""
            grammar P;
            @members { public int Entered; }
            start : {{call}} ;
            child @init { Entered++; } : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntField(context, "Entered"));
    }

    /// <summary>
    /// Verifies named, mixed, and expression arguments are rejected before child entry.
    /// </summary>
    [DataTestMethod]
    [DataRow("value: 42", "Named rule-call arguments are not supported")]
    [DataRow("value = 42", "Named rule-call arguments are not supported")]
    [DataRow("1, value: 2", "Argument count mismatch")]
    [DataRow("SomeMember", "not a supported simple literal")]
    [DataRow("1 + 2", "not a supported simple literal")]
    [DataRow("Other()", "not a supported simple literal")]
    public void ParseWithEmbeddedCode_NamedMixedAndExpressionArguments_AreRejectedBeforeChildEntry(string rawArguments, string stableMessage)
    {
        string grammar = $$"""
            grammar P;
            @members { public int Entered; }
            start : child[{{rawArguments}}] ;
            child[int value] @init { Entered++; } : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParserRuleCallBindingException exception = AssertBindingException(() => InvokeParseWithContext(assembly, "a", context));

        Assert.AreEqual("child", exception.RuleName);
        Assert.AreEqual(rawArguments, exception.RawArguments);
        StringAssert.Contains(exception.Message, stableMessage);
        Assert.AreEqual(0, ReadContextIntField(context, "Entered"));
    }

    /// <summary>
    /// Verifies an invalid multi-parameter call submits no partial seed batch and never enters the child.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InvalidMultiParameterCall_IsAtomicAndDoesNotEnterChild()
    {
        const string grammar = """
            grammar P;
            @members { public int Entered; }
            start : child[1, "invalid-for-second-type"] ;
            child[int first, int second] @init { Entered++; } : A ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParserRuleCallBindingException exception = AssertBindingException(() => InvokeParseWithContext(assembly, "a", context));

        Assert.AreEqual("second", exception.ParameterName);
        Assert.AreEqual(1, exception.ArgumentIndex);
        Assert.AreEqual(0, ReadContextIntField(context, "Entered"));
    }

    /// <summary>
    /// Verifies generated seed values from a rejected alternative roll back when the winning alternative does not rewrite them.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RollbackBetweenAlternatives_DoesNotLeakLosingSeed()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Last;
                public int Final;
                public static readonly global::System.Collections.Generic.List<int> Physical = [];
            }
            start @after { Final = Last; }
                : child[1] B
                | A
                ;
            child[int value]
            @init {
                int v = GetRequiredRuleParameter<int>(context, "value");
                Last = v;
                Physical.Add(v);
            }
                : A ;
            A : 'a';
            B : 'b';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadContextIntField(context, "Final"));
        CollectionAssert.AreEqual(new[] { 1 }, ReadStaticIntList(assembly, "Physical"));
    }

    /// <summary>
    /// Verifies memoization keys are sensitive to effective generated seed values.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_Memoization_DifferentEffectiveValuesDoNotReuseWrongChildFrame()
    {
        const string grammar = """
            grammar P;
            @members { public int Last; public int Final; }
            start @after { Final = Last; }
                : child[1] B
                | child[2]
                ;
            child[int value]
            @init { Last = GetRequiredRuleParameter<int>(context, "value"); }
                : A ;
            A : 'a';
            B : 'b';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntField(context, "Final"));
    }

    /// <summary>
    /// Verifies memoization with equivalent converted values restores the correct effective value.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_Memoization_EquivalentConvertedValuesRemainCorrect()
    {
        const string grammar = """
            grammar P;
            @members { public double Last; public double Final; public static int PhysicalCount; }
            start @after { Final = Last; }
                : child[1] B
                | child[1.0]
                ;
            child[double value]
            @init {
                PhysicalCount++;
                Last = GetRequiredRuleParameter<double>(context, "value");
            }
                : A ;
            A : 'a';
            B : 'b';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1.0d, ReadContextDoubleField(context, "Final"));
        Assert.AreEqual(1, ReadStaticIntField(assembly, "PhysicalCount"));
    }

    /// <summary>
    /// Verifies a right-recursive generated rule call receives its own positional argument.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_RightRecursiveCall_ReceivesRecursiveInvocationParameter()
    {
        const string grammar = """
            grammar P;
            @members { public int Last; }
            start : child[1] ;
            child[int value]
            @init { Last = GetRequiredRuleParameter<int>(context, "value"); }
                : A child[2]?
                ;
            A : 'a';
            """;
        Assembly assembly = CompileGeneratedSource(Emit(grammar));
        object context = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "aa", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntField(context, "Last"));
    }

    /// <summary>
    /// Verifies generated binding runs before fallback and binding errors prevent fallback before-calls.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FallbackOrder_BindsBeforeBeforeCallAndSkipsFallbackOnBindingError()
    {
        Assembly validAssembly = CompileGeneratedSource(Emit(SingleIntGrammar()));
        object validContext = CreateExecutionContext(validAssembly);
        var validPolicy = new RecordingRuleCallPolicy(seedValue: null);
        ParserRuntimeFeaturePolicy validBase = ParserRuntimeFeaturePolicy.Default with { RuleCallExecutionPolicy = validPolicy };

        ParseNode result = InvokeCreateRuntimePolicyParse(validAssembly, "a", validContext, validBase);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, validPolicy.BeforeCalls.Count);
        Assert.AreEqual(42, ReadContextIntField(validContext, "Seen"));
        Assert.AreEqual(1, validPolicy.AfterCalls.Count);

        const string invalidGrammar = """
            grammar P;
            start : child["bad"] ;
            child[int value] : A ;
            A : 'a';
            """;
        Assembly invalidAssembly = CompileGeneratedSource(Emit(invalidGrammar));
        object invalidContext = CreateExecutionContext(invalidAssembly);
        var invalidPolicy = new RecordingRuleCallPolicy(seedValue: null);
        ParserRuntimeFeaturePolicy invalidBase = ParserRuntimeFeaturePolicy.Default with { RuleCallExecutionPolicy = invalidPolicy };

        _ = AssertBindingException(() => InvokeCreateRuntimePolicyParse(invalidAssembly, "a", invalidContext, invalidBase));

        Assert.AreEqual(0, invalidPolicy.BeforeCalls.Count);
        Assert.AreEqual(0, invalidPolicy.AfterCalls.Count);
    }

    /// <summary>
    /// Creates the single integer parameter grammar used by multiple contract tests.
    /// </summary>
    /// <returns>Grammar text.</returns>
    private static string SingleIntGrammar() => """
        grammar P;
        @members { public int Seen; }
        start : child[42] ;
        child[int value]
        @init { Seen = GetRequiredRuleParameter<int>(context, "value"); }
            : A ;
        A : 'a';
        """;

    /// <summary>
    /// Emits generated C# with generated positional argument binding enabled.
    /// </summary>
    /// <param name="grammarText">ANTLR grammar text.</param>
    /// <returns>Generated C# source.</returns>
    private static string Emit(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(
            grammar,
            "Generated.Tests",
            "P",
            "P.g4",
            new CSharpAntlrStyleParserEmbeddedCodeTransformer(grammar),
            enableGeneratedRuleArgumentBinding: true);
    }

    /// <summary>
    /// Compiles generated C# and loads the resulting assembly.
    /// </summary>
    /// <param name="generatedSource">Generated C# source.</param>
    /// <returns>Loaded generated assembly.</returns>
    private static Assembly CompileGeneratedSource(string generatedSource)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(generatedSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), path: "P.g.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratedBindingTests_" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stream = new MemoryStream();
        EmitResult emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            Assert.Fail(string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    /// <summary>
    /// Builds Roslyn metadata references for generated parser compilation.
    /// </summary>
    /// <returns>Metadata references.</returns>
    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (string path in tpa.Split(Path.PathSeparator))
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
    /// Adds an assembly path to a metadata-reference path set when available.
    /// </summary>
    /// <param name="paths">Reference path set.</param>
    /// <param name="assembly">Assembly to add.</param>
    private static void AddAssemblyPath(HashSet<string> paths, Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            paths.Add(assembly.Location);
        }
    }

    /// <summary>
    /// Invokes a generated static parse method with only input text.
    /// </summary>
    /// <param name="assembly">Generated assembly.</param>
    /// <param name="methodName">Static parse method name.</param>
    /// <param name="input">Input text.</param>
    /// <returns>Parse node.</returns>
    private static ParseNode InvokeParse(Assembly assembly, string methodName, string input)
    {
        Type type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string));
        return InvokeParseMethod(method, [input]);
    }

    /// <summary>
    /// Invokes generated embedded-code parsing with an explicit context.
    /// </summary>
    /// <param name="assembly">Generated assembly.</param>
    /// <param name="input">Input text.</param>
    /// <param name="executionContext">Generated execution context.</param>
    /// <returns>Parse node.</returns>
    private static ParseNode InvokeParseWithContext(Assembly assembly, string input, object executionContext)
    {
        Type type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        Type contextType = executionContext.GetType();
        MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is [{ ParameterType: var inputType }, { ParameterType: var executionContextType }]
                && inputType == typeof(string)
                && executionContextType == contextType);
        return InvokeParseMethod(method, [input, executionContext]);
    }

    /// <summary>
    /// Invokes generated embedded-code parsing with explicit context and base policy.
    /// </summary>
    /// <param name="assembly">Generated assembly.</param>
    /// <param name="input">Input text.</param>
    /// <param name="executionContext">Generated execution context.</param>
    /// <param name="basePolicy">Caller base policy.</param>
    /// <returns>Parse node.</returns>
    private static ParseNode InvokeParseWithContextAndPolicy(Assembly assembly, string input, object executionContext, ParserRuntimeFeaturePolicy basePolicy)
    {
        Type type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        Type contextType = executionContext.GetType();
        MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
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
        return InvokeParseMethod(method, [input, executionContext, basePolicy]);
    }

    /// <summary>
    /// Invokes parsing through a generated policy created from an explicit base policy.
    /// </summary>
    /// <param name="assembly">Generated assembly.</param>
    /// <param name="input">Input text.</param>
    /// <param name="executionContext">Generated execution context.</param>
    /// <param name="basePolicy">Base policy wrapped by generated binding.</param>
    /// <returns>Parse node.</returns>
    private static ParseNode InvokeCreateRuntimePolicyParse(Assembly assembly, string input, object executionContext, ParserRuntimeFeaturePolicy basePolicy)
    {
        Type type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        Type contextType = executionContext.GetType();
        MethodInfo createPolicy = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "CreateRuntimePolicy"
                && method.GetParameters() is [{ ParameterType: var executionContextType }, { ParameterType: var policyType }]
                && executionContextType == contextType
                && policyType == typeof(ParserRuntimeFeaturePolicy));
        var policy = (ParserRuntimeFeaturePolicy)createPolicy.Invoke(null, [executionContext, basePolicy])!;
        MethodInfo build = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        var definition = (ParserDefinition)build.Invoke(null, [])!;
        var grammar = new CompiledGrammar(definition, policy);
        return grammar.Parse(input);
    }

    /// <summary>
    /// Invokes a reflected parse method and unwraps invocation exceptions.
    /// </summary>
    /// <param name="method">Method to invoke.</param>
    /// <param name="arguments">Arguments passed to the method.</param>
    /// <returns>Parse node.</returns>
    private static ParseNode InvokeParseMethod(MethodInfo method, object?[] arguments)
    {
        try
        {
            return (ParseNode)method.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    /// <summary>
    /// Asserts that an operation throws the generated binding exception.
    /// </summary>
    /// <param name="action">Operation under test.</param>
    /// <returns>The thrown binding exception.</returns>
    private static ParserRuleCallBindingException AssertBindingException(Action action)
    {
        return Assert.ThrowsExactly<ParserRuleCallBindingException>(action);
    }

    /// <summary>
    /// Creates a generated execution context instance.
    /// </summary>
    /// <param name="assembly">Generated assembly.</param>
    /// <returns>Generated execution context.</returns>
    private static object CreateExecutionContext(Assembly assembly)
    {
        Type type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Reads an integer field from the generated context type.
    /// </summary>
    private static int ReadStaticIntField(Assembly assembly, string fieldName)
    {
        Type type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads a static integer list field from the generated context type.
    /// </summary>
    private static int[] ReadStaticIntList(Assembly assembly, string fieldName)
    {
        Type type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return ((IEnumerable<int>)field.GetValue(null)!).ToArray();
    }

    /// <summary>
    /// Reads an integer field from a generated execution context.
    /// </summary>
    private static int ReadContextIntField(object context, string fieldName) => (int)ReadContextField(context, fieldName)!;

    /// <summary>
    /// Reads a double field from a generated execution context.
    /// </summary>
    private static double ReadContextDoubleField(object context, string fieldName) => (double)ReadContextField(context, fieldName)!;

    /// <summary>
    /// Reads a boolean field from a generated execution context.
    /// </summary>
    private static bool ReadContextBoolField(object context, string fieldName) => (bool)ReadContextField(context, fieldName)!;

    /// <summary>
    /// Reads a nullable string field from a generated execution context.
    /// </summary>
    private static string? ReadContextStringField(object context, string fieldName) => (string?)ReadContextField(context, fieldName);

    /// <summary>
    /// Reads a public instance field from a generated execution context.
    /// </summary>
    private static object? ReadContextField(object context, string fieldName)
    {
        FieldInfo field = context.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)!;
        return field.GetValue(context);
    }

    /// <summary>
    /// Records parser rule-call policy callbacks and optionally submits a controlled seed value.
    /// </summary>
    private sealed class RecordingRuleCallPolicy : IParserRuleCallExecutionPolicy
    {
        private readonly int? _seedValue;

        /// <summary>
        /// Initializes a recording policy.
        /// </summary>
        /// <param name="seedValue">Optional value to seed for the target parameter named <c>value</c>.</param>
        public RecordingRuleCallPolicy(int? seedValue)
        {
            _seedValue = seedValue;
        }

        /// <summary>
        /// Gets before-call contexts observed by this policy.
        /// </summary>
        public List<ParserRuleCallExecutionContext> BeforeCalls { get; } = [];

        /// <summary>
        /// Gets after-call contexts observed by this policy.
        /// </summary>
        public List<ParserRuleCallExecutionContext> AfterCalls { get; } = [];

        /// <inheritdoc />
        public void BeforeRuleCall(ParserRuleCallExecutionContext context)
        {
            BeforeCalls.Add(context);
            if (_seedValue is int value)
            {
                Assert.IsTrue(context.TrySetParameterSeed("value", value));
            }
        }

        /// <inheritdoc />
        public void AfterRuleCall(ParserRuleCallExecutionContext context)
        {
            AfterCalls.Add(context);
        }
    }
}
