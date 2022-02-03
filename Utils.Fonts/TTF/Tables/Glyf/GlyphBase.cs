using System;
using System.IO;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Glyph;

public class GlyphBase
{
	public virtual bool IsCompound => false;

	public short NumContours { get; set; }
	public short MinX { get; set; }
	public short MinY { get; set; }
	public short MaxX { get; set; }
	public short MaxY { get; set; }

	protected internal GlyphBase() { }

	internal GlyfTable GlyfTable { get; set; }

	public virtual short Length => 10;

	public static GlyphBase CreateGlyf(Reader data, GlyfTable glyfTable)
	{
		short numContours = data.ReadInt16(true);
		GlyphBase glyf;
		if (numContours == 0)
		{
			glyf = new GlyphBase();
		}
		else if (numContours == -1)
		{
			glyf = new GlyphCompound();
		}
		else if (numContours <= 0)
		{
			string text = $"Unknown glyf type: {numContours}";
			throw new ArgumentException(text);

		}
		else
		{
			glyf = new GlyphSimple();
		}
		glyf.GlyfTable = glyfTable;
		glyf.NumContours = numContours;
		glyf.MinX = data.ReadInt16(true);
		glyf.MinY = data.ReadInt16(true);
		glyf.MaxX = data.ReadInt16(true);
		glyf.MaxY = data.ReadInt16(true);
		glyf.ReadData(data);
		return glyf;
	}

	public virtual void ReadData(Reader data) { }

	public virtual void WriteData(Writer data)
	{
		data.WriteInt16(NumContours, true);
		data.WriteInt16(MinX, true);
		data.WriteInt16(MinY, true);
		data.WriteInt16(MaxX, true);
		data.WriteInt16(MaxY, true);
	}

	public virtual void Render(IGraphicConverter graphic) { }
}

