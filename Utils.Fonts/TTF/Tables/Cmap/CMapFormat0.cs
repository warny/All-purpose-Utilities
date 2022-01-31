using System;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.CMap;

public class CMapFormat0 : CMap
{
	private byte[] glyphIndex;

	public override byte Map(byte b)
	{
		int num = (sbyte)b;
		int num2 = 0xFF & num;
		return glyphIndex[num2];
	}

	protected internal CMapFormat0(short s)
		: base(0, s)
	{
		byte[] array = new byte[256];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (byte)(sbyte)i;
		}
		MapBytes = array;
	}

	public virtual void setMap(byte src, byte dest)
	{
		int i = 0xFF & src;
		glyphIndex[i] = dest;
	}


	public override short Length => 262;

	public virtual byte[] MapBytes
	{
		get => glyphIndex;
		set
		{
			value.ArgMustBeOfSize(256);
			glyphIndex = value;
		}
	}

	public override char Map(char ch)
	{
		if (ch < '\0' || ch > 'ÿ')
		{
			return '\0';
		}
		return (char)(Map((byte)ch));
	}

	public override char ReverseMap(short s)
	{
		for (int i = 0; i < glyphIndex.Length; i++)
		{
			if (glyphIndex[i] == s)
			{
				return (char)i;
			}
		}
		return '\0';
	}

	public override void WriteData(Writer data)
	{
		data.WriteInt16(Format, true);
		data.WriteInt16(Length, true);
		data.WriteInt16(Language, true);
		data.WriteBytes(MapBytes);
	}

	public override void ReadData(int i, Reader data)
	{
		i.ArgMustBeEqualsTo(262);
		if (data.BytesLeft != 256)
		{
			throw new ArgumentException("Wrong amount of data for CMap format 0", nameof(data));
		}
		MapBytes = data.ReadBytes(256);
	}
}

