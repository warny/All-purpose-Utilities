using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utils.Objects;

public static class ObjectUtils
{
	/// <summary>
	/// Return true if the given object of type T? is null or is default value for type T 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="nullableObj"></param>
	/// <returns></returns>
	public static bool IsNullOrDefault<T>(this T? nullableObj) where T : struct
	{
		if (!nullableObj.HasValue) return true;
		return nullableObj.Equals(default(T));
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="ifNotNull">The function to execute for the current object.</param>
	/// <param name="ifNull">The function to execute isf the object is null</param>
	/// <returns></returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Func<Result> ifNull)
	{
		if (value == null)
		{
			return ifNull();
		}
		return ifNotNull(value);
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="ifNotNull">The function to execute for the current object.</param>
	/// <param name="ifNull">The value to return if the object is null</param>
	/// <returns></returns>
	public static Result Do<T, Result>(this T value, Func<T, Result> ifNotNull, Result ifNull)
	{
		if (value == null)
		{
			return ifNull;
		}
		return ifNotNull(value);
	}

	/// <summary>
	/// Execute the specified function for the current object
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="Result">The type of the esult.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="ifNotNull">The function to execute for the current object.</param>
	/// <param name="ifNull">The function to execute isf the object is null</param>
	/// <returns></returns>
	public static async Task<Result> DoAsync<T, Result>(this T value, Func<T, Result> ifNotNull, Func<Result> ifNull)
	{
		if (value == null)
		{
			return await Task.Run(ifNull);
		}
		return await Task.Run(() => ifNotNull(value));
	}

    /// <summary>
    /// Execute the specified function for the current object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Result">The type of the esult.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="ifNotNull">The function to execute for the current object.</param>
    /// <param name="ifNull">The value to return if the object is null</param>
    /// <returns></returns>
    public static async Task<Result> DoAsync<T, Result>(this T value, Func<T, Result> ifNotNull, Result ifNull)
    {
        if (value == null)
        {
            return ifNull;
        }
        return await Task.Run(() => ifNotNull(value));
    }


    /// <summary>
    /// Calcul le hash d'un tableau multidimensionnel
    /// </summary>
    /// <param name="array"></param>
    /// <returns></returns>
    public static int ComputeHash(this Array array)
	{
		array.ArgMustNotBeNull();
		unchecked
		{
			int hash = 23;
			InnerComputeHash(0, new int[array.Rank], ref hash);
			return hash;
		}

		void InnerComputeHash(int r, int[] positions, ref int hash)
		{
			unchecked
			{
				if (r == positions.Length)
				{
					hash *= 31;
					hash += array.GetValue(positions).GetHashCode();
					return;
				}

				for (int i = array.GetLowerBound(r); i <= array.GetUpperBound(r); i++)
				{
					positions[r] = i;
					InnerComputeHash(r + 1, positions, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Calcul le hash d'un tableau multidimensionnel
	/// </summary>
	/// <param name="array"></param>
	/// <param name="getHashCode">Fonction de calcul de hash</param>
	/// <returns></returns>
	public static int ComputeHash<T>(this Array array, Func<T, int> getHashCode)
	{
		array.ArgMustNotBeNull();
		getHashCode.ArgMustNotBeNull();

		unchecked
		{
			int hash = 23;
			InnerComputeHash(0, new int[array.Rank], ref hash);
			return hash;
		}

		void InnerComputeHash(int r, int[] positions, ref int hash)
		{
			unchecked
			{
				if (r == positions.Length)
				{
					hash *= 31;
					hash += getHashCode((T)array.GetValue(positions));
					return;
				}

				for (int i = array.GetLowerBound(r); i <= array.GetUpperBound(r); i++)
				{
					positions[r] = i;
					InnerComputeHash(r + 1, positions, ref hash);
				}
			}
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash(params object[] objects) => ComputeHash((IEnumerable<object>)objects);

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash(this IEnumerable<object> objects)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => value.GetHashCode() + acc * 31);
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash<T>(Func<T, int> getHashCode, IEnumerable<T> objects)
	{
		unchecked
		{
			return objects.Aggregate(23, (acc, value) => getHashCode(value) + acc * 31);
		}
	}

	/// <summary>
	/// Compute a hash from the hashes of the given objects
	/// </summary>
	/// <param name="objects"></param>
	/// <returns></returns>
	public static int ComputeHash<T>(Func<T, int> getHashCode, params T[] objects) => ComputeHash(getHashCode, (IEnumerable<T>)objects);

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="obj1"></param>
	/// <param name="obj2"></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Swap<T>(ref T obj1, ref T obj2)
	{
		T temp;
		temp = obj1;
		obj1 = obj2;
		obj2 = temp;
	}

    #region comparaisons
    /// <summary>
    /// Retourne <see cref="true"/> si elle est comprise entre <paramref name="lowerBound"/> et <paramref name="upperBound"/> inclus
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="lowerBound">Valeur minimale</param>
    /// <param name="upperBound">Valeur Maximale</param>
    /// <returns></returns>
    public static bool Between<T>(this T value, T lowerBound, T upperBound) where T : IComparable<T>
    {
        return value.CompareTo(lowerBound) >= 0 && value.CompareTo(upperBound) <= 0;
    }

    /// <summary>
    /// Retourne <see cref="true"/> si elle est comprise entre <paramref name="lowerBound"/> et <paramref name="upperBound"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="lowerBound">Valeur minimale</param>
    /// <param name="upperBound">Valeur Maximale</param>
    /// <param name="includeLowerBound">Inclus la valeur minimale</param>
    /// <param name="includeUpperBound">Inclus la valeur maximale</param>
    /// <returns></returns>
    public static bool Between<T>(this T value, T lowerBound, T upperBound, bool includeLowerBound = true, bool includeUpperBound = true) where T : IComparable<T>
    {
        var low = value.CompareTo(lowerBound);
        if (low < 0) return false;
        if (!includeLowerBound && low == 0) return false;

        var up = value.CompareTo(upperBound);
        if (up > 0) return false;
        if (!includeUpperBound && up == 0) return false;

        return true;
    }

    /// <summary>
    /// Retourne <see cref="true"/> si elle est comprise entre <paramref name="lowerBound"/> et <paramref name="upperBound"/> inclus
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="lowerBound">Valeur minimale</param>
    /// <param name="upperBound">Valeur Maximale</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns></returns>
    public static bool Between<T>(this T value, IComparer<T> comparer, T lowerBound, T upperBound)
    {
        return comparer.Compare(value, lowerBound) >= 0 && comparer.Compare(value, upperBound) <= 0;
    }

    /// <summary>
    /// Retourne <see cref="true"/> si elle est comprise entre <paramref name="lowerBound"/> et <paramref name="upperBound"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="lowerBound">Valeur minimale</param>
    /// <param name="upperBound">Valeur Maximale</param>
    /// <param name="includeLowerBound">Inclus la valeur minimale</param>
    /// <param name="includeUpperBound">Inclus la valeur maximale</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns></returns>
    public static bool Between<T>(this T value, IComparer<T> comparer, T lowerBound, T upperBound, bool includeLowerBound = true, bool includeUpperBound = true)
    {
        var low = comparer.Compare(value, lowerBound);
        if (low < 0) return false;
        if (!includeLowerBound && low == 0) return false;

        var up = comparer.Compare(value, upperBound);
        if (up > 0) return false;
        if (!includeUpperBound && up == 0) return false;

        return true;
    }


    /// <summary>
    /// Renvoie si la valeur est dans le tableau
    /// </summary>
    /// <typeparam name="T">Type de la valeur</typeparam>
    /// <param name="value">Valeur à rechercher</param>
    /// <param name="objects">Liste de valeurs dans lesquelles la recherche s'applique</param>
    /// <returns></returns>
    public static bool In<T>(this T value, params T[] objects) => objects.Contains(value);
    /// <summary>
    /// Renvoie si la valeur n'est pas dans le tableau
    /// </summary>
    /// <typeparam name="T">Type de la valeur</typeparam>
    /// <param name="value">Valeur à rechercher</param>
    /// <param name="objects">Liste de valeurs dans lesquelles la recherche s'applique</param>
    /// <returns></returns>
    public static bool NotIn<T>(this T value, params T[] objects) => !objects.Contains(value);

    /// <summary>
    /// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
    /// </summary>
    /// <typeparam name="T">Type des éléments dans le tableau</typeparam>
    /// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
    /// <param name="toFind">Element à retrouver</param>
    /// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
    public static int IndexOf<T>(this IEnumerable<T> enumeration, T toFind)
    {
        int i = 0;
        foreach (var element in enumeration)
        {
            if (element.Equals(toFind)) return i;
            i++;
        }
        return -1;
    }

    /// <summary>
    /// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
    /// </summary>
    /// <typeparam name="T">Type des éléments dans le tableau</typeparam>
    /// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
    /// <param name="toFind">Element à retrouver</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
    public static int IndexOf<T>(this IEnumerable<T> enumeration, T toFind, IEqualityComparer<T> comparer)
    {
        int i = 0;
        foreach (var element in enumeration)
        {
            if (comparer.Equals(element, toFind)) return i;
            i++;
        }
        return -1;
    }

    /// <summary>
    /// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
    /// </summary>
    /// <typeparam name="T">Type des éléments dans le tableau</typeparam>
    /// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
    /// <param name="func">Fonction de recherche</param>
    /// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
    public static int IndexOf<T>(this IEnumerable<T> enumeration, Func<T, bool> func)
    {
        int i = 0;
        foreach (var element in enumeration)
        {
            if (func(element)) return i;
            i++;
        }
        return -1;
    }

    #endregion

}
