using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Utils.Objects;
using Utils.OData.Metadatas;
using Utils.String;

namespace Utils.OData;

/// <summary>
/// Provides an HTTP-based client for executing OData queries and converting the responses to usable shapes.
/// </summary>
public class QueryOData : IDisposable
{
    /// <summary>
    /// Gets the base URL used for oData requests.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// If provided, credentials used by the internal handler. If an external HttpClient is provided in the ctor,
    /// those credentials won't be applied to that external client.
    /// </summary>
    public NetworkCredential? Credentials
    {
        get => _credentials;
        set
        {
            _credentials = value;
            if (_handler is not null)
            {
                _handler.Credentials = value;
            }
        }
    }

    private NetworkCredential? _credentials;
    private readonly HttpClient? _httpClient;
    private readonly HttpClientHandler? _handler;
    private readonly bool _disposeClient;
    // Item 48: 10 MiB limit applied consistently to all runtime metadata downloads.
    private const int MaxMetadataBytes = 10 * 1024 * 1024;

    private readonly SemaphoreSlim _metadataLock = new(1, 1);
    // Item 46: cache keyed by canonical metadata URL so that calls with different
    // endpoints on the same QueryOData instance return the correct schema each time.
    // Item 47: callers must treat the returned Edmx reference as read-only; the metadata
    // model classes (Edmx, DataServices, Schema, EntityType, Property) are mutable and
    // mutating a cached reference corrupts the schema for all concurrent and later readers.
    private readonly Dictionary<string, Edmx> _metadataCache = new(StringComparer.OrdinalIgnoreCase);
    // Item 55: cache deterministic metadata failures so repeated calls for the same broken
    // URL do not trigger expensive network/disk retries on every invocation.
    // Stores the full ErrorReturnValue so the original error code is preserved on repeat calls.
    private readonly Dictionary<string, ErrorReturnValue> _metadataErrorCache = new(StringComparer.OrdinalIgnoreCase);

    // Item 7: allowlist of request-context headers that may safely be forwarded to the
    // OData service.  Host, Connection, Transfer-Encoding, content headers, and security
    // credentials are intentionally excluded.
    private static readonly HashSet<string> AllowedForwardHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept", "Accept-Language", "Accept-Encoding", "Accept-Charset"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOData"/> class with the specified base URL.
    /// </summary>
    /// <remarks>This constructor sets up an <see cref="HttpClient"/> with default credentials and a timeout of 600
    /// seconds.</remarks>
    /// <param name="baseUrl">The base URL for the OData service. Must be a valid absolute HTTP or HTTPS URI.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="baseUrl"/> is not a valid absolute HTTP or HTTPS URI.</exception>
    public QueryOData(string baseUrl)
    {
        BaseUrl = ValidateBaseUrl(baseUrl);
        _handler = new HttpClientHandler()
        {
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            CookieContainer = new CookieContainer(),
            Credentials = Credentials
        };

        _httpClient = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(600)
        };
        _disposeClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOData"/> class with the specified base URL and HTTP client.
    /// </summary>
    /// <param name="baseUrl">The base URL for the OData service. Must be a valid absolute HTTP or HTTPS URI.</param>
    /// <param name="httpClient">The HTTP client used to send requests. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="baseUrl"/> is not a valid absolute HTTP or HTTPS URI.</exception>
    public QueryOData(string baseUrl, HttpClient httpClient)
    {
        BaseUrl = ValidateBaseUrl(baseUrl);
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeClient = false;
    }

