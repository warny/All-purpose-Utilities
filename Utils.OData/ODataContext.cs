using System.Net;
using System.Net.Http;
using Utils.OData.Linq;
using Utils.OData.Metadatas;

namespace Utils.OData;

/// <summary>
/// Provides a base class for generated OData contexts.
/// The context is initialized from an EDMX definition provided as a stream or a path.
/// </summary>
public abstract class ODataContext
{
    /// <summary>
    /// Defines the timeout applied to remote EDMX metadata downloads.
    /// </summary>
    private static readonly TimeSpan MetadataDownloadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Defines the maximum accepted size, in bytes, for EDMX metadata loaded from remote locations.
    /// </summary>
    private const int MaxMetadataBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataContext"/> class from the specified EDMX stream.
    /// </summary>
    /// <param name="edmxStream">Stream containing the EDMX metadata to load.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the EDMX metadata cannot be parsed.</exception>
    protected ODataContext(Stream edmxStream)
    {
        ArgumentNullException.ThrowIfNull(edmxStream);

        Metadata = LoadMetadataFromStream(edmxStream);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataContext"/> class from the specified file or URL.
    /// </summary>
    /// <param name="edmxPathOrUrl">Path or HTTP/HTTPS URL pointing to an EDMX file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="edmxPathOrUrl"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the EDMX metadata cannot be read or parsed.</exception>
    protected ODataContext(string edmxPathOrUrl)
        : this(OpenMetadataStream(edmxPathOrUrl))
    {
    }

    /// <summary>
    /// Gets the EDMX metadata loaded for the current context.
    /// </summary>
    public Edmx Metadata { get; }

    /// <summary>
    /// Creates a queryable sequence targeting the specified entity set.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity returned by the query.</typeparam>
    /// <param name="entitySetName">Name of the entity set to query.</param>
    /// <returns>An <see cref="ODataQueryable{TEntity}"/> representing the entity set.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    protected ODataQueryable<TEntity> Query<TEntity>(string entitySetName)
    {
        if (string.IsNullOrWhiteSpace(entitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(entitySetName));
        }

        var provider = new ODataQueryProvider(entitySetName);
        return new ODataQueryable<TEntity>(provider, entitySetName);
    }

    /// <summary>
    /// Creates a queryable sequence for an entity set when no CLR type exists for the table.
    /// </summary>
    /// <param name="entitySetName">Name of the entity set to query.</param>
    /// <returns>An untyped <see cref="ODataQueryable{TEntity}"/> that exposes <see cref="ODataUntypedRow"/> instances.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
    public ODataQueryable<ODataUntypedRow> Table(string entitySetName)
    {
        if (string.IsNullOrWhiteSpace(entitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(entitySetName));
        }

        var provider = new ODataQueryProvider(entitySetName);
        return new ODataQueryable<ODataUntypedRow>(provider, entitySetName);
    }

    /// <summary>
    /// Reads and deserializes the EDMX metadata from the provided stream.
    /// </summary>
    /// <param name="edmxStream">Stream containing the EDMX content.</param>
    /// <returns>The parsed <see cref="Edmx"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the metadata cannot be deserialized.</exception>
    private static Edmx LoadMetadataFromStream(Stream edmxStream)
    {
        if (edmxStream is null)
        {
            throw new ArgumentNullException(nameof(edmxStream));
        }

        using var memoryStream = new MemoryStream();
        edmxStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var metadata = DeserializeMetadatas.Deserialize(memoryStream);
        if (metadata is null)
        {
            throw new InvalidOperationException("Unable to deserialize the EDMX metadata stream.");
        }

        return metadata;
    }

    /// <summary>
    /// Opens the EDMX stream for the provided path or URL and loads it into memory.
    /// </summary>
    /// <param name="edmxPathOrUrl">Path or URL pointing to an EDMX resource.</param>
    /// <returns>A <see cref="Stream"/> containing the EDMX content.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be located.</exception>
    private static Stream OpenMetadataStream(string edmxPathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(edmxPathOrUrl))
        {
            throw new ArgumentException("The EDMX path or URL must be provided.", nameof(edmxPathOrUrl));
        }

        if (Uri.TryCreate(edmxPathOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return DownloadMetadata(uri);
        }

        var fullPath = Path.GetFullPath(edmxPathOrUrl);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"EDMX file not found at path '{fullPath}'.");
        }

        using var fileStream = File.OpenRead(fullPath);
        return CopyToMemory(fileStream);
    }

    /// <summary>
    /// Downloads the EDMX metadata from a remote location.
    /// </summary>
    /// <param name="uri">The remote URI containing the EDMX content.</param>
    /// <returns>A memory stream with the downloaded metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the download fails.</exception>
    private static MemoryStream DownloadMetadata(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });

        client.Timeout = MetadataDownloadTimeout;

        using var response = client.GetAsync(uri).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to download EDMX metadata from '{uri}'. Status code: {response.StatusCode}.");
        }

        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxMetadataBytes)
        {
            throw new InvalidOperationException($"EDMX metadata from '{uri}' exceeds the maximum allowed size of {MaxMetadataBytes} bytes.");
        }

        using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        return CopyToMemory(responseStream, MaxMetadataBytes);
    }

    /// <summary>
    /// Copies a stream to memory to ensure it can be consumed multiple times.
    /// </summary>
    /// <param name="source">The source stream to copy.</param>
    /// <param name="maximumBytes">Maximum number of bytes accepted when copying from the source stream.</param>
    /// <returns>A <see cref="MemoryStream"/> containing a copy of the source data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the source stream exceeds <paramref name="maximumBytes"/> bytes.</exception>
    private static MemoryStream CopyToMemory(Stream source, int maximumBytes = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "The maximum number of bytes must be greater than zero.");
        }

        var buffer = new byte[81920];
        int totalRead = 0;
        var memoryStream = new MemoryStream();

        while (true)
        {
            int bytesRead = source.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
            if (totalRead > maximumBytes)
            {
                throw new InvalidOperationException($"EDMX metadata exceeds the maximum allowed size of {maximumBytes} bytes.");
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}
