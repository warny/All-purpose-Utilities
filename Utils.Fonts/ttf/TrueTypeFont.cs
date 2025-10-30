using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;
using Utils.Mathematics;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents a TrueType font.
/// </summary>
/// <remarks>
/// See https://developer.apple.com/fonts/TrueType-Reference-Manual/ for details.
/// </remarks>
public class TrueTypeFont : IFont
{
    private static readonly RawReader rawReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter rawWriter = new RawWriter() { BigEndian = true };

    // Dictionary associating a table tag with its descriptor and type.
    private static readonly Dictionary<Tag, (TTFTableAttribute Descriptor, Type TableType)> TablesType = [];

    static TrueTypeFont()
    {
        foreach (var t in typeof(TrueTypeTable).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(TrueTypeTable))))
        {
            TTFTableAttribute descriptor = t.GetCustomAttribute<TTFTableAttribute>();
            if (descriptor is null) { continue; }
            TablesType.Add(descriptor.TableTag, (descriptor, t));
        }
    }

    /// <summary>
    /// Gets the type value read from the font file.
    /// </summary>
    public int Type { get; }

    // Dictionary of tables present in the font.
    private readonly Dictionary<Tag, TrueTypeTable> tables;

    private int Length => 12 + tables.Count * 16 + tables.Values.Sum(t => MathEx.Ceiling(t.Length, 4));


    /// <summary>
    /// Initializes a new instance of the <see cref="TrueTypeFont"/> class.
    /// </summary>
    /// <param name="type">The type value read from the font file.</param>
    public TrueTypeFont(int type)
    {
        Type = type;
        tables = []; // Using target-typed new empty dictionary syntax.
    }

    /// <summary>
    /// Parses a TrueType font from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the font data.</param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static TrueTypeFont ParseFont(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var data = new Reader(ms, rawReader.ReaderDelegates);
        return ParseFont(data);
    }

    /// <summary>
    /// Parses a TrueType font from a stream.
    /// </summary>
    /// <param name="s">The stream containing the font data.</param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    public static TrueTypeFont ParseFont(Stream s)
    {
        if (!s.CanRead)
            throw new InvalidOperationException("The stream must be readable");
        if (!s.CanSeek)
        {
            var ms = new MemoryStream();
            s.CopyTo(ms);
            s = ms;
        }
        var reader = new Reader(s, rawReader.ReaderDelegates);
        return ParseFont(reader);
    }

    /// <summary>
    /// Parses a TrueType font from a Reader.
    /// </summary>
    /// <param name="data">The reader containing the font data.</param>
    /// <returns>An instance of <see cref="TrueTypeFont"/>.</returns>
    private static TrueTypeFont ParseFont(Reader data)
    {
        int type = data.Read<Int32>();
        int numTables = data.Read<Int16>();
        // Skip searchRange, entrySelector and rangeShift.
        data.Read<Int16>();
        data.Read<Int16>();
        data.Read<Int16>();
        TrueTypeFont trueTypeFont = new TrueTypeFont(type);
        ParseDirectories(data, numTables, trueTypeFont);
        return trueTypeFont;
    }

    /// <summary>
    /// Serializes the TrueType font into a byte array.
    /// </summary>
    /// <returns>A byte array representing the font file.</returns>
    public virtual byte[] WriteFont()
    {
        using var ms = new MemoryStream(Length);
        var data = new Writer(ms);

        data.Write<Int32>(Type);
        data.Write<Int16>(TablesCount);
        data.Write<Int16>(SearchRange);
        data.Write<Int16>(EntrySelector);
        data.Write<Int16>(RangeShift);
        int currentoffset = 12 + TablesCount * 16;
        foreach (var tagTable in tables)
        {
            var tag = tagTable.Key;
            TrueTypeTable obj = tagTable.Value;

            using var datasStream = new MemoryStream();
            Writer w = new Writer(datasStream, rawWriter.WriterDelegates);
            obj.WriteData(w);
            var datas = datasStream.ToArray();
            int dataLength = datas.Length;
            data.WriteFixedLengthString(tag, 4, Encoding.ASCII);
            data.Write<Int32>(ComputeChecksum(tag, new ReaderWriter(new MemoryStream(datas))));
            data.Write<Int32>(currentoffset);
            data.Write<Int32>(dataLength);
            data.Push();
            data.Seek(currentoffset, SeekOrigin.Begin);
            data.Write<byte[]>(datas);
            data.Pop();
            currentoffset += dataLength;
            while (currentoffset % 4 > 0)
            {
                currentoffset++;
                data.WriteByte(0);
            }
        }
        data.Position = 0;
        UpdateChecksumAdj(new ReaderWriter(ms));
        return ms.ToArray();
    }

    /// <summary>
    /// Parses the directory of tables from the font data.
    /// </summary>
    /// <param name="data">The reader from which to read the table directory.</param>
    /// <param name="numTables">The number of tables.</param>
    /// <param name="ttf">The TrueTypeFont instance to populate.</param>
    private static void ParseDirectories(Reader data, int numTables, TrueTypeFont ttf)
    {
        var tables = new SortedSet<TableDeclaration>();

        for (int i = 0; i < numTables; i++)
        {
            tables.Add(new TableDeclaration()
            {
                Tag = data.ReadFixedLengthString(4, Encoding.ASCII),
                CheckSum = data.Read<Int32>(),
                Offset = data.Read<Int32>(),
                DataLength = data.Read<Int32>(),
            });
        }

        foreach (var td in tables)
        {
            data.Position = td.Offset;
            td.Data = new MemoryStream(data.ReadBytes(td.DataLength));
            var checkSum = ComputeChecksum(td.Tag, new ReaderWriter(td.Data));
            if (checkSum != td.CheckSum)
            {
                Console.WriteLine($"Declared Checksum {td.CheckSum:X4} is different from {checkSum:X4}");
            }
        }

        void ReadTable(TableDeclaration table)
        {
            if (ttf.ContainsTable(table.Tag)) { return; }

            tables.Remove(table);
            TrueTypeTable ttt;
            if (TablesType.TryGetValue(table.Tag, out var d))
            {
                foreach (var dependency in d.Descriptor.DependsOn)
                {
                    var tableDependency = tables.FirstOrDefault(t => t.Tag == dependency);
                    if (tableDependency != null) { ReadTable(tableDependency); }
                }
                ttt = (TrueTypeTable)Activator.CreateInstance(d.TableType, true);
            }
            else
            {
                ttt = new TrueTypeTable(table.Tag);
            }
            ttf.AddTable(table.Tag, ttt);
            ttt.ReadData(new Reader(table.Data, rawReader.ReaderDelegates));
        }

        while (tables.Count > 0)
        {
            ReadTable(tables.First());
        }
    }

    /// <summary>
    /// Creates a new instance of a table corresponding to the specified tag.
    /// </summary>
    /// <param name="tag">The tag identifying the table.</param>
    /// <returns>An instance of <see cref="TrueTypeTable"/>.</returns>
    public TrueTypeTable CreateTable(Tag tag)
    {
        if (TablesType.TryGetValue(tag, out var d))
        {
            return (TrueTypeTable)Activator.CreateInstance(d.TableType, true);
        }
        else
        {
            return new TrueTypeTable(tag);
        }
    }

    /// <summary>
    /// Gets the number of tables in the font.
    /// </summary>
    public virtual short TablesCount => (short)tables.Count;

    /// <summary>
    /// Gets the search range used in the font header.
    /// </summary>
    public virtual short SearchRange
    {
        get
        {
            double num = Math.Floor(Math.Log(TablesCount, 2));
            double num2 = Math.Pow(2.0, num);
            return (short)(16.0 * num2);
        }
    }

    /// <summary>
    /// Gets the entry selector used in the font header.
    /// </summary>
    public virtual short EntrySelector
    {
        get
        {
            double num = Math.Floor(Math.Log(TablesCount, 2));
            double num2 = Math.Pow(2.0, num);
            return (short)Math.Log(num2, 2);
        }
    }

    /// <summary>
    /// Gets the range shift used in the font header.
    /// </summary>
    public virtual short RangeShift
    {
        get
        {
            double num = Math.Floor(Math.Log(TablesCount, 2));
            short num2 = (short)Math.Pow(2.0, num);
            return (short)(num2 * 16 - SearchRange);
        }
    }

    /// <summary>
    /// Computes the checksum for the data associated with the specified table tag.
    /// </summary>
    /// <param name="tagString">The table tag string.</param>
    /// <param name="data">The ReaderWriter wrapping the data.</param>
    /// <returns>The computed checksum.</returns>
    private static int ComputeChecksum(string tagString, ReaderWriter data)
    {
        unchecked
        {
            int result = 0;
            data.Push();
            if (tagString == "head")
            {
                data.Position = 8;
                data.Writer.Write<Int32>(0);
            }
            int nLongs = ((int)data.BytesLeft + 3) / 4;
            while (nLongs-- > 0)
            {
                if (data.BytesLeft > 3)
                {
                    result += data.Reader.Read<Int32>();
                    continue;
                }
                int b0 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
                int b1 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
                int b2 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
                result += ((0xFF & b0) << 24) | ((0xFF & b1) << 16) | ((0xFF & b2) << 8);
            }
            data.Pop();
            return result;
        }
    }

    /// <summary>
    /// Updates the checksum adjustment value in the 'head' table.
    /// </summary>
    /// <param name="data">The ReaderWriter wrapping the font data.</param>
    private void UpdateChecksumAdj(ReaderWriter data)
    {
        unchecked
        {
            long checksum = ComputeChecksum("", data);
            long checksumAdj = 0xb1b0afbaL - checksum;
            int offset = 12 + TablesCount * 16;
            foreach (var table in tables)
            {
                var tag = table.Key;
                if (tag == TableTypes.HEAD)
                {
                    data.Seek(offset + 8);
                    data.Writer.Write<UInt32>((uint)checksumAdj);
                    break;
                }
                offset += table.Value.Length;
                if ((offset % 4) != 0)
                {
                    offset += (4 - (offset % 4));
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the table corresponding to the specified tag.
    /// </summary>
    /// <typeparam name="T">The type of the table.</typeparam>
    /// <param name="tag">The table tag.</param>
    /// <returns>An instance of the table.</returns>
    public virtual T GetTable<T>(Tag tag) where T : TrueTypeTable => (T)tables[tag];

    /// <summary>
    /// Attempts to retrieve the table corresponding to the specified tag.
    /// </summary>
    /// <typeparam name="T">The type of the table.</typeparam>
    /// <param name="tag">The table tag.</param>
    /// <param name="table">When this method returns, contains the table if found; otherwise, null.</param>
    /// <returns><see langword="true"/> if the table was found; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryGetTable<T>(Tag tag, out T table) where T : TrueTypeTable
    {
        if (tables.TryGetValue(tag, out var result))
        {
            table = (T)result;
            return true;
        }
        else
        {
            table = null;
            return false;
        }
    }

    /// <summary>
    /// Adds a table to the font.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <param name="ttf">The table instance.</param>
    public virtual void AddTable(Tag tag, TrueTypeTable ttf)
    {
        ttf.TrueTypeFont = this;
        tables[tag] = ttf;
    }

    /// <summary>
    /// Indicates whether the font contains a table with the specified tag.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <returns><see langword="true"/> if the table exists; otherwise, <see langword="false"/>.</returns>
    public bool ContainsTable(Tag tag) => tables.ContainsKey(tag);

    /// <summary>
    /// Removes the table with the specified tag from the font.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    public virtual void RemoveTable(Tag tag) => tables.Remove(tag);

    /// <summary>
    /// Returns a string representation of the font, including information from each table.
    /// </summary>
    /// <returns>A string describing the font.</returns>
    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine($"Type         : {Type}");
        result.AppendLine($"NumTables    : {TablesCount}");
        result.AppendLine($"SearchRange  : {SearchRange}");
        result.AppendLine($"EntrySelector: {EntrySelector}");
        result.AppendLine($"RangeShift   : {RangeShift}");
        foreach (var table in tables)
        {
            result.AppendLine(table.Value.ToString());
        }
        return result.ToString();
    }

    /// <summary>
    /// Retrieves the glyph corresponding to the specified character.
    /// </summary>
    /// <param name="c">The character for which to retrieve the glyph.</param>
    /// <returns>An <see cref="IGlyph"/> representing the character glyph, or null if not found.</returns>
    public IGlyph GetGlyph(char c)
    {
        var cmap = GetTable<CmapTable>(TableTypes.CMAP);
        var glyf = GetTable<GlyfTable>(TableTypes.GLYF);
        foreach (var map in cmap.CMaps)
        {
            int index = map.Map(c);
            if (index > 0)
            {
                return new TrueTypeGlyph(glyf.GetGlyph(index));
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the spacing correction (kerning) between two adjacent characters.
    /// If a kern table is present, its value is returned; otherwise, 0 is returned.
    /// </summary>
    /// <param name="before">The preceding character.</param>
    /// <param name="after">The following character.</param>
    /// <returns>The spacing correction in font units.</returns>
    public float GetSpacingCorrection(char before, char after)
    {
        // If the font contains a kern table, retrieve it and use it to compute spacing correction.
        if (ContainsTable(TableTypes.KERN))
        {
            // Assuming a KernTable type with a GetSpacingCorrection method exists.
            var kernTable = GetTable<KernTable>(TableTypes.KERN);
            return kernTable.GetSpacingCorrection(before, after);
        }
        return 0f;
    }

    /// <summary>
    /// Represents a declaration of a table in the TrueType font.
    /// </summary>
    public sealed class TableDeclaration : IComparable<TableDeclaration>, IComparable
    {
        /// <summary>
        /// Gets or sets the table tag.
        /// </summary>
        public Tag Tag { get; set; }
        /// <summary>
        /// Gets or sets the declared checksum.
        /// </summary>
        public int CheckSum { get; set; }
        /// <summary>
        /// Gets or sets the offset to the table data.
        /// </summary>
        public int Offset { get; set; }
        /// <summary>
        /// Gets or sets the length of the table data.
        /// </summary>
        public int DataLength { get; set; }
        /// <summary>
        /// Gets or sets the table data as a MemoryStream.
        /// </summary>
        public MemoryStream Data { get; set; }

        /// <inheritdoc/>
        public override string ToString() => $"{Tag} - CheckSum={CheckSum:X4} - Offset={Offset} - DataLength={DataLength}";

        /// <inheritdoc/>
        public int CompareTo(TableDeclaration other) => Offset.CompareTo(other.Offset);

        /// <inheritdoc/>
        public int CompareTo(object obj) => obj is TableDeclaration td ? CompareTo(td) : -1;
    }
}
