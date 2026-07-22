using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;

namespace UtilsTest.OData;

/// <summary>
/// Tests for <see cref="EdmFieldConverterRegistry"/> covering audit items 21-25:
/// — Conversion failures return DBNull, never the wrong CLR type (items 21, 22)
/// — Edm.Date maps to DateOnly (item 23)
/// — Edm.TimeOfDay maps to TimeOnly, Edm.Duration uses ISO 8601 parser (item 24)
/// — Collection and unknown types use the string/default converter (item 25)
/// </summary>
[TestClass]
public class EdmFieldConverterRegistryTests
{
    // -----------------------------------------------------------------------
    // Helper — uses the internal test-facing overload to avoid Property ambiguity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the converter for <paramref name="edmType"/> and invokes it on <paramref name="node"/>.
    /// </summary>
    private static object Convert(string edmType, JsonNode? node)
    {
        var conv = EdmFieldConverterRegistry.ResolveByEdmType(edmType);
        return conv.Converter(node);
    }

    /// <summary>Returns the advertised CLR type for the given EDM type string.</summary>
    private static Type ClrType(string edmType)
        => EdmFieldConverterRegistry.ResolveByEdmType(edmType).ClrType;

    // -----------------------------------------------------------------------
    // Item 23 — Edm.Date → DateOnly
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EdmDate_ClrType_IsDateOnly()
        => Assert.AreEqual(typeof(DateOnly), ClrType("Edm.Date"));

    [TestMethod]
    public void EdmDate_ValidIsoDate_ReturnsDateOnly()
    {
        var result = Convert("Edm.Date", JsonValue.Create("2024-03-15"));
        Assert.IsInstanceOfType<DateOnly>(result);
        Assert.AreEqual(new DateOnly(2024, 3, 15), (DateOnly)result);
    }

    [TestMethod]
    public void EdmDate_NullNode_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.Date", null));

    [TestMethod]
    public void EdmDate_MalformedValue_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.Date", JsonValue.Create("not-a-date")));

    // -----------------------------------------------------------------------
    // Item 24 — Edm.TimeOfDay → TimeOnly
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EdmTimeOfDay_ClrType_IsTimeOnly()
        => Assert.AreEqual(typeof(TimeOnly), ClrType("Edm.TimeOfDay"));

    [TestMethod]
    public void EdmTimeOfDay_HhMmSs_ReturnsTimeOnly()
    {
        var result = Convert("Edm.TimeOfDay", JsonValue.Create("13:45:30"));
        Assert.IsInstanceOfType<TimeOnly>(result);
        Assert.AreEqual(new TimeOnly(13, 45, 30), (TimeOnly)result);
    }

    [TestMethod]
    public void EdmTimeOfDay_WithMilliseconds_ReturnsTimeOnly()
    {
        var result = Convert("Edm.TimeOfDay", JsonValue.Create("09:00:00.500"));
        Assert.IsInstanceOfType<TimeOnly>(result);
        var t = (TimeOnly)result;
        Assert.AreEqual(9, t.Hour);
        Assert.AreEqual(0, t.Minute);
        Assert.AreEqual(0, t.Second);
        Assert.AreEqual(500, t.Millisecond);
    }

    [TestMethod]
    public void EdmTimeOfDay_NullNode_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.TimeOfDay", null));

    [TestMethod]
    public void EdmTimeOfDay_MalformedValue_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.TimeOfDay", JsonValue.Create("not-a-time")));

    // -----------------------------------------------------------------------
    // Item 24 — Edm.Duration → TimeSpan (ISO 8601)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EdmDuration_ClrType_IsTimeSpan()
        => Assert.AreEqual(typeof(TimeSpan), ClrType("Edm.Duration"));

    [TestMethod]
    public void EdmDuration_Days_ReturnsTimeSpan()
    {
        var result = Convert("Edm.Duration", JsonValue.Create("P3D"));
        Assert.IsInstanceOfType<TimeSpan>(result);
        Assert.AreEqual(TimeSpan.FromDays(3), (TimeSpan)result);
    }

    [TestMethod]
    public void EdmDuration_HoursMinutesSeconds_ReturnsTimeSpan()
    {
        var result = Convert("Edm.Duration", JsonValue.Create("PT1H30M45S"));
        Assert.IsInstanceOfType<TimeSpan>(result);
        Assert.AreEqual(new TimeSpan(1, 30, 45), (TimeSpan)result);
    }

    [TestMethod]
    public void EdmDuration_DaysAndTime_ReturnsTimeSpan()
    {
        var result = Convert("Edm.Duration", JsonValue.Create("P1DT2H3M4S"));
        Assert.IsInstanceOfType<TimeSpan>(result);
        Assert.AreEqual(new TimeSpan(1, 2, 3, 4), (TimeSpan)result);
    }

    [TestMethod]
    public void EdmDuration_Negative_ReturnsNegativeTimeSpan()
    {
        var result = Convert("Edm.Duration", JsonValue.Create("-P1DT12H"));
        Assert.IsInstanceOfType<TimeSpan>(result);
        Assert.AreEqual(new TimeSpan(1, 12, 0, 0).Negate(), (TimeSpan)result);
    }

