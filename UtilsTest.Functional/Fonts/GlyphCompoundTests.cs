using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.Fonts.TTF.Tables.Glyf;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class GlyphCompoundTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // Raw bytes for glyph 0: a simple glyph with a single on-curve point at the origin (0,0). Used
    // as the referenced component in the compound glyphs below, so that a transformed contour point
    // reduces to exactly (offsetScaleX * TranslateX, offsetScaleY * TranslateY): the M11..M22 terms
    // vanish because x=y=0, isolating the translation-offset behavior under test.
    private static byte[] BuildOriginPointGlyphBytes()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<short>(1);  // numberOfContours
        w.Write<short>(0); w.Write<short>(0); w.Write<short>(0); w.Write<short>(0); // bbox
        w.Write<short>(0);  // endPtsOfContours[0] -> 1 point
        w.Write<short>(0);  // instructionLength
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsSame | OutlineFlags.OnCurve));
        return ms.ToArray();
    }

    // Builds a compound glyph with a single component (HasTwoByTwo, ArgsAreXY) referencing glyph
    // index 0, with the given matrix/translation/extra flags. When extraFlags includes
    // HasInstructions, a trailing instruction-length word (and that many bytes) is appended, exactly
    // as ReadData expects.
    private static byte[] BuildCompoundGlyphBytes(
        short translateX, short translateY,
        float m11, float m21, float m12, float m22,
        CompoundGlyfFlags extraFlags,
        byte[] instructions = null)
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<short>(-1); // numberOfContours == -1 => compound glyph
        w.Write<short>(0); w.Write<short>(0); w.Write<short>(0); w.Write<short>(0); // bbox

        var flags = CompoundGlyfFlags.ArgsAreXY | CompoundGlyfFlags.HasTwoByTwo | extraFlags;
        w.Write<short>((short)flags);
        w.Write<short>(0); // glyphIndex: references glyph 0
        w.Write<short>(translateX);
        w.Write<short>(translateY);
        w.Write<short>((short)System.Math.Round(m11 * 16384f));
        w.Write<short>((short)System.Math.Round(m21 * 16384f));
        w.Write<short>((short)System.Math.Round(m12 * 16384f));
        w.Write<short>((short)System.Math.Round(m22 * 16384f));

        if (flags.HasFlag(CompoundGlyfFlags.HasInstructions))
        {
            instructions ??= [];
            w.Write<ushort>((ushort)instructions.Length);
            foreach (byte b in instructions)
            {
                w.WriteByte(b);
            }
        }

        return ms.ToArray();
    }

    // Wires up a minimal TrueTypeFont (maxp/head/loca/glyf) containing the origin-point glyph at
    // index 0 followed by the given compound glyphs, and returns the GlyfTable to query.
    private static GlyfTable BuildFontWithCompoundGlyphs(params byte[][] compoundGlyphBytes)
    {
        byte[] originGlyph = BuildOriginPointGlyphBytes();
        byte[][] allGlyphs = new byte[compoundGlyphBytes.Length + 1][];
        allGlyphs[0] = originGlyph;
        for (int i = 0; i < compoundGlyphBytes.Length; i++)
        {
            allGlyphs[i + 1] = compoundGlyphBytes[i];
        }

        var font = new TrueTypeFont(0);

        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = (short)allGlyphs.Length;
        font.AddTable(TableTypes.MAXP, maxp);

        var head = (HeadTable)font.CreateTable(TableTypes.HEAD);
        head.IndexToLocFormat = 1; // long format, simpler offset arithmetic
        font.AddTable(TableTypes.HEAD, head);

        var loca = (LocaTable)font.CreateTable(TableTypes.LOCA);
        font.AddTable(TableTypes.LOCA, loca); // wires headTable/maxpTable before ReadData needs them
        using (var locaMs = new MemoryStream())
        {
            var locaWriter = new Writer(locaMs, BigEndianWriter.WriterDelegates);
            int offset = 0;
            foreach (var g in allGlyphs)
            {
                locaWriter.Write<int>(offset);
                offset += g.Length;
            }
            locaWriter.Write<int>(offset); // trailing offset (GlyphCount + 1 entries)
            loca.ReadData(MakeReader(locaMs.ToArray()));
        }

        var glyf = (GlyfTable)font.CreateTable(TableTypes.GLYF);
        font.AddTable(TableTypes.GLYF, glyf); // wires loca/maxp before ReadData needs them
        using (var glyfMs = new MemoryStream())
        {
            foreach (var g in allGlyphs)
            {
                glyfMs.Write(g, 0, g.Length);
            }
            glyf.ReadData(MakeReader(glyfMs.ToArray()));
        }

        return glyf;
    }

    [TestMethod]
    public void ReadThenWrite_SingleComponent_ProducesIdenticalBytes()
    {
        byte[] original = BuildCompoundGlyphBytes(50, 100, 2f, 0f, 0f, 2f, default);
        var glyph = (GlyphCompound)GlyphBase.CreateGlyf(MakeReader(original), null);

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        glyph.WriteData(writer);

        Assert.AreEqual(glyph.Length, ms.ToArray().Length);
        CollectionAssert.AreEqual(original, ms.ToArray());
    }

    // Regression test: Transform used to always scale the translation offset by AdjustX/AdjustY,
    // matching the historical Apple-only behavior. Per the OpenType spec, that scaling should only
    // happen when the component explicitly sets SCALED_COMPONENT_OFFSET; with neither that flag nor
    // UNSCALED_COMPONENT_OFFSET present (the common case for fonts built with Microsoft-oriented
    // tooling), the offset must be used as-is.
    [TestMethod]
    public void Transform_NeitherScaledNorUnscaledFlagSet_OffsetIsNotScaled()
    {
        // Uniform 2x scale (HasTwoByTwo with M11=M22=2, M12=M21=0): AdjustX would be 2 under the old
        // always-scale behavior, so a wrongly-scaled offset would double translateX/Y below.
        byte[] compound = BuildCompoundGlyphBytes(50, 100, 2f, 0f, 0f, 2f, default);
        var glyf = BuildFontWithCompoundGlyphs(compound);

        var compoundGlyph = (GlyphCompound)glyf.GetGlyph(1);
        var point = compoundGlyph.Contours.Single().Single();

        Assert.AreEqual(50f, point.X, 1e-4f);
        Assert.AreEqual(100f, point.Y, 1e-4f);
    }

    // Regression test: ComputeTransform used to derive AdjustY's doubling condition from the wrong
    // matrix coefficient pair (|M12|-|M22]| instead of |M21|-|M22|), which only coincided with the
    // correct result for shear-free (M12 == M21) matrices. With M21 == M22 (triggering the correct
    // doubling condition) but M12 far from M22 (which would NOT trigger the old, wrong condition),
    // and SCALED_COMPONENT_OFFSET set so AdjustY actually affects the output, the two implementations
    // diverge: AdjustY = 2 (correct) vs 1 (old bug), so TranslateY ends up doubled only when correct.
    [TestMethod]
    public void Transform_ScaledOffset_AdjustYUsesCorrectCoefficientPair()
    {
        byte[] compound = BuildCompoundGlyphBytes(
            50, 100,
            m11: 1f, m21: 1f, m12: 0.3f, m22: 1f,
            extraFlags: CompoundGlyfFlags.ScaledComponentOffset);
        var glyf = BuildFontWithCompoundGlyphs(compound);

        var compoundGlyph = (GlyphCompound)glyf.GetGlyph(1);
        var point = compoundGlyph.Contours.Single().Single();

        // AdjustX is unaffected by this fix (still 1): X offset is scaled by 1 either way.
        Assert.AreEqual(50f, point.X, 1e-3f);
        // AdjustY must be 2 (doubled because |M21|-|M22| ~ 0), not 1 (the old, wrongly-paired check).
        Assert.AreEqual(200f, point.Y, 1e-3f);
    }

    // Regression test: WriteData/Length used to gate the trailing instruction block on
    // `Instructions.Length > 0` instead of on whether HasInstructions was actually declared. A
    // component with HasInstructions set but zero instruction bytes (a valid, spec-compliant case)
    // round-tripped through ReadData/WriteData would silently drop the 2-byte instruction-length
    // word, truncating the glyph and under-reporting Length by 2 bytes.
    [TestMethod]
    public void ReadThenWrite_HasInstructionsFlagWithZeroLengthInstructions_PreservesLengthWord()
    {
        byte[] original = BuildCompoundGlyphBytes(
            50, 100, 1f, 0f, 0f, 1f,
            extraFlags: CompoundGlyfFlags.HasInstructions,
            instructions: []);
        var glyph = (GlyphCompound)GlyphBase.CreateGlyf(MakeReader(original), null);

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        glyph.WriteData(writer);

        Assert.AreEqual(glyph.Length, ms.ToArray().Length);
        CollectionAssert.AreEqual(original, ms.ToArray());
    }
}
