using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Metadata;

/// <summary>
/// Provides an immutable catalog of parser feature capabilities.
/// </summary>
public static class ParserFeatureCapabilities
{
    /// <summary>Immutable lookup from feature enum value to capability descriptor, used by <see cref="Get"/> and <see cref="All"/>.</summary>
    private static readonly IReadOnlyDictionary<ParserFeature, ParserFeatureCapability> CapabilityByFeature =
        new ReadOnlyDictionary<ParserFeature, ParserFeatureCapability>(
            BuildCapabilities().ToDictionary(static capability => capability.Feature));

    /// <summary>
    /// Gets all declared parser feature capabilities.
    /// </summary>
    public static IReadOnlyList<ParserFeatureCapability> All { get; } =
        new ReadOnlyCollection<ParserFeatureCapability>(CapabilityByFeature.Values.OrderBy(static entry => entry.Feature).ToArray());

    /// <summary>
    /// Gets the capability descriptor for a feature.
    /// </summary>
    /// <param name="feature">Feature to resolve.</param>
    /// <returns>The immutable capability descriptor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the feature value is unknown.</exception>
    public static ParserFeatureCapability Get(ParserFeature feature)
    {
        if (!CapabilityByFeature.TryGetValue(feature, out ParserFeatureCapability? capability))
        {
            throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown parser feature.");
        }

        return capability;
    }

    /// <summary>
    /// Builds the canonical capability descriptors used by this catalog.
    /// </summary>
    /// <remarks>
    /// This method is intentionally descriptive only and must not introduce runtime behavior changes.
    /// </remarks>
    /// <returns>An array containing one descriptor per <see cref="ParserFeature"/>.</returns>
    private static ParserFeatureCapability[] BuildCapabilities()
    {
        return
        [
            new(ParserFeature.AssocRight, ParserFeatureSupportLevel.Supported, "<assoc=right> precedence annotations are applied.", null, null),
            new(ParserFeature.GrammarImports, ParserFeatureSupportLevel.SupportedWithLimits, "Grammar imports are resolved during project compilation.", "Resolution scope is project-compilation level.", null),
            new(ParserFeature.SemanticPredicates, ParserFeatureSupportLevel.RuntimeOptional, "Semantic predicates are recognized and can be evaluated via injected policy.", "Default policy does not enforce predicates semantically.", ParserDiagnostics.SemanticPredicateNotEnforced.Code),
            new(ParserFeature.InlineActions, ParserFeatureSupportLevel.RuntimeOptional, "Inline actions are captured and can be executed via injected policy.", "Default policy stores actions without executing them.", ParserDiagnostics.InlineActionStoredNotExecuted.Code),
            new(ParserFeature.RuleActions, ParserFeatureSupportLevel.RuntimeOptional, "Rule actions (@init/@after) are parsed and can be executed by the generated C# opt-in path.", "Default parsing does not execute them; runtime-inline expression preparation does not support them; lexer lifecycle actions remain unsupported.", ParserDiagnostics.ActionIgnored.Code),
            new(ParserFeature.RuleParameters, ParserFeatureSupportLevel.MetadataOnly, "Rule parameters are preserved as raw metadata.", "No invocation-frame parameter semantics are implemented.", null),
            new(ParserFeature.RuleReturns, ParserFeatureSupportLevel.MetadataOnly, "Rule returns are preserved as raw metadata.", "No runtime return propagation is implemented.", null),
            new(ParserFeature.SharedPrefixMetadata, ParserFeatureSupportLevel.MetadataOnly, "Shared-prefix analysis metadata is available.", "Metadata is descriptive and does not imply runtime execution.", null),
            new(ParserFeature.SharedPrefixExecution, ParserFeatureSupportLevel.Unsupported, "Shared-prefix execution is not implemented.", null, null),
            new(ParserFeature.ContinuationReplay, ParserFeatureSupportLevel.Unsupported, "Continuation replay is not implemented.", null, null),
            new(ParserFeature.ParserGraphExecution, ParserFeatureSupportLevel.Unsupported, "Parser graph execution is not implemented.", null, null),
            new(ParserFeature.AdaptiveLl, ParserFeatureSupportLevel.Unsupported, "Adaptive LL strategy is not implemented.", null, null),
            new(ParserFeature.Gll, ParserFeatureSupportLevel.Unsupported, "GLL strategy is not implemented.", null, null),
            new(ParserFeature.AsyncParsing, ParserFeatureSupportLevel.Unsupported, "Asynchronous parser execution is not implemented.", null, null),
            new(ParserFeature.ParallelParsing, ParserFeatureSupportLevel.Unsupported, "Parallel parser execution is not implemented.", null, null)
        ];
    }
}
