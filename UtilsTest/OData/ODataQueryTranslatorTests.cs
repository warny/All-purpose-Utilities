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
    private sealed class Item
    {
        public int Quantity { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

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
        return compiled.ToUriString();
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
}
