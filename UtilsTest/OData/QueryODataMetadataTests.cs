using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;
using Utils.OData.Metadatas;

namespace UtilsTest.OData;

/// <summary>
/// Contains tests validating metadata retrieval capabilities of <see cref="QueryOData"/>.
/// </summary>
[TestClass]
public class QueryODataMetadataTests
{
    /// <summary>
    /// Ensures metadata fetched from the base URL is cached for the lifetime of the instance.
    /// </summary>
    [TestMethod]
    public async Task GetMetadataFromBaseAsync_CachesResponse()
    {
        const string metadataPayload = "<edmx:Edmx xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\"><edmx:DataServices><Schema xmlns=\"http://docs.oasis-open.org/odata/ns/edm\" Namespace=\"Sample\"><EntityType Name=\"Item\"><Key><PropertyRef Name=\"Id\" /></Key><Property Name=\"Id\" Type=\"Edm.Int32\" /></EntityType></Schema></edmx:DataServices></edmx:Edmx>";
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual("https://service.example.com/$metadata", request.RequestUri!.ToString(), true);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(metadataPayload, Encoding.UTF8, "application/xml")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var firstCall = await query.GetMetadataFromBaseAsync();
        Assert.IsTrue(firstCall.IsSuccess);
        Assert.IsNotNull(firstCall.Value);
        Assert.IsNotNull(firstCall.Value.DataServices);
        Assert.IsNotNull(firstCall.Value.DataServices[0].Schemas);
        Assert.IsNotNull(firstCall.Value.DataServices[0].Schemas[0].EntityTypes);
        Assert.AreEqual("Item", firstCall.Value.DataServices[0].Schemas[0].EntityTypes[0].Name);
        Assert.AreEqual(1, handler.CallCount);

        var secondCall = await query.GetMetadataFromBaseAsync();
        Assert.IsTrue(secondCall.IsSuccess);
        Assert.AreSame(firstCall.Value, secondCall.Value);
        Assert.AreEqual(1, handler.CallCount);
    }

    /// <summary>
    /// Ensures metadata can be loaded using the metadata link returned within a JSON payload.
    /// </summary>
    [TestMethod]
    public async Task GetMetadataFromJsonAsync_UsesMetadataLink()
    {
        const string metadataPayload = "<edmx:Edmx xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\"><edmx:DataServices><Schema xmlns=\"http://docs.oasis-open.org/odata/ns/edm\" Namespace=\"Sample\"><EntityType Name=\"Order\"><Key><PropertyRef Name=\"Id\" /></Key><Property Name=\"Id\" Type=\"Edm.Guid\" /></EntityType></Schema></edmx:DataServices></edmx:Edmx>";
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual("https://service.example.com/$metadata", request.RequestUri!.ToString(), true);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(metadataPayload, Encoding.UTF8, "application/xml")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var payload = JsonNode.Parse("{\"@odata.context\":\"https://service.example.com/$metadata#Products\",\"value\":[]}")!;
        var result = await query.GetMetadataFromJsonAsync(payload);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.IsNotNull(result.Value.DataServices);
        Assert.IsNotNull(result.Value.DataServices[0].Schemas);
        Assert.IsNotNull(result.Value.DataServices[0].Schemas[0].EntityTypes);
        Assert.AreEqual("Order", result.Value.DataServices[0].Schemas[0].EntityTypes[0].Name);
        Assert.AreEqual(1, handler.CallCount);
    }

    /// <summary>
    /// Ensures providing an explicit metadata URL triggers the download of the corresponding document.
    /// </summary>
    [TestMethod]
    public async Task GetMetadataFromUrlAsync_UsesProvidedUrl()
    {
        const string metadataPayload = "<edmx:Edmx xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\"><edmx:DataServices><Schema xmlns=\"http://docs.oasis-open.org/odata/ns/edm\" Namespace=\"Sample\"><EntityType Name=\"Customer\"><Key><PropertyRef Name=\"Id\" /></Key><Property Name=\"Id\" Type=\"Edm.Int64\" /></EntityType></Schema></edmx:DataServices></edmx:Edmx>";
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual("https://metadata.example.com/v1/$metadata", request.RequestUri!.ToString(), true);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(metadataPayload, Encoding.UTF8, "application/xml")
            };
            return Task.FromResult(response);
        });

        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var result = await query.GetMetadataFromUrlAsync("https://metadata.example.com/v1/$metadata");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.IsNotNull(result.Value.DataServices);
        Assert.IsNotNull(result.Value.DataServices[0].Schemas);
        Assert.IsNotNull(result.Value.DataServices[0].Schemas[0].EntityTypes);
        Assert.AreEqual("Customer", result.Value.DataServices[0].Schemas[0].EntityTypes[0].Name);
        Assert.AreEqual(1, handler.CallCount);
    }

    /// <summary>
    /// Ensures attempting to extract metadata from a payload without a metadata link returns an error without performing HTTP requests.
    /// </summary>
    [TestMethod]
    public async Task GetMetadataFromJsonAsync_ReturnsErrorWhenMissingMetadataLink()
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("No request expected."));
        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var payload = JsonNode.Parse("{\"value\":[]}")!;
        var result = await query.GetMetadataFromJsonAsync(payload);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(-10, result.Error.code);
        Assert.AreEqual(0, handler.CallCount);
    }

    /// <summary>
    /// Lightweight HTTP handler used to simulate metadata responses.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="handler">Delegate invoked for each outbound request.</param>
        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Gets the number of times the handler has been invoked.
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return _handler(request, cancellationToken);
        }

    }
}
