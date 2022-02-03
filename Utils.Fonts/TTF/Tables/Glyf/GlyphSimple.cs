using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.Collections;
using Utils.IO.Serialization;
using Utils.Mathematics;

namespace Utils.Fonts.TTF.Tables.Glyph;

public class GlyphSimple : GlyphBase
{
	protected internal byte[] Instructions { get; set; }

	protected internal GlyphSimple() { }

	public override bool IsCompound => false;
	public virtual short PointsCount => (short)Contours.Sum(c=>c.Length);

	public virtual (short X, short Y, bool onCurve)[][] Contours { get; private set; }

	public virtual byte GetInstruction(int i) => Instructions[i];

	public virtual short InstructionsCount => (short)Instructions.Length;

	private IEnumerable<OutLineFlags> GetFlags()
	{
		var points = CollectionUtils.Flatten(Contours).ToArray();
		return GetFlags(points);
	}

	private IEnumerable<OutLineFlags> GetFlags((short X, short Y, bool onCurve)[] points) 
	{
		(short? X, short? Y, bool onCurve) lastPoint = (null, null, false);
		foreach (var point in points)
		{
			OutLineFlags flag = OutLineFlags.None;
			if (point.X == lastPoint.X)
			{
				flag |= OutLineFlags.XIsSame;
			}
			else if (point.X.Between((short)0, (short)0xFF))
			{
				flag |= OutLineFlags.XIsByte;
			}
			else if (point.X.Between((short)-0xFF, (short)0))
			{
				flag |= OutLineFlags.XIsByte;
				flag |= OutLineFlags.XIsSame;
			}

			if (point.Y == lastPoint.Y)
			{
				flag |= OutLineFlags.YIsSame;
			}
			else if (point.Y.Between((short)0, (short)0xFF))
			{
				flag |= OutLineFlags.YIsByte;
			}
			else if (point.X.Between((short)-0xFF, (short)0))
			{
				flag |= OutLineFlags.YIsByte;
				flag |= OutLineFlags.YIsSame;
			}

			flag |= point.onCurve ? OutLineFlags.OnCurve : 0;

			yield return flag;
		}
	}


	public override void ReadData(Reader data)
	{
		var contourEndPoints = data.ReadArray<short>(NumContours, true).Select (nc=>nc + 1).ToArray();
		int numPoints = contourEndPoints[contourEndPoints.Length - 1];

		int length = data.ReadInt16(true);
		Instructions = data.ReadArray<byte>(length);
		OutLineFlags[] flags = new OutLineFlags[numPoints];
		for (int i = 0; i < flags.Length; i++)
		{
			OutLineFlags flag = (OutLineFlags)data.ReadByte();
			flags[i] = flag;
			if ((flag & OutLineFlags.Repeat) != 0)
			{
				int n = data.ReadByte();
				for (int l = 0; l < n; l++)
				{
					i++;
					flags[i] = flag;
				}
			}
		}

		short[] coords (OutLineFlags isByte, OutLineFlags isSame ) 
		{
			short[] result = new short[numPoints];
			for (int i = 0; i < numPoints; i++)
			{
				OutLineFlags flag = flags[i];
				if (i > 0)
				{
					result[i] = result[i - 1];
				}
				if (flag.HasFlag(isByte))
				{
					int val = data.ReadByte();
					if (!flag.HasFlag(isSame))
					{
						val = -val;
					}
					result[i] = (short)(result[i] + val);
				}
				else if (!flag.HasFlag(isSame))
				{
					result[i] += data.ReadInt16(true);
				}
			}
			return result;
		}

		var xCoords = coords(OutLineFlags.XIsByte, OutLineFlags.XIsSame);
		var yCoords = coords(OutLineFlags.YIsByte, OutLineFlags.YIsSame);

		var points = CollectionUtils.Zip(xCoords, yCoords, flags, (x,y,flag) => (x, y, flag.HasFlag(OutLineFlags.OnCurve)));

		Contours = CollectionUtils.Slice(points, contourEndPoints).Select(p=>p.ToArray()).ToArray();
	}

