using System;
using System.Collections.Generic;
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
	public virtual IEnumerable<IEnumerable<TTFPoint>> Contours { get; }

	protected internal GlyphBase() { }

	internal GlyfTable GlyfTable { get; set; }

	public virtual short Length => 10;

	public static GlyphBase CreateGlyf(Reader data, GlyfTable glyfTable)
	{
		short numContours = data.ReadInt16(true);
		GlyphBase glyf;
		if (numContours >= 0)
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
			return null;
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
		foreach (var points in Contours)
		{
			var pointsEnumerator = points.GetEnumerator();
			if (!pointsEnumerator.MoveNext()) continue;
			var firstPoint = pointsEnumerator.Current;
			var lastPoint = pointsEnumerator.Current;
			var lastCurvePoint = lastPoint;
			graphic.StartAt(lastPoint.X, lastPoint.Y);

			while (pointsEnumerator.MoveNext())
			{
				var point = pointsEnumerator.Current;
				if (point.OnCurve)
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
					if (!lastPoint.OnCurve)
					{
						var newCurvePoint = TTFPoint.MidPoint(lastPoint, point);
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
			if (lastPoint.OnCurve)
			{
				graphic.LineTo(firstPoint.X, firstPoint.Y);
			}
			else
			{
				graphic.BezierTo(
					(lastPoint.X, lastPoint.Y),
					(firstPoint.X, firstPoint.Y));
			}
		}
	}
}

