using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Acnt
{
	public abstract class AcntFormat
	{
		private static AcntFormat CreateActn(short descriptionAndIndex)
		{
			var description = descriptionAndIndex >> 14;
			return description switch {
				0 => new AcntFormat0(),
				1 => new AcntFormat1(),
				_ => null
			};

		}

		public abstract void ReadData(Reader data);
		public abstract void WriteData(Writer data);


		public static AcntFormat GetActn(Reader reader)
		{
			var descriptionAndIndex = reader.ReadInt16();
			var result = CreateActn (descriptionAndIndex);
			result.PrimaryGlyphIndex = (short)(descriptionAndIndex & 0x7FFF);
			result.ReadData(reader);
			return result;
		}

		public AcntFormat Format { get; private set; }

		public short PrimaryGlyphIndex { get; private set; }
	}
}
