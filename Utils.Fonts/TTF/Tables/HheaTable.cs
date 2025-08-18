using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'hhea' table contains information needed to layout fonts whose characters are written horizontally,
/// that is, either left-to-right or right-to-left. This table provides global metrics for the font,
/// including typographic values such as ascent, descent, line gap, and other essential data used during glyph layout.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6hhea.html"/>
[TTFTable(TableTypes.Tags.HHEA)]
public class HheaTable : TrueTypeTable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HheaTable"/> class.
	/// </summary>
	protected internal HheaTable() : base(TableTypes.HHEA) { }

	/// <summary>
	/// Gets the fixed length (in bytes) of the hhea table.
	/// </summary>
	public override int Length => 36;

	/// <summary>
	/// Gets or sets the version number of the hhea table.
	/// </summary>
	public virtual int Version { get; set; } = 0x10000;

	/// <summary>
	/// Gets or sets the font ascent.
	/// </summary>
	public virtual short Ascent { get; set; }

	/// <summary>
	/// Gets or sets the font descent.
	/// </summary>
	public virtual short Descent { get; set; }

	/// <summary>
	/// Gets or sets the line gap.
	/// </summary>
	public virtual short LineGap { get; set; }

	/// <summary>
	/// Gets or sets the maximum advance width.
	/// </summary>
	public virtual short AdvanceWidthMax { get; set; }

	/// <summary>
	/// Gets or sets the minimum left side bearing.
	/// </summary>
	public virtual short MinLeftSideBearing { get; set; }

	/// <summary>
	/// Gets or sets the minimum right side bearing.
	/// </summary>
	public virtual short MinRightSideBearing { get; set; }

	/// <summary>
	/// Gets or sets the maximum extent of the glyphs (xMaxExtent).
	/// </summary>
	public virtual short XMaxExtent { get; set; }

	/// <summary>
	/// Gets or sets the caret slope rise.
	/// </summary>
	public virtual short CaretSlopeRise { get; set; }

	/// <summary>
	/// Gets or sets the caret slope run.
	/// </summary>
	public virtual short CaretSlopeRun { get; set; }

	/// <summary>
	/// Gets or sets the caret offset.
	/// </summary>
	public virtual short CaretOffset { get; set; }

	/// <summary>
	/// Gets or sets the metric data format.
	/// </summary>
	public virtual short MetricDataFormat { get; set; }

	/// <summary>
	/// Gets or sets the number of long horizontal metrics.
	/// </summary>
	public virtual short NumOfLongHorMetrics { get; set; }

	/// <summary>
	/// Reads the hhea table data from the specified reader.
	/// </summary>
	/// <param name="data">The reader from which to read the hhea table data.</param>
	/// <exception cref="ArgumentException">Thrown if the remaining data is not exactly 36 bytes.</exception>
	public override void ReadData(Reader data)
	{
		if (data.BytesLeft != 36)
		{
			throw new ArgumentException("Bad hhea table size");
		}
		Version = data.ReadInt32(true);
		Ascent = data.ReadInt16(true);
		Descent = data.ReadInt16(true);
		LineGap = data.ReadInt16(true);
		AdvanceWidthMax = data.ReadInt16(true);
		MinLeftSideBearing = data.ReadInt16(true);
		MinRightSideBearing = data.ReadInt16(true);
		XMaxExtent = data.ReadInt16(true);
		CaretSlopeRise = data.ReadInt16(true);
		CaretSlopeRun = data.ReadInt16(true);
		CaretOffset = data.ReadInt16(true);
		data.ReadInt16(true); // reserved
		data.ReadInt16(true); // reserved
		data.ReadInt16(true); // reserved
		data.ReadInt16(true); // reserved
		MetricDataFormat = data.ReadInt16(true);
		NumOfLongHorMetrics = data.ReadInt16(true);
	}

	/// <summary>
	/// Writes the hhea table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the hhea table data is written.</param>
	public override void WriteData(Writer data)
	{
		data.WriteInt32(Version, true);
		data.WriteInt16(Ascent, true);
		data.WriteInt16(Descent, true);
		data.WriteInt16(LineGap, true);
		data.WriteInt16(AdvanceWidthMax, true);
		data.WriteInt16(MinLeftSideBearing, true);
		data.WriteInt16(MinRightSideBearing, true);
		data.WriteInt16(XMaxExtent, true);
		data.WriteInt16(CaretSlopeRise, true);
		data.WriteInt16(CaretSlopeRun, true);
		data.WriteInt16(CaretOffset, true);
		data.WriteInt16(0, true); // reserved
		data.WriteInt16(0, true); // reserved
		data.WriteInt16(0, true); // reserved
		data.WriteInt16(0, true); // reserved
		data.WriteInt16(MetricDataFormat, true);
		data.WriteInt16(NumOfLongHorMetrics, true);
	}

	/// <summary>
	/// Returns a string representation of the hhea table data.
	/// </summary>
	/// <returns>A string describing the hhea table metrics.</returns>
	public override string ToString()
	{
		StringBuilder result = new StringBuilder();
		result.AppendLine($"    Version             : {Version:X4}");
		result.AppendLine($"    Ascent              : {Ascent}");
		result.AppendLine($"    Descent             : {Descent}");
		result.AppendLine($"    LineGap             : {LineGap}");
		result.AppendLine($"    AdvanceWidthMax     : {AdvanceWidthMax}");
		result.AppendLine($"    MinLSB              : {MinLeftSideBearing}");
		result.AppendLine($"    MinRSB              : {MinRightSideBearing}");
		result.AppendLine($"    MaxExtent           : {XMaxExtent}");
		result.AppendLine($"    CaretSlopeRise      : {CaretSlopeRise}");
		result.AppendLine($"    CaretSlopeRun       : {CaretSlopeRun}");
		result.AppendLine($"    CaretOffset         : {CaretOffset}");
		result.AppendLine($"    MetricDataFormat    : {MetricDataFormat}");
		result.AppendLine($"    NumOfLongHorMetrics : {NumOfLongHorMetrics}");
		return result.ToString();
	}
}
