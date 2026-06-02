using Utils.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Builds opt-in parser runtime policies backed by prepared expression embedded-code artifacts.
/// </summary>
public static class PreparedExpressionRuntimePolicyBuilder
{
    /// <summary>
    /// Builds a runtime feature policy that executes prepared expression semantic predicates and inline parser actions.
    /// </summary>
    /// <param name="definition">Parser definition to scan without modification.</param>
    /// <param name="compiler">Expression compiler selected by the caller for preparation-time compilation.</param>
    /// <param name="options">Optional integration configuration.</param>
    /// <returns>The complete policy, registry, and registry build audit result.</returns>
    public static PreparedExpressionRuntimePolicyBuildResult Build(
        ParserDefinition definition,
        IExpressionCompiler compiler,
        PreparedExpressionRuntimePolicyBuilderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(compiler);

        options ??= PreparedExpressionRuntimePolicyBuilderOptions.Default;

        var preparer = new ExpressionEmbeddedCodePreparer(compiler);
        var registryBuildResult = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(
            definition,
            preparer,
            new PreparedExpressionEmbeddedCodeRegistryBuilderOptions
            {
                GrammarName = options.GrammarName,
                LanguageOrCompilerIdentity = options.LanguageOrCompilerIdentity,
                SupportedSymbols = options.SupportedSymbols
            });

        var registry = registryBuildResult.Registry;
        var policy = (options.BasePolicy ?? ParserRuntimeFeaturePolicy.Default) with
        {
            SemanticPredicateEvaluator = new PreparedExpressionSemanticPredicateEvaluator(registry),
            ParserActionExecutor = new PreparedExpressionParserActionExecutor(registry)
        };

        return new PreparedExpressionRuntimePolicyBuildResult(policy, registry, registryBuildResult);
    }
}
