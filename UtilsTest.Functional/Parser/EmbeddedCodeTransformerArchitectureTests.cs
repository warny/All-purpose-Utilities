using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Parser;

/// <summary>
/// Validates repository-level embedded-code transformer architecture rules.
/// </summary>
[TestClass]
public sealed class EmbeddedCodeTransformerArchitectureTests
{
    private static readonly string CentralPipelineRelativePath = Path.Combine("Utils.Parser.Diagnostics", "EmbeddedCode", "EmbeddedCodeText.cs");

    /// <summary>
    /// Ensures production parser code does not bypass the central embedded-code transformation service.
    /// </summary>
    [TestMethod]
    public void ProductionParserCode_WhenEmbeddedCodeTransformerIsUsed_DoesNotCallTransformOutsideCentralService()
    {
        string repositoryRoot = FindRepositoryRoot();

        string[] violations = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .SelectMany(file => FindForbiddenDirectTransformCallsInFile(repositoryRoot, file))
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    /// <summary>
    /// Ensures the syntax scan catches a direct transform invocation split across multiple lines.
    /// </summary>
    [TestMethod]
    public void DirectTransformScan_WhenInvocationIsSplitAcrossLines_ReportsViolation()
    {
        const string source = """
            namespace Sample;

            internal sealed class Caller
            {
                public void Execute(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.
                        Transform(context);
                }
            }
            """;

        string[] violations = FindForbiddenDirectTransformCalls("Utils.Parser.Generators/Internal/Caller.cs", source)
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        Assert.AreEqual(1, violations.Length);
        StringAssert.StartsWith(violations[0], "Utils.Parser.Generators/Internal/Caller.cs:7: transformer.");
        StringAssert.Contains(violations[0], "\\n");
        StringAssert.Contains(violations[0], "Transform(context)");
    }

    /// <summary>
    /// Ensures the syntax scan does not allow arbitrary transform calls in the central service file.
    /// </summary>
    [TestMethod]
    public void DirectTransformScan_WhenCentralFileContainsUnexpectedTransformInvocation_ReportsViolation()
    {
        const string source = """
            namespace Utils.Parser.Diagnostics.EmbeddedCode;

            internal static class EmbeddedCodeTransformationPipeline
            {
                internal static void TransformAndValidate(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.Transform(context);
                }

                public static void Bypass(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.Transform(context);
                }
            }
            """;

        string[] violations = FindForbiddenDirectTransformCalls(CentralPipelineRelativePath, source)
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        CollectionAssert.AreEqual(new[] { $"{CentralPipelineRelativePath}:12: transformer.Transform(context)" }, violations);
    }

    /// <summary>
    /// Ensures the common boundary stops at validated transformed code and both targets enter it directly.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodeTransformationPipeline_IsInternalTargetIndependentAndShared()
    {
        string repositoryRoot = FindRepositoryRoot();
        string pipelineSource = File.ReadAllText(Path.Combine(repositoryRoot, CentralPipelineRelativePath));
        CompilationUnitSyntax pipelineRoot = CSharpSyntaxTree.ParseText(pipelineSource).GetCompilationUnitRoot();
        ClassDeclarationSyntax pipeline = pipelineRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(static type => type.Identifier.ValueText == "EmbeddedCodeTransformationPipeline");
        MethodDeclarationSyntax entry = pipeline.Members.OfType<MethodDeclarationSyntax>()
            .Single(static method => method.Identifier.ValueText == "TransformAndValidate");

        Assert.IsTrue(pipeline.Modifiers.Any(SyntaxKind.InternalKeyword));
        Assert.AreEqual("TransformedEmbeddedCode", entry.ReturnType.ToString());
        CollectionAssert.AreEqual(
            new[] { "IParserEmbeddedCodeTransformer", "RawEmbeddedCode", "ParserEmbeddedCodeTransformationContext", "ParserEmbeddedCodeTransformationFailureContext" },
            entry.ParameterList.Parameters.Select(static parameter => parameter.Type!.ToString()).ToArray());
        Assert.IsFalse(pipeline.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static identifier =>
            identifier.Identifier.ValueText is "StringBuilder" or "CSharpEmbeddedCodeInjector" or "IExpressionCompiler"));
        Assert.IsFalse(pipeline.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation =>
            invocation.Expression.ToString().Contains("Lambda", StringComparison.Ordinal)));

