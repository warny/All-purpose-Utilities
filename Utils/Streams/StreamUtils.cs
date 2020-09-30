using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO = System.IO;

namespace Utils.Streams
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

		/// <summary>
		/// Copies data from a source stream to a target stream.</summary>
		/// <param name="source">The source stream to copy from.</param>
		/// <param name="target">The destination stream to copy to.</param>
		/// <param name="length">Data Length to copy</param>
		public static void Copy( IO.Stream source, IO.Stream target, int length )
		{
			const int bufferLength = 8196;
			byte[] buffer = new byte[bufferLength];
			while (length > bufferLength) {
				source.Read(buffer, 0, bufferLength);
				target.Write(buffer, 0, bufferLength);

				length-=bufferLength;
			}
			source.Read(buffer, 0, length);
			target.Write(buffer, 0, length);
		}


		/// <summary>
		/// Copies data from a source stream to a target stream.</summary>
		/// <param name="source">The source stream to copy from.</param>
		/// <param name="target">The destination stream to copy to.</param>
		public static void Copy( Stream source, Stream target )
		{
			const int bufferLength = 8196;
			byte[] buf = new byte[bufferLength];
			int bytesRead = 0;
			while ((bytesRead = source.Read(buf, 0, bufferLength)) > 0) {
				target.Write(buf, 0, bytesRead);
			}
		}

	}
}
