using System;

namespace Utils.Objects
{
	public static class NumberUtils
	{
		/// <summary>
		/// Indique si un objet est une valeur numérique
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool IsNumeric(object value)
		{
			return value is double || value is float || value is long || value is int || value is short || value is byte || value is ulong || value is uint || value is ushort || value is decimal;
		}

		public static byte RandomByte(this Random r)
		{
			byte[] result = new byte[sizeof(byte)];
			r.NextBytes(result);
			return result[0];
		}

		public static short RandomShort(this Random r)
		{
			byte[] result = new byte[sizeof(short)];
			r.NextBytes(result);
			return BitConverter.ToInt16(result, 0);
		}

		public static int RandomInt(this Random r)
		{
			byte[] result = new byte[sizeof(int)];
			r.NextBytes(result);
			return BitConverter.ToInt32(result, 0);
		}

		public static long RandomLong(this Random r)
		{
			byte[] result = new byte[sizeof(long)];
			r.NextBytes(result);
			return BitConverter.ToInt64(result, 0);
		}

		public static ushort RandomUShort(this Random r)
		{
			byte[] result = new byte[sizeof(ushort)];
			r.NextBytes(result);
			return BitConverter.ToUInt16(result, 0);
		}

		public static uint RandomUInt(this Random r)
		{
			byte[] result = new byte[sizeof(uint)];
			r.NextBytes(result);
			return BitConverter.ToUInt32(result, 0);
		}

		public static ulong RandomULong(this Random r)
		{
			byte[] result = new byte[sizeof(ulong)];
			r.NextBytes(result);
			return BitConverter.ToUInt64(result, 0);
		}

		public static float RandomFloat(this Random r)
		{
			byte[] result = new byte[sizeof(float)];
			r.NextBytes(result);
			return BitConverter.ToSingle(result, 0);
		}

		public static double RandomDouble(this Random r)
		{
			byte[] result = new byte[sizeof(double)];
			r.NextBytes(result);
			return BitConverter.ToDouble(result, 0);
		}

		public static T RandomFrom<T>(this Random r, params T[] values)
			=> values[r.Next(values.Length)];
	}
}
