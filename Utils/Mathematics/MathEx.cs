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

    #region round

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
        T @base = T.Pow((T)Convert.ChangeType(10, typeof(T)), (T)Convert.ChangeType(exponent, typeof(T)));
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
        double @base = double.Pow(10, exponent);
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
    public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
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
    public static T Clamp<T>(this T value, T min, T max, IComparer<T> comparer)
    {
        if (comparer.Compare(min, max) > 0) throw new ArgumentException("min doit être inférieur à max");
        if (comparer.Compare(value, min) < 0) return min;
        if (comparer.Compare(value, max) > 0) return max;
        return value;
    }

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
            newLine[^1] = 1;
            pascalTriangleCache[i] = newLine;
            lastLine = newLine;
        }
        return lastLine;
    }
}
