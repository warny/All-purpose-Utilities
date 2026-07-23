using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Unit tests for <see cref="ErrorReturnValue"/> validation and error categorisation
/// (audit items 31 and 32).
/// </summary>
[TestClass]
public class ErrorReturnValueTests
{
    // -----------------------------------------------------------------------
    // Item 32: message validation (null / empty / whitespace all rejected)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Constructor_NullMessage_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => new ErrorReturnValue(1, null!));
    }

    [TestMethod]
    public void Constructor_EmptyMessage_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => new ErrorReturnValue(1, string.Empty));
    }

    [TestMethod]
    public void Constructor_WhitespaceMessage_ThrowsArgumentException()
    {
        // Item 32: whitespace-only messages must be rejected, not just null/empty.
        Assert.ThrowsException<ArgumentException>(() => new ErrorReturnValue(1, "   "));
    }

    [TestMethod]
    public void Constructor_ValidMessage_Succeeds()
    {
        var error = new ErrorReturnValue(42, "boom");
        Assert.AreEqual(42, error.code);
        Assert.AreEqual("boom", error.message);
    }

    // -----------------------------------------------------------------------
    // Item 31: error kind / category and optional HTTP status
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Constructor_LegacyOverload_DefaultsToUnspecifiedKindAndNoHttpStatus()
    {
        var error = new ErrorReturnValue(1, "No data returned.");
        Assert.AreEqual(ODataErrorKind.Unspecified, error.Kind);
        Assert.IsNull(error.HttpStatusCode);
    }

    [TestMethod]
    public void Constructor_WithKindAndStatus_PopulatesBothFields()
    {
        var error = new ErrorReturnValue(-404, "404 Not Found", ODataErrorKind.Transport, 404);
        Assert.AreEqual(ODataErrorKind.Transport, error.Kind);
        Assert.AreEqual(404, error.HttpStatusCode);
        Assert.AreEqual(-404, error.code);
    }

    [TestMethod]
    public void Constructor_WithKindOnly_LeavesHttpStatusNull()
    {
        var error = new ErrorReturnValue(-11, "Metadata document is empty.", ODataErrorKind.Metadata);
        Assert.AreEqual(ODataErrorKind.Metadata, error.Kind);
        Assert.IsNull(error.HttpStatusCode);
    }
}
