using System;
using System.Security.Cryptography;
using Utils.Objects;

namespace Utils.Security
{
	public class Authenticator
	{
		public int Digits { get; }
		public HMAC Algorithm { get; }
		public byte[] Key { get; }
		public int IntervalLength { get; }

		public Authenticator(string algorithm, byte[] key, int digits, int intervalLength) : this(HMAC.Create(algorithm), key, digits, intervalLength) { }

		public Authenticator(HMAC algorithm, byte[] key, int digits, int intervalLength)
		{
			this.Digits = digits;
			this.Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
			this.Key = key ?? throw new ArgumentNullException(nameof(key));
			this.IntervalLength = intervalLength;
		}

		public static Authenticator GoogleAuthenticator(byte[] key) => new Authenticator("HMACSHA256", key, 6, 30);

		public string ComputeAuthenticator() => ComputeAuthenticator(DateTime.Now.UnixTimeStamp() / IntervalLength);

		public string ComputeAuthenticator(long message)
		{
			byte[] byteMessage = BitConverter.GetBytes(message);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(byteMessage);
			}
			return ComputeAuthenticator(byteMessage);
		}

		public string ComputeAuthenticator(byte[] message)
		{
			Algorithm.Key = Key;

			var hash = Algorithm.ComputeHash(message);


			int offset = hash[hash.Length - 1] & 0xf;

			var selectedBytes = new byte[sizeof(int)];
			Buffer.BlockCopy(hash, offset, selectedBytes, 0, sizeof(int));

			if (BitConverter.IsLittleEndian)
			{
				//spec interprets bytes in big-endian order
				Array.Reverse(selectedBytes);
			}

			var selectedInteger = BitConverter.ToInt32(selectedBytes, 0);

			//remove the most significant bit for interoperability per spec
			var binary = selectedInteger & 0x7FFFFFFF;

			int password = binary % (int)Math.Pow(10, Digits);
			return password.ToString(new string('0', Digits));
		}

		public bool VerifyAuthenticator(int range, string code)
		{
			long baseMessage = DateTime.Now.UnixTimeStamp() / IntervalLength;

			for (long i = 0; i <= range; i++)
			{
				if (code == ComputeAuthenticator(baseMessage + i)) return true;
				if (code == ComputeAuthenticator(baseMessage - i - 1)) return true;
			}
			return false;
		}

	}
}
