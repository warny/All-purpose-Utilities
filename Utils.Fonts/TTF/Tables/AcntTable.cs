using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The accent attachment table (tag: 'acnt') provides a space-efficient method of combining component glyphs
/// into compound glyphs to form accents. Accented glyphs are a very restricted subclass of compound glyphs.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6acnt.html"/>
[TTFTable(TableTypes.Tags.ACNT)]
public class AcntTable : TrueTypeTable
{
    /// <summary>
    /// Represents the description of an accented glyph, associating a primary glyph
    /// with one or more accent attachments.
    /// </summary>
    public abstract class AccentDescription
    {
        /// <summary>Gets the index of the primary (base) glyph.</summary>
        public ushort PrimaryGlyphIndex { get; }

        /// <summary>Initializes a new <see cref="AccentDescription"/> with the specified primary glyph index.</summary>
        /// <param name="primaryGlyphIndex">Index of the primary (base) glyph.</param>
        protected AccentDescription(ushort primaryGlyphIndex)
        {
            PrimaryGlyphIndex = primaryGlyphIndex;
        }

        /// <summary>
        /// Format 0 description: a single secondary (accent) component identified by a direct table index.
        /// Stored as 4 bytes: UInt16 primary word, byte attachment point, byte secondary index.
        /// </summary>
        public sealed class Single : AccentDescription
        {
            /// <summary>Gets the attachment point on the primary glyph.</summary>
            public byte PrimaryAttachmentPoint { get; }

            /// <summary>Gets the index into the secondary (accent) data table.</summary>
            public byte SecondaryInfoIndex { get; }

            /// <summary>Initializes a new <see cref="Single"/> description.</summary>
            /// <param name="primaryGlyphIndex">Index of the primary glyph.</param>
            /// <param name="primaryAttachmentPoint">Attachment point on the primary glyph.</param>
            /// <param name="secondaryInfoIndex">Index into the secondary data table.</param>
            public Single(ushort primaryGlyphIndex, byte primaryAttachmentPoint, byte secondaryInfoIndex)
                : base(primaryGlyphIndex)
            {
                PrimaryAttachmentPoint = primaryAttachmentPoint;
                SecondaryInfoIndex = secondaryInfoIndex;
            }
        }

        /// <summary>
        /// Format 1 description: two or more secondary components referenced via the extension sub-table.
        /// Stored as 6 bytes: UInt16 primary word (bit15=1), Int32 absolute extension offset.
        /// </summary>
        public sealed class Multiple : AccentDescription
        {
            /// <summary>Gets the list of extension entries for this glyph.</summary>
            public IReadOnlyList<ExtensionEntry> Extensions { get; }

            /// <summary>Initializes a new <see cref="Multiple"/> description.</summary>
            /// <param name="primaryGlyphIndex">Index of the primary glyph.</param>
            /// <param name="extensions">Extension entries describing each accent component.</param>
            public Multiple(ushort primaryGlyphIndex, IReadOnlyList<ExtensionEntry> extensions)
                : base(primaryGlyphIndex)
            {
                Extensions = extensions;
            }
        }
    }

    /// <summary>
    /// A single entry in the extension sub-table (2 bytes), referencing one secondary glyph attachment.
    /// Bit layout: [bit15=isLast | bits14-8=SecondaryInfoIndex(7) | bits7-0=PrimaryAttachmentPoint].
    /// </summary>
    public readonly record struct ExtensionEntry(byte SecondaryInfoIndex, byte PrimaryAttachmentPoint);

    /// <summary>
    /// A single entry in the secondary (accent) data table (3 bytes):
    /// Int16 secondary glyph index followed by one byte attachment point.
    /// </summary>
    public readonly record struct SecondaryEntry(ushort SecondaryGlyphIndex, byte AttachmentPoint);

    /// <summary>Initializes a new instance of the <see cref="AcntTable"/> class.</summary>
    public AcntTable() : base(TableTypes.ACNT) { }

    /// <summary>Gets or sets the table version (fixed32). Default value is <c>0x00010000</c>.</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the glyph index of the first accented glyph covered by this table.</summary>
    public short FirstAccentGlyphIndex { get; set; }

    /// <summary>Gets or sets the glyph index of the last accented glyph covered by this table.</summary>
    public short LastAccentGlyphIndex { get; set; }

    /// <summary>Gets the byte offset from the beginning of the table to the description sub-table.</summary>
    public int DescriptionOffset { get; private set; }

    /// <summary>Gets the byte offset from the beginning of the table to the extension sub-table.</summary>
    public int ExtensionOffset { get; private set; }

    /// <summary>Gets the byte offset from the beginning of the table to the secondary (accent) data sub-table.</summary>
    public int SecondaryOffset { get; private set; }

    /// <summary>
    /// Gets or sets the array of accent descriptions, one entry per accented glyph
    /// in the range [<see cref="FirstAccentGlyphIndex"/>..<see cref="LastAccentGlyphIndex"/>].
    /// </summary>
    public AccentDescription[] Descriptions { get; set; } = [];

    /// <summary>Gets or sets the array of secondary (accent) glyph attachment entries (max 255).</summary>
    public SecondaryEntry[] SecondaryEntries { get; set; } = [];

