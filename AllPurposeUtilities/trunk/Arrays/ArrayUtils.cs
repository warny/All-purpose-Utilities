using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Arrays
{
	public static class ArrayUtils
	{
		/// <summary>
		/// Récupère une partie de ce tableau. La sous-parte démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Tableau dont on veut extraire une partie</param>
		/// <param name="start">Position de caractère de départ de base zéro de la partie à extaire</param>
		/// <param name="length">Nombre d'éléments à extraires</param>
		/// <returns>
		/// Un tableau de longueur length qui commence
		/// à startIndex dans cette instance, ou System.String.Empty si startIndex est
		/// égal à la longueur de cette instance et length est égal à zéro.
		/// </returns>
		public static T[] Mid<T>( this T[] s, int start, int length )
		{
			if (s == null) return null;
			if (start < 0) start = s.Length + start;
			if (start <= -length) return new T[] { };
			if (start < 0) return s.Copy(0, length + start);
			if (start > s.Length) return new T[] { };
			if (start + length > s.Length) return s.Copy(start);
			return s.Copy(start, length);
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Tableau dont on veut extraire une partie</param>
		/// <param name="start">Position de caractère de départ de base zéro de la partie à extaire</param>
		public static T[] Mid<T>( this T[] s, int start )
		{
			if (s==null) return null;
			if (start < 0) start = s.Length + start;
			if (start < 0) return s;
			if (start > s.Length) return new T[] { };
			return s.Copy(start);
		}

		public static T[] Copy<T>( this T[] array, int start, int length ) {
			T[] result = new T[length];
			Array.Copy(array, start, result, 0, length);
			return result;
		}

		public static T[] Copy<T>( this T[] array, int start )
		{
			return array.Copy(start, array.Length-start);
		}

	}
}
