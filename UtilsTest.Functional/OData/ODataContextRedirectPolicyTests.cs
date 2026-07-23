using System;
using System.IO;
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
/// <see cref="HttpClient"/> (item 16) and the cross-origin redirect destination policy (item 30).
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
    // Item 30: prevention — the redirect destination is checked BEFORE any
    // request is sent to it (manual redirect loop in DownloadMetadataAsync).
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_ForbiddenRedirect_SecondOriginNeverRequested()
    {
        // The handler returns an explicit 302 so the manual redirect loop in
        // DownloadMetadataAsync intercepts it before sending a second request.
        int secondOriginCallCount = 0;
        using var handler = new RecordingHandler((request, _) =>
        {
            if (string.Equals(request.RequestUri!.Host, "service.example.com", StringComparison.OrdinalIgnoreCase))
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri("https://evil.example.net/$metadata");
                return Task.FromResult(redirect);
            }

            secondOriginCallCount++;
            var ok = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = request
            };
            return Task.FromResult(ok);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client };

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options));

        StringAssert.Contains(ex.Message, "redirect");
        Assert.AreEqual(0, secondOriginCallCount, "The forbidden origin must never receive a request.");
    }

    [TestMethod]
    public async Task LoadMetadataAsync_CrossPortRedirect_RejectedByDefault()
    {
        // Same scheme and host, but different port — still a different origin.
        using var handler = new RecordingHandler((request, _) =>
        {
            if (request.RequestUri!.Port == 443)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri("https://service.example.com:8443/$metadata");
                return Task.FromResult(redirect);
            }

            var ok = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MetadataPayload, Encoding.UTF8, "application/xml"),
                RequestMessage = request
            };
            return Task.FromResult(ok);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions { HttpClient = client };

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options));

        StringAssert.Contains(ex.Message, "redirect");
    }

    // -----------------------------------------------------------------------
    // Item 30: best-effort post-hoc check for injected clients whose handler
    // has AllowAutoRedirect = true (the redirect is followed before we see it).
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_CrossHostRedirect_RejectedByDefault()
    {
        // Simulates an injected client with AllowAutoRedirect=true: the handler returns 200 OK
        // but with RequestMessage.RequestUri pointing to a different host, as if the client
        // already followed a redirect internally.
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

        var options = new ODataMetadataOptions { HttpClient = client, AllowCrossOriginRedirect = true };
        var metadata = await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options);

        Assert.IsNotNull(metadata);
    }

    // -----------------------------------------------------------------------
    // Item 30 / defect 3: DownloadTimeout covers the body read, not only the
    // connection/headers phase.
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_SlowBody_CancelledByDownloadTimeout()
    {
        using var handler = new RecordingHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new NeverEndingStream()),
                RequestMessage = request
            };
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);

        var options = new ODataMetadataOptions
        {
            HttpClient = client,
            DownloadTimeout = TimeSpan.FromMilliseconds(200)
        };

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync("https://service.example.com/$metadata", options));
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

    // -----------------------------------------------------------------------
    // Test infrastructure
    // -----------------------------------------------------------------------

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

    /// <summary>A read stream that blocks until cancelled, simulating a very slow server body.</summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Task.Delay(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
