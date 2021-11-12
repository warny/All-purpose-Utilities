using System;
using System.IO;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public class Glyf
	{
		private bool IsCompound { get; set; } = false;

		public short NumContours { get; set; }
		public short MinX { get; set; }
		public short MinY { get; set; }
		public short MaxX { get; set; }
		public short MaxY { get; set; }

		protected internal Glyf() { }

		public virtual short Length => 10;

		public static Glyf CreateGlyf(Reader data)
		{
			short numContours = data.ReadInt16(true);
			Glyf glyf;
			if (numContours == 0)
			{
				glyf = new Glyf();
			}
			else if (numContours == -1)
			{
				glyf = new GlyfCompound();
			}
			else if (numContours <= 0)
			{
				string text = $"Unknown glyf type: {numContours}";
				throw new ArgumentException(text);

			}
			else
			{
				glyf = new GlyfSimple();
			}
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
	}
}