    [TestMethod]
    public void EdmDuration_NullNode_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.Duration", null));

    [TestMethod]
    public void EdmDuration_MalformedValue_ReturnsDBNull()
        => Assert.AreEqual(DBNull.Value, Convert("Edm.Duration", JsonValue.Create("1:30:00")));

    [TestMethod]
    public void EdmDuration_FractionalSecondsNearOne_DoesNotThrow()
    {
        // PT0.9999S must not produce ms=1000 (which the TimeSpan ctor rejects).
        var result = Convert("Edm.Duration", JsonValue.Create("PT0.9999S"));
        Assert.IsInstanceOfType<TimeSpan>(result, "PT0.9999S must parse to a TimeSpan, not DBNull.");
        Assert.IsTrue(((TimeSpan)result).TotalSeconds < 1.0,
            "PT0.9999S must be less than 1 second.");
    }

    [TestMethod]
    public void EdmDuration_OverflowingValue_ReturnsDBNull()
    {
        // A duration with absurdly large days exceeds TimeSpan.MaxValue — must return DBNull.
        var result = Convert("Edm.Duration", JsonValue.Create("P999999999999999999D"));
        Assert.AreEqual(DBNull.Value, result,
            "An overflowing duration must return DBNull, not throw.");
    }

    // -----------------------------------------------------------------------
    // Items 21+22 — Malformed values must not return wrong CLR type
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EdmInt32_MalformedValue_ReturnsDBNull_NotString()
    {
        var result = Convert("Edm.Int32", JsonValue.Create("not-an-int"));
        Assert.AreEqual(DBNull.Value, result,
            "A malformed Edm.Int32 value must return DBNull, not a raw string.");
    }

    [TestMethod]
    public void EdmGuid_MalformedValue_ReturnsDBNull_NotString()
    {
        var result = Convert("Edm.Guid", JsonValue.Create("not-a-guid"));
        Assert.AreEqual(DBNull.Value, result,
            "A malformed Edm.Guid value must return DBNull, not a raw string.");
    }

    [TestMethod]
    public void EdmBoolean_MalformedValue_ReturnsDBNull_NotString()
    {
        var result = Convert("Edm.Boolean", JsonValue.Create("yes"));
        Assert.AreEqual(DBNull.Value, result,
            "A malformed Edm.Boolean value must return DBNull, not a raw string.");
    }

    [TestMethod]
    public void EdmInt32_ValidValue_ClrTypeMatchesRuntimeType()
    {
        var conv = EdmFieldConverterRegistry.ResolveByEdmType("Edm.Int32");
        var value = conv.Converter(JsonValue.Create(42));
        if (value is not DBNull)
        {
            Assert.IsTrue(conv.ClrType.IsAssignableFrom(value.GetType()),
                $"Declared ClrType '{conv.ClrType}' must match runtime type '{value.GetType()}'.");
        }
    }

    // -----------------------------------------------------------------------
    // Item 25 — Collection and unknown types use DefaultConverter (string)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void CollectionType_UsesStringConverter()
    {
        Assert.AreEqual(typeof(string), ClrType("Collection(Edm.String)"),
            "Collection EDM types must use the string/default converter.");
    }

    [TestMethod]
    public void UnknownEdmType_UsesStringConverter()
    {
        Assert.AreEqual(typeof(string), ClrType("MyNamespace.CustomType"),
            "Unknown EDM types must use the string/default converter.");
    }

    [TestMethod]
    public void NullEdmType_UsesStringConverter()
    {
        Assert.AreEqual(typeof(string), EdmFieldConverterRegistry.ResolveByEdmType(null).ClrType);
    }

    // -----------------------------------------------------------------------
    // Supported primitives — basic smoke tests
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EdmGuid_ValidGuid_ReturnsGuid()
    {
        var g = Guid.NewGuid();
        var result = Convert("Edm.Guid", JsonValue.Create(g.ToString("D")));
        Assert.IsInstanceOfType<Guid>(result);
        Assert.AreEqual(g, (Guid)result);
    }

    [TestMethod]
    public void EdmBoolean_True_ReturnsBool()
    {
        var result = Convert("Edm.Boolean", JsonValue.Create(true));
        Assert.IsInstanceOfType<bool>(result);
        Assert.IsTrue((bool)result);
    }

    [TestMethod]
    public void EdmDateTimeOffset_ValidValue_ReturnsDateTimeOffset()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(2));
        var result = Convert("Edm.DateTimeOffset", JsonValue.Create(dto.ToString("O")));
        Assert.IsInstanceOfType<DateTimeOffset>(result);
    }

    [TestMethod]
    public void EdmBinary_Base64_ReturnsByteArray()
    {
        byte[] data = [1, 2, 3, 255];
        string b64 = System.Convert.ToBase64String(data);
        var result = Convert("Edm.Binary", JsonValue.Create(b64));
        Assert.IsInstanceOfType<byte[]>(result);
        CollectionAssert.AreEqual(data, (byte[])result);
    }
}
