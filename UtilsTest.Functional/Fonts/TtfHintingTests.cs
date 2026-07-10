using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Hinting;

namespace UtilsTest.Fonts;

[TestClass]
public class TtfHintingTests
{
    private static byte[] MovePointInstruction(short pointIndex, short delta)
    {
        byte[] bytes = new byte[5];
        bytes[0] = 0xA0;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(1, 2), pointIndex);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(3, 2), delta);
        return bytes;
    }

    private static byte[] ScalePointsInstruction(short fixedScale)
    {
        byte[] bytes = new byte[3];
        bytes[0] = 0xB0;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(1, 2), fixedScale);
        return bytes;
    }

    // Regression test: MovePoint used to cast the result of pt.Y + delta down to short before
    // constructing a new TTFPoint, even though TTFPoint stores float coordinates specifically to
    // preserve sub-pixel precision. A delta that is itself a whole number does not exercise the
    // bug, so this asserts on the coordinate that is left untouched (X) staying an exact float and
    // on Y reflecting the (integral, but now correctly typed) delta -- see
    // ScalePoints_PreservesSubPixelPrecision below for a case where the truncation is observable.
    [TestMethod]
    public void MovePoint_AddsDeltaToYCoordinate()
    {
        var context = new TtfHintingContext(MovePointInstruction(0, 10))
        {
            GlyphPoints = [new TTFPoint(12.5f, 100, true)]
        };
        var processor = new TtfHintingProcessor();

        processor.ExecuteHinting(context);

        Assert.AreEqual(12.5f, context.GlyphPoints[0].X);
        Assert.AreEqual(110f, context.GlyphPoints[0].Y);
    }

    // Regression test: ScalePoints used to cast pt.X * scale / pt.Y * scale down to short,
    // discarding any fractional part introduced by the scale factor. With a scale of 1.5 applied to
    // an odd coordinate, the correct result has a fractional part that the old (short) cast would
    // have silently dropped.
    [TestMethod]
    public void ScalePoints_PreservesSubPixelPrecision()
    {
        short fixedScale = (short)(1.5 * 16384.0); // 2.14 fixed-point representation of 1.5
        var context = new TtfHintingContext(ScalePointsInstruction(fixedScale))
        {
            GlyphPoints = [new TTFPoint(3, 5, true)]
        };
        var processor = new TtfHintingProcessor();

        processor.ExecuteHinting(context);

        Assert.AreEqual(4.5f, context.GlyphPoints[0].X, 0.001f);
        Assert.AreEqual(7.5f, context.GlyphPoints[0].Y, 0.001f);
    }
}
