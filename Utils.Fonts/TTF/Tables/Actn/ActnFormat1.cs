using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Actn
{
	internal class ActnFormat1 : ActnFormat
	{
		public override void ReadData(Reader data)
		{
			//throw new NotImplementedException();
		}

		public override void WriteData(Writer data)
		{
			data.WriteInt16(PrimaryGlyphIndex | &0x8000);

			//throw new NotImplementedException();
		}
	}
}
