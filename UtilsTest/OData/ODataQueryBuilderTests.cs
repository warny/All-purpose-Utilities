using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="ODataQueryBuilder"/> covering URI generation correctness
/// (items 1, 2, 3, 5 of the Utils.OData audit).
/// </summary>
[TestClass]
public class ODataQueryBuilderTests
{
    private static string BuildUrl(IQuery query, string baseUrl = "https://example.org/odata", int skip = 0)
    {
        string raw = new ODataQueryBuilder(baseUrl, query, skip).Url;
        return Uri.UnescapeDataString(raw);
    }

    // -----------------------------------------------------------------------
    // Item 1 — Boolean parameter omission/emission (P0)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Count_False_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Count = false };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$count", StringComparison.OrdinalIgnoreCase),
            $"$count should be omitted when false but found in: {url}");
    }

    [TestMethod]
    public void Count_True_IsEmittedAsLowercase()
    {
        var query = new Query { Table = "Products", Count = true };
        string url = BuildUrl(query);
        StringAssert.Contains(url, "$count=true",
            $"$count=true should be present when Count is true, got: {url}");
        Assert.IsFalse(url.Contains("$count=True", StringComparison.Ordinal),
            "Boolean must be serialized as lowercase OData literal");
    }

    // -----------------------------------------------------------------------
    // Item 2 — String parameter removal/assignment correctness (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Filter_Null_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Filters = null };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$filter", StringComparison.OrdinalIgnoreCase),
            $"$filter should be absent when null: {url}");
    }

    [TestMethod]
    public void Filter_WhitespaceOnly_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Filters = "   " };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$filter", StringComparison.OrdinalIgnoreCase),
            $"$filter should be absent when whitespace-only: {url}");
    }

    [TestMethod]
    public void Filter_EmptyString_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Filters = "" };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$filter", StringComparison.OrdinalIgnoreCase),
            $"$filter should be absent when empty: {url}");
    }

    [TestMethod]
    public void Filter_ValidValue_IsIncludedInUrl()
    {
        var query = new Query { Table = "Products", Filters = "Name eq 'Widget'" };
        string url = BuildUrl(query);
        Assert.IsTrue(url.Contains("$filter=", StringComparison.OrdinalIgnoreCase),
            $"$filter should be present when non-empty, got: {url}");
    }

    [TestMethod]
    public void Select_Null_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Select = null };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$select", StringComparison.OrdinalIgnoreCase),
            $"$select should be absent when null: {url}");
    }

    [TestMethod]
    public void Search_EmptyString_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Search = "" };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$search", StringComparison.OrdinalIgnoreCase),
            $"$search should be absent when empty: {url}");
    }

    // -----------------------------------------------------------------------
    // Item 3 — Numeric options use invariant culture (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Top_UsesInvariantCulture_UnderArabicCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var query = new Query { Table = "Products", Top = 100 };
            string url = BuildUrl(query);
            StringAssert.Contains(url, "$top=100",
                $"$top must use invariant digits, got: {url}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Skip_UsesInvariantCulture_UnderFarsiCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            var query = new Query { Table = "Products" };
            string url = BuildUrl(query, skip: 50);
            StringAssert.Contains(url, "$skip=50",
                $"$skip must use invariant digits, got: {url}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Skip_UsesInvariantCulture_UnderFrenchCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var query = new Query { Table = "Products", Top = 1000 };
            string url = BuildUrl(query, skip: 2000);
            StringAssert.Contains(url, "$top=1000", $"$top must not use culture-specific separators: {url}");
            StringAssert.Contains(url, "$skip=2000", $"$skip must not use culture-specific separators: {url}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // -----------------------------------------------------------------------
    // Item 5 — Paging input validation (P1)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void NegativeSkipArgument_ThrowsArgumentOutOfRange()
    {
        var query = new Query { Table = "Products" };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BuildUrl(query, skip: -1));
    }

    [TestMethod]
    public void NegativeQuerySkip_ThrowsArgumentOutOfRange()
    {
        var query = new Query { Table = "Products", Skip = -5 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BuildUrl(query));
    }

    [TestMethod]
    public void ZeroTop_ThrowsArgumentOutOfRange()
    {
        var query = new Query { Table = "Products", Top = 0 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BuildUrl(query));
    }

    [TestMethod]
    public void NegativeTop_ThrowsArgumentOutOfRange()
    {
        var query = new Query { Table = "Products", Top = -10 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BuildUrl(query));
    }

    [TestMethod]
    public void OverflowingCombinedSkip_ThrowsOverflowException()
    {
        var query = new Query { Table = "Products", Skip = int.MaxValue };
        Assert.ThrowsException<OverflowException>(() => BuildUrl(query, skip: 1));
    }

    [TestMethod]
    public void ZeroSkip_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products" };
        string url = BuildUrl(query, skip: 0);
        Assert.IsFalse(url.Contains("$skip=0", StringComparison.OrdinalIgnoreCase),
            $"$skip=0 is redundant and should be omitted: {url}");
    }

    [TestMethod]
    public void ZeroQuerySkip_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Skip = 0 };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$skip=0", StringComparison.OrdinalIgnoreCase),
            $"$skip=0 is redundant and should be omitted: {url}");
    }

    [TestMethod]
    public void CombinedSkip_IsComputedCorrectly()
    {
        var query = new Query { Table = "Products", Skip = 10 };
        string url = BuildUrl(query, skip: 20);
        StringAssert.Contains(url, "$skip=30",
            $"Combined skip should be 30, got: {url}");
    }

    // -----------------------------------------------------------------------
    // General URL construction
    // -----------------------------------------------------------------------

    [TestMethod]
    public void NullBaseUrl_ThrowsArgumentNullException()
    {
        var query = new Query { Table = "Products" };
        Assert.ThrowsException<ArgumentNullException>(() => new ODataQueryBuilder(null!, query));
    }

    [TestMethod]
    public void NullQuery_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new ODataQueryBuilder("https://example.org", null!));
    }

    [TestMethod]
    public void AllDefaultOptions_ProducesMinimalUrl()
    {
        var query = new Query { Table = "Products" };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$count"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$skip"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$top"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$filter"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$orderby"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$select"), $"No optional params should be emitted: {url}");
        Assert.IsFalse(url.Contains("$search"), $"No optional params should be emitted: {url}");
    }

    [TestMethod]
    public void Top_Null_IsOmittedFromUrl()
    {
        var query = new Query { Table = "Products", Top = null };
        string url = BuildUrl(query);
        Assert.IsFalse(url.Contains("$top", StringComparison.OrdinalIgnoreCase),
            $"$top should be absent when null: {url}");
    }

    [TestMethod]
    public void AllOptionsSpecified_AllPresentInUrl()
    {
        var query = new Query
        {
            Table = "Products",
            Select = "Id,Name",
            Filters = "Id gt 0",
            OrderBy = "Name asc",
            Skip = 5,
            Top = 10,
            Count = true,
            Search = "widget"
        };
        string url = BuildUrl(query);
        Assert.IsTrue(url.Contains("$select="), $"Missing $select: {url}");
        Assert.IsTrue(url.Contains("$filter="), $"Missing $filter: {url}");
        Assert.IsTrue(url.Contains("$orderby="), $"Missing $orderby: {url}");
        Assert.IsTrue(url.Contains("$skip="), $"Missing $skip: {url}");
        Assert.IsTrue(url.Contains("$top="), $"Missing $top: {url}");
        Assert.IsTrue(url.Contains("$count=true"), $"Missing $count=true: {url}");
        Assert.IsTrue(url.Contains("$search="), $"Missing $search: {url}");
    }
}
