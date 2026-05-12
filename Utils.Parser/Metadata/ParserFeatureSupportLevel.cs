namespace Utils.Parser.Metadata;

/// <summary>
/// Indicates the current support level of a parser feature.
/// </summary>
public enum ParserFeatureSupportLevel
{
    /// <summary>
    /// The feature is not supported.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The feature is parsed but only retained structurally.
    /// </summary>
    ParsedOnly,

    /// <summary>
    /// The feature is parsed and preserved as metadata only.
    /// </summary>
    MetadataOnly,

    /// <summary>
    /// The feature can be enabled at runtime through an injected policy.
    /// </summary>
    RuntimeOptional,

    /// <summary>
    /// The feature is supported but has explicit limitations.
    /// </summary>
    SupportedWithLimits,

    /// <summary>
    /// The feature is fully supported.
    /// </summary>
    Supported
}
