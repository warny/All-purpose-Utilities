using System;
using System.Collections.Generic;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables.CMap;

public abstract class CMapFormatBase
{


	public static CMapFormatBase CreateCMap(short format, short language)
	{
		return format switch
		{
			0 => new CMapFormat0(language),
			4 => new CMapFormat4(language),
			_ => throw new NotSupportedException()
		};
	}

	public abstract char Map(char ch);
	public abstract byte Map(byte b);
	public abstract char ReverseMap(short s);

	public abstract void ReadData(int i, Reader data);
	public abstract void WriteData(Writer data);

	public virtual short Format { get; private set; }

	public abstract short Length { get; }

	public virtual short Language { get; private set; }

	protected CMapFormatBase(short format, short language)
	{
		Format = format;
		Language = language;
	}

	public static CMapFormatBase GetMap(Reader data)
	{
		var format = data.ReadInt16(true);
		var length = data.ReadInt16(true);
		var language = data.ReadInt16(true);
		CMapFormatBase cMap = CreateCMap(format, language);
		cMap?.ReadData(length, data);
		return cMap;
	}

	public override string ToString() => $"        format: {Format}, length: {Length}, langage: {Language}";
}

