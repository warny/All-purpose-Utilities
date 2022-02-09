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
	public virtual (float X, float Y, bool onCurve)[][] Contours { get; }

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
			throw new NotSupportedException();
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

	public void Render(IGraphicConverter graphic) {
		(float X, float Y, bool onCurve) MidPoint((float X, float Y, bool onCurve) p1, (float X, float Y, bool onCurve) p2)
		=> ((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, true);

		foreach (var points in Contours)
		{
			var lastPoint = points[0];
			var lastCurvePoint = lastPoint;
			graphic.StartAt(lastPoint.X, lastPoint.Y);
			for (int i = 1; i < points.Length; i++)
			{
				var point = points[i];
				if (point.onCurve)
				{
					if (lastPoint == lastCurvePoint)
					{
						graphic.LineTo(point.X, point.Y);
					}
					else
					{
						graphic.BezierTo(
							(lastPoint.X, lastPoint.Y),
							(point.X, point.Y)
						);

					}

					lastPoint = point;
					lastCurvePoint = point;
				}
				else
				{
					if (!lastPoint.onCurve)
					{
						var newCurvePoint = MidPoint(lastPoint, point);
						graphic.BezierTo(
							(point.X, point.Y),
							(newCurvePoint.X, newCurvePoint.Y)
						);
						lastCurvePoint = newCurvePoint;
					}

					lastPoint = point;
				}
			}
			//close the curve
			if (lastPoint.onCurve)
			{
				graphic.LineTo(points[0].X, points[0].Y);
			}
			else
			{
				graphic.BezierTo(
					(lastPoint.X, lastPoint.Y),
					(points[0].X, points[0].Y));
			}
		}
	}
}

