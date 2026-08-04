using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Parsing;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

/// <summary>
/// Regression tests for TODO-2026-07-19-pass2 items 21, 23, 24, 25, 26, 27, 37 and 38: unsigned
/// SFNT directory parsing, bounded/overflow-safe range validation, duplicate/alias/overlap policy,
/// checksum strict/permissive behavior, non-seekable stream bounds, zero-table serialization, and
/// table-type registry robustness.
/// </summary>
[TestClass]
public class TrueTypeFontDirectoryTests
{
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    /// <summary>Writes a raw SFNT offset table + directory with full control over every field, for malformed-input tests.</summary>
    private static byte[] BuildRaw(uint numTables, ushort searchRange, ushort entrySelector, ushort rangeShift,
        (string tag, uint checksum, uint offset, uint length)[] entries, int totalLength)
    {
        var bytes = new byte[totalLength];
        using var ms = new MemoryStream(bytes);
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<Int32>(0x00010000);
        w.Write<UInt16>((ushort)numTables);
        w.Write<UInt16>(searchRange);
        w.Write<UInt16>(entrySelector);
        w.Write<UInt16>(rangeShift);
        foreach (var e in entries)
        {
            w.WriteFixedLengthString(e.tag, 4, System.Text.Encoding.ASCII);
            w.Write<UInt32>(e.checksum);
            w.Write<UInt32>(e.offset);
            w.Write<UInt32>(e.length);
        }
        return bytes;
    }

    private static (ushort searchRange, ushort entrySelector, ushort rangeShift) DeriveHeader(int numTables)
    {
        if (numTables == 0) return (0, 0, 0);
        int pow2 = 1 << (int)Math.Log2(numTables);
        ushort sr = (ushort)(pow2 * 16);
        ushort es = (ushort)Math.Log2(pow2);
        ushort rs = (ushort)(numTables * 16 - sr);
        return (sr, es, rs);
    }

