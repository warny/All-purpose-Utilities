using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class PostTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static PostTable NewTable() => (PostTable)new TrueTypeFont(0).CreateTable(TableTypes.POST);

    private static void WriteHeader(Writer w, int format)
    {
        w.Write<int>(format);   // Format
        w.Write<int>(0);        // ItalicAngle
        w.Write<short>(0);      // UnderlinePosition
        w.Write<short>(0);      // UnderlineThickness
        w.Write<short>(0);      // IsFixedPitch
        w.Write<short>(0);      // reserved
        w.Write<int>(0);        // MinMemType42
        w.Write<int>(0);        // MaxMemType42
        w.Write<int>(0);        // MinMemType1
        w.Write<int>(0);        // MaxMemType1
    }

    // Regression test: index 63 of the standard Macintosh glyph name table was misspelled
    // "ackslash" instead of "backslash", so getGlyphNameIndex("backslash") wrongly returned 0.
    [TestMethod]
    public void Format0_BackslashResolvesToIndex63()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        WriteHeader(w, 0x10000);

        var table = NewTable();
        table.ReadData(MakeReader(ms.ToArray()));

        Assert.AreEqual((short)63, table.getGlyphNameIndex("backslash"));
        Assert.AreEqual((short)0, table.getGlyphNameIndex("ackslash"));
    }

    // Regression test: PostMapFormat2.WriteData used to end with a stray Seek(0, Begin), resetting
    // the stream position to the very start after writing. Any data written after PostTable in the
    // same stream (e.g. the next table in a real font) would then overwrite the post table itself
    // instead of being appended after it.
    [TestMethod]
    public void Format2_WriteData_LeavesStreamPositionedAfterItsOwnData()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        WriteHeader(w, 0x20000);
        w.Write<short>(1);     // one custom glyph name
        w.Write<short>(258);   // glyphNameIndex[0] -> first custom name (index >= 258)
        w.WriteVariableLengthString("MyGlyph", System.Text.Encoding.ASCII, sizeLength: 1);

        var table = NewTable();
        table.ReadData(MakeReader(ms.ToArray()));
        Assert.AreEqual((short)0, table.getGlyphNameIndex("MyGlyph"));

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        Assert.AreEqual(table.Length, outMs.Position);
    }
}