    /// <summary>
    /// Validates the base URL at construction time and returns the normalized string.
    /// </summary>
    /// <param name="baseUrl">The base URL supplied by the caller.</param>
    /// <returns>The validated base URL string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUrl"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the URL is not an absolute HTTP or HTTPS URI.</exception>
    private static string ValidateBaseUrl(string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"The base URL must be a valid absolute HTTP or HTTPS URI. Provided value: '{baseUrl}'.",
                nameof(baseUrl));
        }
        return baseUrl;
    }

    /// <summary>
    /// Gets the <see cref="CookieContainer"/> used by the internal handler when the instance manages its own client.
    /// This allows callers to propagate cookies from an incoming HTTP request.
    /// </summary>
    public CookieContainer? CookieContainer => _handler?.CookieContainer;

    /// <summary>
    /// Executes a query using the internal HTTP client and returns the raw response.
    /// </summary>
    /// <param name="parameter">Parameters used to build the query URL.</param>
    /// <param name="skip">Number of records to skip in addition to <see cref="IQuery.Skip"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>The HTTP response message when the request succeeds.</returns>
    public Task<HttpResponseMessage?> SimpleQuery(IQuery parameter, int skip = 0, CancellationToken cancellationToken = default)
            => SimpleQuery(parameter, sourceRequest: null, skip, cancellationToken);

    /// <summary>
    /// Executes a query while copying headers and cookies from an existing HTTP request to preserve the context.
    /// </summary>
    /// <param name="parameter">Parameters used to build the query URL.</param>
    /// <param name="sourceRequest">Optional request providing HTTP headers to forward.</param>
    /// <param name="skip">Number of records to skip in addition to <see cref="IQuery.Skip"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>The HTTP response message when the request succeeds.</returns>
    public async Task<HttpResponseMessage?> SimpleQuery(IQuery parameter, HttpRequestMessage? sourceRequest = null, int skip = 0, CancellationToken cancellationToken = default)
    {
        var query = new ODataQueryBuilder(BaseUrl, parameter, skip: skip);

        HttpResponseMessage response = await HttpGet(query.Url, sourceRequest, cancellationToken);
        return response;
    }

    /// <summary>
    /// Sends a GET request using the internal <see cref="HttpClient"/> and optionally copies headers from an incoming request.
    /// </summary>
    /// <param name="url">The target URL for the GET request.</param>
    /// <param name="sourceRequest">Optional request whose headers and cookies should be copied to the outgoing request.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP operation.</param>
    private async Task<HttpResponseMessage> HttpGet(
            string url,
            HttpRequestMessage? sourceRequest = null,
            CancellationToken cancellationToken = default)
    {
        if (_httpClient is null)
            throw new InvalidOperationException("HttpClient is not initialized.");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Item 7: forward only explicitly allowed context headers; never copy Host,
        // Connection, Transfer-Encoding, content headers, or security credentials.
        // Item 8: forward per-request cookies directly on the outgoing message so they
        // are scoped to this request only and do not pollute the shared CookieContainer.
        if (sourceRequest is not null)
        {
            foreach (var header in sourceRequest.Headers)
            {
                if (AllowedForwardHeaders.Contains(header.Key))
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            // Content headers are never forwarded for a bodyless GET request.

            if (sourceRequest.Headers.TryGetValues("Cookie", out var cookieHeaders))
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieHeaders));
        }

        Debug.WriteLine($"Requesting : {url}");
        var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        return response;
    }


    /// <summary>
    /// Executes a query and converts the result to JSON, transparently downloading subsequent pages when required.
    /// </summary>
    /// <remarks>This method performs one or more HTTP requests until the requested number of items is retrieved or the
    /// service no longer returns results. Returned payloads are aggregated into a single JSON array while preserving
    /// metadata from the first successful response.</remarks>
    /// <param name="parameter">The query parameter that defines the data retrieval criteria.</param>
    /// <param name="skip">The number of records to skip in the query result. Must be non-negative.</param>
    /// <param name="maxPerRequest">Optional maximum number of records to request per HTTP call.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous processing pipeline.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a <see cref="JsonArray"/>
    /// of data and a dictionary of metadata. Returns an error if the query or conversion fails.</returns>
    public async Task<ReturnValue<(JsonArray? Datas, Dictionary<string, string>? Metadatas)>> QueryToJSon(
            IQuery parameter,
            int skip = 0,
            int? maxPerRequest = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        if (maxPerRequest.HasValue)
        {
            maxPerRequest?.ArgMustBeGreaterThan(0);
        }

        // Item 18: snapshot the query at operation entry so that a caller mutating the same
        // IQuery instance during this asynchronous operation cannot produce mixed requests.
        parameter = SnapshotQuery(parameter);

        JsonArray aggregatedValues = new();
        Dictionary<string, string>? metadatas = null;
        int totalRetrieved = 0;
        int? originalTop = parameter.Top;
        // Item 6: track the server-provided continuation link between iterations.
        string? nextLink = null;

        while (true)
        {
            int? remaining = originalTop.HasValue ? originalTop.Value - totalRetrieved : null;
            if (remaining.HasValue && remaining.Value <= 0)
            {
                break;
            }

            ReturnValue<HttpQueryResult> queryResult;
            if (nextLink is not null)
            {
                // Item 6: follow the server-provided continuation link instead of
                // recalculating the skip offset, which can miss/duplicate rows when
                // the service uses opaque skip-tokens or server-driven partitions.
                queryResult = await QueryUrl(nextLink, cancellationToken);
                nextLink = null;
            }
            else
            {
                int? requestTop = DetermineRequestTop(remaining, maxPerRequest);
                Query requestQuery = CloneQueryForPagination(parameter, requestTop);
                queryResult = await QueryDatas(requestQuery, skip + totalRetrieved, cancellationToken);
            }

            if (queryResult.IsError)
            {
                return new(queryResult.Error);
            }

            await using HttpQueryResult httpResult = queryResult.Value;
            Uri? pageUri = httpResult.ResponseUri;
            var convertResult = ConvertDatas(httpResult.Stream, allowEmpty: true);
            if (convertResult.IsError)
            {
                return convertResult;
            }

            (JsonArray? Datas, Dictionary<string, string>? Metadatas) chunk = convertResult.Value;
            if (chunk.Metadatas is not null && metadatas is null)
            {
                metadatas = new Dictionary<string, string>(chunk.Metadatas);
            }

            // Item 6: extract and validate the continuation link from the current page.
            // Resolve relative links against the page URI, not the service root (item 6 fix).
            // A present but invalid/out-of-origin link is an error — do not silently fall back.
            string? rawNextLink = ExtractNextLink(chunk.Metadatas);
            if (rawNextLink is not null)
            {
                var (resolved, linkError) = ResolveNextLink(rawNextLink, pageUri);
                if (linkError is not null)
                    return new(linkError);
                nextLink = resolved;
            }

            if (chunk.Datas is not JsonArray { Count: > 0 } chunkDataArray)
            {
                break;
            }

            // Cap the number of rows to satisfy the original $top exactly.
            // When a nextLink page contains more rows than the remaining quota
            // the excess must be discarded rather than returned to the caller.
            int rowsToTake = remaining.HasValue
                ? Math.Min(remaining.Value, chunkDataArray.Count)
                : chunkDataArray.Count;

            for (int i = 0; i < rowsToTake; i++)
                aggregatedValues.Add(chunkDataArray[i]?.DeepClone());

            totalRetrieved += rowsToTake;

            if (rowsToTake < chunkDataArray.Count)
                break; // $top satisfied; the page was larger than the remaining quota.
        }

        return new((aggregatedValues, metadatas));
    }

    /// <summary>
    /// Executes the specified query and returns an <see cref="IDataReader"/> that streams rows as they are downloaded.
    /// </summary>
    /// <remarks>The returned reader downloads additional pages in the background until the data source stops returning
    /// results or the configured limit is reached.</remarks>
    /// <param name="parameter">Query definition describing the requested dataset. Cannot be null.</param>
    /// <param name="skip">Number of rows to skip before starting to stream results.</param>
    /// <param name="maxPerRequest">Optional maximum number of rows to fetch per HTTP request.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the streaming reader when successful; otherwise an error.</returns>
    public async Task<ReturnValue<IDataReader>> QueryToDataReader(
            IQuery parameter,
            int skip = 0,
            int? maxPerRequest = null,
            CancellationToken cancellationToken = default)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        if (maxPerRequest.HasValue && maxPerRequest.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPerRequest));
        }

        // Item 18: snapshot the query at operation entry to isolate it from concurrent mutation.
        parameter = SnapshotQuery(parameter);

        int? originalTop = parameter.Top;
        int? requestTop = DetermineRequestTop(originalTop, maxPerRequest);
        Query firstQuery = CloneQueryForPagination(parameter, requestTop);

        var queryResult = await QueryDatas(firstQuery, skip, cancellationToken);
        if (queryResult.IsError)
        {
            return new(queryResult.Error);
        }

        await using HttpQueryResult firstHttpResult = queryResult.Value;
        Uri? firstPageUri = firstHttpResult.ResponseUri;
        var convertResult = ConvertDatas(firstHttpResult.Stream, allowEmpty: true);
        if (convertResult.IsError)
        {
            return new(convertResult.Error);
        }

        (JsonArray? Datas, Dictionary<string, string>? Metadatas) firstChunk = convertResult.Value;

        // When the service returns no rows, build a valid empty reader without starting the streaming task.
        if (firstChunk.Datas is not JsonArray { Count: > 0 } firstBatch)
        {
            string[] selectedColumns = [];
            string? selection = parameter.Select;
            if (!string.IsNullOrWhiteSpace(selection)
                    && !string.Equals(selection.Trim(), "*", StringComparison.Ordinal))
            {
                selectedColumns = selection.Split(',').Select(c => c.Trim()).ToArray();
            }

            IReadOnlyList<ColumnDefinition> emptyColumns = [];
            if (selectedColumns.Length > 0)
            {
                var emptyMeta = await GetMetadataFromBaseAsync(cancellationToken);
                // A metadata failure is intentionally ignored here: an empty result set is a
                // valid OData response and must not become an error just because the schema
                // endpoint is unavailable.  Columns fall back to generic (object/DBNull)
                // types.  The non-empty path, by contrast, does propagate metadata errors
                // because type converters are required to materialise rows correctly.
                Edmx? emptyEdmx = emptyMeta.IsError ? null : emptyMeta.Value;
                string? emptyEntity = ExtractEntityName(firstChunk.Metadatas, parameter);
                EntityType? emptyType = emptyEdmx is not null ? ResolveEntityType(emptyEdmx, emptyEntity) : null;
                emptyColumns = BuildColumnDefinitions(selectedColumns, emptyType, emptyEdmx);
            }

            var emptyChannel = Channel.CreateBounded<object?[]>(1);
            emptyChannel.Writer.TryComplete();
            CancellationTokenSource emptyLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return new(new ODataStreamingDataReader(emptyColumns, emptyChannel.Reader, emptyLinkedCts, Task.CompletedTask));
        }

        var metadataResult = await GetMetadataFromBaseAsync(cancellationToken);
        if (metadataResult.IsError)
        {
            return new(metadataResult.Error);
        }

        Edmx metadata = metadataResult.Value;
        string[] columnNames = GetColumns(parameter, firstBatch);
        string? entityName = ExtractEntityName(firstChunk.Metadatas, parameter);
        EntityType? entityType = ResolveEntityType(metadata, entityName);
        IReadOnlyList<ColumnDefinition> columns = BuildColumnDefinitions(columnNames, entityType, metadata);
        Func<JsonObject, object[]> rowConverter = CompileRowConverter(columns);

        int boundedCapacity = Math.Max(1, Math.Min(maxPerRequest ?? 128, 1024));
        var channelOptions = new BoundedChannelOptions(boundedCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        Channel<object?[]> channel = Channel.CreateBounded<object?[]>(channelOptions);
        CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Item 6: resolve the continuation link from the first page against the first page's
        // own URI so that path-relative links preserve the entity path.
        string? initialNextLink = null;
        string? rawInitialNextLink = ExtractNextLink(firstChunk.Metadatas);
        if (rawInitialNextLink is not null)
        {
            var (resolved, linkError) = ResolveNextLink(rawInitialNextLink, firstPageUri);
            if (linkError is not null)
                return new(linkError);
            initialNextLink = resolved;
        }

        Task streamingTask = StreamBatchesAsync(
                parameter,
                skip,
                maxPerRequest,
                originalTop,
                firstBatch,
                columns,
                rowConverter,
                channel.Writer,
                linkedTokenSource.Token,
                initialNextLink);

        var reader = new ODataStreamingDataReader(columns, channel.Reader, linkedTokenSource, streamingTask);
        return new(reader);
    }

    /// <summary>
    /// Streams the initial and subsequent batches of JSON data to the specified channel writer.
    /// </summary>
    /// <param name="parameter">Query definition used to build subsequent requests.</param>
    /// <param name="skip">Number of rows already skipped before the first batch.</param>
    /// <param name="maxPerRequest">Optional maximum number of rows to request per HTTP call.</param>
    /// <param name="originalTop">Original <c>$top</c> value requested by the consumer.</param>
    /// <param name="firstBatch">First batch of rows returned by the service.</param>
    /// <param name="columns">Column definitions describing the result schema.</param>
    /// <param name="rowConverter">Compiled delegate used to materialize JSON objects into row buffers.</param>
    /// <param name="writer">Channel writer that receives the materialized rows.</param>
    /// <param name="cancellationToken">Token used to cancel the streaming operation.</param>
    /// <param name="nextLink">Optional server-provided continuation URL extracted from the first page (item 6).</param>
    private async Task StreamBatchesAsync(
            IQuery parameter,
            int skip,
            int? maxPerRequest,
            int? originalTop,
            JsonArray firstBatch,
            IReadOnlyList<ColumnDefinition> columns,
            Func<JsonObject, object[]> rowConverter,
            ChannelWriter<object?[]> writer,
            CancellationToken cancellationToken,
            string? nextLink = null)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(firstBatch);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rowConverter);
        ArgumentNullException.ThrowIfNull(writer);

        try
        {
            // Cap the first batch to originalTop so that a server-side page larger than the
            // requested limit does not cause the reader to return excess rows.
            int firstBatchRows = originalTop.HasValue
                ? Math.Min(originalTop.Value, firstBatch.Count)
                : firstBatch.Count;
            await WriteBatchAsync(firstBatch, columns, rowConverter, writer, cancellationToken, firstBatchRows);
            int totalRetrieved = firstBatchRows;

            while (true)
            {
                int? remaining = originalTop.HasValue ? originalTop.Value - totalRetrieved : null;
                if (remaining.HasValue && remaining.Value <= 0)
                {
                    break;
                }

                ReturnValue<HttpQueryResult> queryResult;
                if (nextLink is not null)
                {
                    // Item 6: follow the server-provided continuation link.
                    queryResult = await QueryUrl(nextLink, cancellationToken);
                    nextLink = null;
                }
                else
                {
                    int? requestTop = DetermineRequestTop(remaining, maxPerRequest);
                    Query nextQuery = CloneQueryForPagination(parameter, requestTop);
                    queryResult = await QueryDatas(nextQuery, skip + totalRetrieved, cancellationToken);
                }

                if (queryResult.IsError)
                {
                    throw new ODataDataReaderException(queryResult.Error);
                }

                await using HttpQueryResult httpResult = queryResult.Value;
                Uri? pageUri = httpResult.ResponseUri;
                var convertResult = ConvertDatas(httpResult.Stream, allowEmpty: true);
                if (convertResult.IsError)
                {
                    throw new ODataDataReaderException(convertResult.Error);
                }

                // Item 6: resolve the continuation link against the page URI that provided it
                // so that path-relative and query-string-only links work correctly.
                string? rawChunkNextLink = ExtractNextLink(convertResult.Value.Metadatas);
                if (rawChunkNextLink is not null)
                {
                    var (resolved, linkError) = ResolveNextLink(rawChunkNextLink, pageUri);
                    if (linkError is not null)
                        throw new ODataDataReaderException(linkError);
                    nextLink = resolved;
                }

                JsonArray? batch = convertResult.Value.Datas;
                if (batch is not JsonArray { Count: > 0 } nonEmptyBatch)
                {
                    break;
                }

                // Cap the batch to the remaining quota so that a server-driven page larger
                // than the requested $top does not cause the reader to emit excess rows.
                int rowsToWrite = remaining.HasValue
                    ? Math.Min(remaining.Value, nonEmptyBatch.Count)
                    : nonEmptyBatch.Count;
                await WriteBatchAsync(nonEmptyBatch, columns, rowConverter, writer, cancellationToken, rowsToWrite);
                totalRetrieved += rowsToWrite;
                if (rowsToWrite < nonEmptyBatch.Count)
                    break; // $top satisfied; the page was larger than the remaining quota.
            }

            writer.TryComplete();
        }
        catch (OperationCanceledException oce)
        {
            writer.TryComplete(oce);
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    /// <summary>
    /// Writes up to <paramref name="maxRows"/> entries from <paramref name="batch"/> to the channel.
    /// </summary>
    /// <param name="batch">JSON array containing the rows to materialize.</param>
    /// <param name="columns">Column definitions describing the schema of the dataset.</param>
    /// <param name="rowConverter">Compiled converter used to materialize JSON rows.</param>
    /// <param name="writer">Channel writer that receives the materialized rows.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    /// <param name="maxRows">
    /// Maximum number of rows to write. When <see langword="null"/>, all entries are written.
    /// Used to enforce <c>$top</c> when a server-driven page is larger than the remaining quota.
    /// </param>
    private static async Task WriteBatchAsync(
            JsonArray batch,
            IReadOnlyList<ColumnDefinition> columns,
            Func<JsonObject, object[]> rowConverter,
            ChannelWriter<object?[]> writer,
            CancellationToken cancellationToken,
            int? maxRows = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rowConverter);
        ArgumentNullException.ThrowIfNull(writer);

        int limit = maxRows.HasValue ? Math.Min(maxRows.Value, batch.Count) : batch.Count;
        for (int rowIndex = 0; rowIndex < limit; rowIndex++)
        {
            JsonNode? entry = batch[rowIndex];
            if (entry is not JsonObject jsonObject)
            {
                throw new InvalidOperationException(
                    $"Row {rowIndex} in the OData response is not a JSON object (actual type: {entry?.GetValueKind().ToString() ?? "null"}). " +
                    "Only object entries are supported in the value array.");
            }
            object[] row = rowConverter(jsonObject);
            await writer.WriteAsync(row, cancellationToken);
        }
    }


    /// <summary>
    /// Attempts to determine the entity name associated with the current dataset.
    /// </summary>
    /// <param name="metadata">Metadata dictionary extracted from the JSON payload.</param>
    /// <param name="parameter">Query definition used to retrieve the dataset.</param>
    /// <returns>The inferred entity type name when available; otherwise <see langword="null"/>.</returns>
    private static string? ExtractEntityName(Dictionary<string, string>? metadata, IQuery parameter)
    {
        if (metadata is not null)
        {
            if (metadata.TryGetValue("@odata.context", out string? contextValue))
            {
                string? contextName = ParseEntityNameFromContext(contextValue);
                if (!string.IsNullOrWhiteSpace(contextName))
                {
                    return contextName;
                }
            }

            if (metadata.TryGetValue("@odata.metadata", out string? metadataValue))
            {
                string? metadataName = ParseEntityNameFromContext(metadataValue);
                if (!string.IsNullOrWhiteSpace(metadataName))
                {
                    return metadataName;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(parameter.Table))
        {
            string table = parameter.Table;
            int slashIndex = table.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                table = table[(slashIndex + 1)..];
            }

            int dotIndex = table.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                table = table[(dotIndex + 1)..];
            }

            return table;
        }

        return null;
    }

    /// <summary>
    /// Normalizes a metadata fragment to extract the entity name portion.
    /// </summary>
    /// <param name="context">Metadata fragment describing the entity set or type.</param>
    /// <returns>The extracted entity name when it can be determined; otherwise <see langword="null"/>.</returns>
    private static string? ParseEntityNameFromContext(string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return null;
        }

        int hashIndex = context.IndexOf('#');
        string fragment = hashIndex >= 0 ? context[(hashIndex + 1)..] : context;

        int questionIndex = fragment.IndexOf('?');
        if (questionIndex >= 0)
        {
            fragment = fragment[..questionIndex];
        }

        if (fragment.StartsWith("Collection(", StringComparison.OrdinalIgnoreCase)
                && fragment.EndsWith(")", StringComparison.Ordinal))
        {
            fragment = fragment.Substring("Collection(".Length, fragment.Length - "Collection(".Length - 1);
        }

        int slashIndex = fragment.LastIndexOf('/');
        if (slashIndex >= 0)
        {
            fragment = fragment[(slashIndex + 1)..];
        }

        int dotIndex = fragment.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            fragment = fragment[(dotIndex + 1)..];
        }

        int parenthesisIndex = fragment.IndexOf('(');
        if (parenthesisIndex >= 0)
        {
            fragment = fragment[..parenthesisIndex];
        }

        fragment = fragment.Trim();
        return fragment.Length == 0 ? null : fragment;
    }

    /// <summary>
    /// Resolves the metadata entity type matching the supplied entity name.
    /// </summary>
    /// <param name="metadata">Metadata document describing the service.</param>
    /// <param name="entityName">Entity name extracted from the payload or query.</param>
    /// <returns>The matching <see cref="EntityType"/> when found; otherwise <see langword="null"/>.</returns>
    private static EntityType? ResolveEntityType(Edmx metadata, string? entityName)
    {
        if (metadata?.DataServices is null || string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        foreach (DataServices dataServices in metadata.DataServices)
        {
            if (dataServices?.Schemas is null)
            {
                continue;
            }

            foreach (Schema schema in dataServices.Schemas)
            {
                if (schema?.EntityTypes is null)
                {
                    continue;
                }

                foreach (EntityType entityType in schema.EntityTypes)
                {
                    if (entityType is null || string.IsNullOrWhiteSpace(entityType.Name))
                    {
                        continue;
                    }

                    if (string.Equals(entityType.Name, entityName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entityType;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the metadata property associated with the specified column name.
    /// </summary>
    /// <param name="metadata">Metadata document describing the service.</param>
    /// <param name="entityType">Entity type inferred from the query.</param>
    /// <param name="columnName">Column name that requires metadata information.</param>
    /// <returns>The matching property when available; otherwise <see langword="null"/>.</returns>
    /// <summary>
    /// Resolves the metadata property for the given column within the specified entity type only.
    /// </summary>
    /// <remarks>
    /// Item 45: the previous implementation fell back to a global name-only scan across every
    /// entity type in the schema when the property was not found in <paramref name="entityType"/>.
    /// A common name such as <c>Id</c> or <c>Name</c> could therefore borrow the EDM type of an
    /// unrelated entity, causing wrong <c>FieldType</c> values and incorrect converters.  The
    /// global fallback has been removed; if the property is not declared on the resolved entity
    /// type the converter falls back to the default (string/untyped).
    /// </remarks>
    private static Property? ResolveProperty(Edmx? metadata, EntityType? entityType, string columnName)
    {
        if (entityType?.Properties is null)
            return null;

        foreach (Property property in entityType.Properties)
        {
            if (property is null || string.IsNullOrWhiteSpace(property.Name))
                continue;

            if (string.Equals(property.Name, columnName, StringComparison.OrdinalIgnoreCase))
                return property;
        }

        return null;
    }

    /// <summary>
    /// Builds the column definitions describing the schema of the streamed dataset.
    /// </summary>
    /// <param name="columnNames">Ordered collection of column names requested by the query.</param>
    /// <param name="entityType">Entity type inferred for the dataset.</param>
    /// <param name="metadata">Metadata document describing the service.</param>
    /// <returns>A read-only collection containing the computed column definitions.</returns>
    private static IReadOnlyList<ColumnDefinition> BuildColumnDefinitions(
            IReadOnlyList<string> columnNames,
            EntityType? entityType,
            Edmx? metadata)
    {
        if (columnNames is null)
        {
            throw new ArgumentNullException(nameof(columnNames));
        }

        List<ColumnDefinition> columns = new(columnNames.Count);
        for (int index = 0; index < columnNames.Count; index++)
        {
            string name = columnNames[index];
            Property? property = ResolveProperty(metadata, entityType, name);
            EdmFieldConverterRegistry.EdmFieldConverter converter = EdmFieldConverterRegistry.Resolve(property);
            // Item 54: propagate EDM Nullable facet; default true when metadata is absent.
            bool allowDbNull = property?.IsNullable ?? true;
            columns.Add(new ColumnDefinition(name, converter.ClrType, index, converter.Converter, allowDbNull));
        }

        return columns;
    }

    /// <summary>
    /// Compiles an optimized delegate able to materialize JSON objects into typed row arrays.
    /// </summary>
    /// <param name="columns">Column definitions describing the dataset schema.</param>
    /// <returns>A delegate converting <see cref="JsonObject"/> instances into typed row buffers.</returns>
    private static Func<JsonObject, object[]> CompileRowConverter(IReadOnlyList<ColumnDefinition> columns)
    {
        if (columns is null)
        {
            throw new ArgumentNullException(nameof(columns));
        }

        ParameterExpression jsonObjectParameter = Expression.Parameter(typeof(JsonObject), "jsonObject");
        ParameterExpression valuesVariable = Expression.Variable(typeof(object[]), "values");

        MethodInfo? resolverMethod = typeof(QueryOData).GetMethod(
                nameof(TryResolvePropertyValue),
                BindingFlags.NonPublic | BindingFlags.Static);
        if (resolverMethod is null)
        {
            throw new InvalidOperationException("Unable to locate the JSON property resolution helper.");
        }

        List<Expression> expressions = new(columns.Count + 2)
        {
            Expression.Assign(
                    valuesVariable,
                    Expression.NewArrayBounds(typeof(object), Expression.Constant(columns.Count)))
        };

        for (int index = 0; index < columns.Count; index++)
        {
            ColumnDefinition column = columns[index];
            Expression nodeExpression = Expression.Call(
                    resolverMethod,
                    jsonObjectParameter,
                    Expression.Constant(column.Name));
            Expression convertExpression = Expression.Invoke(Expression.Constant(column.Converter), nodeExpression);
            expressions.Add(Expression.Assign(Expression.ArrayAccess(valuesVariable, Expression.Constant(index)), convertExpression));
        }

        expressions.Add(valuesVariable);

        Expression body = Expression.Block(new[] { valuesVariable }, expressions);
        return Expression.Lambda<Func<JsonObject, object[]>>(body, jsonObjectParameter).Compile();
    }

    /// <summary>
    /// Attempts to retrieve the JSON node matching the specified property name, ignoring case differences.
    /// </summary>
    /// <param name="jsonObject">JSON object containing the row data.</param>
    /// <param name="propertyName">Name of the property to retrieve.</param>
    /// <returns>The matching JSON node when available; otherwise <see langword="null"/>.</returns>
    private static JsonNode? TryResolvePropertyValue(JsonObject jsonObject, string propertyName)
    {
        // Item 53: use ordinal (case-sensitive) lookup first — OData property names are
        // case-sensitive identifiers.
        if (jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return value;
        }

        // Case-insensitive fallback for servers that deviate from the spec.
        // If multiple keys differ only by case, the lookup is ambiguous and we return null
        // rather than silently picking an arbitrary match.
        JsonNode? candidate = null;
        bool ambiguous = false;
        foreach (KeyValuePair<string, JsonNode?> entry in jsonObject)
        {
            if (string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (candidate is not null)
                {
                    ambiguous = true;
                    break;
                }

                candidate = entry.Value;
            }
        }

        return ambiguous ? null : candidate;
    }

    /// <summary>
    /// Creates an array filled with <see cref="DBNull.Value"/> for the specified field count.
    /// </summary>
    /// <param name="fieldCount">Number of fields in the row.</param>
    /// <returns>An array populated with <see cref="DBNull.Value"/>.</returns>
    private static object[] CreateEmptyRow(int fieldCount)
    {
        if (fieldCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldCount));
        }

        object[] values = new object[fieldCount];
        Array.Fill(values, DBNull.Value);
        return values;
    }

    /// <summary>
    /// Represents a column definition used by the streaming data reader.
    /// </summary>
    /// <param name="Name">Name of the column.</param>
    /// <param name="FieldType">CLR type associated with the column.</param>
    /// <param name="Ordinal">Zero-based ordinal assigned to the column.</param>
    /// <param name="Converter">Converter used to materialize JSON nodes into column values.</param>
    /// <param name="AllowDbNull">
    /// Indicates whether the column allows <see cref="DBNull"/> values.
    /// Derived from the EDM <c>Nullable</c> facet when metadata is available;
    /// defaults to <see langword="true"/> (item 54).
    /// </param>
    private sealed record ColumnDefinition(string Name, Type FieldType, int Ordinal, Func<JsonNode?, object> Converter, bool AllowDbNull = true);

    /// <summary>
    /// Pairs an HTTP response body stream with the final effective request URI so that relative
    /// <c>@odata.nextLink</c> values can be resolved against the correct page.
    /// </summary>
    private sealed record HttpQueryResult(Stream Stream, Uri? ResponseUri) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }

    /// <summary>
    /// Raised by <see cref="ReadBoundedAsync"/> when the stream exceeds the configured byte limit.
    /// Caught exclusively in <see cref="FetchMetadataAsync"/> to produce a uniform error return value.
    /// </summary>
    private sealed class MetadataSizeLimitException : Exception
    {
        public MetadataSizeLimitException(int maxBytes)
            : base($"Metadata response stream exceeded the {maxBytes / (1024 * 1024)} MiB size limit.") { }
    }

    /// <summary>
    /// Represents an error raised while streaming paginated OData results.
    /// </summary>
    private sealed class ODataDataReaderException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ODataDataReaderException"/> class.
        /// </summary>
        /// <param name="error">Error returned by the underlying pipeline.</param>
        public ODataDataReaderException(ErrorReturnValue error)
                : base(error.message)
        {
            Error = error;
        }

        /// <summary>
        /// Gets the error returned by the streaming pipeline.
        /// </summary>
        public ErrorReturnValue Error { get; }
    }

    /// <summary>
    /// Streams OData query results through the <see cref="IDataReader"/> interface.
    /// </summary>
    private sealed class ODataStreamingDataReader : IDataReader
    {
        private readonly IReadOnlyList<ColumnDefinition> _columns;
        private readonly ChannelReader<object?[]> _reader;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task _backgroundTask;
        private readonly Dictionary<string, int> _ordinals;
        private object?[]? _currentRow;
        private bool _isClosed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataStreamingDataReader"/> class.
        /// </summary>
        /// <param name="columns">Column definitions describing the schema of the dataset.</param>
        /// <param name="reader">Channel reader providing materialized rows.</param>
        /// <param name="cancellationSource">Cancellation token source linked to the reader lifetime.</param>
        /// <param name="backgroundTask">Task responsible for streaming additional batches.</param>
        public ODataStreamingDataReader(
                IReadOnlyList<ColumnDefinition> columns,
                ChannelReader<object?[]> reader,
                CancellationTokenSource cancellationSource,
                Task backgroundTask)
        {
            _columns = columns ?? throw new ArgumentNullException(nameof(columns));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _cancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));
            _backgroundTask = backgroundTask ?? throw new ArgumentNullException(nameof(backgroundTask));
            _ordinals = new Dictionary<string, int>(columns.Count, StringComparer.OrdinalIgnoreCase);
            foreach (ColumnDefinition column in columns)
            {
                if (!_ordinals.TryAdd(column.Name, column.Ordinal))
                {
                    throw new ArgumentException(
                        $"Duplicate column name '{column.Name}' (case-insensitive) detected in the schema. " +
                        "Each column must have a unique name.");
                }
            }
        }

        /// <inheritdoc />
        public int FieldCount => _columns.Count;

        /// <inheritdoc />
        public bool IsClosed => _isClosed;

        /// <inheritdoc />
        public int Depth => 0;

        /// <inheritdoc />
        public int RecordsAffected => -1;

        /// <inheritdoc />
        public object this[int i] => GetValue(i);

        /// <inheritdoc />
        public object this[string name] => GetValue(GetOrdinal(name));

        /// <inheritdoc />
        public void Close()
        {
            Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(disposing: true);
        }

        /// <summary>
        /// Releases managed resources.
        /// </summary>
        /// <param name="disposing">Always <see langword="true"/>; kept for extensibility.</param>
        private void Dispose(bool disposing)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            _cancellationSource.Cancel();
            if (disposing)
            {
                try
                {
                    _backgroundTask.Wait();
                }
                catch (AggregateException aggregate)
                        when (aggregate.InnerExceptions.Count == 1
                              && aggregate.InnerExceptions[0] is OperationCanceledException)
                {
                    // Suppress only cancellation that was initiated by this Dispose call.
                }
                catch (OperationCanceledException)
                {
                    // Suppress only cancellation that was initiated by this Dispose call.
                }

                _cancellationSource.Dispose();
            }
        }

        /// <inheritdoc />
        public DataTable? GetSchemaTable()
        {
            var table = new DataTable();
            table.Columns.Add("ColumnName", typeof(string));
            table.Columns.Add("ColumnOrdinal", typeof(int));
            table.Columns.Add("DataType", typeof(Type));
            table.Columns.Add("AllowDBNull", typeof(bool));

            foreach (ColumnDefinition column in _columns)
            {
                DataRow row = table.NewRow();
                row["ColumnName"] = column.Name;
                row["ColumnOrdinal"] = column.Ordinal;
                row["DataType"] = column.FieldType;
                row["AllowDBNull"] = column.AllowDbNull;
                table.Rows.Add(row);
            }

            return table;
        }

        /// <inheritdoc />
        public bool NextResult() => false;

        /// <inheritdoc />
        public bool Read()
        {
            EnsureNotClosed();

            try
            {
                object?[] row = _reader.ReadAsync(_cancellationSource.Token).AsTask().GetAwaiter().GetResult();
                _currentRow = row;
                return true;
            }
            catch (ChannelClosedException channelClosedException)
            {
                _currentRow = null;
                if (channelClosedException.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(channelClosedException.InnerException).Throw();
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                _currentRow = null;
                throw;
            }
        }

        /// <inheritdoc />
        public int GetOrdinal(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (_ordinals.TryGetValue(name, out int ordinal))
            {
                return ordinal;
            }

            throw new IndexOutOfRangeException($"Column \"{name}\" was not found.");
        }

        /// <inheritdoc />
        public string GetName(int i)
        {
            return _columns[i].Name;
        }

        /// <inheritdoc />
        public Type GetFieldType(int i)
        {
            return _columns[i].FieldType;
        }

        /// <inheritdoc />
        public string GetDataTypeName(int i)
        {
            return GetFieldType(i).Name;
        }

        /// <inheritdoc />
        public object GetValue(int i)
        {
            EnsureHasCurrentRow();
            return _currentRow![i];
        }

        /// <inheritdoc />
        public int GetValues(object[] values)
        {
            if (values is null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            EnsureHasCurrentRow();
            int count = Math.Min(values.Length, _columns.Count);
            Array.Copy(_currentRow!, values, count);
            return count;
        }

        /// <inheritdoc />
        public bool IsDBNull(int i)
        {
            return GetValue(i) is DBNull;
        }

        /// <inheritdoc />
        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public byte GetByte(int i) => Convert.ToByte(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
        {
            if (fieldOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(fieldOffset), "Field offset must be non-negative.");
            if (fieldOffset > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(fieldOffset), "Field offset exceeds the supported int range.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

            object value = GetValue(i);
            if (value is DBNull)
                return 0;

            if (value is not byte[] data)
                throw new InvalidCastException($"Column {i} does not contain binary data.");

            if (buffer is null)
                return data.Length;

            if (bufferoffset < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferoffset), "Buffer offset must be non-negative.");
            if (bufferoffset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferoffset), "Buffer offset exceeds buffer length.");
            if (length > buffer.Length - bufferoffset)
                throw new ArgumentException(
                    "The requested copy length exceeds the available buffer capacity (bufferoffset + length > buffer.Length).",
                    nameof(length));

            int sourceOffset = (int)fieldOffset;
            int available = Math.Max(0, data.Length - sourceOffset);
            int count = Math.Min(available, length);
            if (count > 0)
                Array.Copy(data, sourceOffset, buffer, bufferoffset, count);

            return count;
        }

        /// <inheritdoc />
        public char GetChar(int i) => Convert.ToChar(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
        {
            if (fieldoffset < 0)
                throw new ArgumentOutOfRangeException(nameof(fieldoffset), "Field offset must be non-negative.");
            if (fieldoffset > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(fieldoffset), "Field offset exceeds the supported int range.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

            string data = GetString(i);

            if (buffer is null)
                return data.Length;

            if (bufferoffset < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferoffset), "Buffer offset must be non-negative.");
            if (bufferoffset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferoffset), "Buffer offset exceeds buffer length.");
            if (length > buffer.Length - bufferoffset)
                throw new ArgumentException(
                    "The requested copy length exceeds the available buffer capacity (bufferoffset + length > buffer.Length).",
                    nameof(length));

            int sourceOffset = (int)fieldoffset;
            int available = Math.Max(0, data.Length - sourceOffset);
            int count = Math.Min(available, length);
            if (count > 0)
                data.CopyTo(sourceOffset, buffer, bufferoffset, count);

            return count;
        }

        /// <inheritdoc />
        public Guid GetGuid(int i)
        {
            object value = GetValue(i);
            return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        /// <inheritdoc />
        public short GetInt16(int i) => Convert.ToInt16(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public long GetInt64(int i) => Convert.ToInt64(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public float GetFloat(int i) => Convert.ToSingle(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public double GetDouble(int i) => Convert.ToDouble(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public string GetString(int i)
        {
            object value = GetValue(i);
            return value is DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <inheritdoc />
        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public DateTime GetDateTime(int i)
        {
            object value = GetValue(i);
            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
                // Edm.Date is materialised as DateOnly (item 23); convert to midnight DateTime.
                DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            };
        }

        /// <inheritdoc />
        public IDataReader GetData(int i)
        {
            throw new NotSupportedException("Nested readers are not supported.");
        }

        /// <summary>
        /// Ensures the reader is not closed before performing an operation.
        /// </summary>
        private void EnsureNotClosed()
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("The data reader has been closed.");
            }
        }

        /// <summary>
        /// Ensures a row has been materialized before attempting to access column values.
        /// </summary>
        private void EnsureHasCurrentRow()
        {
            if (_currentRow is null)
            {
                throw new InvalidOperationException("Call Read() before accessing column values.");
            }
        }
    }

    /// <summary>
    /// Converts the data from the provided stream into a JSON array and extracts metadata.
    /// </summary>
    /// <remarks>The method parses the JSON content from the stream, expecting an array under the "value" key and
    /// additional metadata as key-value pairs. When <paramref name="allowEmpty"/> is <see langword="false"/>, the method
    /// returns an error when the array is empty.</remarks>
    /// <param name="stream">The input stream containing JSON data to be parsed.</param>
    /// <param name="allowEmpty">Indicates whether an empty payload should be treated as a valid result.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing a tuple with a <see cref="JsonArray"/> of data and a dictionary of
    /// metadata. Returns an error code and message if no data is found.</returns>
    private static ReturnValue<(JsonArray? Datas, Dictionary<string, string>? Metadatas)> ConvertDatas(Stream stream, bool allowEmpty = false)
    {
        var content = JsonNode.Parse(stream);
        var array = content?["value"]?.AsArray();

        var metadatas = content?.AsObject()
                .Where(e => e.Key != "value")
                .ToDictionary(e => e.Key, e => e.Value?.ToString() ?? string.Empty);

        array ??= new JsonArray();

        if (array.Count == 0 && !allowEmpty)
        {
            return new(1, "No data returned.");
        }

        return new((array, metadatas));
    }

    /// <summary>
    /// Creates an immutable snapshot copy of the supplied query so that later mutation of the
    /// caller's instance cannot affect a running asynchronous operation (item 18).
    /// </summary>
    /// <param name="source">Query to snapshot.</param>
    /// <returns>A detached <see cref="Query"/> carrying the same values as <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    private static Query SnapshotQuery(IQuery source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return CloneQueryForPagination(source, source.Top);
    }

    /// <summary>
    /// Creates a clone of the provided query with an updated <c>$top</c> value for pagination.
    /// </summary>
    /// <param name="source">Source query used as the template for the request.</param>
    /// <param name="requestTop">Top value applied to the cloned query.</param>
    /// <returns>A new <see cref="Query"/> containing the same parameters as <paramref name="source"/>.</returns>
    private static Query CloneQueryForPagination(IQuery source, int? requestTop)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Query
        {
            Table = source.Table,
            Filters = source.Filters,
            Count = source.Count,
            Top = requestTop,
            Skip = source.Skip,
            OrderBy = source.OrderBy,
            Select = source.Select,
            Search = source.Search
        };
    }

    /// <summary>
    /// Stream implementation that forwards operations to the underlying HTTP content while disposing the response when closed.
    /// </summary>
    private sealed class HttpResponseContentStream : Stream, IAsyncDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _innerStream;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseContentStream"/> class.
        /// </summary>
        /// <param name="response">HTTP response whose lifetime is bound to the stream.</param>
        /// <param name="innerStream">Underlying stream exposing the response payload.</param>
        public HttpResponseContentStream(HttpResponseMessage response, Stream innerStream)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        }

        /// <inheritdoc />
        public override bool CanRead => _innerStream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => _innerStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => _innerStream.CanWrite;

        /// <inheritdoc />
        public override long Length => _innerStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            EnsureNotDisposed();
            _innerStream.Flush();
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            return _innerStream.FlushAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureNotDisposed();
            return _innerStream.Read(buffer, offset, count);
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            EnsureNotDisposed();
            return _innerStream.Read(buffer);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerStream.ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureNotDisposed();
            return _innerStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            EnsureNotDisposed();
            _innerStream.SetLength(value);
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureNotDisposed();
            _innerStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureNotDisposed();
            _innerStream.Write(buffer);
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerStream.WriteAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public override void Close()
        {
            Dispose(disposing: true);
            base.Close();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                _innerStream.Dispose();
                _response.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                await base.DisposeAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                if (_innerStream is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _innerStream.Dispose();
                }
            }
            finally
            {
                _response.Dispose();
                _disposed = true;
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Ensures the stream has not already been disposed before accessing the underlying content.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpResponseContentStream));
            }
        }
    }

    /// <summary>
    /// Determines the top value to apply to the next HTTP request while paginating results.
    /// </summary>
    /// <param name="remaining">Number of records remaining to satisfy the original <c>$top</c> value.</param>
    /// <param name="maxPerRequest">Optional cap for the number of records to download per HTTP request.</param>
    /// <returns>The <c>$top</c> value to apply to the next request.</returns>
    private static int? DetermineRequestTop(int? remaining, int? maxPerRequest)
    {
        if (remaining.HasValue)
        {
            int candidate = remaining.Value;
            if (maxPerRequest.HasValue)
            {
                candidate = Math.Min(candidate, maxPerRequest.Value);
            }

            return candidate;
        }

        return maxPerRequest;
    }

    /// <summary>
    /// Extracts the <c>@odata.nextLink</c> continuation URL from response metadata (item 6).
    /// </summary>
    private static string? ExtractNextLink(Dictionary<string, string>? metadata)
    {
        if (metadata is null) return null;
        return metadata.TryGetValue("@odata.nextLink", out string? link) && !string.IsNullOrWhiteSpace(link)
            ? link : null;
    }

    /// <summary>
    /// Resolves a raw <c>@odata.nextLink</c> value to an absolute URI and validates that it
    /// stays within the same HTTP origin as <paramref name="baseUri"/> (item 6 / item 30).
    /// Exposed as <c>internal static</c> so the resolution behaviour can be unit-tested without
    /// an HTTP server.
    /// </summary>
    /// <param name="rawLink">Raw value of the <c>@odata.nextLink</c> property.</param>
    /// <param name="contextUri">
    /// Effective URI of the response page that provided the nextLink.
    /// Relative links (e.g. <c>Products?$skiptoken=abc</c> or <c>?$skiptoken=abc</c>) are
    /// resolved against this URI so that path segments like <c>/odata/</c> are preserved.
    /// When <see langword="null"/>, falls back to <paramref name="baseUri"/>.
    /// </param>
    /// <param name="baseUri">Absolute base URI of the service; used exclusively for same-origin validation.</param>
    /// <returns>
    /// The resolved absolute link string, or an explicit error when the link is unparseable,
    /// unresolvable, or outside the allowed origin.
    /// </returns>
    internal static (string? resolvedLink, ErrorReturnValue? error) ResolveAndValidateNextLink(
        string rawLink, Uri? contextUri, Uri baseUri)
    {
        Uri? nextUri;
        if (!Uri.TryCreate(rawLink, UriKind.Absolute, out nextUri))
        {
            // Relative link: resolve against the context URI (the page that provided the nextLink)
            // so that path-relative and query-string-only links preserve the correct entity path.
            // Fall back to the service base URI when the context is not available.
            Uri resolveBase = contextUri ?? baseUri;
            if (!Uri.TryCreate(resolveBase, rawLink, out nextUri))
                return (null, new ErrorReturnValue(-6,
                    $"The @odata.nextLink value '{rawLink}' could not be resolved to a valid URI."));
        }

        // Same-origin check is always performed against the configured service base URL.
        if (!string.Equals(nextUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(nextUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || nextUri.Port != baseUri.Port)
        {
            return (null, new ErrorReturnValue(-6,
                $"The @odata.nextLink '{nextUri.GetLeftPart(UriPartial.Authority)}' is outside the allowed service origin and was rejected."));
        }

        return (nextUri.ToString(), null);
    }

    /// <summary>
    /// Instance wrapper around <see cref="ResolveAndValidateNextLink"/> that supplies the
    /// configured <see cref="BaseUrl"/> as the origin anchor (item 6).
    /// </summary>
    /// <param name="rawLink">Raw value of the <c>@odata.nextLink</c> property.</param>
    /// <param name="contextUri">
    /// Effective URI of the response page that contained the nextLink; used to resolve
    /// relative continuation links correctly.
    /// </param>
    private (string? resolvedLink, ErrorReturnValue? error) ResolveNextLink(string rawLink, Uri? contextUri)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? baseUri))
            return (null, new ErrorReturnValue(-6,
                "The service base URL is invalid; cannot validate @odata.nextLink origin."));
        return ResolveAndValidateNextLink(rawLink, contextUri, baseUri);
    }

    /// <summary>
    /// Fetches a raw URL and wraps the response as an <see cref="HttpQueryResult"/> (item 6).
    /// Used to follow <c>@odata.nextLink</c> continuation links; the effective request URI is
    /// captured from <see cref="HttpRequestMessage.RequestUri"/> so relative nextLinks on the
    /// returned page can be resolved correctly.
    /// </summary>
    private async Task<ReturnValue<HttpQueryResult>> QueryUrl(string url, CancellationToken cancellationToken = default)
    {
        var response = await HttpGet(url, sourceRequest: null, cancellationToken);
        return await ValidateAndWrapResponse(response, cancellationToken);
    }

    /// <summary>
    /// Centralizes HTTP response validation and body extraction so that transport success/error
    /// handling lives in one place instead of being duplicated across query methods (item 19).
    /// On failure the response is disposed and a categorized <see cref="ErrorReturnValue"/> is
    /// returned (item 31). On success the caller owns the returned <see cref="HttpQueryResult"/>
    /// and must dispose it.
    /// </summary>
    /// <param name="response">The HTTP response to validate. May be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the body read.</param>
    /// <returns>A wrapped response stream on success; otherwise a categorized error.</returns>
    private static async Task<ReturnValue<HttpQueryResult>> ValidateAndWrapResponse(
            HttpResponseMessage? response,
            CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return new(new ErrorReturnValue(-2, "no response returned", ODataErrorKind.Transport));
        }

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                return new(new ErrorReturnValue(
                    -1,
                    $"{(int)response.StatusCode} {response.ReasonPhrase}",
                    ODataErrorKind.Transport,
                    (int)response.StatusCode));
            }
            finally
            {
                response.Dispose();
            }
        }

        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (contentStream is null)
        {
            response.Dispose();
            return new(new ErrorReturnValue(-3, "The HTTP response did not contain a readable stream.", ODataErrorKind.Transport));
        }

        Uri? responseUri = response.RequestMessage?.RequestUri;
        return new(new HttpQueryResult(CreateResponseStream(response, contentStream), responseUri));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the failure is deterministic (e.g. HTTP 4xx, empty or
    /// unparseable document) and should be cached to prevent repeated expensive retries (item 55).
    /// Transient failures (HTTP 5xx, network errors) are not cached.
    /// </summary>
    private static bool IsPermanentMetadataFailure(ErrorReturnValue error)
    {
        int code = error.code;
        // HTTP 4xx errors are stored as -(status code): -400 to -499.
        if (code >= -499 && code <= -400) return true;
        // Internal deterministic errors: empty document (-11), parse failure (-12), size limit (-13).
        return code == -11 || code == -12 || code == -13;
    }

    /// <summary>
    /// Executes a query based on the specified parameters and returns the result as a stream.
    /// </summary>
    /// <remarks>This method performs an asynchronous query operation and returns the result as a stream. If the
    /// query fails, the method returns an error code and message indicating the failure reason.</remarks>
    /// <param name="parameter">The query parameters used to define the data retrieval operation. Cannot be null.</param>
    /// <param name="skip">The number of records to skip in the query results. Must be non-negative.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>A <see cref="ReturnValue{Stream}"/> containing the query result stream if successful; otherwise, an error code and
    /// message.</returns>
    private async Task<ReturnValue<HttpQueryResult>> QueryDatas(IQuery parameter, int skip = 0, CancellationToken cancellationToken = default)
    {
        var response = await SimpleQuery(parameter, skip: skip, cancellationToken: cancellationToken);
        // Item 19: reuse the centralized validation/extraction path shared with QueryUrl.
        return await ValidateAndWrapResponse(response, cancellationToken);
    }

    /// <summary>
    /// Wraps the provided HTTP response and content stream in a stream that ensures disposal once consumption finishes.
    /// </summary>
    /// <param name="response">HTTP response associated with the content stream.</param>
    /// <param name="contentStream">Stream exposing the response payload.</param>
    /// <returns>A stream that disposes the response when the consumer disposes the stream.</returns>
    private static Stream CreateResponseStream(HttpResponseMessage response, Stream contentStream)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(contentStream);

        return new HttpResponseContentStream(response, contentStream);
    }

    /// <summary>
    /// Retrieves the service metadata using the instance <see cref="BaseUrl"/>.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    public Task<ReturnValue<Edmx>> GetMetadataFromBaseAsync(CancellationToken cancellationToken = default)
            => GetMetadataAsyncInternal(
                    _ => Task.FromResult<string?>(BuildMetadataUrl(BaseUrl)),
                    cancellationToken);

    /// <summary>
    /// Retrieves the service metadata using the provided metadata document URL.
    /// </summary>
    /// <param name="metadataUrl">Absolute or relative URL pointing to the metadata document.</param>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="metadataUrl"/> is null or whitespace.</exception>
    public Task<ReturnValue<Edmx>> GetMetadataFromUrlAsync(
            string metadataUrl,
            CancellationToken cancellationToken = default)
    {
        metadataUrl.ArgMustNotBe(a=>a.IsNullOrWhiteSpace(), "Metadata URL cannot be null or whitespace.");
        return GetMetadataAsyncInternal(_ => Task.FromResult<string?>(metadataUrl), cancellationToken);
    }

    /// <summary>
    /// Retrieves the service metadata by extracting the metadata URL from the provided JSON payload.
    /// </summary>
    /// <param name="jsonResult">The JSON payload returned by an OData service.</param>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonResult"/> is null.</exception>
    public Task<ReturnValue<Edmx>> GetMetadataFromJsonAsync(
            JsonNode jsonResult,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonResult);

        return GetMetadataAsyncInternal(
                _ => Task.FromResult<string?>(ExtractMetadataUrl(jsonResult)),
                cancellationToken);
    }

    /// <summary>
    /// Retrieves and caches the metadata document using the provided URL resolver.
    /// The cache is keyed by canonical metadata URL (item 46): distinct URLs on the same
    /// instance each receive their own cached entry.
    /// </summary>
    /// <param name="metadataUrlProvider">Delegate responsible for determining the metadata URL.</param>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    private async Task<ReturnValue<Edmx>> GetMetadataAsyncInternal(
            Func<CancellationToken, Task<string?>> metadataUrlProvider,
            CancellationToken cancellationToken)
    {
        var rawUrl = await metadataUrlProvider(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawUrl))
            return new(new ErrorReturnValue(-10, "Metadata URL could not be determined."));

        var resolvedUrl = ResolveMetadataUrl(rawUrl);

        lock (_metadataCache)
        {
            // Item 55: return the cached permanent failure verbatim so the original error
            // code (-404, -11, -12, -13) is preserved on every subsequent call.
            if (_metadataErrorCache.TryGetValue(resolvedUrl, out ErrorReturnValue? cachedError))
                return new(cachedError);

            if (_metadataCache.TryGetValue(resolvedUrl, out Edmx? cached))
                return new(cached);
        }

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            lock (_metadataCache)
            {
                if (_metadataErrorCache.TryGetValue(resolvedUrl, out ErrorReturnValue? cachedError))
                    return new(cachedError);

                if (_metadataCache.TryGetValue(resolvedUrl, out Edmx? cached))
                    return new(cached);
            }

            var metadataResult = await FetchMetadataAsync(resolvedUrl, cancellationToken);
            if (metadataResult.IsError)
            {
                // Item 55: cache deterministic failures (HTTP 4xx, empty/unparseable document)
                // to prevent expensive retries.  Transient failures (5xx, network) are not cached.
                // The full ErrorReturnValue is stored so the original code is returned unchanged.
                if (IsPermanentMetadataFailure(metadataResult.Error))
                {
                    lock (_metadataCache)
                    {
                        _metadataErrorCache[resolvedUrl] = metadataResult.Error;
                    }
                }

                return metadataResult;
            }

            var metadataValue = metadataResult.Value
                    ?? throw new InvalidOperationException("Metadata content cannot be null when the operation succeeds.");
            lock (_metadataCache)
            {
                _metadataCache[resolvedUrl] = metadataValue;
            }

            return new(metadataValue);
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    /// <summary>
    /// Downloads and deserializes the metadata document from the specified URL.
    /// </summary>
    /// <param name="metadataUrl">Resolved metadata URL to download.</param>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    private async Task<ReturnValue<Edmx>> FetchMetadataAsync(string metadataUrl, CancellationToken cancellationToken)
    {
        using var response = await HttpGet(metadataUrl, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new(new ErrorReturnValue(-(int)response.StatusCode, response.ReasonPhrase ?? "Unknown error"));

        // Item 48: reject oversized metadata before reading the body.
        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxMetadataBytes)
        {
            return new(new ErrorReturnValue(-13,
                $"Metadata response from '{metadataUrl}' exceeds the {MaxMetadataBytes / (1024 * 1024)} MiB limit."));
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (contentStream is null || (contentStream.CanSeek && contentStream.Length == 0))
            return new(new ErrorReturnValue(-11, "Metadata document is empty."));

        // Item 48: read with a bounded buffer so that streaming responses cannot allocate
        // unbounded memory even when Content-Length is absent or spoofed.
        MemoryStream bounded;
        try
        {
            bounded = await ReadBoundedAsync(contentStream, MaxMetadataBytes, cancellationToken);
        }
        catch (MetadataSizeLimitException ex)
        {
            return new(new ErrorReturnValue(-13, ex.Message));
        }

        Edmx? metadata;
        using (bounded)
        {
            metadata = DeserializeMetadatas.Deserialize(bounded);
        }

        if (metadata is null)
            return new(new ErrorReturnValue(-12, "Metadata document could not be parsed."));

        return new(metadata);
    }

    /// <summary>
    /// Copies <paramref name="source"/> into a <see cref="MemoryStream"/>, throwing when
    /// the total byte count exceeds <paramref name="maxBytes"/>.
    /// </summary>
    private static async Task<MemoryStream> ReadBoundedAsync(
            Stream source, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        int totalRead = 0;
        var ms = new MemoryStream();

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0)
                break;

            totalRead += bytesRead;
            if (totalRead > maxBytes)
                throw new MetadataSizeLimitException(maxBytes);

            ms.Write(buffer, 0, bytesRead);
        }

        ms.Position = 0;
        return ms;
    }

    private static string? ExtractMetadataUrl(JsonNode jsonNode)
    {
        var url = TryReadMetadataProperty(jsonNode);
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        if (jsonNode is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                if (property.Value is null)
                    continue;

                var nestedUrl = ExtractMetadataUrl(property.Value);
                if (!string.IsNullOrWhiteSpace(nestedUrl))
                    return nestedUrl;
            }
        }

        if (jsonNode is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item is null)
                    continue;

                var nestedUrl = ExtractMetadataUrl(item);
                if (!string.IsNullOrWhiteSpace(nestedUrl))
                    return nestedUrl;
            }
        }

        return null;
    }

    private static string? TryReadMetadataProperty(JsonNode node)
    {
        if (node is not JsonObject jsonObject)
            return null;

        if (jsonObject.TryGetPropertyValue("@odata.metadata", out var metadataNode))
        {
            var value = metadataNode?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        if (jsonObject.TryGetPropertyValue("@odata.context", out var contextNode))
        {
            var value = contextNode?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private string ResolveMetadataUrl(string rawUrl)
    {
        var cleanedUrl = RemoveFragment(rawUrl);
        if (Uri.TryCreate(cleanedUrl, UriKind.Absolute, out var absoluteUrl))
            return absoluteUrl.ToString();

        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
                && Uri.TryCreate(baseUri, cleanedUrl, out var combinedUri))
            return combinedUri.ToString();

        return cleanedUrl;
    }

    private static string RemoveFragment(string url)
    {
        var fragmentIndex = url.IndexOf('#');
        return fragmentIndex >= 0 ? url[..fragmentIndex] : url;
    }

    private static string BuildMetadataUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return baseUrl.TrimEnd('/') + "/$metadata";

        var builder = new UriBuilder(baseUri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        var path = builder.Path;
        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        builder.Path = path + "$metadata";
        return builder.Uri.ToString();
    }

    /// <summary>
    /// Retrieves the column names from the specified query and JSON array.
    /// </summary>
    /// <param name="parameter">The query parameter containing the selection criteria. If the <c>Select</c> property is null or whitespace, all
    /// columns from the first JSON object in the array are returned.</param>
    /// <param name="array">The JSON array from which to extract column names. The array must contain at least one JSON object.</param>
    /// <returns>An array of strings representing the column names. If <paramref name="parameter"/> has a non-empty <c>Select</c>
    /// property, the specified columns are returned; otherwise, all columns from the first JSON object are returned.</returns>
    public static string[] GetColumns(IQuery parameter, JsonArray array)
    {
        string[] columns;
        string? selection = parameter.Select;
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection.Trim(), "*", StringComparison.Ordinal))
        {
            var firstRow = array[0] as JsonObject
                    ?? throw new InvalidOperationException("The JSON array does not contain an object at index 0.");
            columns = firstRow.Select(c => c.Key).ToArray();
        }
        else
        {
            columns = selection.Split(',').Select(c => c.Trim()).ToArray();
        }

        return columns;
    }

    #region IDisposable
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient?.Dispose();
            _handler?.Dispose();
        }
        _metadataLock.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}
