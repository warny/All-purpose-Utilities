using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Utils.Fonts;
using Utils.Fonts.PostScript;

namespace UtilsTest.Fonts;

// Type3Font itself is already smoke-tested (Scale/BaseLineY/glyph presence) in
// PostScriptFontTests.cs; these tests go one level deeper into the actual path commands and glyph
// name mapping that ParseCharProc/MapName produce, which weren't exercised there.
[TestClass]
public class Type3FontTests
{
    [TestMethod]
    public void ToGraphic_InvokesPathCommandsInOrder()
    {
        string ps =
            """
            /FontMatrix [0.001 0 0 0.001 0 0] def
            /CharProcs 1 dict dup begin
            /A { 700 0 0 0 700 700 setcachedevice
            0 0 moveto
            700 0 lineto
            350 700 200 700 100 350 curveto
            closepath
            } bind def
            end
            """;
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(ps));
        var font = Type3Font.Load(ms);
        var glyph = font.GetGlyph('A');

        Assert.IsNotNull(glyph);
        Assert.AreEqual(700f, glyph.Width);

        // Tuple literals can't appear directly inside a Moq expression tree (CS8143), hence the
        // intermediate variable.
        (float, float)[] curvePoints = [(350f, 700f), (200f, 700f), (100f, 350f)];
        var mock = new Mock<IGraphicConverter>(MockBehavior.Strict);
        mock.Setup(g => g.StartAt(0f, 0f));
        mock.Setup(g => g.LineTo(700f, 0f));
        mock.Setup(g => g.BezierTo(curvePoints));
        mock.Setup(g => g.ClosePath());
        glyph.ToGraphic(mock.Object);
        mock.VerifyAll();
    }

    // Regression coverage for MapName: names longer than one character only resolve through the
    // explicit switch (space/comma/period/hyphen); anything else maps to '?' rather than throwing
    // or silently dropping the glyph.
    [TestMethod]
    public void MapName_ResolvesKnownMultiCharacterNames()
    {
        string ps =
            """
            /CharProcs 2 dict dup begin
            /space { 300 0 0 0 0 0 setcachedevice } bind def
            /hyphen { 300 0 0 0 300 100 setcachedevice 0 0 moveto 300 0 lineto } bind def
            end
            """;
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(ps));
        var font = Type3Font.Load(ms);

        var space = font.GetGlyph(' ');
        var hyphen = font.GetGlyph('-');
        Assert.IsNotNull(space);
        Assert.AreEqual(300f, space.Width);
        Assert.IsNotNull(hyphen);
        Assert.AreEqual(300f, hyphen.Width);
    }

    [TestMethod]
    public void MapName_UnknownMultiCharacterName_MapsToQuestionMark()
    {
        string ps =
            """
            /CharProcs 1 dict dup begin
            /florin { 400 0 0 0 400 700 setcachedevice } bind def
            end
            """;
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(ps));
        var font = Type3Font.Load(ms);

        Assert.IsNotNull(font.GetGlyph('?'));
    }
}
