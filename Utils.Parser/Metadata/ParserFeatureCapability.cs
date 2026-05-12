namespace Utils.Parser.Metadata;

/// <summary>
/// Describes the support status of one parser feature.
/// </summary>
/// <param name="Feature">Feature being described.</param>
/// <param name="SupportLevel">Current support level.</param>
/// <param name="Summary">Short support summary.</param>
/// <param name="Limitation">Optional limitation details.</param>
/// <param name="RelatedDiagnosticCode">Optional related diagnostic code.</param>
public sealed record ParserFeatureCapability(
    ParserFeature Feature,
    ParserFeatureSupportLevel SupportLevel,
    string Summary,
    string? Limitation,
    string? RelatedDiagnosticCode);
