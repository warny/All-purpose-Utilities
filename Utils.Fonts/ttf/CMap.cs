using System;
using System.Collections.Generic;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public abstract class CMap
	{


		public static CMap CreateMap(short format, short language)
		{
			switch (format)
			{
				case 0:	return new CMapFormat0(language);
				case 4: return new CMapFormat4(language);
				default:
					Console.WriteLine($"Unsupport CMap format: {format}");
					return null;
			}
		}

		public abstract char Map(char ch);
		public abstract byte Map(byte b);
		public abstract char ReverseMap(short s);

		public abstract void ReadData(int i, Reader data);
		public abstract void WriteData(Writer data);

		public virtual short Format { get; private set; }

		public abstract short Length { get; }

		public virtual short Language { get; private set; }

		protected CMap(short format, short language)
		{
			Format = format;
			Language = language;
		}

		public static CMap getMap(Reader data)
		{
			var format = data.ReadInt16(true);
			var length = data.ReadInt16(true);
			var language = data.ReadInt16(true);
			CMap cMap = CreateMap(format, language);
			cMap?.ReadData(length, data);
			return cMap;
		}

		public override string ToString() => $"        format: {Format}, length: {Length}, langage: {Language}";
	}
}
