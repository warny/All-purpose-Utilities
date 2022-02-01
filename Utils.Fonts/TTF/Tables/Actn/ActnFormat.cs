using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Actn
{
	public abstract class ActnFormat
	{
		private static ActnFormat CreateActn(short descriptionAndIndex)
		{
			var description = descriptionAndIndex >> 14;
			return description switch {
				0 => new ActnFormat0(),
				1 => new ActnFormat1(),
				_ => null
			};

		}

		public abstract void ReadData(Reader data);
		public abstract void WriteData(Writer data);


		public static ActnFormat GetActn(Reader reader)
		{
			var descriptionAndIndex = reader.ReadInt16();
			var result = CreateActn (descriptionAndIndex);
			result.PrimaryGlyphIndex = (short)(descriptionAndIndex & 0x8FFF);
			result.ReadData(reader);
			return result;
		}

		public ActnFormat Format { get; private set; }

		public short PrimaryGlyphIndex { get; }
	}
}
