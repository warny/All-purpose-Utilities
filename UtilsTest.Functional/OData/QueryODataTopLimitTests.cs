using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Integration tests verifying that <see cref="QueryOData"/> strictly respects
/// <see cref="IQuery.Top"/> even when the OData service returns pages that are larger
/// than the remaining row quota — both when following <c>@odata.nextLink</c> and when
/// the server silently ignores <c>$top</c> on the first page.
/// </summary>
[TestClass]
public class QueryODataTopLimitTests
{
    // -----------------------------------------------------------------------
    // QueryToJSon tests
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task QueryToJSon_Top150_TwoPages100Each_ReturnsExactly150Rows()
    {
        // Arrange: the server returns 100 rows on the first page with a nextLink,
        // and another 100 rows on the second page. The caller requested only 150.
        string baseUrl = StartODataServer(
            BuildTwoPageHandler(rowsPerPage: 100),
            out HttpListener listener,
            out Task serverTask);
        try
        {
            using var client = new QueryOData(baseUrl);
            var query = new Query { Table = "Products", Top = 150 };

            var result = await client.QueryToJSon(query);

            Assert.IsFalse(result.IsError, $"Unexpected error: {result.Error?.message}");
            Assert.IsNotNull(result.Value.Datas);
            Assert.AreEqual(150, result.Value.Datas!.Count,
                "QueryToJSon must return exactly 150 rows, not the 200 rows available across two full pages.");
        }
        finally
        {
            listener.Stop();
            listener.Close();
            await serverTask.ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task QueryToJSon_Top50_FirstPageHas100Rows_ReturnsExactly50Rows()
    {
        // Arrange: the server ignores $top and returns 100 rows on a single page.
        // The caller requested only 50.
        string baseUrl = StartODataServer(
            BuildSinglePageHandler(rowCount: 100),
            out HttpListener listener,
            out Task serverTask);
        try
        {
            using var client = new QueryOData(baseUrl);
            var query = new Query { Table = "Products", Top = 50 };

            var result = await client.QueryToJSon(query);

            Assert.IsFalse(result.IsError, $"Unexpected error: {result.Error?.message}");
            Assert.IsNotNull(result.Value.Datas);
            Assert.AreEqual(50, result.Value.Datas!.Count,
                "QueryToJSon must cap the result at 50 rows even when the first page returns 100.");
        }
        finally
        {
            listener.Stop();
            listener.Close();
            await serverTask.ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // QueryToDataReader tests
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task QueryToDataReader_Top150_TwoPages100Each_ReturnsExactly150Rows()
    {
        string metadataContent = GetSampleEdmxContent();
        string baseUrl = StartODataServer(
            BuildTwoPageHandlerWithMetadata(rowsPerPage: 100, metadataContent),
            out HttpListener listener,
            out Task serverTask);
        try
        {
            using var client = new QueryOData(baseUrl);
            var query = new Query { Table = "Products", Top = 150 };

            var readerResult = await client.QueryToDataReader(query);
            Assert.IsFalse(readerResult.IsError, $"Unexpected error: {readerResult.Error?.message}");

            using IDataReader reader = readerResult.Value;
            int rowCount = 0;
            while (reader.Read())
                rowCount++;

            Assert.AreEqual(150, rowCount,
                "QueryToDataReader must yield exactly 150 rows, not the 200 rows available across two full pages.");
        }
        finally
        {
            listener.Stop();
            listener.Close();
            await serverTask.ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task QueryToDataReader_Top50_FirstPageHas100Rows_ReturnsExactly50Rows()
    {
        string metadataContent = GetSampleEdmxContent();
        string baseUrl = StartODataServer(
            BuildSinglePageHandlerWithMetadata(rowCount: 100, metadataContent),
            out HttpListener listener,
            out Task serverTask);
        try
        {
            using var client = new QueryOData(baseUrl);
            var query = new Query { Table = "Products", Top = 50 };

            var readerResult = await client.QueryToDataReader(query);
            Assert.IsFalse(readerResult.IsError, $"Unexpected error: {readerResult.Error?.message}");

            using IDataReader reader = readerResult.Value;
            int rowCount = 0;
            while (reader.Read())
                rowCount++;

            Assert.AreEqual(50, rowCount,
                "QueryToDataReader must yield exactly 50 rows even when the first page returns 100.");
        }
        finally
        {
            listener.Stop();
            listener.Close();
            await serverTask.ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Response handler factories
    // -----------------------------------------------------------------------

    private static Func<HttpListenerRequest, (int, string, string)> BuildTwoPageHandler(int rowsPerPage)
    {
        return request =>
        {
            // Detect the second page by the skip-token in the query string.
            string query = request.Url?.Query ?? string.Empty;
            if (query.Contains("skiptoken=page2", StringComparison.OrdinalIgnoreCase))
                return (200, "application/json", MakeODataPage(startId: rowsPerPage + 1, count: rowsPerPage, nextLink: null));

            // First page includes a nextLink pointing at the second page.
            string origin = request.Url!.GetLeftPart(UriPartial.Authority);
            string nextLink = $"{origin}/Products?$skiptoken=page2";
            return (200, "application/json", MakeODataPage(startId: 1, count: rowsPerPage, nextLink: nextLink));
        };
    }

    private static Func<HttpListenerRequest, (int, string, string)> BuildSinglePageHandler(int rowCount)
    {
        return _ => (200, "application/json", MakeODataPage(startId: 1, count: rowCount, nextLink: null));
    }

    private static Func<HttpListenerRequest, (int, string, string)> BuildTwoPageHandlerWithMetadata(
        int rowsPerPage, string metadataContent)
    {
        var dataHandler = BuildTwoPageHandler(rowsPerPage);
        return request =>
        {
            if (string.Equals(request.Url?.AbsolutePath, "/$metadata", StringComparison.OrdinalIgnoreCase))
                return (200, "application/xml", metadataContent);
            return dataHandler(request);
        };
    }

    private static Func<HttpListenerRequest, (int, string, string)> BuildSinglePageHandlerWithMetadata(
        int rowCount, string metadataContent)
    {
        var dataHandler = BuildSinglePageHandler(rowCount);
        return request =>
        {
            if (string.Equals(request.Url?.AbsolutePath, "/$metadata", StringComparison.OrdinalIgnoreCase))
                return (200, "application/xml", metadataContent);
            return dataHandler(request);
        };
    }

    // -----------------------------------------------------------------------
    // HTTP server helpers
    // -----------------------------------------------------------------------

    private static string StartODataServer(
        Func<HttpListenerRequest, (int statusCode, string contentType, string body)> handler,
        out HttpListener listener,
        out Task serverTask)
    {
        int port = ReserveEphemeralPort();
        string prefix = $"http://127.0.0.1:{port}/";

        listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        HttpListener capturedListener = listener;
        serverTask = Task.Run(async () =>
        {
            while (capturedListener.IsListening)
            {
                try
                {
                    var context = await capturedListener.GetContextAsync().ConfigureAwait(false);
                    var (statusCode, contentType, body) = handler(context.Request);
                    context.Response.StatusCode = statusCode;
                    context.Response.ContentType = contentType;
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    context.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }
            }
        });

        return $"http://127.0.0.1:{port}";
    }

    private static int ReserveEphemeralPort()
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    // -----------------------------------------------------------------------
    // Data helpers
    // -----------------------------------------------------------------------

    private static string MakeODataPage(int startId, int count, string? nextLink)
    {
        var sb = new StringBuilder();
        sb.Append("{\"@odata.context\":\"$metadata#Products\",\"value\":[");
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"Id\":{startId + i},\"Name\":\"Row{startId + i}\"}}");
        }
        sb.Append(']');
        if (nextLink is not null)
            sb.Append($",\"@odata.nextLink\":\"{nextLink}\"");
        sb.Append('}');
        return sb.ToString();
    }

    private static string GetSampleEdmxContent()
    {
        string? baseDirectory = AppContext.BaseDirectory;
        if (baseDirectory is null)
            throw new InvalidOperationException("Unable to resolve the base directory of the test run.");

        string path = Path.GetFullPath(
            Path.Combine(baseDirectory, "..", "..", "..", "OData", "TestData", "Sample.edmx"));
        if (!File.Exists(path))
            throw new FileNotFoundException("Sample EDMX metadata file not found for tests.", path);

        return File.ReadAllText(path);
    }
}
