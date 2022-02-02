using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.Collections;
using Utils.IO.Serialization;
using Utils.Mathematics;

namespace Utils.Fonts.TTF.Tables.Glyf;

public class GlyphSimple : GlyphBase
{
	protected internal short[] ContourEndPoints { get; set; }
	protected internal byte[] Instructions { get; set; }

	protected (short X, short Y, bool onCurve)[] points { get; set; }

	protected internal GlyphSimple() { }

	public override bool IsCompound => false;
	public virtual short PointsCount => (short)Flags.Length;

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

		short[] xCoords = new short[numPoints];
		for (int i = 0; i < xCoords.Length; i++)
		{
			OutLineFlags flag = flags[i];
			if (i > 0)
			{
				xCoords[i] = xCoords[i - 1];
			}
			if ((flag & OutLineFlags.XIsByte) != 0)
			{
				int val = data.ReadByte();
				if ((flag & OutLineFlags.XIsSame) == 0)
				{
					val = -val;
				}
				xCoords[i] = (short)(xCoords[i] + val);
			}
			else if ((flag & OutLineFlags.XIsSame) == 0)
			{
				xCoords[i] += data.ReadInt16(true);
			}
		}
		short[] yCoords = new short[numPoints];
		for (int i = 0; i < yCoords.Length; i++)
		{
			OutLineFlags flag = flags[i];
			if (i > 0)
			{
				yCoords[i] = yCoords[i - 1];
			}
			if ((flag & OutLineFlags.YIsByte) != 0)
			{
				int val = data.ReadByte();
				if ((flag & OutLineFlags.YIsSame) == 0)
				{
					val = -val;
				}
				yCoords[i] = (short)(yCoords[i] + val);
			}
			else if ((flag & OutLineFlags.YIsSame) == 0)
			{

				yCoords[i] += data.ReadInt16(true);
			}
		}
		points = CollectionUtils.Zip(CollectionUtils.Zip(xCoords, yCoords), flags, (p, f) => (p.Left, p.Right, f.HasFlag(OutLineFlags.OnCurve))).ToArray();
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
					data.WriteByte((byte)MathEx.Min(255, flag.repetition));
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
			byte r = 0;
			while (i > 0 && flags[i] == flags[i - 1])
			{
				i++;
				r++;
			}
			if (r > 0)
			{
				data.WriteByte(r);
			}
			else
			{
				data.WriteByte((byte)flags[i]);
			}
		}
		for (int i = 0; i < PointsCount; i++)
		{
			OutLineFlags flag = flags[i];
			if ((flag & OutLineFlags.XIsByte) != 0)
			{
				if ((flag & OutLineFlags.XIsSame) == 0)
				{
					data.WriteByte((byte)(-points[i].X));
				}
				else
				{
					data.WriteByte((byte)points[i].X);
				}
			}
			else if ((flag & OutLineFlags.XIsSame) == 0)
			{
				data.WriteInt16(points[i].X, true);
			}
		}
		for (int i = 0; i < PointsCount; i++)
		{
			OutLineFlags flag = flags[i];
			if ((flag & OutLineFlags.YIsByte) != 0)
			{
				if ((flag & OutLineFlags.YIsSame) == 0)
				{
					data.WriteByte((byte)(-points[i].Y));
				}
				else
				{
					data.WriteByte((byte)points[i].Y);
				}
			}
			else if ((flag & OutLineFlags.YIsSame) == 0)
			{
				data.WriteInt16(points[i].Y, true);
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

}
