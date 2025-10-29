using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Contains tests verifying that HTTP resources used by <see cref="QueryOData"/> are properly disposed.
/// </summary>
[TestClass]
public class QueryODataResponseDisposalTests
{
    /// <summary>
    /// Ensures that the HTTP response used for JSON queries is disposed after the payload stream is consumed.
    /// </summary>
    [TestMethod]
    public async Task QueryToJson_DisposesHttpResponseAfterStreamConsumption()
    {
        using var handler = new DisposalTrackingHandler();
        using var client = new HttpClient(handler);
        using var query = new QueryOData("https://service.example.com", client);

        var parameter = new Query
        {
            Table = "Entities",
            Filters = string.Empty,
            Select = "*",
            Top = 1
        };

        var result = await query.QueryToJSon(parameter);

        Assert.IsTrue(result.IsSuccess, "The query should succeed.");
        Assert.IsTrue(handler.LastResponseDisposed, "The HTTP response should be disposed after consumption.");
    }

    /// <summary>
    /// HTTP handler that tracks whether created responses are disposed.
    /// </summary>
    private sealed class DisposalTrackingHandler : HttpMessageHandler
    {
        private bool _responseDisposed;

        /// <summary>
        /// Gets a value indicating whether the last response has been disposed.
        /// </summary>
        public bool LastResponseDisposed => _responseDisposed;

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _responseDisposed = false;
            string payload = "{\"@odata.context\":\"https://service.example.com/$metadata#Entities\",\"value\":[{\"Id\":0}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new TrackingStringContent(payload, () => _responseDisposed = true)
            };

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Custom <see cref="StringContent"/> that invokes a callback when disposed.
    /// </summary>
    private sealed class TrackingStringContent : StringContent
    {
        private readonly Action _onDispose;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackingStringContent"/> class.
        /// </summary>
        /// <param name="content">Content emitted by the HTTP response.</param>
        /// <param name="onDispose">Callback invoked when the content is disposed.</param>
        public TrackingStringContent(string content, Action onDispose)
            : base(content, Encoding.UTF8, "application/json")
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _onDispose();
            }
        }
    }
}
