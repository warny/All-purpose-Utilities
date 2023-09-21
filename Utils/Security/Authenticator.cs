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

		/// <summary>
		/// Créé un calculateur d'authentification
		/// </summary>
		/// <param name="algorithm">Nom de l'algorithme de hashage mac</param>
		/// <param name="key">Clef binaire</param>
		/// <param name="digits">Taille du code</param>
		/// <param name="intervalLength">Durée de conservation de la clée</param>
		public Authenticator(string algorithm, byte[] key, int digits, int intervalLength) : this((HMAC)CryptoConfig.CreateFromName(algorithm), key, digits, intervalLength) { }

		/// <summary>
		/// Créé un calculateur d'authentification
		/// </summary>
		/// <param name="algorithm">Algorithme de hashage mac</param>
		/// <param name="key">Clef binaire</param>
		/// <param name="digits">Taille du code</param>
		/// <param name="intervalLength">Durée de conservation de la clée</param>
		public Authenticator(HMAC algorithm, byte[] key, int digits, int intervalLength)
		{
			this.Digits = digits;
			this.Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
			this.Key = key ?? throw new ArgumentNullException(nameof(key));
			this.IntervalLength = intervalLength;
		}

		/// <summary>
		/// Créé un authentificateur compatible Google Authenticator
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public static Authenticator GoogleAuthenticator(byte[] key) => new Authenticator("HMACSHA256", key, 6, 30);

		/// <summary>
		/// Calcule le code actuel d'authentification
		/// </summary>
		/// <returns></returns>
		public string ComputeAuthenticator() => ComputeAuthenticator(CurrentMessage);

		/// <summary>
		/// Calcule le code d'authentification lié au message
		/// </summary>
		/// <param name="message">message numérique</param>
		/// <returns></returns>
		public string ComputeAuthenticator(long message)
		{
			byte[] byteMessage = BitConverter.GetBytes(message);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(byteMessage);
			}
			return ComputeAuthenticator(byteMessage);
		}

		/// <summary>
		/// Calcule le code d'authentification lié au message
		/// </summary>
		/// <param name="message">message binaire</param>
		/// <returns></returns>
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

		/// <summary>
		/// Vérifie le code par rapport au + ou - <paramref name="range"/> codes avant ou après le code actuel
		/// </summary>
		/// <param name="range">Nombre de code à vérifier avant ou après</param>
		/// <param name="code">Code à vérifier</param>
		/// <returns></returns>
		public bool VerifyAuthenticator(int range, string code)
		{
			long baseMessage = CurrentMessage;

			for (long i = 0; i <= range; i++)
			{
				if (code == ComputeAuthenticator(baseMessage + i)) return true;
				if (code == ComputeAuthenticator(baseMessage - i - 1)) return true;
			}
			return false;
		}

		/// <summary>
		/// Message en cours permettant de générer le code en cours
		/// </summary>
		public long CurrentMessage => DateTime.Now.UnixTimeStamp() / IntervalLength;
	}
}
