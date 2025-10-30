using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Contains unit tests covering the streaming <see cref="IDataReader"/> returned by <see cref="QueryOData"/>.
/// </summary>
[TestClass]
public class QueryODataDataReaderTests
{
    /// <summary>
    /// Ensures the data reader streams rows across multiple HTTP calls while exposing metadata-driven types.
    /// </summary>
    [TestMethod]
    public async Task QueryToDataReader_StreamsTypedRowsAcrossBatches()
    {
        const string metadataPayload =
            """
            <edmx:Edmx xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                <edmx:DataServices>
                    <Schema xmlns="http://docs.oasis-open.org/odata/ns/edm" Namespace="Sample">
                        <EntityType Name="Entity">
                            <Key>
                                <PropertyRef Name="Id" />
                            </Key>
                            <Property Name="Id" Type="Edm.Int32" />
                            <Property Name="Name" Type="Edm.String" />
                        </EntityType>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>
            """;
        using var handler = new DataReaderStubHttpMessageHandler(metadataPayload, totalItems: 5, serverBatchLimit: 5);
        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var parameter = new Query
        {
            Table = "Entity",
            Filters = string.Empty,
            Select = "*",
            Top = 5
        };

        var result = await query.QueryToDataReader(parameter, maxPerRequest: 2);

        Assert.IsTrue(result.IsSuccess);
        using IDataReader reader = result.Value;

        Assert.AreEqual(2, reader.FieldCount);
        Assert.AreEqual("Id", reader.GetName(0));
        Assert.AreEqual(typeof(int), reader.GetFieldType(0));
        Assert.AreEqual("Name", reader.GetName(1));
        Assert.AreEqual(typeof(string), reader.GetFieldType(1));

        var identifiers = new List<int>();
        var names = new List<string>();
        while (reader.Read())
        {
            identifiers.Add(reader.GetInt32(0));
            names.Add(reader.GetString(1));
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, identifiers);
        CollectionAssert.AreEqual(new[] { "Name-0", "Name-1", "Name-2", "Name-3", "Name-4" }, names);

        Assert.AreEqual(1, handler.MetadataCallCount);
        Assert.AreEqual(3, handler.DataCallCount);
        CollectionAssert.AreEqual(new int?[] { 2, 2, 1 }, handler.ObservedTops);
    }

    /// <summary>
    /// HTTP handler used to simulate metadata and paginated data responses for the streaming tests.
    /// </summary>
    private sealed class DataReaderStubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _metadataPayload;
        private readonly int _totalItems;
        private readonly int _serverBatchLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReaderStubHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="metadataPayload">XML document representing the service metadata.</param>
        /// <param name="totalItems">Total number of items available for download.</param>
        /// <param name="serverBatchLimit">Maximum number of items returned per request by the simulated server.</param>
        public DataReaderStubHttpMessageHandler(string metadataPayload, int totalItems, int serverBatchLimit)
        {
            _metadataPayload = metadataPayload;
            _totalItems = totalItems;
            _serverBatchLimit = serverBatchLimit;
        }

        /// <summary>
        /// Gets the number of metadata requests processed by the handler.
        /// </summary>
        public int MetadataCallCount { get; private set; }

        /// <summary>
        /// Gets the number of data requests processed by the handler.
        /// </summary>
        public int DataCallCount { get; private set; }

        /// <summary>
        /// Gets the sequence of observed <c>$top</c> values across requests.
        /// </summary>
        public List<int?> ObservedTops { get; } = new();

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri.Contains("$metadata", System.StringComparison.OrdinalIgnoreCase))
            {
                MetadataCallCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_metadataPayload, Encoding.UTF8, "application/xml")
                };

                return Task.FromResult(response);
            }

            DataCallCount++;
            (int Skip, int? Top) parameters = ReadPagingParameters(request.RequestUri!);
            ObservedTops.Add(parameters.Top);

            int remaining = System.Math.Max(_totalItems - parameters.Skip, 0);
            int batchSize = System.Math.Min(remaining, _serverBatchLimit);
            if (parameters.Top.HasValue)
            {
                batchSize = System.Math.Min(batchSize, System.Math.Max(parameters.Top.Value, 0));
            }

            string payload = BuildPayload(parameters.Skip, batchSize);
            var dataResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(dataResponse);
        }

        private static (int Skip, int? Top) ReadPagingParameters(Uri requestUri)
        {
            int skip = 0;
            int? top = null;
            string query = requestUri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (string part in query.Split('&', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] tokens = part.Split('=', 2);
                    string key = Uri.UnescapeDataString(tokens[0]);
                    string value = tokens.Length > 1 ? Uri.UnescapeDataString(tokens[1]) : string.Empty;

                    if (key.Equals("$skip", System.StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int parsedSkip))
                    {
                        skip = parsedSkip;
                    }
                    else if (key.Equals("$top", System.StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int parsedTop))
                    {
                        top = parsedTop;
                    }
                }
            }

            return (skip, top);
        }

        private static string BuildPayload(int skip, int batchSize)
        {
            var builder = new StringBuilder();
            builder.Append("{\"@odata.context\":\"https://service.example.com/$metadata#Entity\",\"value\":[");
            for (int index = 0; index < batchSize; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                int identifier = skip + index;
                builder.Append("{\"Id\":");
                builder.Append(identifier);
                builder.Append(",\"Name\":\"Name-");
                builder.Append(identifier);
                builder.Append("\"}");
            }

            builder.Append("]}");
            return builder.ToString();
        }
    }
}
