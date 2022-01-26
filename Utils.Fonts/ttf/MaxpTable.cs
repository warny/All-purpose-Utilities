using System;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	/// <summary>
	/// The 'maxp' table establishes the memory requirements for a font. It begins with a table version number. The next entry is the number of glyphs in the font. 
	/// The remaining entries all establish maximum values for a number of parameters. Most of these are self explanatory. A few, however, need some clarification.
	/// </summary>
	[TTFTable(TrueTypeTableTypes.Tags.maxp)]
	public class MaxpTable : TrueTypeTable
	{

		protected internal MaxpTable() : base(TrueTypeTableTypes.maxp) { }

		public override int Length => 32;
		public virtual short NumGlyphs { get; set; } = 0;
		public virtual int Version { get; set; } = 0x10000;
		public virtual short MaxPoints { get; set; } = 0;
		public virtual short MaxContours { get; set; } = 0;
		public virtual short MaxComponentPoints { get; set; } = 0;
		public virtual short MaxComponentContours { get; set; } = 0;
		public virtual short MaxZones { get; set; } = 2;
		public virtual short MaxTwilightPoints { get; set; } = 0;
		public virtual short MaxStorage { get; set; } = 0;
		public virtual short MaxFunctionDefs { get; set; } = 0;
		public virtual short MaxInstructionDefs { get; set; } = 0;
		public virtual short MaxStackElements { get; set; } = 0;
		public virtual short MaxSizeOfInstructions { get; set; } = 0;
		public virtual short MaxComponentElements { get; set; } = 0;
		public virtual short MaxComponentDepth { get; set; } = 0;

		public override void ReadData(Reader data)
		{
			if (data.BytesLeft != 32)
			{
				throw new ArgumentException("Bad size for Maxp table");
			}
			Version = data.ReadInt32(true);
			NumGlyphs = data.ReadInt16(true);
			MaxPoints = data.ReadInt16(true);
			MaxContours = data.ReadInt16(true);
			MaxComponentPoints = data.ReadInt16(true);
			MaxComponentContours = data.ReadInt16(true);
			MaxZones = data.ReadInt16(true);
			MaxTwilightPoints = data.ReadInt16(true);
			MaxStorage = data.ReadInt16(true);
			MaxFunctionDefs = data.ReadInt16(true);
			MaxInstructionDefs = data.ReadInt16(true);
			MaxStackElements = data.ReadInt16(true);
			MaxSizeOfInstructions = data.ReadInt16(true);
			MaxComponentElements = data.ReadInt16(true);
			MaxComponentDepth = data.ReadInt16(true);
		}

		public override void WriteData(Writer data)
		{
			data.WriteInt32(Version, true);
			data.WriteInt16(NumGlyphs, true);
			data.WriteInt16(MaxPoints, true);
			data.WriteInt16(MaxContours, true);
			data.WriteInt16(MaxComponentPoints, true);
			data.WriteInt16(MaxComponentContours, true);
			data.WriteInt16(MaxZones, true);
			data.WriteInt16(MaxTwilightPoints, true);
			data.WriteInt16(MaxStorage, true);
			data.WriteInt16(MaxFunctionDefs, true);
			data.WriteInt16(MaxInstructionDefs, true);
			data.WriteInt16(MaxStackElements, true);
			data.WriteInt16(MaxSizeOfInstructions, true);
			data.WriteInt16(MaxComponentElements, true);
			data.WriteInt16(MaxComponentDepth, true);
		}

		public override string ToString()
		{
			StringBuilder result = new StringBuilder();
			result.AppendLine($"    Version          : {Version:X4}");
			result.AppendLine($"    NumGlyphs        : {NumGlyphs}");
			result.AppendLine($"    MaxPoints        : {MaxPoints}");
			result.AppendLine($"    MaxContours      : {MaxContours}");
			result.AppendLine($"    MaxCompPoints    : {MaxComponentPoints}");
			result.AppendLine($"    MaxCompContours  : {MaxComponentContours}");
			result.AppendLine($"    MaxZones         : {MaxZones}");
			result.AppendLine($"    MaxTwilightPoints: {MaxTwilightPoints}");
			result.AppendLine($"    MaxStorage       : {MaxStorage}");
			result.AppendLine($"    MaxFuncDefs      : {MaxFunctionDefs}");
			result.AppendLine($"    MaxInstDefs      : {MaxInstructionDefs}");
			result.AppendLine($"    MaxStackElements : {MaxStackElements}");
			result.AppendLine($"    MaxSizeInst      : {MaxSizeOfInstructions}");
			result.AppendLine($"    MaxCompElements  : {MaxComponentElements}");
			result.AppendLine($"    MaxCompDepth     : {MaxComponentDepth}");
			return result.ToString();
		}
	}
}
