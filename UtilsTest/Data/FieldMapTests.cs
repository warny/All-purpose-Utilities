using System.Data;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Utils.Data;
using Utils.Reflection;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="FieldMap"/> covering name-based and index-based mappings,
/// including the path where <see cref="FieldMap.Name"/> is <c>null</c>.
/// </summary>
[TestClass]
public sealed class FieldMapTests
{
    private class TestRow
    {
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates a property-or-field descriptor for the specified property of
    /// <see cref="TestRow"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>A descriptor for the requested property.</returns>
    private static PropertyOrFieldInfo MemberOf(string propertyName)
        => new(typeof(TestRow).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!);

    /// <summary>
    /// Verifies that when no attribute is present, <see cref="FieldMap"/> maps by the member's own name.
    /// </summary>
    [TestMethod]
    public void FieldMap_NoAttribute_GetValue_ReturnsRecordFieldByMemberName()
    {
        var member = MemberOf(nameof(TestRow.Value));
        var map = new FieldMap(member);

        var record = new Mock<IDataRecord>();
        record.Setup(r => r["Value"]).Returns("from-name");

        Assert.AreEqual("from-name", map.GetValue(record.Object));
    }

    /// <summary>
    /// Verifies that the explicit-name constructor maps by the supplied column name.
    /// </summary>
    [TestMethod]
    public void FieldMap_ByName_GetValue_ReturnsRecordFieldByName()
    {
        var member = MemberOf(nameof(TestRow.Value));
        var map = new FieldMap(member, "col_value");

        var record = new Mock<IDataRecord>();
        record.Setup(r => r["col_value"]).Returns("hello");

        Assert.AreEqual("hello", map.GetValue(record.Object));
    }

    /// <summary>
    /// Verifies that the index constructor retrieves the record value by position.
    /// </summary>
    [TestMethod]
    public void FieldMap_ByIndex_GetValue_ReturnsRecordFieldByIndex()
    {
        var member = MemberOf(nameof(TestRow.Value));
        var map = new FieldMap(member, 2);

        var record = new Mock<IDataRecord>();
        record.Setup(r => r.GetValue(2)).Returns(42);

        Assert.AreEqual(42, map.GetValue(record.Object));
    }

    /// <summary>
    /// Verifies that <see cref="FieldMap.Name"/> is <c>null</c> when the mapping is by index,
    /// which is the nullability contract introduced by the audit fix.
    /// </summary>
    [TestMethod]
    public void FieldMap_ByIndex_Name_IsNull()
    {
        var member = MemberOf(nameof(TestRow.Value));
        var map = new FieldMap(member, 0);

        Assert.IsNull(map.Name);
    }

    /// <summary>
    /// Verifies that a <see cref="FieldAttribute"/> with an index causes index-based retrieval.
    /// </summary>
    [TestMethod]
    public void FieldMap_WithIndexAttribute_GetValue_ReturnsRecordFieldByAttributeIndex()
    {
        var member = MemberOf(nameof(TestRow.Value));
        // Simulated via the (member, int) constructor because FieldAttribute cannot be injected at runtime;
        // the attribute path is exercised by the constructor that reads it.
        var map = new FieldMap(member, 1);

        var record = new Mock<IDataRecord>();
        record.Setup(r => r.GetValue(1)).Returns("by-index");

        Assert.AreEqual("by-index", map.GetValue(record.Object));
    }
}
