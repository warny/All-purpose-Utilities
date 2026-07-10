using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.Fonts.TTF.Tables.CMap;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts
{
    [TestClass]
    public class TrueTypeFontTests
    {
        private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
        private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

        private static Reader MakeReader(byte[] data)
            => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

        [TestMethod]
        public void LoadFontTest()
        {
            TrueTypeFont font = TrueTypeFont.ParseFont((byte[])Fonts.ResourceManager.GetObject("Arial"));
        }

        [TestMethod]
        public void LoadFontGlyphTest()
        {
            TrueTypeFont font = TrueTypeFont.ParseFont((byte[])Fonts.ResourceManager.GetObject("Arial"));
            var glyph = font.GetGlyph('a');
            Assert.IsNotNull(glyph);
        }

        // Regression test: GetSpacingCorrection used to pass raw character codes straight to
        // KernTable, which stores pairs by glyph index (never by character code) -- unlike
        // GetGlyph, which correctly resolves through cmap first. Here 'A' and 'B' map to glyph
        // indices 200/201 (deliberately different from their char codes 65/66), and the kern pair
        // is stored for (200, 201): the bug would look up (65, 66), find nothing, and return 0.
        [TestMethod]
        public void GetSpacingCorrection_ResolvesCharactersToGlyphIndicesViaCmap()
        {
            var font = new TrueTypeFont(0);

            var cmap = (CmapTable)font.CreateTable(TableTypes.CMAP);
            var subtable = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
            subtable.SetMap((byte)'A', 200);
            subtable.SetMap((byte)'B', 201);
            cmap.AddCMap(3, 1, subtable);
            font.AddTable(TableTypes.CMAP, cmap);

            using var ms = new MemoryStream();
            var w = new Writer(ms, BigEndianWriter.WriterDelegates);
            w.Write<ushort>(0);  // version
            w.Write<ushort>(1);  // nTables
            w.Write<ushort>(0);  // subVersion
            w.Write<ushort>(20); // subtable length (14 header + 6 for one pair)
            w.Write<ushort>(0);  // coverage
            w.Write<ushort>(1);  // nPairs
            w.Write<ushort>(0); w.Write<ushort>(0); w.Write<ushort>(0); // searchRange/entrySelector/rangeShift
            w.Write<ushort>(200); // left glyph
            w.Write<ushort>(201); // right glyph
            w.Write<short>(-40);  // kerning value

            var kern = new KernTable();
            kern.ReadData(MakeReader(ms.ToArray()));
            font.AddTable(TableTypes.KERN, kern);

            Assert.AreEqual(-40f, font.GetSpacingCorrection('A', 'B'));
        }
    }
}
