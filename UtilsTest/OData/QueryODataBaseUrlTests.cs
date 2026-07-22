using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="QueryOData"/> base URL validation (item 9 of the audit).
/// Validates that the constructor rejects unsupported schemes and malformed URLs
/// at construction time rather than deferring errors to request time.
/// </summary>
[TestClass]
public class QueryODataBaseUrlTests
{
    // -----------------------------------------------------------------------
    // Valid base URLs
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Constructor_HttpUrl_DoesNotThrow()
    {
        using var client = new System.Net.Http.HttpClient();
        using var sut = new QueryOData("http://example.org/odata", client);
        Assert.AreEqual("http://example.org/odata", sut.BaseUrl);
    }

    [TestMethod]
    public void Constructor_HttpsUrl_DoesNotThrow()
    {
        using var client = new System.Net.Http.HttpClient();
        using var sut = new QueryOData("https://example.org/odata", client);
        Assert.AreEqual("https://example.org/odata", sut.BaseUrl);
    }

    // -----------------------------------------------------------------------
    // Invalid base URLs — must fail at construction time (not later)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Constructor_NullBaseUrl_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new QueryOData(null!));
    }

    [TestMethod]
    public void Constructor_NullBaseUrl_WithClient_ThrowsArgumentNullException()
    {
        using var client = new System.Net.Http.HttpClient();
        Assert.ThrowsException<ArgumentNullException>(
            () => new QueryOData(null!, client));
    }

    [TestMethod]
    public void Constructor_RelativeUrl_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData("/odata"));
    }

    [TestMethod]
    public void Constructor_NonHttpScheme_File_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData("file:///C:/data"));
    }

    [TestMethod]
    public void Constructor_NonHttpScheme_Ftp_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData("ftp://example.org/data"));
    }

    [TestMethod]
    public void Constructor_PlainString_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData("not-a-url"));
    }

    [TestMethod]
    public void Constructor_EmptyString_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData(string.Empty));
    }

    [TestMethod]
    public void Constructor_WhitespaceString_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new QueryOData("   "));
    }
}
