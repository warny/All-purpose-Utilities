using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;
using Utils.OData.Metadatas;

namespace UtilsTest.OData;

/// <summary>
/// Functional tests for <see cref="ODataContext"/> remote metadata loading: injectable/reused
/// <see cref="HttpClient"/> (item 16) and the cross-host redirect destination policy (item 30).
/// </summary>
[TestClass]
public class ODataContextRedirectPolicyTests
{
    private const string MetadataPayload =
        "<edmx:Edmx xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\">" +
        "<edmx:DataServices>" +
        "<Schema xmlns=\"http://docs.oasis-open.org/odata/ns/edm\" Namespace=\"Sample\">" +
        "<EntityType Name=\"Item\"><Key><PropertyRef Name=\"Id\" /></Key>" +
        "<Property Name=\"Id\" Type=\"Edm.Int32\" /></EntityType>" +
        "</Schema></edmx:DataServices></edmx:Edmx>";

    // -----------------------------------------------------------------------
    // Item 16: an injected HttpClient is used for the download.
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_InjectedClient_IsUsed()
    {
        using var handler = new RecordingHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client };
        var metadata = await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options);

        Assert.IsNotNull(metadata);
        Assert.AreEqual(1, handler.CallCount);
    }

    // -----------------------------------------------------------------------
    // Item 30: a redirect to a different host is rejected by default.
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_CrossHostRedirect_RejectedByDefault()
    {
        // The handler simulates a client that already followed a redirect: the response's
        // RequestMessage.RequestUri points to a different host than the requested one.
        using var handler = new RecordingHandler((request, _) =>
        {
            var finalRequest = new HttpRequestMessage(HttpMethod.Get, "https://evil.example.net/$metadata");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = finalRequest
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client };

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options));
        StringAssert.Contains(ex.Message, "redirect");
    }

    [TestMethod]
    public async Task LoadMetadataAsync_CrossHostRedirect_AllowedWhenOptedIn()
    {
        using var handler = new RecordingHandler((request, _) =>
        {
            var finalRequest = new HttpRequestMessage(HttpMethod.Get, "https://mirror.example.net/$metadata");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = finalRequest
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client, AllowCrossHostRedirect = true };
        var metadata = await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options);

        Assert.IsNotNull(metadata);
    }

    // -----------------------------------------------------------------------
    // Item 15/33: the size cap is enforced on remote downloads too.
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_RemoteOverLimit_Throws()
    {
        using var handler = new RecordingHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client, MaxMetadataBytes = 8 };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options));
    }

    /// <summary>Test handler that records invocation count and delegates response production.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return _handler(request, cancellationToken);
        }
    }
}
