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

		/// <summary>
		/// Return array without values at start and end
		/// </summary>
		/// <typeparam name="T">Type of elements</typeparam>
		/// <param name="obj">Array to trim</param>
		/// <param name="values">Values to trim from array</param>
		/// <returns></returns>
		public static T[] Trim<T>( this T[] obj, params T[] values )
		{
			int start = 0, end = values.Length;
			for (start = 0 ; start < end ; start++) {
				T value = obj[start];
				if (!values.Contains(value)) break;
			}
			for (end = end - 1 ; end > start ; end--) {
				T value = obj[end];
				if (!values.Contains(value)) break;
			}
			if (start >= end) return new T[0];
			T[] result = new T[end - start];
			Array.Copy(obj, start, result, 0, result.Length);
			return result;
		}

		/// <summary>
		/// Vérifie si le tableau commence par les valeurs indiqués
		/// </summary>
		/// <typeparam name="T">Type des éléments du tableau</typeparam>
		/// <param name="obj">Tableua de référence</param>
		/// <param name="start">Elements à tester</param>
		/// <returns>Vrai si le tableua commence par les éléments en paramètre</returns>
		public static bool StartWith<T>(this T[] obj, params T[] start) where T : IEquatable<T> 
		{
			if (start.Length > obj.Length) return false;
			for (int i = 0; i < start.Length; i++) {
				if (!obj[i].Equals(start[i])) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Vérifie si le tableau finit par les valeurs indiqués
		/// </summary>
		/// <typeparam name="T">Type des éléments du tableau</typeparam>
		/// <param name="obj">Tableua de référence</param>
		/// <param name="start">Elements à tester</param>
		/// <returns>Vrai si le tableua commence par les éléments en paramètre</returns>
		public static bool EndWith<T>(this T[] obj, params T[] start) where T : IEquatable<T> 
		{
			if (start.Length > obj.Length) return false;
			var objStart = obj.Length - obj.Length;
			for (int i = 0; i < start.Length; i++) {
				if (!obj[i].Equals(start[objStart + i])) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Renvoie la copie des valeurs d'un tableau
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array">Tableau à copier</param>
		/// <param name="start">Position de départ</param>
		/// <param name="length">Nombre d'éléments à copier</param>
		/// <returns>Tableau contenant le sous ensemble des éléments</returns>
		public static T[] Copy<T>( this T[] array, int start, int length ) {
			T[] result = new T[length];
			Array.Copy(array, start, result, 0, length);
			return result;
		}

		/// <summary>
		/// Renvoie la copie des éléments d'un tableau à partir de l'index donné
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="array">Tableau à copier</param>
		/// <param name="start">Position de départ</param>
		/// <returns></returns>
		public static T[] Copy<T>( this T[] array, int start )
		{
			return array.Copy(start, array.Length-start);
		}

	}
}