    /// <inheritdoc/>
    public override int Length =>
        20
        + Descriptions.Sum(d => d is AccentDescription.Single ? 4 : 6)
        + Descriptions.OfType<AccentDescription.Multiple>().Sum(m => m.Extensions.Count * 2)
        + SecondaryEntries.Length * 3;

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        // Header: 20 bytes total
        Version = data.Read<Int32>();
        FirstAccentGlyphIndex = data.Read<Int16>();
        LastAccentGlyphIndex = data.Read<Int16>();
        DescriptionOffset = data.Read<Int32>();
        ExtensionOffset = data.Read<Int32>();
        SecondaryOffset = data.Read<Int32>();

        int count = LastAccentGlyphIndex - FirstAccentGlyphIndex + 1;
        if (count <= 0)
        {
            Descriptions = [];
            SecondaryEntries = [];
            return;
        }

        // Read descriptions — seek to description sub-table
        var descriptions = new AccentDescription[count];
        data.Push(DescriptionOffset, SeekOrigin.Begin);
        for (int i = 0; i < count; i++)
        {
            ushort word0 = data.Read<UInt16>();
            bool isFormat1 = (word0 & 0x8000) != 0;
            ushort primaryGlyphIndex = (ushort)(word0 & 0x7FFF);

            if (!isFormat1)
            {
                // Format 0: 4 bytes total (2 + 1 + 1)
                byte primaryAtt = (byte)data.ReadByte();
                byte secondaryIdx = (byte)data.ReadByte();
                descriptions[i] = new AccentDescription.Single(primaryGlyphIndex, primaryAtt, secondaryIdx);
            }
            else
            {
                // Format 1: 6 bytes total (2 + 4), then seek to extension sub-table
                int extOffset = data.Read<Int32>();
                data.Push(extOffset, SeekOrigin.Begin);
                var extensions = new List<ExtensionEntry>();
                bool found = false;
                for (int j = 0; j < 256; j++)
                {
                    ushort word = data.Read<UInt16>();
                    bool isLast = (word & 0x8000) != 0;
                    byte secIdx = (byte)((word >> 8) & 0x7F);
                    byte attPoint = (byte)(word & 0xFF);
                    extensions.Add(new ExtensionEntry(secIdx, attPoint));
                    if (isLast) { found = true; break; }
                }
                if (!found)
                    throw new InvalidDataException("Extension sub-table has no terminating entry within 256 entries.");
                data.Pop();
                descriptions[i] = new AccentDescription.Multiple(primaryGlyphIndex, extensions);
            }
        }
        data.Pop();
        Descriptions = descriptions;

        // Read secondary entries — seek to secondary sub-table (max 255 entries, 3 bytes each)
        var secondaries = new List<SecondaryEntry>();
        data.Push(SecondaryOffset, SeekOrigin.Begin);
        int secCount = 0;
        while (data.BytesLeft >= 3 && secCount < 256)
        {
            ushort glyphIndex = data.Read<UInt16>();
            byte att = (byte)data.ReadByte();
            secondaries.Add(new SecondaryEntry(glyphIndex, att));
            secCount++;
        }
        data.Pop();
        SecondaryEntries = [.. secondaries];
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        // Recompute sub-table offsets from the current layout
        int descSize = Descriptions.Sum(d => d is AccentDescription.Single ? 4 : 6);
        int extSize = Descriptions.OfType<AccentDescription.Multiple>().Sum(m => m.Extensions.Count * 2);
        DescriptionOffset = 20;
        ExtensionOffset = 20 + descSize;
        SecondaryOffset = ExtensionOffset + extSize;

        // Header (20 bytes)
        data.Write<Int32>(Version);
        data.Write<Int16>(FirstAccentGlyphIndex);
        data.Write<Int16>(LastAccentGlyphIndex);
        data.Write<Int32>(DescriptionOffset);
        data.Write<Int32>(ExtensionOffset);
        data.Write<Int32>(SecondaryOffset);

        // Description sub-table
        int currentExtOffset = ExtensionOffset;
        foreach (var desc in Descriptions)
        {
            if (desc is AccentDescription.Single single)
            {
                data.Write<UInt16>((ushort)(single.PrimaryGlyphIndex & 0x7FFF));
                data.WriteByte(single.PrimaryAttachmentPoint);
                data.WriteByte(single.SecondaryInfoIndex);
            }
            else if (desc is AccentDescription.Multiple multiple)
            {
                data.Write<UInt16>((ushort)((multiple.PrimaryGlyphIndex & 0x7FFF) | 0x8000));
                data.Write<Int32>(currentExtOffset);
                currentExtOffset += multiple.Extensions.Count * 2;
            }
        }

        // Extension sub-table (one flat list, terminated per-entry with bit15=isLast)
        foreach (var multiple in Descriptions.OfType<AccentDescription.Multiple>())
        {
            var extensions = multiple.Extensions;
            for (int i = 0; i < extensions.Count; i++)
            {
                var entry = extensions[i];
                bool isLast = i == extensions.Count - 1;
                ushort word = (ushort)((isLast ? 0x8000 : 0) | ((entry.SecondaryInfoIndex & 0x7F) << 8) | entry.PrimaryAttachmentPoint);
                data.Write<UInt16>(word);
            }
        }

        // Secondary data sub-table
        foreach (var sec in SecondaryEntries)
        {
            data.Write<UInt16>(sec.SecondaryGlyphIndex);
            data.WriteByte(sec.AttachmentPoint);
        }
    }
}
