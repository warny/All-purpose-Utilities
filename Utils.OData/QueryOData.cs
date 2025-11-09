using System.Data;
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
    private readonly SemaphoreSlim _metadataLock = new(1, 1);
    private Edmx? _metadataCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryOData"/> class with the specified base URL.
    /// </summary>
    /// <remarks>This constructor sets up an <see cref="HttpClient"/> with default credentials and a timeout of 600
    /// seconds.</remarks>
    /// <param name="baseUrl">The base URL for the OData service. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> is null.</exception>
    public QueryOData(string baseUrl)
    {
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
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
    /// <param name="baseUrl">The base URL for the OData service. Cannot be null.</param>
    /// <param name="httpClient">The HTTP client used to send requests. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUrl"/> or <paramref name="httpClient"/> is null.</exception>
    public QueryOData(string baseUrl, HttpClient httpClient)
    {
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _disposeClient = false;
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

        // Copy standard headers (excluding sensitive ones handled elsewhere).
        if (sourceRequest is not null)
        {
            foreach (var header in sourceRequest.Headers)
            {
                // Skip headers that would be invalid on DefaultRequestHeaders but add to the outgoing request
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (sourceRequest.Content is not null)
            {
                foreach (var header in sourceRequest.Content.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Copy cookies to the handler container when available.
            if (_handler?.CookieContainer is not null && sourceRequest.Headers.TryGetValues("Cookie", out var cookieHeaders))
            {
                var cookieHeader = string.Join("; ", cookieHeaders);
                try
                {
                    var uri = new Uri(BaseUrl);
                    _handler.CookieContainer.SetCookies(uri, cookieHeader);
                }
                catch
                {
                    // Ignore failures (for instance when BaseUrl is not a valid URI) and continue the request.
                }
            }
        }

        Console.WriteLine($"Requesting : {url}");
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

        JsonArray aggregatedValues = new();
        Dictionary<string, string>? metadatas = null;
        int totalRetrieved = 0;
        int? originalTop = parameter.Top;
        bool hasAnyData = false;

        while (true)
        {
            int? remaining = originalTop.HasValue ? originalTop.Value - totalRetrieved : null;
            if (remaining.HasValue && remaining.Value <= 0)
            {
                break;
            }

            int? requestTop = DetermineRequestTop(remaining, maxPerRequest);
            Query requestQuery = CloneQueryForPagination(parameter, requestTop);
            var queryResult = await QueryDatas(requestQuery, skip + totalRetrieved, cancellationToken);

            if (queryResult.IsError)
            {
                return new(queryResult.Error);
            }

            await using Stream stream = queryResult.Value;
            var convertResult = ConvertDatas(stream, allowEmpty: true);
            if (convertResult.IsError)
            {
                return convertResult;
            }

            (JsonArray? Datas, Dictionary<string, string>? Metadatas) chunk = convertResult.Value;
            if (chunk.Metadatas is not null && metadatas is null)
            {
                metadatas = new Dictionary<string, string>(chunk.Metadatas);
            }

            if (chunk.Datas is not JsonArray { Count: > 0 } chunkDataArray)
            {
                break;
            }

            hasAnyData = true;

            int chunkCount = chunkDataArray.Count;

            foreach (JsonNode? item in chunkDataArray)
            {
                aggregatedValues.Add(item?.DeepClone());
            }

            totalRetrieved += chunkCount;
        }

        if (!hasAnyData)
        {
            return new(1, "No data returned.");
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

        int? originalTop = parameter.Top;
        int? requestTop = DetermineRequestTop(originalTop, maxPerRequest);
        Query firstQuery = CloneQueryForPagination(parameter, requestTop);

        var queryResult = await QueryDatas(firstQuery, skip, cancellationToken);
        if (queryResult.IsError)
        {
            return new(queryResult.Error);
        }

        await using Stream firstStream = queryResult.Value;
        var convertResult = ConvertDatas(firstStream, allowEmpty: false);
        if (convertResult.IsError)
        {
            return new(convertResult.Error);
        }

        (JsonArray? Datas, Dictionary<string, string>? Metadatas) firstChunk = convertResult.Value;
        if (firstChunk.Datas is not JsonArray { Count: > 0 } firstBatch)
        {
            return new(1, "No data returned.");
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

        Task streamingTask = StreamBatchesAsync(
                parameter,
                skip,
                maxPerRequest,
                originalTop,
                firstBatch,
                columns,
                rowConverter,
                channel.Writer,
                linkedTokenSource.Token);

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
    private async Task StreamBatchesAsync(
            IQuery parameter,
            int skip,
            int? maxPerRequest,
            int? originalTop,
            JsonArray firstBatch,
            IReadOnlyList<ColumnDefinition> columns,
            Func<JsonObject, object[]> rowConverter,
            ChannelWriter<object?[]> writer,
            CancellationToken cancellationToken)
    {
		ArgumentNullException.ThrowIfNull(parameter);
		ArgumentNullException.ThrowIfNull(firstBatch);
		ArgumentNullException.ThrowIfNull(columns);
		ArgumentNullException.ThrowIfNull(rowConverter);
		ArgumentNullException.ThrowIfNull(writer);

		try
        {
            int totalRetrieved = 0;
            await WriteBatchAsync(firstBatch, columns, rowConverter, writer, cancellationToken);
            totalRetrieved += firstBatch.Count;

            while (true)
            {
                int? remaining = originalTop.HasValue ? originalTop.Value - totalRetrieved : null;
                if (remaining.HasValue && remaining.Value <= 0)
                {
                    break;
                }

                int? requestTop = DetermineRequestTop(remaining, maxPerRequest);
                Query nextQuery = CloneQueryForPagination(parameter, requestTop);
                var queryResult = await QueryDatas(nextQuery, skip + totalRetrieved, cancellationToken);
                if (queryResult.IsError)
                {
                    throw new ODataDataReaderException(queryResult.Error);
                }

                await using Stream stream = queryResult.Value;
                var convertResult = ConvertDatas(stream, allowEmpty: true);
                if (convertResult.IsError)
                {
                    throw new ODataDataReaderException(convertResult.Error);
                }

                JsonArray? batch = convertResult.Value.Datas;
                if (batch is not JsonArray { Count: > 0 } nonEmptyBatch)
                {
                    break;
                }

                await WriteBatchAsync(nonEmptyBatch, columns, rowConverter, writer, cancellationToken);
                totalRetrieved += nonEmptyBatch.Count;
            }

            writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    /// <summary>
    /// Writes the specified JSON batch to the channel using the provided column definitions.
    /// </summary>
    /// <param name="batch">JSON array containing the rows to materialize.</param>
    /// <param name="columns">Column definitions describing the schema of the dataset.</param>
    /// <param name="rowConverter">Compiled converter used to materialize JSON rows.</param>
    /// <param name="writer">Channel writer that receives the materialized rows.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    private static async Task WriteBatchAsync(
            JsonArray batch,
            IReadOnlyList<ColumnDefinition> columns,
            Func<JsonObject, object[]> rowConverter,
            ChannelWriter<object?[]> writer,
            CancellationToken cancellationToken)
    {
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(columns);
		ArgumentNullException.ThrowIfNull(rowConverter);
		ArgumentNullException.ThrowIfNull(writer);
		
        foreach (JsonNode? entry in batch)
        {
            object[] row = entry is JsonObject jsonObject
                    ? rowConverter(jsonObject)
                    : CreateEmptyRow(columns.Count);
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
    private static Property? ResolveProperty(Edmx metadata, EntityType? entityType, string columnName)
    {
        if (entityType?.Properties is not null)
        {
            foreach (Property property in entityType.Properties)
            {
                if (property is null || string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                if (string.Equals(property.Name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }
        }

        if (metadata?.DataServices is null)
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

                foreach (EntityType candidate in schema.EntityTypes)
                {
                    if (candidate?.Properties is null)
                    {
                        continue;
                    }

                    foreach (Property property in candidate.Properties)
                    {
                        if (property is null || string.IsNullOrWhiteSpace(property.Name))
                        {
                            continue;
                        }

                        if (string.Equals(property.Name, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return property;
                        }
                    }
                }
            }
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
            Edmx metadata)
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
            columns.Add(new ColumnDefinition(name, converter.ClrType, index, converter.Converter));
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
        if (jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return value;
        }

        foreach (KeyValuePair<string, JsonNode?> entry in jsonObject)
        {
            if (string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
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
        for (int index = 0; index < fieldCount; index++)
        {
            values[index] = DBNull.Value;
        }

        return values;
    }

    /// <summary>
    /// Represents a column definition used by the streaming data reader.
    /// </summary>
    /// <param name="Name">Name of the column.</param>
    /// <param name="FieldType">CLR type associated with the column.</param>
    /// <param name="Ordinal">Zero-based ordinal assigned to the column.</param>
    /// <param name="Converter">Converter used to materialize JSON nodes into column values.</param>
    private sealed record ColumnDefinition(string Name, Type FieldType, int Ordinal, Func<JsonNode?, object> Converter);

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
                _ordinals[column.Name] = column.Ordinal;
            }
        }

        /// <summary>
        /// Finalizes the reader instance.
        /// </summary>
        ~ODataStreamingDataReader()
        {
            Dispose(disposing: false);
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
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged resources and optionally managed ones.
        /// </summary>
        /// <param name="disposing">Indicates whether managed resources should be released.</param>
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
                catch (AggregateException aggregate) when (aggregate.InnerExceptions.Count == 1)
                {
                    if (aggregate.InnerException is not OperationCanceledException)
                    {
                        _ = aggregate.InnerException;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation during disposal.
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
                row["AllowDBNull"] = true;
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
                return false;
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
            object value = GetValue(i);
            if (value is DBNull)
            {
                return 0;
            }

            if (value is not byte[] data)
            {
                throw new InvalidCastException($"Column {i} does not contain binary data.");
            }

            int available = Math.Max(0, data.Length - (int)fieldOffset);
            int count = Math.Min(available, length);
            if (buffer is not null && count > 0)
            {
                Array.Copy(data, (int)fieldOffset, buffer, bufferoffset, count);
            }

            return count;
        }

        /// <inheritdoc />
        public char GetChar(int i) => Convert.ToChar(GetValue(i), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
        {
            string data = GetString(i);
            int available = Math.Max(0, data.Length - (int)fieldoffset);
            int count = Math.Min(available, length);
            if (buffer is not null && count > 0)
            {
                data.CopyTo((int)fieldoffset, buffer, bufferoffset, count);
            }

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
    /// Executes a query based on the specified parameters and returns the result as a stream.
    /// </summary>
    /// <remarks>This method performs an asynchronous query operation and returns the result as a stream. If the
    /// query fails, the method returns an error code and message indicating the failure reason.</remarks>
    /// <param name="parameter">The query parameters used to define the data retrieval operation. Cannot be null.</param>
    /// <param name="skip">The number of records to skip in the query results. Must be non-negative.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>A <see cref="ReturnValue{Stream}"/> containing the query result stream if successful; otherwise, an error code and
    /// message.</returns>
    private async Task<ReturnValue<Stream>> QueryDatas(IQuery parameter, int skip = 0, CancellationToken cancellationToken = default)
    {
        var response = await SimpleQuery(parameter, skip: skip, cancellationToken: cancellationToken);

        if (response is null)
        {
            return new(-2, "no response returned");
        }

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                return new(-1, $"{(int)response.StatusCode} {response.ReasonPhrase}");
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
            return new(-3, "The HTTP response did not contain a readable stream.");
        }

        return CreateResponseStream(response, contentStream);
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
    /// </summary>
    /// <param name="metadataUrlProvider">Delegate responsible for determining the metadata URL.</param>
    /// <param name="cancellationToken">Token used to cancel the retrieval operation.</param>
    /// <returns>A <see cref="ReturnValue{T}"/> containing the metadata document when successful.</returns>
    private async Task<ReturnValue<Edmx>> GetMetadataAsyncInternal(
            Func<CancellationToken, Task<string?>> metadataUrlProvider,
            CancellationToken cancellationToken)
    {
        if (_metadataCache is not null)
            return new(_metadataCache);

        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            if (_metadataCache is not null)
                return new(_metadataCache);

            var rawUrl = await metadataUrlProvider(cancellationToken);
            if (string.IsNullOrWhiteSpace(rawUrl))
                return new(new ErrorReturnValue(-10, "Metadata URL could not be determined."));

            var resolvedUrl = ResolveMetadataUrl(rawUrl);
            var metadataResult = await FetchMetadataAsync(resolvedUrl, cancellationToken);
            if (metadataResult.IsError)
                return metadataResult;

            var metadataValue = metadataResult.Value
                    ?? throw new InvalidOperationException("Metadata content cannot be null when the operation succeeds.");
            _metadataCache = metadataValue;
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

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (contentStream is null || (contentStream.CanSeek && contentStream.Length == 0))
            return new(new ErrorReturnValue(-11, "Metadata document is empty."));

        var metadata = DeserializeMetadatas.Deserialize(contentStream);
        if (metadata is null)
            return new(new ErrorReturnValue(-12, "Metadata document could not be parsed."));

        return new(metadata);
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
