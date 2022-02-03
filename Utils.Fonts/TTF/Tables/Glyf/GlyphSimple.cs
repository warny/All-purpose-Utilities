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
	protected internal short[] ContourEndPoints { get; set; }
	protected internal byte[] Instructions { get; set; }

	protected (short X, short Y, bool onCurve)[] points { get; set; }

	protected internal GlyphSimple() { }

	public override bool IsCompound => false;
	public virtual short PointsCount => (short)points.Length;

	public virtual short GetContourEndPoint(int i) => ContourEndPoints[i];

	public virtual (short X, short Y, bool onCurve)[] Points => points;

	public virtual byte GetInstruction(int i) => Instructions[i];

	public virtual short InstructionsCount => (short)Instructions.Length;

	private IEnumerable<OutLineFlags> GetFlags()
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
		ContourEndPoints = data.ReadArray<short>(NumContours, true);
		int numPoints = GetContourEndPoint(NumContours - 1) + 1;
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
			for (int i = 0; i < result.Length; i++)
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

		points = CollectionUtils.Zip(xCoords, yCoords, flags, (x, y, flag) => (x, y, flag.HasFlag(OutLineFlags.OnCurve))).ToArray();
	}

	public override void WriteData(Writer data)
	{
		var flags = GetFlags().ToArray();
		var compactFlags = CollectionUtils.Pack(flags);

		base.WriteData(data);
		for (int i = 0; i < NumContours; i++)
		{
			data.WriteInt16(GetContourEndPoint(i), true);
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

		for (int i = 0; i < PointsCount; i++)
		{
			OutLineFlags flag = flags[i];
			if ((flag & OutLineFlags.XIsByte) == 0)
			{
				if ((flag & OutLineFlags.XIsSame) == 0)
				{
					data.WriteInt16(points[i].X, true);
				}
			}
			else if ((flag & OutLineFlags.XIsSame) == 0)
			{
				data.WriteByte((byte)(-points[i].X));
			}
			else
			{
				data.WriteByte((byte)points[i].X);
			}
		}
		for (int i = 0; i < PointsCount; i++)
		{
			OutLineFlags flag = flags[i];
			if ((flag & OutLineFlags.YIsByte) == 0)
			{
				if ((flag & OutLineFlags.YIsSame) == 0)
				{
					data.WriteInt16(points[i].Y, true);
				}
			}
			else if ((flag & OutLineFlags.YIsSame) == 0)
			{
				data.WriteByte((byte)(-points[i].Y));
			}
			else
			{
				data.WriteByte((byte)points[i].Y);
			}
		}
	}

	public override short Length
	{
		get
		{
			var flags = GetFlags().ToArray();
			var compactFlags = CollectionUtils.Pack(flags);

			int length = base.Length;
			length += (NumContours * 2);
			length += (2 + InstructionsCount);
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
