using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO = System.IO;

namespace Utils.IO
{
	public static class StreamUtils
	{
		/// <summary>
		/// Lit <paramref name="length"/> octets du flux et les renvoie dans un tableau
		/// </summary>
		/// <param name="length"></param>
		/// <returns></returns>
		public static byte[] ReadBytes(this Stream s, int length, bool raiseException = false)
		{
			byte[] result = new byte[length];
			var dataLength = s.Read(result, 0, length);
			if (raiseException && dataLength < length) throw new EndOfStreamException();
			return result;
		}

		public static byte[] ReadToEnd(this Stream s)
		{
			List<byte> result = new List<byte>(512);
			byte[] temp = new byte[512];
			for (var dataLength = s.Read(temp, 0, temp.Length); dataLength == 512; dataLength = s.Read(temp, 0, temp.Length))
			{
				result.AddRange(temp);
			}
			return result.ToArray();
		}
	}
}
