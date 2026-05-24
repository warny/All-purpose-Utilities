using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Contains tests covering paginated data retrieval behaviours of <see cref="QueryOData"/>.
/// </summary>
[TestClass]
public class QueryODataPaginationTests
{
    /// <summary>
    /// Ensures additional requests are issued when the service limits the number of records per response.
    /// </summary>
    [TestMethod]
    public async Task QueryToJson_FetchesMultipleBatchesWhenServerCapsResults()
    {
        using var handler = new PaginatedStubHttpMessageHandler(totalItems: 5, serverBatchLimit: 2);
        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var parameter = new Query
        {
            Table = "Entities",
            Filters = string.Empty,
            Select = "*",
            Top = 5
        };

        var result = await query.QueryToJSon(parameter);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value.Datas);
        Assert.AreEqual(5, result.Value.Datas!.Count);

        int[] identifiers = result.Value.Datas!
            .Select(node => node?["Id"]?.GetValue<int>() ?? -1)
            .ToArray();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, identifiers);

        Assert.AreEqual(3, handler.CallCount);
        CollectionAssert.AreEqual(new int?[] { 5, 3, 1 }, handler.ObservedTops);
    }

    /// <summary>
    /// Ensures the per-request limit parameter governs the requested page size.
    /// </summary>
    [TestMethod]
    public async Task QueryToJson_UsesMaxPerRequestOverride()
    {
        using var handler = new PaginatedStubHttpMessageHandler(totalItems: 5, serverBatchLimit: 5);
        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var parameter = new Query
        {
            Table = "Entities",
            Filters = string.Empty,
            Select = "*"
        };

        var result = await query.QueryToJSon(parameter, maxPerRequest: 2);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value.Datas);
        Assert.AreEqual(5, result.Value.Datas!.Count);

        int[] identifiers = result.Value.Datas!
            .Select(node => node?["Id"]?.GetValue<int>() ?? -1)
            .ToArray();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, identifiers);

        Assert.AreEqual(4, handler.CallCount);
        CollectionAssert.AreEqual(new int?[] { 2, 2, 2, 2 }, handler.ObservedTops);
    }

    /// <summary>
    /// HTTP handler that simulates server-side paging behaviour for OData endpoints.
    /// </summary>
    private sealed class PaginatedStubHttpMessageHandler : HttpMessageHandler
    {
        private readonly int _totalItems;
        private readonly int _serverBatchLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaginatedStubHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="totalItems">Total number of items available for download.</param>
        /// <param name="serverBatchLimit">Maximum number of items the simulated server returns per request.</param>
        public PaginatedStubHttpMessageHandler(int totalItems, int serverBatchLimit)
        {
            _totalItems = totalItems;
            _serverBatchLimit = serverBatchLimit;
        }

        /// <summary>
        /// Gets the number of HTTP requests processed by the handler.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Gets the sequence of <c>$top</c> values observed across requests.
        /// </summary>
        public List<int?> ObservedTops { get; } = new();

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            (int Skip, int? Top) parameters = ReadPagingParameters(request.RequestUri!);
            ObservedTops.Add(parameters.Top);

            int remaining = Math.Max(_totalItems - parameters.Skip, 0);
            int batchSize = Math.Min(remaining, _serverBatchLimit);
            if (parameters.Top.HasValue)
            {
                batchSize = Math.Min(batchSize, Math.Max(parameters.Top.Value, 0));
            }

            string payload = BuildPayload(parameters.Skip, batchSize);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }

        private static (int Skip, int? Top) ReadPagingParameters(Uri requestUri)
        {
            int skip = 0;
            int? top = null;
            string query = requestUri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] tokens = part.Split('=', 2);
                    string key = Uri.UnescapeDataString(tokens[0]);
                    string value = tokens.Length > 1 ? Uri.UnescapeDataString(tokens[1]) : string.Empty;

                    if (key.Equals("$skip", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int parsedSkip))
                    {
                        skip = parsedSkip;
                    }
                    else if (key.Equals("$top", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int parsedTop))
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
            builder.Append("{\"@odata.context\":\"https://service.example.com/$metadata#Entities\",\"value\":[");
            for (int index = 0; index < batchSize; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append("{\"Id\":");
                builder.Append(skip + index);
                builder.Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }
    }
}
