using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData.Linq;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="ODataQueryTranslator"/> covering audit item 11:
/// — Reversed comparisons (member on the right) are normalised to member-on-left
///   with the operator flipped.
/// </summary>
[TestClass]
public class ODataQueryTranslatorTests
{
    /// <summary>Entity type used as the element type in translator tests.</summary>
    private sealed class Item
    {
        public int Quantity { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Translates <paramref name="predicate"/> against the <c>Items</c> entity set and returns
    /// the percent-decoded OData URI string.
    /// </summary>
    /// <remarks>
    /// <c>ToUriString()</c> percent-encodes query option values (item 26); decoding here keeps the
    /// assertions focused on OData expression logic rather than URI encoding rules.
    /// </remarks>
    private static string Filter<T>(Expression<Func<Item, bool>> predicate)
    {
        var queryable = new ODataQueryable<Item>(
            new ODataQueryProvider("Items"), "Items");
        var compiled = ODataQueryTranslator.Translate(
            Expression.Call(
                typeof(System.Linq.Queryable),
                nameof(System.Linq.Queryable.Where),
                [typeof(Item)],
                Expression.Constant(queryable),
                predicate),
            "Items");
        return Uri.UnescapeDataString(compiled.ToUriString());
    }

    // -----------------------------------------------------------------------
    // Standard (member on left) — must still work
    // -----------------------------------------------------------------------

    [TestMethod]
    public void MemberLeft_Equal_ProducesEq()
    {
        var uri = Filter<Item>(x => x.Quantity == 10);
        StringAssert.Contains(uri, "Quantity eq 10");
    }

    [TestMethod]
    public void MemberLeft_GreaterThan_ProducesGt()
    {
        var uri = Filter<Item>(x => x.Quantity > 5);
        StringAssert.Contains(uri, "Quantity gt 5");
    }

    [TestMethod]
    public void MemberLeft_LessThan_ProducesLt()
    {
        var uri = Filter<Item>(x => x.Quantity < 100);
        StringAssert.Contains(uri, "Quantity lt 100");
    }

    // -----------------------------------------------------------------------
    // Item 11 — reversed comparisons (value on left, member on right)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ValueLeft_Equal_ProducesEq()
    {
        // 10 == x.Quantity  →  Quantity eq 10
        var uri = Filter<Item>(x => 10 == x.Quantity);
        StringAssert.Contains(uri, "Quantity eq 10");
    }

    [TestMethod]
    public void ValueLeft_LessThan_NormalisedToGt()
    {
        // 5 < x.Quantity  →  Quantity gt 5
        var uri = Filter<Item>(x => 5 < x.Quantity);
        StringAssert.Contains(uri, "Quantity gt 5");
    }

    [TestMethod]
    public void ValueLeft_LessThanOrEqual_NormalisedToGe()
    {
        // 5 <= x.Quantity  →  Quantity ge 5
        var uri = Filter<Item>(x => 5 <= x.Quantity);
        StringAssert.Contains(uri, "Quantity ge 5");
    }

    [TestMethod]
    public void ValueLeft_GreaterThan_NormalisedToLt()
    {
        // 100 > x.Quantity  →  Quantity lt 100
        var uri = Filter<Item>(x => 100 > x.Quantity);
        StringAssert.Contains(uri, "Quantity lt 100");
    }

    [TestMethod]
    public void ValueLeft_GreaterThanOrEqual_NormalisedToLe()
    {
        // 100 >= x.Quantity  →  Quantity le 100
        var uri = Filter<Item>(x => 100 >= x.Quantity);
        StringAssert.Contains(uri, "Quantity le 100");
    }

    [TestMethod]
    public void ValueLeft_NotEqual_ProducesNe()
    {
        // 0 != x.Quantity  →  Quantity ne 0
        var uri = Filter<Item>(x => 0 != x.Quantity);
        StringAssert.Contains(uri, "Quantity ne 0");
    }

    // -----------------------------------------------------------------------
    // Item 11 — captured variables must be evaluated, not emitted as paths
    // -----------------------------------------------------------------------

    [TestMethod]
    public void CapturedVariable_MemberLeft_EvaluatesLocally()
    {
        // threshold is a captured local — must become the literal 5, not "threshold"
        int threshold = 5;
        var uri = Filter<Item>(x => x.Quantity > threshold);
        StringAssert.Contains(uri, "Quantity gt 5",
            "Captured variable must be evaluated to its value, not emitted as a path.");
        StringAssert.DoesNotMatch(uri, new System.Text.RegularExpressions.Regex("threshold"),
            "The identifier 'threshold' must never appear in the generated filter.");
    }

    [TestMethod]
    public void CapturedVariable_ValueLeft_NormalisedAndEvaluated()
    {
        // threshold < x.Quantity  →  Quantity gt 5
        int threshold = 5;
        var uri = Filter<Item>(x => threshold < x.Quantity);
        StringAssert.Contains(uri, "Quantity gt 5",
            "Reversed comparison with captured variable must normalise and evaluate correctly.");
        StringAssert.DoesNotMatch(uri, new System.Text.RegularExpressions.Regex("threshold"),
            "The identifier 'threshold' must never appear in the generated filter.");
    }

    // -----------------------------------------------------------------------
    // Item 10 — safe evaluation: method calls must be rejected
    // -----------------------------------------------------------------------

    private static int GetThreshold() => 5;

    [TestMethod]
    public void MethodCallInFilter_ThrowsNotSupported()
    {
        // item 10: arbitrary method calls must not be compiled and invoked during
        // query translation; NotSupportedException is the required signal.
        Assert.ThrowsException<NotSupportedException>(
            () => Filter<Item>(x => x.Quantity > GetThreshold()),
            "A method call in a filter value expression must throw NotSupportedException.");
    }

    [TestMethod]
    public void NewArrayInExpand_EvaluatesCorrectly()
    {
        // item 10: new-array initialisers are in the safe subset and must work for Expand.
        var queryable = new ODataQueryable<Item>(new ODataQueryProvider("Items"), "Items");
        var compiled = ODataQueryTranslator.Translate(
            Expression.Call(
                typeof(ODataQueryableExtensions),
                nameof(ODataQueryableExtensions.Expand),
                [typeof(Item)],
                Expression.Constant(queryable),
                Expression.NewArrayInit(typeof(string),
                    Expression.Constant("Orders"),
                    Expression.Constant("Tags"))),
            "Items");
        string uri = Uri.UnescapeDataString(compiled.ToUriString());
        StringAssert.Contains(uri, "$expand=Orders,Tags",
            "New-array initialisers must be safely evaluated when used in Expand calls.");
    }
}
