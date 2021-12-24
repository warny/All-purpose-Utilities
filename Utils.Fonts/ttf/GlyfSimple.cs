using System;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public class GlyfSimple : Glyf
	{
		protected internal short[] ContourEndPoints { get; set; }
		protected internal byte[] Instructions { get; set; }
		protected internal OutLineFlags[] Flags { get; set; }
		protected internal short[] XCoords { get; set; }
		protected internal short[] YCoords { get; set; }

		protected internal GlyfSimple() { }

		public virtual short PointsCount => (short)Flags.Length;

		public virtual short GetContourEndPoint(int i) => ContourEndPoints[i];
		public virtual short GetXCoord(int i) => XCoords[i];
		public virtual short GetYCoord(int i) => YCoords[i];
		public virtual byte GetInstruction(int i) => Instructions[i];
		public OutLineFlags this[int i] => Flags[i];

		public virtual short InstructionsCount => (short)Instructions.Length;



		public override void ReadData(Reader data)
		{
			ContourEndPoints = data.ReadArray<short>(NumContours, true);
			int numPoints = GetContourEndPoint(NumContours - 1) + 1;
			int length = data.ReadInt16(true);
			Instructions = data.ReadArray<byte>(length);
			OutLineFlags[] flags = new OutLineFlags[numPoints];
			for (int i = 0; i < flags.Length; i++)
			{
				flags[i] = (OutLineFlags)data.ReadByte();
				if ((flags[i] & OutLineFlags.Repeat) != 0)
				{
					OutLineFlags f = flags[i];
					int n = data.ReadByte();
					for (int l = 0; l < n; l++)
					{
						i++;
						flags[i] = f;
					}
				}
			}
			Flags = flags;
			short[] xCoords = new short[numPoints];
			for (int i = 0; i < xCoords.Length; i++)
			{
				OutLineFlags flag = this[i];
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
			XCoords = xCoords;
			short[] yCoords = new short[numPoints];
			for (int i = 0; i < yCoords.Length; i++)
			{
				OutLineFlags flag = this[i];
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
			YCoords = yCoords;
		}

		public override void WriteData(Writer data)
		{
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
			for (int i = 0; i < PointsCount; i++)
			{
				byte r = 0;
				while (i > 0 && Flags[i] == Flags[i - 1])
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
					data.WriteByte((byte)Flags[i]);
				}
			}
			for (int i = 0; i < PointsCount; i++)
			{
				OutLineFlags flag = this[i];
				if ((flag & OutLineFlags.XIsByte) != 0)
				{
					data.WriteByte((byte)XCoords[i]);
				}
				else if ((flag & OutLineFlags.XIsSame) == 0)
				{
					data.WriteInt16(GetXCoord(i), true);
				}
			}
			for (int i = 0; i < PointsCount; i++)
			{
				OutLineFlags flag = this[i];
				if ((flag & OutLineFlags.YIsByte) != 0)
				{
					data.WriteByte((byte)YCoords[i]);
				}
				else if ((flag & OutLineFlags.YIsSame) == 0)
				{
					data.WriteInt16(YCoords[i], true);
				}
			}
		}

		public override short Length
		{
			get {
				short length = base.Length;
				length += (short)(NumContours * 2);
				length += (short)(2 + InstructionsCount);
				for (int i = 0; i < PointsCount; i++)
				{
					if (this[i] == this[i - 1]) continue;
					length++;
				}
				for (int i = 0; i < PointsCount; i++)
				{
					OutLineFlags flag = this[i];
					if ((flag & OutLineFlags.XIsByte) != 0) { length++; }
					else if ((flag & OutLineFlags.XIsSame) == 0) { length += 2; }
					if ((flag & OutLineFlags.YIsByte) != 0) { length++; }
					else if ((flag & OutLineFlags.YIsSame) == 0) { length += 2; }
				}
				return length;
			}
		}

	}
}
