using System;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>Builds the compiled grammar of a parser probe run for a <c>--plimits</c> selector.</summary>
internal static class ParserLimitsAdapter
{
    /// <summary>Creates a compiled grammar.</summary>
    /// <param name="definition">The resolved definition.</param>
    /// <param name="policy">The instrumented runtime policy.</param>
    /// <param name="limits">
    /// The <c>--plimits</c> selector: <c>default</c> (<see cref="ParserResourceLimits.Default"/>) or <c>unlimited</c>
    /// (<see cref="ParserResourceLimits.Unlimited"/>: only the stack guard stays active).
    /// </param>
    /// <returns>The grammar.</returns>
    public static CompiledGrammar Create(ParserDefinition definition, ParserRuntimeFeaturePolicy policy, string limits)
        => new(definition, policy, limits switch
        {
            "default" => ParserResourceLimits.Default,
            "unlimited" => ParserResourceLimits.Unlimited,
            _ => throw new ArgumentException($"Unknown parser limits '{limits}'.", nameof(limits)),
        });
}
