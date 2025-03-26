using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF;

/// <summary>
/// The 'head' table contains global information about the font. It records facts such as the font version number, creation and modification dates,
/// revision number, and basic typographic data that applies to the font as a whole. This includes the font bounding box, the primary writing direction,
/// and other information regarding glyph placement within the em square. The checksum is used to verify the integrity of the font data and can help
/// distinguish between similar fonts.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6head.html"/>
[TTFTable(TableTypes.Tags.HEAD)]
public class HeadTable : TrueTypeTable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HeadTable"/> class.
	/// </summary>
	protected internal HeadTable() : base(TableTypes.HEAD) { }

	/// <summary>
	/// Gets the length (in bytes) of the head table.
	/// </summary>
	public override int Length => 54;

	/// <summary>
	/// Gets or sets the number of units per em.
	/// </summary>
	public virtual short UnitsPerEm { get; set; } = 64;

	/// <summary>
	/// Gets or sets the font version number.
	/// </summary>
	public virtual int Version { get; set; } = 0x10000;

	/// <summary>
	/// Gets or sets the font revision number.
	/// </summary>
	public virtual int FontRevision { get; set; } = 0x10000;

	/// <summary>
	/// Gets or sets the checksum adjustment value.
	/// </summary>
	public virtual int ChecksumAdjustment { get; set; } = 0;

	/// <summary>
	/// Gets or sets the magic number.
	/// </summary>
	public virtual int MagicNumber { get; set; } = 0x5F0F3CF5;

	/// <summary>
	/// Gets or sets the head table flags.
	/// </summary>
	public virtual HeadFlags Flags { get; set; } = 0;

	/// <summary>
	/// Gets or sets the creation date and time.
	/// </summary>
	public virtual DateTime Created { get; set; } = DateTime.Now;

	/// <summary>
	/// Gets or sets the modification date and time.
	/// </summary>
	public virtual DateTime Modified { get; set; } = DateTime.Now;

	/// <summary>
	/// Gets or sets the minimum x-coordinate for the font bounding box.
	/// </summary>
	public virtual short XMin { get; set; } = 0;

	/// <summary>
	/// Gets or sets the maximum x-coordinate for the font bounding box.
	/// </summary>
	public virtual short XMax { get; set; } = short.MaxValue;

	/// <summary>
	/// Gets or sets the minimum y-coordinate for the font bounding box.
	/// </summary>
	public virtual short YMin { get; set; } = 0;

	/// <summary>
	/// Gets or sets the maximum y-coordinate for the font bounding box.
	/// </summary>
	public virtual short YMax { get; set; } = short.MaxValue;

	/// <summary>
	/// Gets or sets the macStyle flags.
	/// </summary>
	public virtual MacStyleFlags MacStyle { get; set; } = MacStyleFlags.None;

	/// <summary>
	/// Gets or sets the lowest recommended pixels per em.
	/// </summary>
	public virtual short LowestRecPPem { get; set; } = 0;

	/// <summary>
	/// Gets or sets the font direction hint.
	/// </summary>
	public virtual FontDirectionHintEnum FontDirectionHint { get; set; } = FontDirectionHintEnum.Mixed;

	/// <summary>
	/// Gets or sets the index to location format.
	/// </summary>
	public virtual short IndexToLocFormat { get; set; } = 0;

	/// <summary>
	/// Gets or sets the glyph data format.
	/// </summary>
	public virtual short GlyphDataFormat { get; set; } = 0;

	/// <summary>
	/// Reads the head table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which to read the data.</param>
	/// <exception cref="ArgumentException">Thrown if the remaining data does not equal 54 bytes.</exception>
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

	/// <summary>
	/// Writes the head table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the data is written.</param>
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

	/// <summary>
	/// Returns a string representation of the head table data.
	/// </summary>
	/// <returns>A string containing the head table details.</returns>
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