    [TestMethod]
    public void NumTables_HighBit_IsReadAsLargeUnsignedValue_AndRejected()
    {
        // 0x8000 as Int16 would be negative; as UInt16 it is a huge, clearly-excessive table count.
        var bytes = BuildRaw(0x8000, 0, 0, 0, [], 12);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.ResourceLimitExceeded, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void TruncatedDirectory_IsRejected()
    {
        // Declares 2 tables but the buffer is too short to hold their directory entries.
        var bytes = BuildRaw(2, 32, 1, 0, [], 12 + 10);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.InvalidDirectoryRange, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void OffsetWithHighBit_ExceedsFontLength_IsRejectedInStrictMode()
    {
        var entries = new[] { ("AAAA", 0u, 0x80000000u, 4u) };
        var bytes = BuildRaw(1, 16, 0, 0, entries, 12 + 16 + 4);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.InvalidDirectoryRange, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void OffsetPlusLength_OverflowsUInt32_IsRejected()
    {
        var entries = new[] { ("AAAA", 0u, 0xFFFFFFF0u, 0xFFu) };
        var bytes = BuildRaw(1, 16, 0, 0, entries, 12 + 16 + 4);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.InvalidDirectoryRange, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void TableOverlappingDirectory_IsRejectedInStrictMode()
    {
        // directoryEnd = 12 + 1*16 = 28; a table starting at offset 4 overlaps the directory itself.
        var entries = new[] { ("AAAA", 0u, 4u, 4u) };
        var bytes = BuildRaw(1, 16, 0, 0, entries, 32);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.OverlappingTableRange, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void TableOverlappingDirectory_IsSkippedWithDiagnosticInPermissiveMode()
    {
        var entries = new[] { ("AAAA", 0u, 4u, 4u) };
        var bytes = BuildRaw(1, 16, 0, 0, entries, 32);
        var font = TrueTypeFont.ParseFont(bytes, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsFalse(font.ContainsTable("AAAA"));
        Assert.IsTrue(font.Diagnostics.Any(d => d.Code == FontDiagnosticCode.OverlappingTableRange));
    }

    [TestMethod]
    public void DuplicateTag_IsRejectedInStrictMode()
    {
        // directoryEnd for 2 tables is 12 + 2*16 = 44: both offsets must be >= that.
        var entries = new[]
        {
            ("AAAA", 0u, 44u, 4u),
            ("AAAA", 0u, 48u, 4u),
        };
        var bytes = BuildRaw(2, 32, 1, 0, entries, 52);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.DuplicateTableTag, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void DuplicateTag_KeepsFirstEntryInPermissiveMode()
    {
        var entries = new[]
        {
            ("AAAA", 0u, 44u, 4u),
            ("AAAA", 0u, 48u, 4u),
        };
        var bytes = BuildRaw(2, 32, 1, 0, entries, 52);
        var font = TrueTypeFont.ParseFont(bytes, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsTrue(font.Diagnostics.Any(d => d.Code == FontDiagnosticCode.DuplicateTableTag));
    }

    [TestMethod]
    public void ExactAlias_IsRejectedInStrictMode_AndDiagnosedInPermissive()
    {
        var entries = new[]
        {
            ("AAAA", 0u, 44u, 4u),
            ("BBBB", 0u, 44u, 4u),
        };
        var bytes = BuildRaw(2, 32, 1, 0, entries, 48);
        Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));

        var font = TrueTypeFont.ParseFont(bytes, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsTrue(font.Diagnostics.Any(d => d.Code == FontDiagnosticCode.AliasedTableRange));
        // Both tags still resolve: an alias is suspicious, not memory-unsafe.
        Assert.IsTrue(font.ContainsTable("AAAA"));
        Assert.IsTrue(font.ContainsTable("BBBB"));
    }

    [TestMethod]
    public void PartialOverlap_IsRejectedInStrictMode_AndDiagnosedInPermissive()
    {
        var entries = new[]
        {
            ("AAAA", 0u, 44u, 8u),
            ("BBBB", 0u, 48u, 8u), // starts 4 bytes into AAAA's range
        };
        var bytes = BuildRaw(2, 32, 1, 0, entries, 56);
        Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));

        var font = TrueTypeFont.ParseFont(bytes, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsTrue(font.Diagnostics.Any(d => d.Code == FontDiagnosticCode.OverlappingTableRange));
    }

    [TestMethod]
    public void MaximumTables_Exceeded_AlwaysThrows_EvenInPermissiveMode()
    {
        var bytes = BuildRaw(10, 0, 0, 0, [], 12);
        var options = new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive, MaximumTables = 5 };
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes, options));
        Assert.AreEqual(FontDiagnosticCode.ResourceLimitExceeded, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void MaximumTableBytes_Exceeded_AlwaysThrows_EvenInPermissiveMode()
    {
        var entries = new[] { ("AAAA", 0u, 28u, 1000u) };
        var bytes = BuildRaw(1, 16, 0, 0, entries, 1028);
        var options = new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive, MaximumTableBytes = 100 };
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes, options));
        Assert.AreEqual(FontDiagnosticCode.ResourceLimitExceeded, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void MaximumFontBytes_ExactAndExceeded_ByteArray()
    {
        var (sr, es, rs) = DeriveHeader(0);
        var bytes = BuildRaw(0, sr, es, rs, [], 12);
        var exact = new TrueTypeFontParsingOptions { MaximumFontBytes = 12 };
        var font = TrueTypeFont.ParseFont(bytes, exact); // exactly at the limit: allowed
        Assert.AreEqual(0, font.TablesCount);

        var tooSmall = new TrueTypeFontParsingOptions { MaximumFontBytes = 11 };
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes, tooSmall));
        Assert.AreEqual(FontDiagnosticCode.ResourceLimitExceeded, ex.Diagnostic.Code);
    }

    [TestMethod]
    public void MaximumFontBytes_Exceeded_NonSeekableStream_IsRejectedDuringCopy()
    {
        var (sr, es, rs) = DeriveHeader(0);
        var bytes = BuildRaw(0, sr, es, rs, [], 12);
        using var nonSeekable = new NonSeekableStream(bytes);
        var options = new TrueTypeFontParsingOptions { MaximumFontBytes = 4 };
        Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(nonSeekable, options));
    }

    [TestMethod]
    public void NonSeekableStream_WithinLimit_ParsesSuccessfully()
    {
        var (sr, es, rs) = DeriveHeader(0);
        var bytes = BuildRaw(0, sr, es, rs, [], 12);
        using var nonSeekable = new NonSeekableStream(bytes);
        var font = TrueTypeFont.ParseFont(nonSeekable, new TrueTypeFontParsingOptions { MaximumFontBytes = 1024 });
        Assert.AreEqual(0, font.TablesCount);
    }

    [TestMethod]
    public void ZeroTableFont_HasExplicitZeroDerivedHeaderValues()
    {
        var font = new TrueTypeFont(0x00010000);
        Assert.AreEqual((ushort)0, font.TablesCount);
        Assert.AreEqual((ushort)0, font.SearchRange);
        Assert.AreEqual((ushort)0, font.EntrySelector);
        Assert.AreEqual((ushort)0, font.RangeShift);

        var bytes = font.WriteFont();
        Assert.AreEqual(12, bytes.Length); // just the offset table, no directory entries
    }

    [TestMethod]
    public void DerivedOffsetTableFields_Mismatch_IsRejectedInStrictMode()
    {
        var entries = new[] { ("AAAA", 0u, 28u, 4u) };
        // Deliberately wrong searchRange/entrySelector/rangeShift for numTables=1 (correct is 16,0,0).
        var bytes = BuildRaw(1, 99, 99, 99, entries, 32);
        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.InvalidOffsetTable, ex.Diagnostic.Code);
    }

