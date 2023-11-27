using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Arrays;

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
	public static T[] Mid<T>(this T[] s, int start, int length)
	{
		if (s is null) return null;
		if (start < 0) start = s.Length + start;
		if (start <= -length) return [];
		if (start < 0) return s.Copy(0, length + start);
		if (start > s.Length) return [];
		if (start + length > s.Length) return s.Copy(start);
		return s.Copy(start, length);
	}

	/// <summary>
	/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
	/// </summary>
	/// <param name="s">Tableau dont on veut extraire une partie</param>
	/// <param name="start">Position de caractère de départ de base zéro de la partie à extaire</param>
	public static T[] Mid<T>(this T[] s, int start)
	{
		if (s is null) return null;
		if (start < 0) start = s.Length + start;
		if (start < 0) return s;
		if (start > s.Length) return [];
		return s.Copy(start);
	}

	/// <summary>
	/// Return array without values at start and end
	/// </summary>
	/// <typeparam name="T">Type of elements</typeparam>
	/// <param name="obj">Array to trim</param>
	/// <param name="values">Values to trim from array</param>
	/// <returns></returns>
	public static T[] Trim<T>(this T[] obj, params T[] values) => obj.Trim(value => values.Contains(value));

	/// <summary>
	/// Retourne un tableau sans les éléments au début et à la fin qui correspondent à la fonction passée en paramètre
	/// </summary>
	/// <typeparam name="T">Type des éléments du tableau</typeparam>
	/// <param name="obj">Tableau à traiter</param>
	/// <param name="trimTester">Fonction qui renvoie vrai sui l'élément doit être supprimé</param>
	/// <returns>Tableau sans les éléments supprimés</returns>
	public static T[] Trim<T>(this T[] obj, Func<T, bool> trimTester)
	{
		int start, end = obj.Length;
		for (start = 0; start < end; start++)
		{
			if (!trimTester(obj[start])) break;
		}
		for (end = obj.Length - 1; end > start; end--)
		{
			if (!trimTester(obj[end])) break;
		}
		if (start >= end) return new T[0];
		T[] result = new T[end - start + 1];
		Array.Copy(obj, start, result, 0, result.Length);
		return result;
	}

	/// <summary>
	/// Return array without values at start and end
	/// </summary>
	/// <typeparam name="T">Type of elements</typeparam>
	/// <param name="obj">Array to trim</param>
	/// <param name="values">Values to trim from array</param>
	/// <returns></returns>
	public static T[] TrimStart<T>(this T[] obj, params T[] values) => obj.TrimStart(value => values.Contains(value));

	/// <summary>
	/// Retourne un tableau sans les éléments au début qui correspondent à la fonction passée en paramètre
	/// </summary>
	/// <typeparam name="T">Type des éléments du tableau</typeparam>
	/// <param name="obj">Tableau à traiter</param>
	/// <param name="trimTester">Fonction qui renvoie vrai sui l'élément doit être supprimé</param>
	/// <returns>Tableau sans les éléments supprimés</returns>
	public static T[] TrimStart<T>(this T[] obj, Func<T, bool> trimTester)
	{
		int start, end = obj.Length;
		for (start = 0; start < end; start++)
		{
			if (!trimTester(obj[start])) break;
		}
		if (start >= end) return new T[0];
		T[] result = new T[end - start];
		Array.Copy(obj, start, result, 0, result.Length);
		return result;
	}

	/// <summary>
	/// Return array without values at start and end
	/// </summary>
	/// <typeparam name="T">Type of elements</typeparam>
	/// <param name="obj">Array to trim</param>
	/// <param name="values">Values to trim from array</param>
	/// <returns></returns>
	public static T[] TrimEnd<T>(this T[] obj, params T[] values) => obj.TrimEnd(value => values.Contains(value));

	/// <summary>
	/// Retourne un tableau sans les éléments au début et à la fin qui correspondent à la fonction passée en paramètre
	/// </summary>
	/// <typeparam name="T">Type des éléments du tableau</typeparam>
	/// <param name="obj">Tableau à traiter</param>
	/// <param name="trimTester">Fonction qui renvoie vrai sui l'élément doit être supprimé</param>
	/// <returns>Tableau sans les éléments supprimés</returns>
	public static T[] TrimEnd<T>(this T[] obj, Func<T, bool> trimTester)
	{
		int start = 0, end;
		for (end = obj.Length - 1; end > start; end--)
		{
			if (!trimTester(obj[end])) break;
		}
		if (start >= end) return new T[0];
		T[] result = new T[end - start + 1];
		Array.Copy(obj, start, result, 0, result.Length);
		return result;
	}

	/// <summary>
	/// Vérifie si le tableau commence par les valeurs indiqués
	/// </summary>
	/// <typeparam name="T">Type des éléments du tableau</typeparam>
	/// <param name="obj">Tableau de référence</param>
	/// <param name="start">Elements à tester</param>
	/// <returns>Vrai si le tableau commence par les éléments en paramètre</returns>
	public static bool StartWith<T>(this T[] obj, params T[] start)
	{
		if (obj.Length == 0) return start.Length == 0;
		if (start.Length > obj.Length) return false;

		Func<T, T, bool> areEquals;
		if (typeof(IEquatable<T>).IsAssignableFrom(typeof(T))) areEquals = (T obj1, T obj2) => ((IEquatable<T>)obj1).Equals(obj2);
		else if (typeof(IComparable<T>).IsAssignableFrom(typeof(T))) areEquals = (T obj1, T obj2) => ((IComparable<T>)obj1).CompareTo(obj2) == 0;
		else areEquals = (T obj1, T obj2) => obj1.Equals(obj2);

		if (start.Length > obj.Length) return false;
		for (int i = 0; i < start.Length; i++)
		{
			if (!areEquals(obj[i], start[i]))
			{
				return false;
			}
		}
		return true;
	}

    /// <summary>
    /// Vérifie si le tableau commence par les valeurs indiqués
    /// </summary>
    /// <typeparam name="T">Type des éléments du tableau</typeparam>
    /// <param name="obj">Tableau de référence</param>
    /// <param name="start">Elements à tester</param>
    /// <returns>Vrai si le tableau se termine par les éléments en paramètre</returns>
    public static bool EndWith<T>(this T[] obj, params T[] end)
	{
		if (obj.Length == 0) return end.Length == 0;
		if (end.Length > obj.Length) return false;

		int shift = obj.Length - end.Length;

		Func<T, T, bool> areEquals;
		if (typeof(IEquatable<T>).IsAssignableFrom(typeof(T))) areEquals = (T obj1, T obj2) => ((IEquatable<T>)obj1).Equals(obj2);
		else if (typeof(IComparable<T>).IsAssignableFrom(typeof(T))) areEquals = (T obj1, T obj2) => ((IComparable<T>)obj1).CompareTo(obj2) == 0;
		else areEquals = (T obj1, T obj2) => obj1.Equals(obj2);

		if (end.Length > obj.Length) return false;
		for (int i = 0; i < end.Length; i++)
		{
			if (!areEquals(obj[i + shift], end[i]))
			{
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
	public static T[] Copy<T>(this T[] array, int start, int length)
	{
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
	public static T[] Copy<T>(this T[] array, int start) => array.Copy(start, array.Length - start);

	/// <summary>
	/// Copie le tableau multidimentionnel
	/// </summary>
	/// <param name="array"></param>
	/// <returns></returns>
	public static Array Copy(Array array)
	{
		var elementType = array.GetType().GetElementType();
		int[] lowerBounds = new int[array.Rank];
		int[] dimensions = new int[array.Rank];
		for (int i = 0; i < dimensions.Length; i++)
		{
			lowerBounds[i] = array.GetLowerBound(i);
			dimensions[i] = array.GetUpperBound(i) - array.GetLowerBound(i) + 1;
		}
		var result = Array.CreateInstance(elementType, dimensions, lowerBounds);
		Array.Copy(array, result, array.Length);
		return result;
	}

	/// <summary>
	/// Redimensionne le tableau et rajoute un remplissage à gauche
	/// </summary>
	/// <typeparam name="T">Type des éléments</typeparam>
	/// <param name="array">Tableau à redimensionner</param>
	/// <param name="length">Longueur</param>
	/// <param name="value">Valeur à ajouter aux nouveau éléments</param>
	/// <returns>Tableau redimensionner</returns>
	public static T[] PadLeft<T>(this T[] array, int length, T value = default)
	{
		if (array.Length > length) throw new ArgumentOutOfRangeException(nameof(length));
		T[] result = new T[length];
		int start = length - array.Length;
		for (int i = 0; i < start; i++) result[i] = value;
		for (int i = start; i < length; i++) result[i] = array[i - start];
		return result;
	}

	/// <summary>
	/// Redimensionne le tableau et rajoute un remplissage à droite
	/// </summary>
	/// <typeparam name="T">Type des éléments</typeparam>
	/// <param name="array">Tableau à redimensionner</param>
	/// <param name="length">Longueur</param>
	/// <param name="value">Valeur à ajouter aux nouveau éléments</param>
	/// <returns>Tableau redimensionner</returns>
	public static T[] PadRight<T>(this T[] array, int length, T value = default)
	{
		if (array.Length > length) throw new ArgumentOutOfRangeException(nameof(length));
		T[] result = new T[length];
		int end = array.Length;
		for (int i = 0; i < end; i++) result[i] = array[i];
		for (int i = end; i < length; i++) result[i] = value;
		return result;
	}

	/// <summary>
	/// Ajuste la taille d'un tableau à une taille déterminée pour l'encodage des nombres
	/// </summary>
	/// <typeparam name="T">Type des éléments du tableau</typeparam>
	/// <param name="array">Tableau à ajuster</param>
	/// <param name="invert">Indique s'il faut inverser l'ordre des éléments</param>
	/// <param name="fullLength">Longueur finale</param>
	/// <returns>Tableau ajusté</returns>
	public static T[] Adjust<T>(this T[] array, bool invert, int fullLength)
	{
		int length = Math.Min(fullLength, array.Length);
		T[] result = new T[fullLength];
		Array.Clear(result, 0, fullLength);
		if (invert)
		{
			for (int i = 0; i < length; i++)
			{
				result[i] = array[i];
			}
		}
		else
		{
			for (int i = 0; i < length; i++)
			{
				result[fullLength - 1 - i] = array[i];
			}
		}
		array = result;
		return array;
	}

	/// <summary>
	/// Converti la liste de chaine de caractère en tableau
	/// </summary>
	/// <param name="values">Valeurs à convertir</param>
	/// <param name="elementType">Type cible</param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException">si <paramref name="values"/> ou <paramref name="elementType"/> est null</exception>
	public static Array ConvertToArrayOf(this IEnumerable<string> values, Type elementType)
	{
		values.ArgMustNotBeNull();
		elementType.ArgMustNotBeNull();

		var results = new System.Collections.ArrayList();

		foreach (var value in values)
		{
			results.Add(Parsers.Parse(value, elementType));
		}
		return results.ToArray(elementType);
	}

	/// <summary>
	/// Converti la liste de chaine de caractère en tableau
	/// </summary>
	/// <param name="values">Valeurs à convertir</param>
	/// <typeparam name="T">Type cible</typeparam>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"></exception>
	public static T[] ConvertToArrayOf<T>(this IEnumerable<string> values)
		=> (T[])values.ConvertToArrayOf(typeof(T));
}
