using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// Builds the compiled grammar of a parser probe run for a <c>--plimits</c> selector. The baseline build has no parser
/// resource limits, so every selector produces the same historical grammar.
/// </summary>
internal static class ParserLimitsAdapter
{
    /// <summary>Creates a compiled grammar.</summary>
    /// <param name="definition">The resolved definition.</param>
    /// <param name="policy">The instrumented runtime policy.</param>
    /// <param name="limits">The <c>--plimits</c> selector.</param>
    /// <returns>The grammar.</returns>
    public static CompiledGrammar Create(ParserDefinition definition, ParserRuntimeFeaturePolicy policy, string limits)
        => new(definition, policy);
}