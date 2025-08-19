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
		Version = data.Read<Int32>();
		Ascent = data.Read<Int16>();
		Descent = data.Read<Int16>();
		LineGap = data.Read<Int16>();
		AdvanceWidthMax = data.Read<Int16>();
		MinLeftSideBearing = data.Read<Int16>();
		MinRightSideBearing = data.Read<Int16>();
		XMaxExtent = data.Read<Int16>();
		CaretSlopeRise = data.Read<Int16>();
		CaretSlopeRun = data.Read<Int16>();
		CaretOffset = data.Read<Int16>();
		data.Read<Int16>(); // reserved
		data.Read<Int16>(); // reserved
		data.Read<Int16>(); // reserved
		data.Read<Int16>(); // reserved
		MetricDataFormat = data.Read<Int16>();
		NumOfLongHorMetrics = data.Read<Int16>();
	}

	/// <summary>
	/// Writes the hhea table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the hhea table data is written.</param>
	public override void WriteData(Writer data)
	{
		data.Write<Int32>(Version);
		data.Write<Int16>(Ascent);
		data.Write<Int16>(Descent);
		data.Write<Int16>(LineGap);
		data.Write<Int16>(AdvanceWidthMax);
		data.Write<Int16>(MinLeftSideBearing);
		data.Write<Int16>(MinRightSideBearing);
		data.Write<Int16>(XMaxExtent);
		data.Write<Int16>(CaretSlopeRise);
		data.Write<Int16>(CaretSlopeRun);
		data.Write<Int16>(CaretOffset);
		data.Write<Int16>(0); // reserved
		data.Write<Int16>(0); // reserved
		data.Write<Int16>(0); // reserved
		data.Write<Int16>(0); // reserved
		data.Write<Int16>(MetricDataFormat);
		data.Write<Int16>(NumOfLongHorMetrics);
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
