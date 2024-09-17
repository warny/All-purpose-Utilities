using System;

namespace Utils.Objects;

public static class BitConverterEx
{
	public static decimal ToDecimal(byte[] bytes)
	{
		int[] bits =
		[
			bytes[0] | bytes[1] << 8 | bytes[2] << 0x10 | bytes[3] << 0x18, //lo
			bytes[4] | bytes[5] << 8 | bytes[6] << 0x10 | bytes[7] << 0x18, //mid
			bytes[8] | bytes[9] << 8 | bytes[10] << 0x10 | bytes[11] << 0x18, //hi
			bytes[12] | bytes[13] << 8 | bytes[14] << 0x10 | bytes[15] << 0x18 //flags
		];
		return new decimal(bits);
	}

	public static byte[] GetBytes(decimal d)
	{
		byte[] bytes = new byte[16];

		int[] bits = decimal.GetBits(d);
		int lo = bits[0];
		int mid = bits[1];
		int hi = bits[2];
		int flags = bits[3];

		bytes[0] = (byte)lo;
		bytes[1] = (byte)(lo >> 8);
		bytes[2] = (byte)(lo >> 0x10);
		bytes[3] = (byte)(lo >> 0x18);
		bytes[4] = (byte)mid;
		bytes[5] = (byte)(mid >> 8);
		bytes[6] = (byte)(mid >> 0x10);
		bytes[7] = (byte)(mid >> 0x18);
		bytes[8] = (byte)hi;
		bytes[9] = (byte)(hi >> 8);
		bytes[10] = (byte)(hi >> 0x10);
		bytes[11] = (byte)(hi >> 0x18);
		bytes[12] = (byte)flags;
		bytes[13] = (byte)(flags >> 8);
		bytes[14] = (byte)(flags >> 0x10);
		bytes[15] = (byte)(flags >> 0x18);

		return bytes;
	}

	public static unsafe byte[] GetBytes(Int128 i)
	{
		byte[] result = new byte[16];
		fixed (byte* numPtr = result)
		{
			*(Int128*)numPtr = i;
		}
		return result;
	}

	/// <summary>
	/// Converts a byte array to an Int128.
	/// </summary>
	public static unsafe Int128 ToInt128(byte[] bytes)
	{
		if (bytes.Length != 16)
		{
			throw new ArgumentException("An Int128 must be created from a 16-byte array.");
		}

		fixed (byte* ptr = bytes)
		{
			return *(Int128*)ptr;
		}
	}

	public static unsafe byte[] GetBytes(UInt128 i)
	{
		byte[] result = new byte[16];
		fixed (byte* numPtr = result)
		{
			*(UInt128*)numPtr = i;
		}
		return result;
	}

	/// <summary>
	/// Converts a byte array to a UInt128.
	/// </summary>
	public static unsafe UInt128 ToUInt128(byte[] bytes)
	{
		if (bytes.Length != 16)
		{
			throw new ArgumentException("A UInt128 must be created from a 16-byte array.");
		}

		fixed (byte* ptr = bytes)
		{
			return *(UInt128*)ptr;
		}
	}

}