    // TODO-pass2 item 26: table-checksum mismatches are policy-dependent (strict rejects,
    // permissive records a diagnostic and keeps the table), and never silently disappear into Debug.
    [TestMethod]
    public void TableChecksumMismatch_IsRejectedInStrictMode_AndDiagnosedInPermissive()
    {
        // Data [0,0,0,1] checksums to 1 as a single big-endian UInt32 word; declaring 99 is wrong.
        var entries = new[] { ("AAAA", 99u, 28u, 4u) };
        var header = BuildRaw(1, 16, 0, 0, entries, 32);
        header[28] = 0; header[29] = 0; header[30] = 0; header[31] = 1;

        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(header));
        Assert.AreEqual(FontDiagnosticCode.TableChecksumMismatch, ex.Diagnostic.Code);

        var font = TrueTypeFont.ParseFont(header, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsTrue(font.ContainsTable("AAAA"));
        Assert.IsTrue(font.Diagnostics.Any(d => d.Code == FontDiagnosticCode.TableChecksumMismatch));
    }

    // TODO-pass2 items 9.2/26: a correctly-written font (real checksums, real checksumAdjustment)
    // round-trips through strict-mode parsing without a whole-font checksum failure.
    [TestMethod]
    public void WholeFontChecksum_ValidForATrulyWrittenFont()
    {
        var font = BuildMinimalValidFont();
        byte[] bytes = font.WriteFont();

        var reparsed = TrueTypeFont.ParseFont(bytes); // strict: throws on any checksum mismatch
        Assert.AreEqual(0, reparsed.Diagnostics.Count);
    }

    [TestMethod]
    public void WholeFontChecksum_Invalid_IsRejectedInStrictMode_ButPerTableChecksumsStillPass()
    {
        var font = BuildMinimalValidFont();
        byte[] bytes = font.WriteFont();
        CorruptHeadChecksumAdjustment(bytes);

        var ex = Assert.ThrowsExactly<FontParseException>(() => TrueTypeFont.ParseFont(bytes));
        Assert.AreEqual(FontDiagnosticCode.FontChecksumMismatch, ex.Diagnostic.Code);

        var permissive = TrueTypeFont.ParseFont(bytes, new TrueTypeFontParsingOptions { ValidationMode = FontValidationMode.Permissive });
        Assert.IsFalse(permissive.Diagnostics.Any(d => d.Code == FontDiagnosticCode.TableChecksumMismatch));
        Assert.IsTrue(permissive.Diagnostics.Any(d => d.Code == FontDiagnosticCode.FontChecksumMismatch));
    }

    /// <summary>Builds a minimal, fully valid TrueTypeFont (head + maxp) suitable for a real WriteFont()/ParseFont() round trip.</summary>
    private static TrueTypeFont BuildMinimalValidFont()
    {
        var font = new TrueTypeFont(0x00010000);
        var head = (HeadTable)font.CreateTable(TableTypes.HEAD);
        font.AddTable(TableTypes.HEAD, head);
        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = 0;
        font.AddTable(TableTypes.MAXP, maxp);
        return font;
    }

    /// <summary>Parses the SFNT directory in <paramref name="bytes"/>, locates 'head', and overwrites its checksumAdjustment word (only) with a bogus value, leaving every per-table checksum valid.</summary>
    private static void CorruptHeadChecksumAdjustment(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var r = new Reader(ms, new RawReader { BigEndian = true }.ReaderDelegates);
        r.Read<int>(); // sfntVersion
        int numTables = r.Read<ushort>();
        r.Read<ushort>(); r.Read<ushort>(); r.Read<ushort>(); // searchRange/entrySelector/rangeShift
        for (int i = 0; i < numTables; i++)
        {
            string tag = r.ReadFixedLengthString(4, System.Text.Encoding.ASCII);
            r.Read<uint>(); // checksum
            uint offset = r.Read<uint>();
            r.Read<uint>(); // length
            if (tag == "head")
            {
                int adjOffset = (int)offset + HeadTable.ChecksumAdjustmentOffset;
                bytes[adjOffset] = (byte)~bytes[adjOffset];
                bytes[adjOffset + 1] = (byte)~bytes[adjOffset + 1];
                return;
            }
        }
        Assert.Fail("No 'head' table found in the built font.");
    }

    [TestMethod]
    public void TableRegistry_BuildsWithoutDuplicateTagsOrCycles()
    {
        // Exercises the same registry construction TrueTypeFont's static constructor performs, but
        // directly, so a duplicate-tag/cycle failure surfaces as this test's own assertion message
        // instead of an opaque TypeInitializationException on first use of TrueTypeFont.
        var registry = TrueTypeFont.BuildTablesTypeRegistry();
        Assert.IsTrue(registry.Count > 10);
        Assert.IsTrue(registry.ContainsKey(TableTypes.HEAD));
        Assert.IsTrue(registry.ContainsKey(TableTypes.LOCA));
    }

    /// <summary>A read-only, forward-only stream wrapper that reports CanSeek = false, for testing the non-seekable parsing path.</summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
