using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class LcarTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    [TestMethod]
    public void RoundTrip_PreservesPerGlyphCarets()
    {
        var source = new LcarTable
        {
            Format = 0,
            GlyphCarets = new()
            {
                [10] = [250],       // two-component ligature: one intermediate caret
                [20] = [150, 300],  // three-component ligature: two intermediate carets
            }
        };

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);

        var table = new LcarTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.GlyphCarets.Count);
        CollectionAssert.AreEqual(new short[] { 250 }, table.GlyphCarets[10]);
        CollectionAssert.AreEqual(new short[] { 150, 300 }, table.GlyphCarets[20]);
    }

    [TestMethod]
    public void RoundTrip_NoGlyphs()
    {
        var source = new LcarTable();

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);

        var table = new LcarTable();
        table.ReadData(MakeReader(bytes));
        Assert.AreEqual(0, table.GlyphCarets.Count);
    }
}
