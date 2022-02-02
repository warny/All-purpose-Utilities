using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Acnt
{
	public abstract class AcntFormatBase
	{
		private static AcntFormatBase CreateActn(short descriptionAndIndex)
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


		public static AcntFormatBase GetActn(Reader reader)
		{
			var descriptionAndIndex = reader.ReadInt16();
			var result = CreateActn (descriptionAndIndex);
			result.PrimaryGlyphIndex = (short)(descriptionAndIndex & 0x7FFF);
			result.ReadData(reader);
			return result;
		}

		public AcntFormatBase Format { get; private set; }

		public short PrimaryGlyphIndex { get; private set; }
	}
}
