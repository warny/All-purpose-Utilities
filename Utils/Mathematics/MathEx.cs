using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics;

public static class MathEx
{
    public const double Deg2Rad = Math.PI / 180;
    public const double Rad2Deg = 180 / Math.PI;

    #region Modulus
    /// <summary>
    /// Calcul alternatif du modulo (différent de l'opérateur %)
    /// Le résultat est toujours compris entre 0 et y-1
    /// Exemple : 
    ///   -1 % 3 = -1
    ///	  Modulo(-1, 3) = 2
    /// </summary>
    public static T Mod<T>(T a, T b)
        where T : struct, IModulusOperators<T, T, T>, IAdditionOperators<T, T, T>
         => ((a % b + b) % b);
    #endregion

    public static T Abs<T>(T value)
        where T : struct, INumber<T>
        => value < T.Zero ? -value : value;

    #region round

    public static float Round(float value, float @base) => (float)Round((double)value, @base);

    public static T Round<T>(T value, T @base)
        where T : struct, INumber<T>
    {
        T middle = @base / (T.One + T.One);
        T r = Mod(value, @base);
        if (r < middle)
        {
            return value - r;
        }
        else
        {
            return value - r + @base;
        }
    }

    public static T Round<T>(T value, int exponent = 0)
        where T : struct, INumber<T>, IPowerFunctions<T>
    {
        T @base = T.Pow((T)(object)10, (T)(object)exponent);
        return Round<T>(value, @base);
    }



    public static double Round(double value, double @base)
    {
        double middle = @base / 2;
        double r = Mod(value, @base);
        if (r < middle)
        {
            return value - r;
        }
        else
        {
            return value - r + @base;
        }
    }

    public static double Round(double value, int exponent = 0)
    {
        double @base = Math.Pow(10, exponent);
        return Round(value, @base);
    }
    #endregion

    #region floor
    /// <summary>
    /// retrouve le multiple de la base inférieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple inférieur</returns>
    public static T Floor<T>(T value, T @base)
        where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
        => (value, @base) switch
        {
            (double d, double b) => (T)(object)Floor(d, b),
            (float d, float b) => (T)(object)Floor(d, b),
            _ => (value - T.One) - (Mod(value - T.One, @base))
        };

    /// <summary>
    /// retrouve le multiple de la base inférieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple inférieur</returns>
    public static float Floor(float value, float @base) => (float)((value - 1) - (Math.IEEERemainder(value - 1, @base)));

    /// <summary>
    /// retrouve le multiple de la base inférieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple inférieur</returns>
    /// <summary>
    /// retrouve le multiple de la base inférieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple inférieur</returns>
    public static double Floor(double value, double @base) => ((value - 1) - (Math.IEEERemainder(value - 1, @base)));
    #endregion

    #region ceiling
    /// <summary>
    /// retrouve le multiple de la base supérieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple supérieur</returns>
    public static T Ceiling<T>(T value, T @base)
        where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
        => (value, @base) switch {
            (double d, double b) => (T)(object)Ceiling(d, b),
            (float d, float b) => (T)(object)Ceiling(d, b),
            _ => (value - T.One) + @base - (Mod(value - T.One, @base))
        };

    /// <summary>
    /// retrouve le multiple de la base supérieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple supérieur</returns>
    public static float Ceiling(float value, float @base) => (float)((value - 1) + @base - (Math.IEEERemainder(value - 1, @base)));

    /// <summary>
    /// retrouve le multiple de la base supérieur à value
    /// </summary>
    /// <param name="value">valeur</param>
    /// <param name="base">base</param>
    /// <returns>multiple supérieur</returns>
    public static double Ceiling(double value, double @base) => (value - 1) + @base - (Math.IEEERemainder(value - 1, @base));
    #endregion