        IReadOnlyList<SourceScan> scans = CreateProductionParserScans(repositoryRoot);
        Assert.AreEqual(1, CountPipelineCalls(scans, "Utils.Parser.Generators/Internal/GrammarEmitter.ExecutionContext.Hooks.cs"));
        Assert.AreEqual(1, CountPipelineCalls(scans, "Utils.Parser.Expressions/ExpressionEmbeddedCodePreparer.cs"));
    }

    /// <summary>
    /// Ensures semantic pipeline-call counting recognizes type aliases and static imports.
    /// </summary>
    [TestMethod]
    public void PipelineCallScan_WhenAliasOrStaticImportIsUsed_CountsBothCalls()
    {
        IReadOnlyList<SourceScan> scans = CreateSamplePipelineScans(
            """
            using Pipeline = Utils.Parser.Diagnostics.EmbeddedCode.EmbeddedCodeTransformationPipeline;
            using static Utils.Parser.Diagnostics.EmbeddedCode.EmbeddedCodeTransformationPipeline;

            namespace Sample;

            internal static class Caller
            {
                internal static void Execute()
                {
                    Pipeline.TransformAndValidate();
                    TransformAndValidate();
                }
            }
            """);

        Assert.AreEqual(2, CountPipelineCalls(scans, "Caller.cs"));
    }

    /// <summary>
    /// Ensures embedded-code preparers do not introduce a second direct expression-compiler path.
    /// </summary>
    [TestMethod]
    public void ProductionEmbeddedCodePreparers_WhenCompileIsCalled_OnlyExpressionEmbeddedCodePreparerCompiles()
    {
        string repositoryRoot = FindRepositoryRoot();

        string[] violations = FindForbiddenEmbeddedCodePreparerCompileCalls(CreateProductionParserScans(repositoryRoot));

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    /// <summary>
    /// Ensures the embedded-code preparer compiler scan reports a second direct compiler path.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparerCompileScan_WhenCustomPreparerCompiles_ReportsViolation()
    {
        const string source = """
            namespace Sample;

            using Utils.Expressions;
            using Utils.Parser.EmbeddedCode;

            internal sealed class OtherEmbeddedCodePreparer : IEmbeddedCodePreparer<object, object>
            {
                public void Prepare(IExpressionCompiler compiler)
                {
                    compiler.Compile("raw");
                }
            }
            """;

        string[] violations = FindForbiddenEmbeddedCodePreparerCompileCalls(CreateSampleExpressionCompilerScans("Utils.Parser.Expressions/OtherEmbeddedCodePreparer.cs", source));

        Assert.AreEqual(1, violations.Length);
        StringAssert.StartsWith(violations[0], "Utils.Parser.Expressions/OtherEmbeddedCodePreparer.cs:10: compiler.Compile(\"raw\")");
    }

    /// <summary>
    /// Ensures the supported runtime embedded-code facade remains the sole production component that combines the
    /// shared transformation pipeline, expression compilation, specialized lambdas, and prepared parser artifacts.
    /// </summary>
    [TestMethod]
    public void ExpressionEmbeddedCodePreparer_IsOnlySupportedRuntimeEmbeddedCodeCompilationFacade()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyList<SourceScan> scans = CreateProductionParserScans(repositoryRoot);
        Compilation compilation = scans[0].SemanticModel.Compilation;
        INamedTypeSymbol preparerType = compilation.GetTypeByMetadataName("Utils.Parser.Expressions.ExpressionEmbeddedCodePreparer")!;
        INamedTypeSymbol preparerContract = compilation.GetTypeByMetadataName("Utils.Parser.EmbeddedCode.IEmbeddedCodePreparer`2")!;
        INamedTypeSymbol predicateArtifact = compilation.GetTypeByMetadataName("Utils.Parser.Expressions.PreparedExpressionSemanticPredicate")!;
        INamedTypeSymbol actionArtifact = compilation.GetTypeByMetadataName("Utils.Parser.Expressions.PreparedExpressionParserAction")!;
        INamedTypeSymbol compilerContract = compilation.GetTypeByMetadataName("Utils.Expressions.IExpressionCompiler")!;
        INamedTypeSymbol pipelineType = compilation.GetTypeByMetadataName("Utils.Parser.Diagnostics.EmbeddedCode.EmbeddedCodeTransformationPipeline")!;

        Assert.IsTrue(preparerType.IsSealed);
        INamedTypeSymbol implementedContract = preparerType.AllInterfaces.Single(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, preparerContract));
        Assert.IsTrue(SymbolEqualityComparer.Default.Equals(implementedContract.TypeArguments[0], predicateArtifact));
        Assert.IsTrue(SymbolEqualityComparer.Default.Equals(implementedContract.TypeArguments[1], actionArtifact));
        CollectionAssert.AreEquivalent(
            new[] { "PrepareParserAction", "PrepareSemanticPredicate" },
            preparerType.GetMembers().OfType<IMethodSymbol>()
                .Where(static method => method.DeclaredAccessibility == Accessibility.Public && method.MethodKind == MethodKind.Ordinary)
                .Select(static method => method.Name)
                .ToArray());
        Assert.AreEqual(1, preparerType.InstanceConstructors.Count(static constructor => constructor.DeclaredAccessibility == Accessibility.Public));

        SourceScan facadeScan = scans.Single(scan => scan.RelativePath == "Utils.Parser.Expressions/ExpressionEmbeddedCodePreparer.cs");
        ClassDeclarationSyntax facadeDeclaration = facadeScan.Tree.GetCompilationUnitRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(static type => type.Identifier.ValueText == "ExpressionEmbeddedCodePreparer");
        IFieldSymbol compilerField = preparerType.GetMembers("_compiler").OfType<IFieldSymbol>().Single();
        MethodDeclarationSyntax transformSource = facadeDeclaration.Members.OfType<MethodDeclarationSyntax>()
            .Single(static method => method.Identifier.ValueText == "TransformSource");
        Assert.AreEqual(1, transformSource.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation =>
            facadeScan.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, pipelineType)));

        foreach (string methodName in new[] { "PrepareSemanticPredicate", "PrepareParserAction" })
        {
            MethodDeclarationSyntax method = facadeDeclaration.Members.OfType<MethodDeclarationSyntax>()
                .Single(candidate => candidate.Identifier.ValueText == methodName);
            Assert.AreEqual(1, method.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation =>
                facadeScan.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol called
                && SymbolEqualityComparer.Default.Equals(called.ContainingType, preparerType)
                && called.Name == "TransformSource"));
            InvocationExpressionSyntax compilerCall = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "Compile"
                && facadeScan.SemanticModel.GetSymbolInfo(access.Expression).Symbol is IFieldSymbol receiver
                && SymbolEqualityComparer.Default.Equals(receiver, compilerField));
            Assert.IsTrue(compilerCall.Expression is MemberAccessExpressionSyntax memberAccess
                && facadeScan.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol is IFieldSymbol receiver
                && SymbolEqualityComparer.Default.Equals(receiver, compilerField));
        }

        INamedTypeSymbol[] matchingPreparers = scans.SelectMany(scan => scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(type => scan.SemanticModel.GetDeclaredSymbol(type)))
            .OfType<INamedTypeSymbol>()
            .Where(type => type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, implementedContract)))
            .ToArray();
        CollectionAssert.AreEqual(new[] { preparerType.ToDisplayString() }, matchingPreparers.Select(static type => type.ToDisplayString()).ToArray());

        foreach (INamedTypeSymbol artifactType in new[] { predicateArtifact, actionArtifact })
        {
            string[] constructors = scans.SelectMany(scan => scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                    .Where(creation => SymbolEqualityComparer.Default.Equals((facadeScan.SemanticModel.Compilation.GetSemanticModel(scan.Tree).GetSymbolInfo(creation).Symbol as IMethodSymbol)?.ContainingType, artifactType))
                    .Select(creation => GetEnclosingTypeName(creation)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "ExpressionEmbeddedCodePreparer" }, constructors);
        }

        string[] specializedLambdaOwners = scans.SelectMany(scan => scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax expressionType,
                        Name: GenericNameSyntax { Identifier.ValueText: "Lambda", TypeArgumentList.Arguments.Count: 1 } lambda
                    }
                    && scan.SemanticModel.GetSymbolInfo(expressionType).Symbol is INamedTypeSymbol containingType
                    && containingType.ToDisplayString() == "System.Linq.Expressions.Expression"
                    && lambda.TypeArgumentList.Arguments[0].ToString() is
                        "Func<SemanticPredicateEvaluationContext, bool>" or
                        "Action<ParserActionExecutionContext>")
                .Select(GetEnclosingTypeName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "ExpressionEmbeddedCodePreparer" }, specializedLambdaOwners);

        foreach (SourceScan scan in scans.Where(static scan => scan.RelativePath.StartsWith("Utils.Parser.Generators/", StringComparison.Ordinal)))
        {
            Assert.IsFalse(scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier =>
                scan.SemanticModel.GetSymbolInfo(identifier).Symbol is INamedTypeSymbol type
                && (SymbolEqualityComparer.Default.Equals(type, preparerType) || SymbolEqualityComparer.Default.Equals(type, compilerContract))));
        }

        string[] forbiddenFacadeDependencies = facadeDeclaration.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(identifier => facadeScan.SemanticModel.GetSymbolInfo(identifier).Symbol)
            .OfType<INamedTypeSymbol>()
            .Where(static type => type.Name is "StringBuilder" or "GeneratedEmbeddedCodeBody" or "CSharpEmbeddedCodeInjector")
            .Select(static type => type.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), forbiddenFacadeDependencies);

        string[] parallelPipelines = scans.SelectMany(scan => scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Where(type => scan.SemanticModel.GetDeclaredSymbol(type) is INamedTypeSymbol symbol && !SymbolEqualityComparer.Default.Equals(symbol, preparerType))
                .Where(type => type.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Select(identifier => scan.SemanticModel.GetSymbolInfo(identifier).Symbol)
                    .OfType<INamedTypeSymbol>()
                    .Any(type => SymbolEqualityComparer.Default.Equals(type, pipelineType)))
                .Where(type => type.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Select(identifier => scan.SemanticModel.GetSymbolInfo(identifier).Symbol)
                    .OfType<INamedTypeSymbol>()
                    .Any(type => SymbolEqualityComparer.Default.Equals(type, compilerContract)))
                .Where(type => type.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Select(identifier => scan.SemanticModel.GetSymbolInfo(identifier).Symbol)
                    .OfType<INamedTypeSymbol>()
                    .Any(type => SymbolEqualityComparer.Default.Equals(type, predicateArtifact) || SymbolEqualityComparer.Default.Equals(type, actionArtifact)))
                .Select(static type => type.Identifier.ValueText))
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), parallelPipelines);
    }



    /// <summary>
    /// Ensures runtime expression context symbols are built through one shared implementation.
    /// </summary>
    [TestMethod]
    public void ExpressionEmbeddedCodePreparer_RuntimeContextSymbols_UseSingleSharedBuilder()
    {
        string repositoryRoot = FindRepositoryRoot();
        string relativePath = NormalizePath(Path.Combine("Utils.Parser.Expressions", "ExpressionEmbeddedCodePreparer.cs"));
        string source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Expressions.SymbolArchitectureScan",
            [tree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        ClassDeclarationSyntax preparer = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "ExpressionEmbeddedCodePreparer");
        MethodDeclarationSyntax[] methods = preparer.Members.OfType<MethodDeclarationSyntax>().ToArray();
        MethodDeclarationSyntax semanticWrapper = methods.Single(static method => method.Identifier.ValueText == "BuildSemanticPredicateSymbols");
        MethodDeclarationSyntax actionWrapper = methods.Single(static method => method.Identifier.ValueText == "BuildParserActionSymbols");
        MethodDeclarationSyntax sharedBuilder = methods.Single(static method => method.Identifier.ValueText == "BuildRuntimeContextSymbols");

        AssertWrapperDelegatesToSharedBuilder(semanticWrapper, semanticModel);
        AssertWrapperDelegatesToSharedBuilder(actionWrapper, semanticModel);
        Assert.AreEqual(0, semanticWrapper.DescendantNodes().OfType<SwitchStatementSyntax>().Count());
        Assert.AreEqual(0, actionWrapper.DescendantNodes().OfType<SwitchStatementSyntax>().Count());
        Assert.IsFalse(methods.Any(static method => method.Identifier.ValueText == "AddSemanticPredicateSymbol"));
        Assert.IsFalse(methods.Any(static method => method.Identifier.ValueText == "AddParserActionSymbol"));

        MethodDeclarationSyntax[] symbolClassifiers = methods
            .Where(static method => method.DescendantNodes().OfType<SwitchStatementSyntax>().Any(@switch => @switch.Expression.ToString() == "symbol"))
            .ToArray();
        CollectionAssert.AreEqual(new[] { "BuildRuntimeContextSymbols" }, symbolClassifiers.Select(static method => method.Identifier.ValueText).ToArray());
        Assert.IsTrue(sharedBuilder.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment => assignment.Right is InvocationExpressionSyntax invocation && invocation.ToString().Contains("Expression.Property", StringComparison.Ordinal)));

        InvocationExpressionSyntax[] expressionCompilerCalls = preparer.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(static invocation => invocation.ToString().StartsWith("_compiler.Compile", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(2, expressionCompilerCalls.Length, "The preparer must keep exactly one IExpressionCompiler.Compile call per runtime artifact path.");

        Assert.IsTrue(preparer.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("Expression.Lambda<Func<SemanticPredicateEvaluationContext, bool>>", StringComparison.Ordinal)));
        Assert.IsTrue(preparer.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("Expression.Lambda<Action<ParserActionExecutionContext>>", StringComparison.Ordinal)));

        string[] publicContracts = tree.GetCompilationUnitRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(static type => type.Modifiers.Any(SyntaxKind.PublicKeyword) && type.Identifier.ValueText.Contains("RuntimeContext", StringComparison.Ordinal))
            .Select(static type => type.Identifier.ValueText)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), publicContracts);
    }

    /// <summary>
    /// Ensures generated embedded-code hooks use one typed data model with explicit parser/lexer and predicate/action discriminants.
    /// </summary>
    [TestMethod]
    public void GrammarEmitterEmbeddedCodeHooks_UseSingleCommonHookModel()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] emitterFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "Utils.Parser.Generators", "Internal"), "GrammarEmitter*.cs", SearchOption.TopDirectoryOnly);
        SyntaxTree contractsTree = CSharpSyntaxTree.ParseText(
            """
            namespace Utils.Parser.Diagnostics.EmbeddedCode
            {
                public sealed class RawEmbeddedCode
                {
                    public RawEmbeddedCode(string text) { Text = text; }
                    public string Text { get; }
                }

                public sealed class TransformedEmbeddedCode
                {
                    public string Text { get; } = string.Empty;
                }
            }
            """,
            path: "EmbeddedCodeContracts.cs");
        SyntaxTree[] trees = emitterFiles
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: NormalizePath(Path.GetRelativePath(repositoryRoot, file))))
            .Append(contractsTree)
            .ToArray();
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Generators.HookArchitectureScan",
            trees,
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        INamedTypeSymbol? grammarEmitter = compilation.GetTypeByMetadataName("Utils.Parser.Generators.Internal.GrammarEmitter");
        Assert.IsNotNull(grammarEmitter);

        INamedTypeSymbol[] nestedTypes = grammarEmitter!.GetTypeMembers().ToArray();
        Assert.IsNull(nestedTypes.SingleOrDefault(static type => type.Name == "LexerEmbeddedCodeHook"));
        INamedTypeSymbol hookType = nestedTypes.Single(static type => type.Name == "EmbeddedCodeHook");
        INamedTypeSymbol ownerType = nestedTypes.Single(static type => type.Name == "EmbeddedCodeHookOwner");
        INamedTypeSymbol kindType = nestedTypes.Single(static type => type.Name == "EmbeddedCodeHookKind");

        string[] expectedOwners = ["Parser", "Lexer"];
        CollectionAssert.AreEquivalent(expectedOwners, ownerType.GetMembers().OfType<IFieldSymbol>().Where(static field => field.HasConstantValue).Select(static field => field.Name).ToArray());
        string[] expectedKinds = ["SemanticPredicate", "InlineAction"];
        CollectionAssert.AreEquivalent(expectedKinds, kindType.GetMembers().OfType<IFieldSymbol>().Where(static field => field.HasConstantValue).Select(static field => field.Name).ToArray());
        Assert.AreEqual("Utils.Parser.Diagnostics.EmbeddedCode.RawEmbeddedCode", hookType.GetMembers("RawCode").OfType<IPropertySymbol>().Single().Type.ToDisplayString());
        Assert.AreEqual("Utils.Parser.Diagnostics.EmbeddedCode.TransformedEmbeddedCode?", hookType.GetMembers("TransformedCode").OfType<IPropertySymbol>().Single().Type.ToDisplayString());
        Assert.AreEqual(NullableAnnotation.Annotated, hookType.GetMembers("TransformedCode").OfType<IPropertySymbol>().Single().NullableAnnotation);
        string[] forbiddenCodeMembers = ["EmittedCode", "ProcessedCode", "FinalCode", "PreparedCode", "ReadyCode"];
        Assert.IsFalse(hookType.GetMembers().Any(member => forbiddenCodeMembers.Contains(member.Name, StringComparer.Ordinal)));
        Assert.AreEqual(ownerType, hookType.GetMembers("Owner").OfType<IPropertySymbol>().Single().Type, SymbolEqualityComparer.Default);
        Assert.AreEqual(kindType, hookType.GetMembers("Kind").OfType<IPropertySymbol>().Single().Type, SymbolEqualityComparer.Default);

        IMethodSymbol[] hookFactories = hookType.GetMembers().OfType<IMethodSymbol>().Where(static method => method.MethodKind == MethodKind.Ordinary && (method.Name == "CreateParser" || method.Name == "CreateLexer")).ToArray();
        string[] expectedFactories = ["CreateParser", "CreateLexer"];
        CollectionAssert.AreEquivalent(expectedFactories, hookFactories.Select(static method => method.Name).ToArray());

        string[] hookLikeTypes = nestedTypes
            .Where(type => type.Name.Contains("EmbeddedCodeHook", StringComparison.Ordinal) && type.TypeKind == TypeKind.Class)
            .Select(static type => type.Name)
            .ToArray();
        string[] expectedHookLikeTypes = ["EmbeddedCodeHook"];
        CollectionAssert.AreEqual(expectedHookLikeTypes, hookLikeTypes);

        string embeddedHooksPath = NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.EmbeddedHooks.cs"));
        SourceScan embeddedHooksScan = trees.Select(tree => new SourceScan(tree.FilePath, tree, compilation.GetSemanticModel(tree))).Single(scan => string.Equals(scan.RelativePath, embeddedHooksPath, StringComparison.Ordinal));
        string[] factoryCalls = embeddedHooksScan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => embeddedHooksScan.SemanticModel.GetSymbolInfo(invocation).Symbol)
            .OfType<IMethodSymbol>()
            .Where(symbol => SymbolEqualityComparer.Default.Equals(symbol.ContainingType, hookType) && (symbol.Name == "CreateParser" || symbol.Name == "CreateLexer"))
            .Select(static symbol => symbol.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedFactoryCalls = ["CreateLexer", "CreateParser"];
        CollectionAssert.AreEqual(expectedFactoryCalls, factoryCalls);

        IPropertySymbol ownerProperty = hookType.GetMembers("Owner").OfType<IPropertySymbol>().Single();
        int ownerReadCount = trees
            .Where(static tree => tree.FilePath.EndsWith("GrammarEmitter.EmbeddedHooks.cs", StringComparison.Ordinal) || tree.FilePath.EndsWith("GrammarEmitter.ExecutionContext.Policy.cs", StringComparison.Ordinal))
            .SelectMany(tree => tree.GetCompilationUnitRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().Select(memberAccess => compilation.GetSemanticModel(tree).GetSymbolInfo(memberAccess).Symbol))
            .OfType<IPropertySymbol>()
            .Count(symbol => SymbolEqualityComparer.Default.Equals(symbol, ownerProperty));
        Assert.IsTrue(ownerReadCount > 0, "The hook owner discriminant must be read by production validation code.");

        string[] recursiveCollectors = grammarEmitter.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.Name == "CollectEmbeddedCodeHooks" || method.Name == "CollectLexerEmbeddedCodeHooks")
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedRecursiveCollectors = ["CollectEmbeddedCodeHooks", "CollectLexerEmbeddedCodeHooks"];
        CollectionAssert.AreEqual(expectedRecursiveCollectors, recursiveCollectors);
    }



    /// <summary>
    /// Ensures parser and lexer generated hook collection share one recursive collector with explicit strategies.
    /// </summary>
    [TestMethod]
    public void GrammarEmitterEmbeddedCodeHookCollection_UsesSharedCollectorAndStrategies()
    {
        string repositoryRoot = FindRepositoryRoot();
        string relativePath = NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.EmbeddedHooks.cs"));
        string source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Generators.HookCollectorArchitectureScan",
            [tree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        ClassDeclarationSyntax grammarEmitter = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "GrammarEmitter");
        ClassDeclarationSyntax collector = grammarEmitter.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookCollector");
        InterfaceDeclarationSyntax strategy = grammarEmitter.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "IEmbeddedHookCollectionStrategy");
        ClassDeclarationSyntax parserStrategy = grammarEmitter.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "ParserEmbeddedHookCollectionStrategy");
        ClassDeclarationSyntax lexerStrategy = grammarEmitter.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "LexerEmbeddedHookCollectionStrategy");
        StructDeclarationSyntax traversalPosition = grammarEmitter.DescendantNodes().OfType<StructDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "HookTraversalPosition");

        Assert.IsTrue(collector.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(strategy.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(parserStrategy.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(lexerStrategy.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(traversalPosition.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
        CollectionAssert.AreEquivalent(new[] { "AlternativeIndex", "ElementIndex" }, traversalPosition.Members.OfType<PropertyDeclarationSyntax>().Select(static property => property.Identifier.ValueText).Where(static name => name is "AlternativeIndex" or "ElementIndex").ToArray());

        MethodDeclarationSyntax parserWrapper = grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "CollectEmbeddedCodeHooks");
        MethodDeclarationSyntax lexerWrapper = grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "CollectLexerEmbeddedCodeHooks");
        AssertWrapperDelegatesToCollector(parserWrapper, "ParserEmbeddedHookCollectionStrategy", semanticModel);
        AssertWrapperDelegatesToCollector(lexerWrapper, "LexerEmbeddedHookCollectionStrategy", semanticModel);
        Assert.AreEqual(0, parserWrapper.DescendantNodes().OfType<SwitchStatementSyntax>().Count());
        Assert.AreEqual(0, lexerWrapper.DescendantNodes().OfType<SwitchStatementSyntax>().Count());

        MethodDeclarationSyntax[] recursiveVisitors = collector.Members.OfType<MethodDeclarationSyntax>()
            .Where(method => method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("VisitContent(", StringComparison.Ordinal)))
            .ToArray();
        CollectionAssert.AreEqual(new[] { "Collect", "VisitAlternative", "VisitContent", "VisitSequence" }, recursiveVisitors.Select(static method => method.Identifier.ValueText).OrderBy(static name => name, StringComparer.Ordinal).ToArray());
        MethodDeclarationSyntax sharedVisitor = collector.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "VisitContent");
        Assert.AreEqual(1, sharedVisitor.DescendantNodes().OfType<SwitchStatementSyntax>().Count(), "Only the shared collector should switch over G4Content node shapes.");

        string[] removedCollectors = ["CollectRuleEmbeddedCodeHooks", "CollectLeftRecursiveTailEmbeddedCodeHooks", "CollectAlternativeEmbeddedCodeHooks", "CollectSequenceEmbeddedCodeHooks"];
        foreach (string removedCollector in removedCollectors)
        {
            Assert.IsFalse(grammarEmitter.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == removedCollector), $"{removedCollector} must not remain as a duplicated traversal method.");
        }

        Assert.IsFalse(grammarEmitter.DescendantNodes().OfType<ParameterSyntax>().Any(parameter => parameter.Identifier.ValueText == "isLexer" && parameter.Type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.BoolKeyword)));
        Assert.IsFalse(collector.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() is "EmbeddedCodeHookOwner.Parser" or "EmbeddedCodeHookOwner.Lexer"), "The shared collector must not branch on parser/lexer owners.");
        Assert.IsTrue(parserStrategy.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("EmbeddedCodeHook.CreateParser", StringComparison.Ordinal)));
        Assert.IsTrue(lexerStrategy.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("EmbeddedCodeHook.CreateLexer", StringComparison.Ordinal)));
        Assert.IsTrue(parserStrategy.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("StartsWithRuleRef", StringComparison.Ordinal)), "Parser left-recursion preparation must stay in the parser strategy.");
        Assert.IsTrue(lexerStrategy.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "grammar.ExtraModes"), "Lexer mode traversal must stay in the lexer strategy.");
        Assert.AreEqual(1, collector.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation => invocation.ToString().StartsWith("TransformEmbeddedCode", StringComparison.Ordinal)), "Hook transformation must stay centralized in the shared collector.");
    }

    /// <summary>
    /// Ensures parser and lexer runtime hook dispatchers share one emitter while keeping four explicit descriptor-backed wrappers.
    /// </summary>
    [TestMethod]
    public void GrammarEmitterEmbeddedHookDispatchers_UseSharedEmitterAndImmutableDescriptors()
    {
        string repositoryRoot = FindRepositoryRoot();
        string relativePath = NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.ExecutionContext.Policy.cs"));
        string source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Generators.DispatcherArchitectureScan",
            [tree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        ClassDeclarationSyntax grammarEmitter = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "GrammarEmitter");
        ClassDeclarationSyntax emitter = grammarEmitter.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookDispatcherEmitter");
        StructDeclarationSyntax descriptor = grammarEmitter.DescendantNodes().OfType<StructDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookDispatcherDescriptor");

        Assert.IsTrue(emitter.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(descriptor.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(descriptor.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
        CollectionAssert.AreEquivalent(new[] { "ParserPredicate", "ParserAction", "LexerPredicate", "LexerAction" }, descriptor.Members.OfType<FieldDeclarationSyntax>().SelectMany(static field => field.Declaration.Variables).Select(static variable => variable.Identifier.ValueText).ToArray());

        string[] wrapperNames = ["EmitSemanticPredicateEvaluator", "EmitParserActionExecutor", "EmitLexerPredicateEvaluator", "EmitLexerActionExecutor"];
        foreach (string wrapperName in wrapperNames)
        {
            MethodDeclarationSyntax wrapper = grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == wrapperName);
            InvocationExpressionSyntax invocation = wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
            IMethodSymbol? symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            Assert.AreEqual("Emit", symbol?.Name);
            Assert.AreEqual("EmbeddedHookDispatcherEmitter", symbol?.ContainingType.Name);
            Assert.AreEqual(0, wrapper.DescendantNodes().OfType<ForEachStatementSyntax>().Count(), $"{wrapperName} must not loop over hooks.");
            Assert.AreEqual(0, wrapper.DescendantNodes().OfType<IfStatementSyntax>().Count(), $"{wrapperName} must not emit dispatcher conditions.");
        }

        MethodDeclarationSyntax sharedEmit = emitter.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "Emit");
        Assert.AreEqual(1, sharedEmit.DescendantNodes().OfType<ForEachStatementSyntax>().Count(statement => statement.Expression.ToString() == "hooks"), "Only the shared emitter should iterate over runtime dispatcher hooks.");
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("ValidateEmbeddedCodeHook", StringComparison.Ordinal)), "The shared emitter must keep Owner and Kind validation.");
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "descriptor.Owner"));
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "descriptor.Kind"));
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<SwitchStatementSyntax>().Count());
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookOwner.Parser" or "EmbeddedCodeHookOwner.Lexer"));
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookKind.SemanticPredicate" or "EmbeddedCodeHookKind.InlineAction"));
        Assert.IsFalse(grammarEmitter.DescendantNodes().OfType<ParameterSyntax>().Any(parameter => parameter.Identifier.ValueText is "isLexer" or "isPredicate"));
        Assert.AreEqual(4, descriptor.DescendantNodes().OfType<LiteralExpressionSyntax>().Count(literal => literal.Token.ValueText.Contains("_fallback.", StringComparison.Ordinal)), "Each descriptor-backed dispatcher must keep a fallback expression.");
        Assert.AreEqual(4, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookOwner.Parser" or "EmbeddedCodeHookOwner.Lexer"));
        Assert.AreEqual(4, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookKind.SemanticPredicate" or "EmbeddedCodeHookKind.InlineAction"));

        string[] hookMethodEmitters = ["EmitPredicateHook", "EmitActionHook", "EmitLexerPredicateHook", "EmitLexerActionHook"];
        foreach (string hookMethodEmitter in hookMethodEmitters)
        {
            Assert.IsTrue(grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == hookMethodEmitter), $"{hookMethodEmitter} must remain separate until TODO 8c is addressed.");
        }
    }



    /// <summary>
    /// Ensures generated hook methods use one shared method emitter with immutable descriptors.
    /// </summary>
    [TestMethod]
    public void GrammarEmitterEmbeddedHookMethods_UseSharedEmitterAndImmutableDescriptors()
    {
        string repositoryRoot = FindRepositoryRoot();
        string relativePath = NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.ExecutionContext.Policy.cs"));
        string source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Generators.HookMethodArchitectureScan",
            [tree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);
        ClassDeclarationSyntax grammarEmitter = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "GrammarEmitter");
        ClassDeclarationSyntax emitter = grammarEmitter.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookMethodEmitter");
        StructDeclarationSyntax descriptor = grammarEmitter.DescendantNodes().OfType<StructDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookMethodDescriptor");
        EnumDeclarationSyntax profile = grammarEmitter.DescendantNodes().OfType<EnumDeclarationSyntax>().Single(static type => type.Identifier.ValueText == "EmbeddedHookContextLocalProfile");

        Assert.IsTrue(emitter.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(descriptor.Modifiers.Any(SyntaxKind.PrivateKeyword));
        Assert.IsTrue(descriptor.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
        CollectionAssert.AreEquivalent(new[] { "ParserPredicate", "ParserAction", "LexerPredicate", "LexerAction" }, descriptor.Members.OfType<PropertyDeclarationSyntax>().Where(static property => property.Modifiers.Any(SyntaxKind.StaticKeyword)).Select(static property => property.Identifier.ValueText).ToArray());
        CollectionAssert.AreEquivalent(new[] { "None", "ParserPredicate", "ParserAction" }, profile.Members.Select(static member => member.Identifier.ValueText).ToArray());

        string[] wrapperNames = ["EmitPredicateHook", "EmitActionHook", "EmitLexerPredicateHook", "EmitLexerActionHook"];
        string[] forbiddenCalls = ["ValidateEmbeddedCodeHook", "ForPredicate", "ForAction", "EmitContextLocals", "EmitGeneratedEmbeddedCodeBody", "AppendLine"];
        foreach (string wrapperName in wrapperNames)
        {
            MethodDeclarationSyntax wrapper = grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == wrapperName);
            InvocationExpressionSyntax invocation = wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
            IMethodSymbol? symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            Assert.AreEqual("Emit", symbol?.Name);
            Assert.AreEqual("EmbeddedHookMethodEmitter", symbol?.ContainingType.Name);
            foreach (InvocationExpressionSyntax call in wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callText = call.Expression.ToString();
                Assert.IsFalse(forbiddenCalls.Any(callText.Contains), $"{wrapperName} must not directly call {callText}.");
            }
        }

        MethodDeclarationSyntax sharedEmit = emitter.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "Emit");
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => invocation.ToString().StartsWith("ValidateEmbeddedCodeHook", StringComparison.Ordinal)), "The shared method emitter must keep Owner and Kind validation.");
        Assert.AreEqual(1, sharedEmit.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation => invocation.ToString().StartsWith("RequireTransformedCode", StringComparison.Ordinal)), "The shared emitter must validate the phase through one central accessor.");
        MethodDeclarationSyntax phaseGuard = emitter.Members.OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "RequireTransformedCode");
        Assert.IsTrue(phaseGuard.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "hook.TransformedCode"));
        Assert.IsFalse(emitter.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "hook.RawCode.Text"), "Hook emitters must never read raw code text.");
        Assert.AreEqual(1, sharedEmit.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation => invocation.ToString().StartsWith("EmitGeneratedEmbeddedCodeBody", StringComparison.Ordinal)), "The shared method emitter must centralize generated body emission.");
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "descriptor.Owner"));
        Assert.IsTrue(sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.ToString() == "descriptor.Kind"));
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookOwner.Parser" or "EmbeddedCodeHookOwner.Lexer"));
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookKind.SemanticPredicate" or "EmbeddedCodeHookKind.InlineAction"));
        Assert.AreEqual(0, sharedEmit.DescendantNodes().OfType<SwitchStatementSyntax>().Count(statement => statement.Expression.ToString().Contains("Owner", StringComparison.Ordinal) || statement.Expression.ToString().Contains("Kind", StringComparison.Ordinal)));
        Assert.IsFalse(grammarEmitter.DescendantNodes().OfType<ParameterSyntax>().Any(parameter => parameter.Identifier.ValueText is "isLexer" or "isPredicate"));
        Assert.AreEqual(2, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() == "GeneratedEmbeddedCodeBody.ForPredicate"));
        Assert.AreEqual(2, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() == "GeneratedEmbeddedCodeBody.ForAction"));
        Assert.AreEqual(4, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookOwner.Parser" or "EmbeddedCodeHookOwner.Lexer"));
        Assert.AreEqual(4, descriptor.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Count(member => member.ToString() is "EmbeddedCodeHookKind.SemanticPredicate" or "EmbeddedCodeHookKind.InlineAction"));
        Assert.IsTrue(grammarEmitter.Members.OfType<MethodDeclarationSyntax>().Any(static method => method.Identifier.ValueText == "EmitLifecycleHookMethod"), "Lifecycle hooks must remain outside the embedded hook method abstraction.");
    }

    /// <summary>
    /// Asserts that a specialized symbol wrapper delegates directly to the shared runtime symbol builder.
    /// </summary>
    /// <param name="wrapper">Wrapper method declaration to inspect.</param>
    /// <param name="semanticModel">Semantic model used to resolve the delegated invocation.</param>
    private static void AssertWrapperDelegatesToSharedBuilder(MethodDeclarationSyntax wrapper, SemanticModel semanticModel)
    {
        InvocationExpressionSyntax invocation = wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        Assert.AreEqual("BuildRuntimeContextSymbols", (semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)?.Name);
    }



    /// <summary>
    /// Asserts that an embedded hook collection wrapper delegates to the shared collector with the expected strategy singleton.
    /// </summary>
    /// <param name="wrapper">Wrapper method declaration to inspect.</param>
    /// <param name="expectedStrategyTypeName">Expected strategy type name.</param>
    /// <param name="semanticModel">Semantic model used to resolve the collector invocation.</param>
    private static void AssertWrapperDelegatesToCollector(MethodDeclarationSyntax wrapper, string expectedStrategyTypeName, SemanticModel semanticModel)
    {
        InvocationExpressionSyntax invocation = wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        Assert.AreEqual("Collect", (semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)?.Name);
        ArgumentSyntax strategyArgument = invocation.ArgumentList.Arguments.Last();
        Assert.AreEqual($"{expectedStrategyTypeName}.Instance", strategyArgument.Expression.ToString());
    }
    /// <summary>
    /// Finds the repository root from the functional test output folder.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "Utils.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    /// <summary>
    /// Determines whether a file belongs to parser production sources scanned by this architecture test.
    /// </summary>
    /// <param name="file">Absolute source file path.</param>
    /// <returns><see langword="true" /> when the file is a production parser C# source file.</returns>
    private static bool IsProductionParserSource(string file)
    {
        return !ContainsDirectory(file, "bin")
            && !ContainsDirectory(file, "obj")
            && !ContainsDirectory(file, "UtilsTest")
            && !ContainsDirectory(file, "UtilsTest.Functional")
            && file.Contains($"{Path.DirectorySeparatorChar}Utils.Parser", StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates semantic scans for parser production sources plus the shared expression compiler contract.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <returns>Semantic scans used by the architecture guard.</returns>
    private static IReadOnlyList<SourceScan> CreateProductionParserScans(string repositoryRoot)
    {
        string expressionCompilerPath = Path.Combine(repositoryRoot, "Utils", "Expressions", "IExpressionCompiler.cs");
        SyntaxTree[] trees = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .Concat([expressionCompilerPath])
            .Distinct(StringComparer.Ordinal)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: NormalizePath(Path.GetRelativePath(repositoryRoot, file))))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.EmbeddedCodeTransformer.ArchitectureScan",
            trees,
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return trees.Select(tree => new SourceScan(tree.FilePath, tree, compilation.GetSemanticModel(tree))).ToArray();
    }

    /// <summary>
    /// Creates semantic scans for a sample that references the embedded-code preparer and expression compiler contracts.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Semantic scans for the supplied sample source.</returns>
    private static IReadOnlyList<SourceScan> CreateSampleExpressionCompilerScans(string relativePath, string source)
    {
        SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        SyntaxTree contractsTree = CSharpSyntaxTree.ParseText(
            """
            namespace Utils.Expressions
            {
                public interface IExpressionCompiler
                {
                    object Compile(string content, System.Collections.Generic.IReadOnlyDictionary<string, object>? symbols = null);
                }
            }

            namespace Utils.Parser.EmbeddedCode
            {
                public interface IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>
                {
                }
            }
            """,
            path: "EmbeddedCodePreparerContracts.cs");

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.EmbeddedCodeTransformer.SampleArchitectureScan",
            [sourceTree, contractsTree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return [new SourceScan(relativePath, sourceTree, compilation.GetSemanticModel(sourceTree))];
    }

    /// <summary>
    /// Creates semantic scans for pipeline calls expressed through aliases or static imports.
    /// </summary>
    /// <param name="callerSource">Caller source containing the invocation forms to inspect.</param>
    /// <returns>Semantic scans containing the pipeline contract and caller.</returns>
    private static IReadOnlyList<SourceScan> CreateSamplePipelineScans(string callerSource)
    {
        SyntaxTree pipelineTree = CSharpSyntaxTree.ParseText(
            """
            namespace Utils.Parser.Diagnostics.EmbeddedCode;

            internal static class EmbeddedCodeTransformationPipeline
            {
                internal static object TransformAndValidate() => new();
            }
            """,
            path: CentralPipelineRelativePath);
        SyntaxTree callerTree = CSharpSyntaxTree.ParseText(callerSource, path: "Caller.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.EmbeddedCodeTransformer.SamplePipelineScan",
            [pipelineTree, callerTree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return new[] { pipelineTree, callerTree }
            .Select(tree => new SourceScan(tree.FilePath, tree, compilation.GetSemanticModel(tree)))
            .ToArray();
    }

    /// <summary>
    /// Finds forbidden direct <c>IExpressionCompiler.Compile(...)</c> calls inside embedded-code preparer implementations.
    /// </summary>
    /// <param name="scans">Semantic source scans to inspect.</param>
    /// <returns>Forbidden direct compile invocation occurrences found in embedded-code preparers.</returns>
    private static string[] FindForbiddenEmbeddedCodePreparerCompileCalls(IReadOnlyList<SourceScan> scans)
    {
        Compilation compilation = scans[0].SemanticModel.Compilation;
        INamedTypeSymbol? preparerContract = compilation.GetTypeByMetadataName("Utils.Parser.EmbeddedCode.IEmbeddedCodePreparer`2");
        INamedTypeSymbol? expressionCompilerContract = compilation.GetTypeByMetadataName("Utils.Expressions.IExpressionCompiler");
        if (preparerContract is null || expressionCompilerContract is null)
        {
            Assert.Fail("Required embedded-code preparer or expression compiler contracts were not resolved by the architecture scan.");
        }

        return scans.SelectMany(scan => FindForbiddenEmbeddedCodePreparerCompileCalls(scan, preparerContract!, expressionCompilerContract!)).ToArray();
    }

    /// <summary>
    /// Finds forbidden direct <c>IExpressionCompiler.Compile(...)</c> calls inside one semantic source scan.
    /// </summary>
    /// <param name="scan">Semantic source scan to inspect.</param>
    /// <param name="preparerContract">Resolved embedded-code preparer contract.</param>
    /// <param name="expressionCompilerContract">Resolved expression compiler contract.</param>
    /// <returns>Forbidden direct compile invocation occurrences found in embedded-code preparers.</returns>
    private static IEnumerable<string> FindForbiddenEmbeddedCodePreparerCompileCalls(
        SourceScan scan,
        INamedTypeSymbol preparerContract,
        INamedTypeSymbol expressionCompilerContract)
    {
        CompilationUnitSyntax root = scan.Tree.GetCompilationUnitRoot();

        foreach (ClassDeclarationSyntax type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (scan.SemanticModel.GetDeclaredSymbol(type) is not INamedTypeSymbol typeSymbol
                || !ImplementsGenericContract(typeSymbol, preparerContract)
                || IsAllowedExpressionCompilerPreparer(typeSymbol))
            {
                continue;
            }

            foreach (InvocationExpressionSyntax invocation in type.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (scan.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
                    && method.Name == "Compile"
                    && SymbolEqualityComparer.Default.Equals(method.ContainingType, expressionCompilerContract))
                {
                    FileLinePositionSpan lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                    yield return $"{scan.RelativePath}:{lineSpan.StartLinePosition.Line + 1}: {invocation}";
                }
            }
        }
    }

    /// <summary>
    /// Determines whether a type implements the expected generic embedded-code preparer contract.
    /// </summary>
    /// <param name="type">Type symbol to classify.</param>
    /// <param name="genericContract">Generic preparer contract definition.</param>
    /// <returns><see langword="true" /> when the type implements the contract.</returns>
    private static bool ImplementsGenericContract(INamedTypeSymbol type, INamedTypeSymbol genericContract)
    {
        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, genericContract));
    }

    /// <summary>
    /// Determines whether a preparer type is the approved runtime transformation-to-compilation boundary.
    /// </summary>
    /// <param name="type">Preparer type symbol to classify.</param>
    /// <returns><see langword="true" /> when direct expression compilation is allowed.</returns>
    private static bool IsAllowedExpressionCompilerPreparer(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == "Utils.Parser.Expressions.ExpressionEmbeddedCodePreparer";
    }

    /// <summary>
    /// Finds forbidden direct <c>.Transform(...)</c> invocation expressions in a source file.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <param name="file">Absolute source file path.</param>
    /// <returns>Forbidden direct transform invocation occurrences found in the file.</returns>
    private static IEnumerable<SourceOccurrence> FindForbiddenDirectTransformCallsInFile(string repositoryRoot, string file)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, file);
        string source = File.ReadAllText(file);

        return FindForbiddenDirectTransformCalls(relativePath, source);
    }

    /// <summary>
    /// Finds forbidden direct <c>.Transform(...)</c> invocation expressions in source text.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Forbidden direct transform invocation occurrences found in the source text.</returns>
    private static IEnumerable<SourceOccurrence> FindForbiddenDirectTransformCalls(string relativePath, string source)
    {
        return FindDirectTransformCalls(relativePath, source)
            .Where(static occurrence => !IsCentralPipelineOccurrence(occurrence));
    }

    /// <summary>
    /// Finds direct <c>.Transform(...)</c> invocation expressions in source text.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Direct transform invocation occurrences found in the source text.</returns>
    private static IEnumerable<SourceOccurrence> FindDirectTransformCalls(string relativePath, string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == "Transform")
            {
                FileLinePositionSpan lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                yield return new SourceOccurrence(
                    relativePath,
                    lineSpan.StartLinePosition.Line + 1,
                    invocation.ToString(),
                    GetEnclosingMethodName(invocation),
                    GetEnclosingTypeName(invocation),
                    memberAccess.Expression.ToString());
            }
        }
    }

    /// <summary>
    /// Determines whether a direct transform call is the single authorized central service invocation.
    /// </summary>
    /// <param name="occurrence">Source occurrence to classify.</param>
    /// <returns><see langword="true" /> when the occurrence is allowed.</returns>
    private static bool IsCentralPipelineOccurrence(SourceOccurrence occurrence)
    {
        return occurrence.RelativePath == CentralPipelineRelativePath
            && occurrence.EnclosingTypeName == "EmbeddedCodeTransformationPipeline"
            && occurrence.EnclosingMethodName == "TransformAndValidate"
            && occurrence.ReceiverExpression == "transformer";
    }

    /// <summary>
    /// Counts calls to the common transformation boundary in one source file using resolved method symbols.
    /// </summary>
    /// <param name="scans">Semantic scans containing the pipeline declaration and callers.</param>
    /// <param name="relativePath">Relative path of the caller to inspect.</param>
    /// <returns>The number of calls to the common boundary.</returns>
    private static int CountPipelineCalls(IReadOnlyList<SourceScan> scans, string relativePath)
    {
        Compilation compilation = scans[0].SemanticModel.Compilation;
        INamedTypeSymbol? pipelineType = compilation.GetTypeByMetadataName("Utils.Parser.Diagnostics.EmbeddedCode.EmbeddedCodeTransformationPipeline");
        IMethodSymbol? pipelineMethod = pipelineType?.GetMembers("TransformAndValidate").OfType<IMethodSymbol>().SingleOrDefault();
        if (pipelineMethod is null)
        {
            Assert.Fail("The embedded-code transformation pipeline entry point was not resolved by the architecture scan.");
        }

        SourceScan scan = scans.Single(candidate => NormalizePath(candidate.RelativePath) == NormalizePath(relativePath));
        return scan.Tree.GetCompilationUnitRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Count(invocation =>
            scan.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, pipelineMethod));
    }

    /// <summary>
    /// Gets the nearest enclosing method name for a syntax node.
    /// </summary>
    /// <param name="node">Syntax node to inspect.</param>
    /// <returns>The enclosing method name, or <c>null</c> when none exists.</returns>
    private static string? GetEnclosingMethodName(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    }

    /// <summary>
    /// Gets the nearest enclosing type name for a syntax node.
    /// </summary>
    /// <param name="node">Syntax node to inspect.</param>
    /// <returns>The enclosing type name, or <c>null</c> when none exists.</returns>
    private static string? GetEnclosingTypeName(SyntaxNode node)
    {
        return node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    }

    /// <summary>
    /// Determines whether an absolute path contains the specified directory segment.
    /// </summary>
    /// <param name="path">Absolute file path to inspect.</param>
    /// <param name="directoryName">Directory segment to search for.</param>
    /// <returns><see langword="true" /> when the path contains the directory segment.</returns>
    private static bool ContainsDirectory(string path, string directoryName)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(directoryName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates metadata references for semantic architecture scans.
    /// </summary>
    /// <returns>Trusted platform assembly references available to Roslyn.</returns>
    private static IEnumerable<MetadataReference> CreateRuntimeReferences()
    {
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is null)
        {
            yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            yield return MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
            yield break;
        }

        HashSet<string> emitted = new(StringComparer.Ordinal);
        foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (emitted.Add(path))
            {
                yield return MetadataReference.CreateFromFile(path);
            }
        }
    }

    /// <summary>
    /// Normalizes paths to slash-separated relative paths.
    /// </summary>
    /// <param name="path">Path to normalize.</param>
    /// <returns>A slash-separated path.</returns>
    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Represents one source file with the semantic model used by the architecture guard.
    /// </summary>
    /// <param name="RelativePath">Source file path relative to the repository root.</param>
    /// <param name="Tree">Parsed syntax tree.</param>
    /// <param name="SemanticModel">Semantic model for the syntax tree.</param>
    private sealed record SourceScan(string RelativePath, SyntaxTree Tree, SemanticModel SemanticModel);

    /// <summary>
    /// Describes a source-code occurrence found by the architecture scan.
    /// </summary>
    /// <param name="RelativePath">Source file path relative to the repository root.</param>
    /// <param name="LineNumber">One-based source line number.</param>
    /// <param name="SourceText">Invocation expression source text.</param>
    /// <param name="EnclosingMethodName">Nearest enclosing method name.</param>
    /// <param name="EnclosingTypeName">Nearest enclosing type name.</param>
    /// <param name="ReceiverExpression">Invocation receiver expression.</param>
    private sealed record SourceOccurrence(
        string RelativePath,
        int LineNumber,
        string SourceText,
        string? EnclosingMethodName,
        string? EnclosingTypeName,
        string ReceiverExpression)
    {
        /// <summary>
        /// Formats the occurrence for assertion messages.
        /// </summary>
        /// <returns>A readable source occurrence.</returns>
        public override string ToString() => $"{RelativePath}:{LineNumber}: {SourceText.Replace("\r\n", "\\n").Replace("\n", "\\n")}";
    }
}
