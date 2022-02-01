using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.Actn
{
	public class ActnFormat0 : ActnFormat
	{
		public byte PrimaryAttachmentPoint { get; set; }
		public byte SecondaryInfoIndex { get; set; }

		public override void ReadData(Reader data)
		{
			PrimaryAttachmentPoint = data.ReadByte();
			SecondaryInfoIndex = data.ReadByte();
		}

		public override void WriteData(Writer data)
		{
			data.WriteInt16(PrimaryGlyphIndex);
			data.WriteByte(PrimaryAttachmentPoint);
			data.WriteByte(SecondaryInfoIndex);
		}
	}
}
