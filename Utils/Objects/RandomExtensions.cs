using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public static class RandomExtensions
	{
		private static readonly char[] defaultRandomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ".ToCharArray();

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="r">Générateur de nombres aléatoires</param>
		/// <param name="length">Longueur de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(this Random r, int length, char[] characters = null)
			=> RandomString(r, length, length, characters);

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="r">Générateur de nombres aléatoires</param>
		/// <param name="minLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="maxLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(this Random r, int minLength, int maxLength, char[] characters = null)
		{
			r.Arg().MustNotBeNull();
			characters ??= defaultRandomCharacters;
			var length = r.Next(minLength, maxLength);

			char[] result = new char[length];
			for (int i = 0; i < length; i++)
			{
				result[i] = characters[r.Next(0, characters.Length - 1)];
			}
			return new string(result);
		}


	}
}
