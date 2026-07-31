namespace Utils.IO.Serialization;

/// <summary>
/// Configures safety limits applied by a <see cref="Reader"/> to length-prefixed payloads.
/// </summary>
public sealed class ReaderOptions
{
    /// <summary>
    /// Gets the largest accepted string or arbitrary-precision integer payload in bytes.
    /// A <see langword="null"/> value preserves the historical behavior and imposes no limit beyond the format's 32-bit length prefix.
    /// Zero permits empty payloads only.
    /// </summary>
    public int? MaximumPayloadLength { get; init; }
}
