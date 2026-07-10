using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Utils.Fonts;
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

        // Records every call made to it as a string, so two glyph outlines can be compared for
        // equality without depending on a mocking framework's strict call-order verification.
        private sealed class RecordingGraphicConverter : IGraphicConverter
        {
            public List<string> Commands { get; } = [];
            public void StartAt(float x, float y) => Commands.Add($"StartAt({x:F2},{y:F2})");
            public void LineTo(float x, float y) => Commands.Add($"LineTo({x:F2},{y:F2})");
            public void BezierTo(params (float x, float y)[] points)
                => Commands.Add($"BezierTo({string.Join(";", points.Select(p => $"{p.x:F2},{p.y:F2}"))})");
            public void ClosePath() => Commands.Add("ClosePath");
            public void BeginDrawGlyph(float x, float y, Matrix3x2 transform)
                => Commands.Add($"BeginDrawGlyph({x:F2},{y:F2},{transform})");
            public void EndDrawGlyph() => Commands.Add("EndDrawGlyph");
        }

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

        // Regression coverage for a gap noted in TODO.md: every existing test validates a single
        // table in isolation, but nothing exercised a full WriteFont() round trip, which is the only
        // way to catch interactions between tables (loca/glyf offset drift, checksum, table
        // directory). Loads the embedded Arial font, writes it back out, reparses the result, and
        // compares metrics and outlines for a sample of glyphs -- including 'g', a compound glyph on
        // most fonts (e.g. built from a dotless component) -- between the original and the round
        // tripped font.
        [TestMethod]
        public void WriteFont_RoundTrip_PreservesGlyphMetricsAndOutlines()
        {
            var original = TrueTypeFont.ParseFont((byte[])Fonts.ResourceManager.GetObject("Arial"));
            byte[] rewritten = original.WriteFont();
            var reparsed = TrueTypeFont.ParseFont(rewritten);

            Assert.AreEqual(original.TablesCount, reparsed.TablesCount);

            foreach (char c in "AaGg1.")
            {
                var originalGlyph = original.GetGlyph(c);
                var reparsedGlyph = reparsed.GetGlyph(c);
                Assert.IsNotNull(originalGlyph, $"Original font has no glyph for '{c}'");
                Assert.IsNotNull(reparsedGlyph, $"Round-tripped font has no glyph for '{c}'");

                Assert.AreEqual(originalGlyph.Width, reparsedGlyph.Width, 1e-3f, $"Width mismatch for '{c}'");
                Assert.AreEqual(originalGlyph.Height, reparsedGlyph.Height, 1e-3f, $"Height mismatch for '{c}'");
                Assert.AreEqual(originalGlyph.BaseLine, reparsedGlyph.BaseLine, 1e-3f, $"BaseLine mismatch for '{c}'");

                var originalCommands = new RecordingGraphicConverter();
                originalGlyph.ToGraphic(originalCommands);
                var reparsedCommands = new RecordingGraphicConverter();
                reparsedGlyph.ToGraphic(reparsedCommands);
                CollectionAssert.AreEqual(originalCommands.Commands, reparsedCommands.Commands, $"Outline mismatch for '{c}'");
            }
        }
    }
}
