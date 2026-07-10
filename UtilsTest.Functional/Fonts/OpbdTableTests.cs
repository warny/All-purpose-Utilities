using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class OpbdTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    [TestMethod]
    public void RoundTrip_PreservesPerGlyphBounds()
    {
        var source = new OpbdTable
        {
            Format = 0,
            GlyphBounds = new()
            {
                [5] = new OpbdTable.OpticalBounds(-20, 0, 20, 0),
                [7] = new OpbdTable.OpticalBounds(-10, 5, 15, -5),
            }
        };

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);

        var table = new OpbdTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.GlyphBounds.Count);
        Assert.AreEqual(new OpbdTable.OpticalBounds(-20, 0, 20, 0), table.GlyphBounds[5]);
        Assert.AreEqual(new OpbdTable.OpticalBounds(-10, 5, 15, -5), table.GlyphBounds[7]);
    }
}
