using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
using Utils.Parser.Generators.Internal;
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
        StringAssert.Contains(source, "private static bool __Predicate_start_0_0_0");
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

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private static void __Action_start_0_0_0");
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

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
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

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
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

        Assert.AreEqual(2, ReadActionCount(assembly));
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
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        return (ParseNode)method.Invoke(null, new object[] { input })!;
    }

    /// <summary>
    /// Reads the test action counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current action count.</returns>
    private static int ReadActionCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var field = type.GetField("ActionCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Captures Roslyn compilation output for generated embedded-code tests.
    /// </summary>
    /// <param name="Success">Whether compilation succeeded.</param>
    /// <param name="AssemblyStream">Emitted assembly stream.</param>
    /// <param name="Diagnostics">Roslyn diagnostics reported during compilation.</param>
    private sealed record CompilationResult(bool Success, MemoryStream AssemblyStream, IReadOnlyList<Diagnostic> Diagnostics);
}