    #region MinMax
    /// <summary>
    /// Retourne la valeur minimale de <paramref name="values"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values">Valeurs à comparer</param>
    /// <returns>Valeur minimale</returns>
    public static T Min<T>(params T[] values) where T : IComparable<T>
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            if (value.CompareTo(result) < 0)
            {
                result = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Retourne la valeur minimale de <paramref name="values"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values">Valeurs à comparer</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns>Valeur minimale</returns>
    public static T Min<T>(IComparer<T> comparer, params T[] values)
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            if (comparer.Compare(value, result) < 0)
            {
                result = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Retourne la valeur maximale de <paramref name="values"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values">Valeurs à comparer</param>
    /// <returns>Valeur maximale</returns>
    public static T Max<T>(params T[] values) where T : IComparable<T>
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            if (value.CompareTo(result) > 0)
            {
                result = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Retourne la valeur maximale de <paramref name="values"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values">Valeurs à comparer</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns>Valeur maximale</returns>
    public static T Max<T>(IComparer<T> comparer, params T[] values)
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            if (comparer.Compare(value, result) > 0)
            {
                result = value;
            }
        }
        return result;
    }

    /// <summary>
    /// Retourne <paramref name="value"/> si elle est comprise entre <paramref name="min"/> et <paramref name="max"/>, <paramref name="min"/> si elle est inférieur et <paramref name="max"/> si elle est supérieure
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="min">Valeur minimale</param>
    /// <param name="max">Valeur Maximale</param>
    /// <returns></returns>
    public static T MinMax<T>(this T value, T min, T max) where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0) throw new ArgumentException("min doit être inférieur à max");
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    /// <summary>
    /// Retourne <paramref name="value"/> si elle est comprise entre <paramref name="min"/> et <paramref name="max"/>, <paramref name="min"/> si elle est inférieur et <paramref name="max"/> si elle est supérieure
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">Valeur à comparer</param>
    /// <param name="min">Valeur minimale</param>
    /// <param name="max">Valeur Maximale</param>
    /// <param name="comparer">Comparateur</param>
    /// <returns></returns>
    public static T MinMax<T>(this T value, T min, T max, IComparer<T> comparer)
    {
        if (comparer.Compare(min, max) > 0) throw new ArgumentException("min doit être inférieur à max");
        if (comparer.Compare(value, min) < 0) return min;
        if (comparer.Compare(value, max) > 0) return max;
        return value;
    }

    #endregion

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

    #region calculs
    /// <summary>
    /// Compute the square root of <paramref name="value"/>
    /// </summary>
    /// <param name="value">Value to compute the square root of</param>
    /// <returns>Square root of <paramref name="value"/></returns>
    /// <typeparam name="T">Type of number</typeparam>
    public static T Sqrt<T>(T value)
        where T : struct, INumberBase<T>, IPowerFunctions<T>
        => T.Pow(value, T.One / (T.One + T.One));

    public static T Tan<T>(Task<T> value)
        where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
        => (T)(object)Math.Tan((double)(object)value);

    public static T Sin<T>(Task<T> value)
        where T : struct, IModulusOperators<T, T, T>, INumberBase<T>, IPowerFunctions<T>
        => (T)(object)Math.Sin((double)(object)value);

    public static T Cos<T>(Task<T> value)
        where T : struct, IModulusOperators<T, T, T>, INumberBase<T>
        => (T)(object)Math.Cos((double)(object)value);
    #endregion

    private static readonly Dictionary<int, int[]> pascalTriangleCache = new()
        {
            { 0, new [] { 1 } },
            { 1, new [] { 1, 1 } },
            { 2, new [] { 1, 2, 1 } },
            { 3, new [] { 1, 3, 3, 1 } },
            { 4, new [] { 1, 4, 6, 4, 1 } },
            { 5, new [] { 1, 5, 10, 10, 5, 1 } },
            { 6, new [] { 1, 6, 15, 20, 15, 6, 1 } }
        };

    public static int[] ComputePascalTriangleLine(int lineNumber)
    {
        lineNumber.ArgMustBeGreaterOrEqualsThan(0);
        if (pascalTriangleCache.TryGetValue(lineNumber, out var pascalTriangleLine))
        {
            return pascalTriangleLine;
        }
        var maxLine = pascalTriangleCache.Keys.Max();
        int[] lastLine = pascalTriangleCache[maxLine];

        for (int i = maxLine + 1; i <= lineNumber; i++)
        {
            var newLine = new int[i + 1];
            newLine[0] = 1;
            for (int j = 1; j < i; j++)
            {
                newLine[j] = lastLine[j - 1] + lastLine[j];
            }
            newLine[newLine.Length - 1] = 1;
            pascalTriangleCache[i] = newLine;
            lastLine = newLine;
        }
        return lastLine;
    }
}
