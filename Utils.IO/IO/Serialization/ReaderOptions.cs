namespace Utils.IO.Serialization;

/// <summary>
/// Configures independent safety limits applied by a <see cref="Reader"/> while parsing wire data.
/// </summary>
public sealed class ReaderOptions
{
    /// <summary>
    /// Gets the largest accepted string, arbitrary-precision integer, or length-prefixed wire-codec payload in bytes.
    /// A <see langword="null"/> value preserves the historical behavior and imposes no limit beyond the format's 32-bit length prefix.
    /// Zero permits empty payloads only.
    /// </summary>
    public int? MaximumPayloadLength { get; init; }

    /// <summary>
    /// Gets the maximum number of wire bytes that may be consumed by one reader operation tree.
    /// Prefixes and payload bytes are both counted. Child readers and slices share the same budget;
    /// seeking and rereading consumes the bytes again. A <see langword="null"/> value preserves the
    /// unlimited historical behavior, while zero rejects every attempted wire-byte read.
    /// </summary>
    public long? MaximumReadBytes { get; init; }

    /// <summary>
    /// Gets the largest collection element count accepted before allocation. This is independent of
    /// byte budgets because the managed or wire size of an arbitrary element is not always knowable.
    /// A <see langword="null"/> value preserves unlimited historical behavior; zero permits empty collections only.
    /// </summary>
    public int? MaximumCollectionLength { get; init; }
}
