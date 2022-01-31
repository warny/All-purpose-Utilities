using System;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'head' table contains global information about the font. It records such facts as the font version number, the creation and modification dates, revision number and basic typographic 
/// data that applies to the font as a whole. This includes a specification of the font bounding box, the direction in which the font's glyphs are most likely to be written and other information 
/// about the placement of glyphs in the em square. The checksum is used to verify the integrity of the data in the font. It can also be used to distinguish between two similar fonts.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6head.html"/>
[TTFTable(TrueTypeTableTypes.Tags.head)]
public class HeadTable : TrueTypeTable
{
	protected internal HeadTable() : base(TrueTypeTableTypes.head) { }

	public override int Length => 54;
	public virtual short UnitsPerEm { get; set; } = 64;
	public virtual int Version { get; set; } = 0x10000;
	public virtual int FontRevision { get; set; } = 0x10000;
	public virtual int ChecksumAdjustment { get; set; } = 0;
	public virtual int MagicNumber { get; set; } = 0x5F0F3CF5;
	public virtual HeadFlags Flags { get; set; } = 0;
	public virtual DateTime Created { get; set; } = DateTime.Now;
	public virtual DateTime Modified { get; set; } = DateTime.Now;
	public virtual short XMin { get; set; } = 0;
	public virtual short XMax { get; set; } = short.MaxValue;
	public virtual short YMin { get; set; } = 0;
	public virtual short YMax { get; set; } = short.MaxValue;
	public virtual MacStyleFlags MacStyle { get; set; } = MacStyleFlags.None;
	public virtual short LowestRecPPem { get; set; } = 0;
	public virtual FontDirectionHintEnum FontDirectionHint { get; set; } = FontDirectionHintEnum.Mixed;
	public virtual short IndexToLocFormat { get; set; } = 0;
	public virtual short GlyphDataFormat { get; set; } = 0;

	public override void ReadData(Reader data)
	{
		if (data.BytesLeft != 54)
		{
			throw new ArgumentException("Bad Head table size");
		}
		Version = data.ReadInt32(true);
		FontRevision = data.ReadInt32(true);
		ChecksumAdjustment = data.ReadInt32(true);
		MagicNumber = data.ReadInt32(true);
		Flags = (HeadFlags)data.ReadInt16(true);
		UnitsPerEm = data.ReadInt16(true);
		Created = data.ReadDateTime(true);
		Modified = data.ReadDateTime(true);
		XMin = data.ReadInt16(true);
		XMax = data.ReadInt16(true);
		YMin = data.ReadInt16(true);
		YMax = data.ReadInt16(true);
		MacStyle = (MacStyleFlags)data.ReadInt16(true);
		LowestRecPPem = data.ReadInt16(true);
		FontDirectionHint = (FontDirectionHintEnum)data.ReadInt16(true);
		IndexToLocFormat = data.ReadInt16(true);
		GlyphDataFormat = data.ReadInt16(true);
	}

	public override void WriteData(Writer data)
	{
		data.WriteInt32(Version, true);
		data.WriteInt32(FontRevision, true);
		data.WriteInt32(ChecksumAdjustment, true);
		data.WriteInt32(MagicNumber, true);
		data.WriteInt16((short)Flags, true);
		data.WriteInt16(UnitsPerEm, true);
		data.WriteDateTime(Created, true);
		data.WriteDateTime(Modified, true);
		data.WriteInt16(XMin, true);
		data.WriteInt16(XMax, true);
		data.WriteInt16(YMin, true);
		data.WriteInt16(YMax, true);
		data.WriteInt16((short)MacStyle, true);
		data.WriteInt16(LowestRecPPem, true);
		data.WriteInt16((short)FontDirectionHint, true);
		data.WriteInt16(IndexToLocFormat, true);
		data.WriteInt16(GlyphDataFormat, true);
	}

	public override string ToString()
	{
		StringBuilder result = new StringBuilder();
		result.AppendLine($"    Version          : {Version:X4}");
		result.AppendLine($"    Revision         : {FontRevision:X4}");
		result.AppendLine($"    ChecksumAdj      : {ChecksumAdjustment:X2}");
		result.AppendLine($"    MagicNumber      : {MagicNumber:X2}");
		result.AppendLine($"    Flags            : {Flags}");
		result.AppendLine($"    UnitsPerEm       : {UnitsPerEm}");
		result.AppendLine($"    Created          : {Created:g}");
		result.AppendLine($"    Modified         : {Modified:g}");
		result.AppendLine($"    XMin             : {XMin}");
		result.AppendLine($"    XMax             : {XMax}");
		result.AppendLine($"    YMin             : {YMin}");
		result.AppendLine($"    YMax             : {YMax}");
		result.AppendLine($"    MacStyle         : {MacStyle}");
		result.AppendLine($"    LowestPPem       : {LowestRecPPem}");
		result.AppendLine($"    FontDirectionHint: {FontDirectionHint}");
		result.AppendLine($"    IndexToLocFormat : {IndexToLocFormat}");
		result.AppendLine($"    GlyphDataFormat  : {GlyphDataFormat}");
		return result.ToString();
	}
}

