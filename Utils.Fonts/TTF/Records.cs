using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Fonts.TTF
{
	public record TTFPoint(float X, float Y, bool OnCurve)
	{
		public static TTFPoint MidPoint(TTFPoint p1, TTFPoint p2) 
			=> new TTFPoint((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, true);
	}

	public record LocaRecord(int Index, int Offset, int Size);
}
