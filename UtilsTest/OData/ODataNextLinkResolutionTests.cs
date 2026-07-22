using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Unit tests for <see cref="QueryOData.ResolveAndValidateNextLink"/>.
/// Verifies that relative <c>@odata.nextLink</c> values are resolved against the context URI
/// (the page that provided them), not the service root, and that the same-origin check is
/// always evaluated against <c>BaseUrl</c>.
/// </summary>
[TestClass]
public class ODataNextLinkResolutionTests
{
    private static Uri BaseUri(string url) => new(url, UriKind.Absolute);
    private static Uri Context(string url) => new(url, UriKind.Absolute);

    // -----------------------------------------------------------------------
    // Relative nextLink with path segment — the classic segmented-base bug
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RelativeNextLink_WithPathSegment_ResolvesAgainstContextNotServiceRoot()
    {
        // BaseUrl = https://server/odata (no trailing slash).
        // If resolved against the base root, URI resolution replaces the last path segment
        // and produces https://server/Products?$skiptoken=abc (wrong — drops /odata/).
        // Resolved against the context URI of the page, the path is preserved correctly.
        var baseUri = BaseUri("https://server/odata");
        var contextUri = Context("https://server/odata/Products?$top=100");

        var (resolved, error) = QueryOData.ResolveAndValidateNextLink(
            "Products?$skiptoken=abc", contextUri, baseUri);

        Assert.IsNull(error, $"Unexpected error: {error?.message}");
        Assert.AreEqual(
            "https://server/odata/Products?$skiptoken=abc", resolved,
            "Path-relative nextLink must be resolved against the context URI so the /odata/ segment is preserved.");
    }

    // -----------------------------------------------------------------------
    // Query-string-only nextLink — must keep the entity path
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RelativeNextLink_QueryStringOnly_PreservesEntityPath()
    {
        // A link like ?$skiptoken=abc refers to the current resource with a new query string.
        // It must keep the /odata/Products path of the current page, not collapse to /odata.
        var baseUri = BaseUri("https://server/odata");
        var contextUri = Context("https://server/odata/Products?$top=100&$filter=Active eq true");

        var (resolved, error) = QueryOData.ResolveAndValidateNextLink(
            "?$skiptoken=abc", contextUri, baseUri);

        Assert.IsNull(error, $"Unexpected error: {error?.message}");
        Assert.AreEqual(
            "https://server/odata/Products?$skiptoken=abc", resolved,
            "Query-string-only nextLink must preserve the entity path from the context URI.");
    }

    // -----------------------------------------------------------------------
    // Absolute same-origin nextLink — must be accepted unchanged
    // -----------------------------------------------------------------------

    [TestMethod]
    public void AbsoluteNextLink_SameOrigin_IsAccepted()
    {
        var baseUri = BaseUri("https://server/odata");
        var contextUri = Context("https://server/odata/Products?$top=100");
        const string nextLink = "https://server/odata/Products?$skiptoken=def";

        var (resolved, error) = QueryOData.ResolveAndValidateNextLink(nextLink, contextUri, baseUri);

        Assert.IsNull(error, $"Unexpected error: {error?.message}");
        Assert.AreEqual(nextLink, resolved,
            "An absolute same-origin nextLink must be returned as-is.");
    }

    // -----------------------------------------------------------------------
    // Absolute cross-origin nextLink — must be rejected
    // -----------------------------------------------------------------------

    [TestMethod]
    public void AbsoluteNextLink_DifferentOrigin_IsRejected()
    {
        var baseUri = BaseUri("https://server/odata");
        var contextUri = Context("https://server/odata/Products?$top=100");

        var (resolved, error) = QueryOData.ResolveAndValidateNextLink(
            "https://evil.server/odata/Products?$skiptoken=xyz", contextUri, baseUri);

        Assert.IsNull(resolved, "No resolved link must be returned for a cross-origin nextLink.");
        Assert.IsNotNull(error, "A cross-origin nextLink must produce an error.");
        StringAssert.Contains(error!.message, "outside the allowed service origin",
            "Error message must identify the same-origin rejection reason.");
    }
}
