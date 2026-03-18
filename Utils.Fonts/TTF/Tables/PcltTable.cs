using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'PCLT' table contains PCL 5 characterization data: information needed to identify and
/// render the font on PCL 5-capable printers. The table has a fixed size of 54 bytes.
/// </summary>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/pclt"/>
[TTFTable(TableTypes.Tags.PCLT)]
public class PcltTable : TrueTypeTable
{
    // ── Fixed table size ──────────────────────────────────────────────────

    private const int TableSize = 54;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="PcltTable"/> class.</summary>
    public PcltTable() : base(TableTypes.PCLT) { }

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the table version as a 16.16 fixed-point value (normally 0x00010000).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the unique PCL font number assigned by the font vendor.</summary>
    public uint FontNumber { get; set; }

    /// <summary>Gets or sets the stroke width (in design units) for the medium stroke.</summary>
    public ushort Pitch { get; set; }

    /// <summary>Gets or sets the height of a lowercase 'x' in quarter-dot units.</summary>
    public ushort XHeight { get; set; }

    /// <summary>Gets or sets the font appearance style.</summary>
    public ushort Style { get; set; }

    /// <summary>Gets or sets the typeface family code.</summary>
    public ushort TypeFamily { get; set; }

    /// <summary>Gets or sets the height of an uppercase letter in quarter-dot units.</summary>
    public ushort CapHeight { get; set; }

    /// <summary>Gets or sets the symbol set code.</summary>
    public ushort SymbolSet { get; set; }

    /// <summary>Gets or sets the 16-character typeface name (ASCII, null-padded).</summary>
    public string Typeface { get; set; } = string.Empty;

    /// <summary>Gets or sets the 8-byte character complement bit array.</summary>
    public byte[] CharacterComplement { get; set; } = new byte[8];

    /// <summary>Gets or sets the 6-character file name (ASCII, null-padded).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the stroke weight (signed: −7 to +7).</summary>
    public sbyte StrokeWeight { get; set; }

    /// <summary>Gets or sets the width type code (condensed/normal/expanded).</summary>
    public byte WidthType { get; set; }

    /// <summary>Gets or sets the serif style code.</summary>
    public byte SerifStyle { get; set; }

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length => TableSize;

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Reads a fixed-length ASCII string from the reader, stripping trailing nulls.</summary>
    private static string ReadAscii(Reader data, int length)
    {
        byte[] buf = new byte[length];
        for (int i = 0; i < length; i++)
            buf[i] = (byte)data.ReadByte();
        return Encoding.ASCII.GetString(buf).TrimEnd('\0');
    }

    /// <summary>Writes a fixed-length ASCII string (null-padded to <paramref name="length"/> bytes).</summary>
    private static void WriteAscii(Writer data, string value, int length)
    {
        byte[] src = Encoding.ASCII.GetBytes(value ?? string.Empty);
        for (int i = 0; i < length; i++)
            data.WriteByte(i < src.Length ? src[i] : (byte)0);
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when the data is not exactly 54 bytes.</exception>
    public override void ReadData(Reader data)
    {
        if (data.BytesLeft != TableSize)
            throw new ArgumentException($"Bad PCLT table size: expected {TableSize}, got {data.BytesLeft}");

        Version    = data.Read<Int32>();
        FontNumber = data.Read<UInt32>();
        Pitch      = data.Read<UInt16>();
        XHeight    = data.Read<UInt16>();
        Style      = data.Read<UInt16>();
        TypeFamily = data.Read<UInt16>();
        CapHeight  = data.Read<UInt16>();
        SymbolSet  = data.Read<UInt16>();
        Typeface   = ReadAscii(data, 16);

        CharacterComplement = new byte[8];
        for (int i = 0; i < 8; i++)
            CharacterComplement[i] = (byte)data.ReadByte();

        FileName     = ReadAscii(data, 6);
        StrokeWeight = (sbyte)(byte)data.ReadByte();
        WidthType    = (byte)data.ReadByte();
        SerifStyle   = (byte)data.ReadByte();
        data.ReadByte();  // reserved
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt32>(FontNumber);
        data.Write<UInt16>(Pitch);
        data.Write<UInt16>(XHeight);
        data.Write<UInt16>(Style);
        data.Write<UInt16>(TypeFamily);
        data.Write<UInt16>(CapHeight);
        data.Write<UInt16>(SymbolSet);
        WriteAscii(data, Typeface, 16);

        byte[] cc = CharacterComplement ?? new byte[8];
        for (int i = 0; i < 8; i++)
            data.WriteByte(i < cc.Length ? cc[i] : (byte)0);

        WriteAscii(data, FileName, 6);
        data.WriteByte((byte)StrokeWeight);
        data.WriteByte(WidthType);
        data.WriteByte(SerifStyle);
        data.WriteByte(0);  // reserved
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version     : {Version:X8}");
        sb.AppendLine($"    FontNumber  : {FontNumber}");
        sb.AppendLine($"    Typeface    : {Typeface}");
        sb.AppendLine($"    FileName    : {FileName}");
        sb.AppendLine($"    WeightClass : {StrokeWeight}");
        return sb.ToString();
    }
}
