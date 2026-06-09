using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Unit tests for <see cref="ParserRawArgumentParameterMapping"/>.
/// </summary>
[TestClass]
public class ParserRawArgumentParameterMappingTests
{
    [TestMethod]
    public void Record_RequiredProperties_CanBeInitialized()
    {
        var m = new ParserRawArgumentParameterMapping
        {
            ParameterName = "value",
            Index = 0,
            Map = s => int.Parse(s),
        };

        Assert.AreEqual("value", m.ParameterName);
        Assert.AreEqual(0, m.Index);
        Assert.IsNotNull(m.Map);
        Assert.AreEqual(42, m.Map("42"));
    }

    [TestMethod]
    public void Record_WithExpression_CreatesUpdatedCopy()
    {
        var original = new ParserRawArgumentParameterMapping
        {
            ParameterName = "a",
            Index = 0,
            Map = s => s,
        };
        var copy = original with { Index = 2 };

        Assert.AreEqual("a", copy.ParameterName);
        Assert.AreEqual(2, copy.Index);
        Assert.AreSame(original.Map, copy.Map);
    }

    [TestMethod]
    public void Record_Equality_SameValues_AreEqual()
    {
        Func<string, object?> map = s => s;
        var a = new ParserRawArgumentParameterMapping { ParameterName = "x", Index = 1, Map = map };
        var b = new ParserRawArgumentParameterMapping { ParameterName = "x", Index = 1, Map = map };

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Record_Equality_DifferentIndex_NotEqual()
    {
        Func<string, object?> map = s => s;
        var a = new ParserRawArgumentParameterMapping { ParameterName = "x", Index = 0, Map = map };
        var b = new ParserRawArgumentParameterMapping { ParameterName = "x", Index = 1, Map = map };

        Assert.AreNotEqual(a, b);
    }
}
