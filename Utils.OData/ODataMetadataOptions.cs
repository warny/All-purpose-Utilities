using System.Net.Http;

namespace Utils.OData;

/// <summary>
/// Configures how EDMX metadata is loaded by <see cref="ODataContext"/>, providing a single,
/// consistent policy for the maximum accepted size, the transport used for remote downloads,
/// the download timeout, and the cross-host redirect behaviour.
/// </summary>
/// <remarks>
/// This type addresses several audit findings for <see cref="ODataContext"/>:
/// injectable/reusable <see cref="HttpClient"/> (item 16), a consistent bounded size policy
/// across every metadata source (items 15 and 33), cancellation and dependency injection
/// (items 14 and 29), and an explicit redirect destination policy (item 30).
/// </remarks>
public sealed class ODataMetadataOptions
{
    /// <summary>
    /// The default maximum accepted size, in bytes, for EDMX metadata (10 MiB).
    /// </summary>
    public const int DefaultMaxMetadataBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Gets a shared, immutable default configuration: a 10 MiB size cap, a 30-second download
    /// timeout, no injected client, and cross-host redirects disallowed.
    /// </summary>
    public static ODataMetadataOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the maximum accepted size, in bytes, for EDMX metadata from any source.
    /// Defaults to <see cref="DefaultMaxMetadataBytes"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
    public int MaxMetadataBytes
    {
        get => _maxMetadataBytes;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The maximum metadata size must be greater than zero.");
            }

            _maxMetadataBytes = value;
        }
    }

    private readonly int _maxMetadataBytes = DefaultMaxMetadataBytes;

    /// <summary>
    /// Gets or sets the <see cref="HttpClient"/> used to download remote metadata. When
    /// <see langword="null"/>, a shared internal client is reused so that a new client is not
    /// created per download (item 16). The caller owns and is responsible for disposing any
    /// injected client.
    /// </summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>
    /// Gets or sets the timeout applied to remote metadata downloads. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether the metadata download may follow a redirect to a
    /// different host or scheme. Defaults to <see langword="false"/>, so a trusted metadata URL
    /// that redirects to another origin is rejected (item 30).
    /// </summary>
    public bool AllowCrossHostRedirect { get; init; }
}
