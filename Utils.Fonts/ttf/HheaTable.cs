using System;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	/// <summary>
	/// The 'hhea' table contains information needed to layout fonts whose characters are written horizontally, that is, either left to right or right to left. 
	/// This table contains information that is general to the font as a whole. Information which pertains to specific glyphs is given in the 'hmtx' table 
	/// defined below.
	/// </summary>
	/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6hhea.html"/>
	[TTFTable(TrueTypeTableTypes.Tags.hhea)]
	public class HheaTable : TrueTypeTable
	{
		protected internal HheaTable() : base(TrueTypeTableTypes.hhea) { }

		public override int Length => 36;
		public virtual int Version { get; set; } = 0x10000;
		public virtual short Ascent { get; set; }
		public virtual short Descent { get; set; }
		public virtual short LineGap { get; set; }
		public virtual short AdvanceWidthMax { get; set; }
		public virtual short MinLeftSideBearing { get; set; }
		public virtual short MinRightSideBearing { get; set; }
		public virtual short XMaxExtent { get; set; }
		public virtual short CaretSlopeRise { get; set; }
		public virtual short CaretSlopeRun { get; set; }
		public virtual short CaretOffset { get; set; }
		public virtual short MetricDataFormat { get; set; }
		public virtual short NumOfLongHorMetrics { get; set; }

		public override void ReadData(Reader data)
		{
			if (data.BytesLeft != 36)
			{
				throw new ArgumentException("Bad Head table size");
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
			data.ReadInt16(true);
			data.ReadInt16(true);
			data.ReadInt16(true);
			data.ReadInt16(true);
			MetricDataFormat = data.ReadInt16(true);
			NumOfLongHorMetrics = data.ReadInt16(true);
		}

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
			data.WriteInt16(0, true);
			data.WriteInt16(0, true);
			data.WriteInt16(0, true);
			data.WriteInt16(0, true);
			data.WriteInt16(MetricDataFormat, true);
			data.WriteInt16(NumOfLongHorMetrics, true);
		}

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
}