	public override void WriteData(Writer data)
	{
		var points = CollectionUtils.Flatten(Contours).ToArray();
		var flags = GetFlags(points).ToArray();
		var compactFlags = CollectionUtils.Pack(flags);

		base.WriteData(data);
		for (int i = 0; i < NumContours; i++)
		{
			data.WriteInt16((short)Contours[i].Length, true);
		}
		data.WriteInt16(InstructionsCount, true);
		for (int i = 0; i < InstructionsCount; i++)
		{
			data.WriteByte(GetInstruction(i));
		}
		foreach (var flag in compactFlags)
		{
			var repetition = flag.repetition;
			while (repetition > 0)
			{
				if (repetition > 1)
				{
					data.WriteByte((byte)(flag.item | OutLineFlags.Repeat));
					data.WriteByte((byte)MathEx.Min(255, flag.repetition - 1));
				}
				else
				{
					data.WriteByte((byte)(flag.item));
				}
				repetition -= 255;
			}
		}

		void Write(OutLineFlags isByte, OutLineFlags isSame, Func<(short X, short Y, bool OnCurve), short> getValue)
		{
			for (int i = 0; i < PointsCount; i++)
			{
				OutLineFlags flag = flags[i];
				short value = getValue(points[i]);
				if (flag.HasFlag(isByte))
				{
					if (flag.HasFlag(isSame))
					{
						data.WriteInt16(value, true);
					}
				}
				else if (flag.HasFlag(isSame))
				{
					data.WriteByte((byte)(-value));
				}
				else
				{
					data.WriteByte((byte)value);
				}
			}
		}

		Write(OutLineFlags.XIsByte, OutLineFlags.XIsSame, p => p.X);
		Write(OutLineFlags.YIsByte, OutLineFlags.YIsSame, p => p.Y);
	}

	public override short Length
	{
		get
		{
			var flags = GetFlags().ToArray();
			var compactFlags = CollectionUtils.Pack(flags);

			int length = base.Length;
			length += NumContours * 2;
			length += 2 + InstructionsCount;
			length += compactFlags.Count();
			for (int i = 0; i < PointsCount; i++)
			{
				OutLineFlags flag = flags[i];
				if ((flag & OutLineFlags.XIsByte) != 0) { length++; }
				else if ((flag & OutLineFlags.XIsSame) == 0) { length += 2; }
				if ((flag & OutLineFlags.YIsByte) != 0) { length++; }
				else if ((flag & OutLineFlags.YIsSame) == 0) { length += 2; }
			}
			return (short)length;
		}
	}

	public override void Render(IGraphicConverter graphic)
	{
		(short X, short Y, bool onCurve) MidPoint((short X, short Y, bool onCurve) p1, (short X, short Y, bool onCurve) p2)
			=> ((short)((p1.X + p2.X) / 2), (short)((p1.Y + p2.Y) / 2), true);

		foreach (var points in Contours)
		{
			var lastPoint = points[0];
			var lastCurvePoint = lastPoint;
			for (int i = 1; i < points.Length; i++)
			{
				var point = points[i];
				if (point.onCurve)
				{
					if (lastPoint == lastCurvePoint)
					{
						graphic.Line(lastCurvePoint.X, lastCurvePoint.Y, point.X, point.Y);
					}
					else
					{
						graphic.Spline(
							(lastCurvePoint.X, lastCurvePoint.Y),
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
						graphic.Spline(
							(lastCurvePoint.X, lastCurvePoint.Y),
							(point.X, point.Y),
							(newCurvePoint.X, newCurvePoint.Y)
						);
						lastCurvePoint = newCurvePoint;
					}

					lastPoint = point;
				}
			}
		}
	}
}
