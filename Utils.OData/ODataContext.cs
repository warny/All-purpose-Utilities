using System.Net;
using System.Net.Http;
using Utils.OData.Linq;
using Utils.OData.Metadatas;

namespace Utils.OData;

/// <summary>
/// Provides a base class for generated OData contexts.
/// The context is initialized from an EDMX definition provided as a stream or a path.
/// </summary>
/// <remarks>
/// <para>
/// The synchronous constructors are retained for backward compatibility with generated
/// code. They perform blocking I/O and, for remote URLs, block a thread on a network call
/// (item 14 / item 29). New code should prefer the asynchronous <c>CreateAsync</c> and
/// <see cref="LoadMetadataAsync(string, ODataMetadataOptions?, CancellationToken)"/> factory
/// methods, which accept a caller-supplied <see cref="HttpClient"/>, honour a
/// <see cref="CancellationToken"/>, and apply a consistent bounded size policy to every metadata
/// source (items 15, 16, 33, 34).
/// </para>
/// </remarks>
public abstract class ODataContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ODataContext"/> class from the specified EDMX stream.
    /// </summary>
    /// <param name="edmxStream">Stream containing the EDMX metadata to load.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the EDMX metadata cannot be parsed.</exception>
    protected ODataContext(Stream edmxStream)
    {
        ArgumentNullException.ThrowIfNull(edmxStream);

        Metadata = LoadMetadataFromStream(edmxStream, ODataMetadataOptions.Default);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataContext"/> class from the specified file or URL.
    /// </summary>
    /// <param name="edmxPathOrUrl">Path or HTTP/HTTPS URL pointing to an EDMX file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="edmxPathOrUrl"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the EDMX metadata cannot be read or parsed.</exception>
    protected ODataContext(string edmxPathOrUrl)
        : this(OpenMetadataStream(edmxPathOrUrl, ODataMetadataOptions.Default))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataContext"/> class from already-parsed metadata.
    /// This constructor performs no I/O and is used by the asynchronous factory methods.
    /// </summary>
    /// <param name="metadata">The parsed EDMX metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is <see langword="null"/>.</exception>
    protected ODataContext(Edmx metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
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

    // -----------------------------------------------------------------------
    // Asynchronous, injectable, bounded metadata loading (items 14/15/16/29/30/33/34)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads and parses EDMX metadata from the specified file path or HTTP/HTTPS URL without blocking
    /// the calling thread, using a caller-supplied transport and a bounded size policy.
    /// </summary>
    /// <param name="edmxPathOrUrl">Path or HTTP/HTTPS URL pointing to an EDMX resource.</param>
    /// <param name="options">
    /// Optional loading options controlling the maximum metadata size, the <see cref="HttpClient"/>
    /// to reuse, the download timeout, and the cross-host redirect policy. When <see langword="null"/>,
    /// <see cref="ODataMetadataOptions.Default"/> is used.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the load operation.</param>
    /// <returns>A task producing the parsed <see cref="Edmx"/> metadata.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="edmxPathOrUrl"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the metadata cannot be read or parsed.</exception>
    public static async Task<Edmx> LoadMetadataAsync(
        string edmxPathOrUrl,
        ODataMetadataOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(edmxPathOrUrl))
        {
            throw new ArgumentException("The EDMX path or URL must be provided.", nameof(edmxPathOrUrl));
        }

        options ??= ODataMetadataOptions.Default;

        if (Uri.TryCreate(edmxPathOrUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            await using Stream remote = await DownloadMetadataAsync(uri, options, cancellationToken).ConfigureAwait(false);
            return await LoadMetadataFromStreamAsync(remote, options, cancellationToken).ConfigureAwait(false);
        }

        string fullPath = Path.GetFullPath(edmxPathOrUrl);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"EDMX file not found at path '{fullPath}'.");
        }

        // Item 34: async, non-blocking file access with the same bounded size policy as remote loads.
        await using FileStream fileStream = new(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return await LoadMetadataFromStreamAsync(fileStream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads and parses EDMX metadata from a caller-provided stream without blocking the calling thread,
    /// enforcing the configured maximum size (item 15 / item 33).
    /// </summary>
    /// <param name="edmxStream">Stream containing the EDMX content.</param>
    /// <param name="options">
    /// Optional loading options controlling the maximum metadata size. When <see langword="null"/>,
    /// <see cref="ODataMetadataOptions.Default"/> is used.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the load operation.</param>
    /// <returns>A task producing the parsed <see cref="Edmx"/> metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the metadata cannot be parsed or exceeds the size limit.</exception>
    /// <remarks>
    /// For seekable streams the EDMX document is read starting at the stream's current
    /// <see cref="Stream.Position"/>. The size limit is checked against the number of bytes
    /// remaining from that position, not the total stream length. The position is not reset
    /// before reading.
    /// </remarks>
    public static async Task<Edmx> LoadMetadataFromStreamAsync(
        Stream edmxStream,
        ODataMetadataOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edmxStream);
        options ??= ODataMetadataOptions.Default;

        // Item 15: deserialize directly from an already-seekable stream when possible, avoiding
        // an unnecessary in-memory copy; otherwise buffer with a bounded copy.
        // The document is read from the current Position; the size check uses remaining bytes.
        Stream deserializeSource;
        MemoryStream? bounded = null;
        if (edmxStream.CanSeek)
        {
            long remaining = edmxStream.Length - edmxStream.Position;
            if (remaining > options.MaxMetadataBytes)
            {
                throw new InvalidOperationException(
                    $"EDMX metadata exceeds the maximum allowed size of {options.MaxMetadataBytes} bytes.");
            }

            deserializeSource = edmxStream;
        }
        else
        {
            bounded = await CopyToMemoryAsync(edmxStream, options.MaxMetadataBytes, cancellationToken).ConfigureAwait(false);
            deserializeSource = bounded;
        }

        try
        {
            Edmx? metadata = DeserializeMetadatas.Deserialize(deserializeSource);
            if (metadata is null)
            {
                throw new InvalidOperationException("Unable to deserialize the EDMX metadata stream.");
            }

            return metadata;
        }
        finally
        {
            bounded?.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously creates a context instance from a caller-provided EDMX stream, delegating parsing
    /// to <see cref="LoadMetadataFromStreamAsync(Stream, ODataMetadataOptions?, CancellationToken)"/> and
    /// then constructing <typeparamref name="TContext"/> through the supplied factory.
    /// </summary>
    /// <typeparam name="TContext">Concrete context type to create.</typeparam>
    /// <param name="edmxStream">Stream containing the EDMX content.</param>
    /// <param name="factory">Factory that builds the context from the parsed metadata.</param>
    /// <param name="options">Optional loading options. When <see langword="null"/>, <see cref="ODataMetadataOptions.Default"/> is used.</param>
    /// <param name="cancellationToken">Token used to cancel the load operation.</param>
    /// <returns>A task producing the constructed context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static async Task<TContext> CreateAsync<TContext>(
        Stream edmxStream,
        Func<Edmx, TContext> factory,
        ODataMetadataOptions? options = null,
        CancellationToken cancellationToken = default)
        where TContext : ODataContext
    {
        ArgumentNullException.ThrowIfNull(edmxStream);
        ArgumentNullException.ThrowIfNull(factory);

        Edmx metadata = await LoadMetadataFromStreamAsync(edmxStream, options, cancellationToken).ConfigureAwait(false);
        return factory(metadata);
    }

    /// <summary>
    /// Asynchronously creates a context instance from an EDMX file path or HTTP/HTTPS URL, delegating
    /// loading to <see cref="LoadMetadataAsync(string, ODataMetadataOptions?, CancellationToken)"/> and
    /// then constructing <typeparamref name="TContext"/> through the supplied factory.
    /// </summary>
    /// <typeparam name="TContext">Concrete context type to create.</typeparam>
    /// <param name="edmxPathOrUrl">Path or HTTP/HTTPS URL pointing to an EDMX resource.</param>
    /// <param name="factory">Factory that builds the context from the parsed metadata.</param>
    /// <param name="options">Optional loading options. When <see langword="null"/>, <see cref="ODataMetadataOptions.Default"/> is used.</param>
    /// <param name="cancellationToken">Token used to cancel the load operation.</param>
    /// <returns>A task producing the constructed context.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="edmxPathOrUrl"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static async Task<TContext> CreateAsync<TContext>(
        string edmxPathOrUrl,
        Func<Edmx, TContext> factory,
        ODataMetadataOptions? options = null,
        CancellationToken cancellationToken = default)
        where TContext : ODataContext
    {
        ArgumentNullException.ThrowIfNull(factory);

        Edmx metadata = await LoadMetadataAsync(edmxPathOrUrl, options, cancellationToken).ConfigureAwait(false);
        return factory(metadata);
    }

    // -----------------------------------------------------------------------
    // Synchronous legacy loading (retained for backward compatibility)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads and deserializes the EDMX metadata from the provided stream.
    /// </summary>
    /// <param name="edmxStream">Stream containing the EDMX content.</param>
    /// <param name="options">Loading options controlling the maximum metadata size.</param>
    /// <returns>The parsed <see cref="Edmx"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edmxStream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the metadata cannot be deserialized or exceeds the size limit.</exception>
    private static Edmx LoadMetadataFromStream(Stream edmxStream, ODataMetadataOptions options)
    {
        ArgumentNullException.ThrowIfNull(edmxStream);

        // Item 15/33: enforce the configurable size cap for caller-provided streams too.
        using var memoryStream = CopyToMemory(edmxStream, options.MaxMetadataBytes);

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
    /// <param name="options">Loading options controlling the maximum metadata size and transport policy.</param>
    /// <returns>A <see cref="Stream"/> containing the EDMX content.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be located.</exception>
    private static Stream OpenMetadataStream(string edmxPathOrUrl, ODataMetadataOptions options)
    {
        if (string.IsNullOrWhiteSpace(edmxPathOrUrl))
        {
            throw new ArgumentException("The EDMX path or URL must be provided.", nameof(edmxPathOrUrl));
        }

        if (Uri.TryCreate(edmxPathOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return DownloadMetadataAsync(uri, options, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        var fullPath = Path.GetFullPath(edmxPathOrUrl);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"EDMX file not found at path '{fullPath}'.");
        }

        using var fileStream = File.OpenRead(fullPath);
        return CopyToMemory(fileStream, options.MaxMetadataBytes);
    }

    /// <summary>
    /// Downloads the EDMX metadata from a remote location using the transport configured in <paramref name="options"/>.
    /// </summary>
    /// <param name="uri">The remote URI containing the EDMX content.</param>
    /// <param name="options">Loading options controlling the transport, size limit, timeout, and redirect policy.</param>
    /// <param name="cancellationToken">Token used to cancel the download.</param>
    /// <returns>A memory stream with the downloaded metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the download fails, exceeds the size limit, or violates the redirect policy.</exception>
    private static async Task<MemoryStream> DownloadMetadataAsync(
        Uri uri, ODataMetadataOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // Item 16: reuse an injected HttpClient when provided, avoiding a per-download client/handler.
        HttpClient client = options.HttpClient ?? SharedMetadataClient.Value;
        Uri currentUri = uri;

        for (int hop = 0; hop <= options.MaxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.DownloadTimeout);

            HttpResponseMessage response;
            try
            {
                response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"Timed out while connecting to '{currentUri}'.");
            }

            // Item 30: manually follow 3xx so the destination is validated BEFORE sending any
            // request to the redirect target.
            bool isRedirect = response.StatusCode is
                HttpStatusCode.MovedPermanently or HttpStatusCode.Found or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or
                HttpStatusCode.PermanentRedirect;

            Uri? redirectLocation = isRedirect ? response.Headers.Location : null;

            using (response)
            {
                if (isRedirect)
                {
                    if (redirectLocation is null)
                    {
                        throw new InvalidOperationException(
                            $"Redirect response from '{currentUri}' has no Location header.");
                    }

                    Uri nextUri = redirectLocation.IsAbsoluteUri
                        ? redirectLocation
                        : new Uri(currentUri, redirectLocation);

                    if (!options.AllowCrossOriginRedirect && !IsSameOrigin(nextUri, uri))
                    {
                        throw new InvalidOperationException(
                            $"EDMX metadata request for '{uri}' was redirected to a different origin " +
                            $"('{nextUri.GetLeftPart(UriPartial.Authority)}'), which is not allowed by the current redirect policy.");
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Failed to download EDMX metadata from '{currentUri}'. Status code: {response.StatusCode}.");
                }

                // Best-effort post-hoc check for injected clients whose handler auto-follows redirects.
                Uri finalUri = response.RequestMessage?.RequestUri ?? currentUri;
                if (!options.AllowCrossOriginRedirect && !IsSameOrigin(finalUri, uri))
                {
                    throw new InvalidOperationException(
                        $"EDMX metadata request for '{uri}' was redirected to a different origin " +
                        $"('{finalUri.GetLeftPart(UriPartial.Authority)}'), which is not allowed by the current redirect policy.");
                }

                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > options.MaxMetadataBytes)
                {
                    throw new InvalidOperationException(
                        $"EDMX metadata from '{currentUri}' exceeds the maximum allowed size of {options.MaxMetadataBytes} bytes.");
                }

                // Defect 3: apply timeoutCts.Token to the body read so DownloadTimeout covers
                // the complete download, not just the connection/headers phase.
                try
                {
                    await using Stream bodyStream = await response.Content
                        .ReadAsStreamAsync(timeoutCts.Token)
                        .ConfigureAwait(false);
                    return await CopyToMemoryAsync(bodyStream, options.MaxMetadataBytes, timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException(
                        $"Timed out while downloading EDMX metadata body from '{currentUri}'.");
                }
            }
        }

        throw new InvalidOperationException(
            $"EDMX metadata download from '{uri}' exceeded the maximum allowed number of redirects ({options.MaxRedirects}).");
    }

    /// <summary>
    /// Returns <see langword="true"/> when two URIs share the same origin: same scheme, host (case-insensitive), and port.
    /// </summary>
    private static bool IsSameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port;

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

    /// <summary>
    /// Asynchronously copies a stream to memory, enforcing a bounded size (items 33 and 34).
    /// </summary>
    /// <param name="source">The source stream to copy.</param>
    /// <param name="maximumBytes">Maximum number of bytes accepted when copying from the source stream.</param>
    /// <param name="cancellationToken">Token used to cancel the copy.</param>
    /// <returns>A <see cref="MemoryStream"/> containing a copy of the source data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the source stream exceeds <paramref name="maximumBytes"/> bytes.</exception>
    private static async Task<MemoryStream> CopyToMemoryAsync(
        Stream source, int maximumBytes, CancellationToken cancellationToken)
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
            int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Lazily-created shared <see cref="HttpClient"/> used when the caller does not inject one (item 16).
    /// A single static instance enables handler pooling instead of creating one client per download.
    /// </summary>
    // AllowAutoRedirect = false so DownloadMetadataAsync can validate the redirect destination
    // before sending any request to it (item 30).
    private static readonly Lazy<HttpClient> SharedMetadataClient = new(() =>
        new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false
        }));
}
