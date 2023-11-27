using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Utils.Arrays;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Point
/// <remarks>
/// Un point de dimension n est multipliable par une matrice d'espace normal de dimension n+1
/// </remarks>
/// </summary>
public sealed partial class Point<T> : IEquatable<Point<T>>, IEquatable<T[]>
    where T : struct, IFloatingPoint<T>, IPowerFunctions<T>
{
    private static ArrayEqualityComparer<T> ComponentsComparer { get; } = new();

    /// <summary>
    /// composantes du vecteur
    /// </summary>
    internal readonly T[] components;

    private Point()
    {
    }

    /// <summary>
    /// constructeur par dimensions
    /// </summary>
    /// <param name="dimensions"></param>
    public Point(int dimensions)
    {
        this.components = new T[dimensions + 1];
        this.components[components.Length - 1] = T.One;
    }

    /// <summary>
    /// constructeur par valeurs
    /// </summary>
    /// <param name="components"></param>
    public Point(params T[] components)
    {
        components.ArgMustNotBeNull();

        this.components = new T[components.Length + 1];
        Array.Copy(components, this.components, components.Length);
        this.components[this.components.Length - 1] = T.One;
    }

    /// <summary>
    /// Constructeur de copie
    /// </summary>
    /// <param name="point"></param>
    public Point(Point<T> point)
    {
        point.ArgMustNotBeNull();
        this.components = new T[point.components.Length];
        Array.Copy(point.components, this.components, point.components.Length);
    }

    /// <summary>
    /// Retourne ou définie la valeur à la dimension indiquée
    /// </summary>
    /// <param name="dimension"></param>
    /// <returns></returns>
    public T this[int dimension]
    {
        get { return this.components[dimension]; }
        set
        {
            this.components[dimension] = value;
        }
    }

    /// <summary>
    /// dimension du vecteur
    /// </summary>
    public int Dimension => this.components.Length - 1;

    public override bool Equals(object obj) => obj switch
    {
        Point<T> p => Equals(p),
        double[] array => Equals(array),
        _ => false
    };

    public bool Equals(Point<T> other)
    {
        if (other is null) return false;
        if (Object.ReferenceEquals(this, other)) return true;
        return ComponentsComparer.Equals(this.components, other.components);
    }
    public bool Equals(T[] other)
    {
        if (other is null) return false;
        return ComponentsComparer.Equals(this.components, other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var temp = this.components.Length.GetHashCode();
            foreach (var el in this.components)
            {
                temp = ((temp * 23) << 1) + el.GetHashCode();
            }
            return temp;
        }
    }

    public static T Distance(Point<T> point1, Point<T>  point2)
    {
        if (point1.components.Length != point2.components.Length)
        {
            throw new ArgumentException("Les deux points n'ont pas la même dimension");
        }
        T temp = T.Zero;
        for (int i = 0; i < point1.components.Length - 1; i++)
        {
            T diff = point1.components[i] - point2.components[i];
            temp += diff * diff;
        }

        return MathEx.Sqrt(temp);
    }

    public override string ToString()
    {
        return string.Format("({0})", string.Join(";", this.components.Select(c => c.ToString()).ToArray(), 0, this.Dimension));
    }

    public static Point<T> IntermediatePoint(Point<T> point1, Point<T> point2, T position)
    {
        if (point1.Dimension != point2.Dimension) { throw new ArgumentException("Les dimensions de point1 et point2 sont incompatibles", nameof(point2)); }

        var result = new T[point1.Dimension];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (T.One - position) * point1[i] + position * point2[i];
        }
        return new Point<T>(result);
    }
}
