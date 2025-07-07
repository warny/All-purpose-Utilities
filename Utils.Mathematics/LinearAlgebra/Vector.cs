using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Vecteur
/// </summary>
public sealed partial class Vector<T> : IEquatable<Vector<T>>, IEquatable<T[]>, ICloneable
    where T : struct, IFloatingPoint<T>, IRootFunctions<T>
{
    private static EnumerableEqualityComparer<T> ComponentComparer { get; } = EnumerableEqualityComparer<T>.Default;

    /// <summary>
    /// composantes du vecteur
    /// </summary>
    internal readonly T[] components;

    /// <summary>
    /// Longueur du vecteur	(calculée à la demande)
    /// </summary>
    private T? norm;

    /// <summary>
    /// constructeur par dimensions
    /// </summary>
    /// <param name="dimensions"></param>
    private Vector(int dimensions)
    {
        components = new T[dimensions];
        norm = T.Zero;
    }

    /// <summary>
    /// constructeur par valeurs
    /// </summary>
    /// <param name="components"></param>
    public Vector(params T[] components)
    {
        if (components.Length == 0) throw new ArgumentException("La dimension du vecteur ne peut pas être 0", nameof(components));
        this.components = new T[components.Length];
        Array.Copy(components, this.components, components.Length);
    }

    public Vector(Vector<T> vector)
    {
        components = new T[vector.components.Length];
        Array.Copy(vector.components, components, vector.components.Length);
    }

    /// <summary>
    /// Retourne ou défini la valeur de la dimension indiquée
    /// </summary>
    /// <param name="dimension"></param>
    /// <returns></returns>
    public T this[int dimension] => this.components[dimension];

    /// <summary>
    /// dimension du vecteur
    /// </summary>
    public int Dimension => this.components.Length;

    /// <summary>
    /// Longueur du vecteur
    /// </summary>
    public T Norm
    {
        get
        {
            if (norm is not null) return norm.Value;
            T temp = T.Zero;
            for (int i = 0; i < this.components.Length; i++)
            {
                temp += this.components[i] * this.components[i];
            }
            norm = T.CreateChecked(Math.Sqrt(double.CreateChecked(temp)));
            return norm.Value;
        }
    }

    public Vector<T> Normalize() => this / Norm;

    public override bool Equals(object? obj) => obj switch
    {
        Vector<T> v => Equals(v),
        double[] a => Equals(a),
        _ => false,
    };

    public bool Equals(Vector<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(other.components);
    }

    public bool Equals(T[] other)
    {
        if (other is null) return false;
        return ComponentComparer.Equals(this.components, other);
    }

    public override string ToString()
        => $"({string.Join(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator, components)})";

    /// <summary>
    /// Converts a vector for use in a normal space.
    /// </summary>
    /// <returns>The converted vector for normal space.</returns>
    public Vector<T> ToNormalSpace()
    {
        Vector<T> result = new (Dimension + 1);
        Array.Copy(components, result.components, Dimension);
        result.components[Dimension] = T.One;
        return result;
    }

    /// <summary>
    /// Converts a vector usable in normal space to a vector usable in Cartesian space.
    /// </summary>
    /// <returns>The converted vector for Cartesian space.</returns>
    public Vector<T> FromNormalSpace()
    {
        var temp = this;
        if (temp[temp.Dimension - 1] != T.One)
        {
            temp /= temp[temp.Dimension - 1];
        }
        Vector<T> result = new (Dimension - 1);
        Array.Copy(temp.components, result.components, Dimension - 1);
        return result;
    }

    /// <summary>
    /// Returns the cross product of (n-1) vectors of dimension n.
    /// </summary>
    /// <param name="vectors">Vectors of dimension n.</param>
    /// <returns>A normal vector.</returns>
    /// <exception cref="ArgumentException">Throws an exception if vectors are not all of dimension n.</exception>
    public static Vector<T> Product(params Vector<T>[] vectors)
    {
        int dimensions = vectors.Length + 1;
        foreach (var vector in vectors)
        {
            if (vector.components.Length != dimensions)
            {
                throw new ArgumentException(string.Format("Tous les vecteurs ne sont pas de dimension {0}", dimensions), "vectors");
            }
        }

        T[] result = new T[dimensions];
        var columns = Enumerable.Range(0, dimensions);
        T sign = T.One;
        foreach (var column in columns)
        {
            var nextColumns = columns.Where(c => c != column);
            result[column] = sign * ComputeProduct(1, nextColumns, vectors);
            sign = -sign;
        }

        return new Vector<T>(result);
    }

    /// <summary>
    /// Recursive computation of the cross product of n-1 vectors in an n-dimensional space.
    /// </summary>
    /// <param name="recurrence">The current recursion step.</param>
    /// <param name="columns">The columns to process for computation.</param>
    /// <param name="vectors">The vectors to compute the product.</param>
    /// <returns>The computed product.</returns>
    private static T ComputeProduct(int recurrence, IEnumerable<int> columns, Vector<T>[] vectors)
    {
        T result = T.Zero;
        T sign = T.One;
        foreach (int column in columns)
        {
            T temp = sign;
            if (recurrence > 0)
            {
                temp *= vectors[recurrence - 1].components[column];

                var nextColumns = columns.Where(c => c != column);
                if (temp != T.Zero && nextColumns.Any())
                {
                    temp *= ComputeProduct(recurrence + 1, nextColumns, vectors);
                }
            }
            result += temp;
            sign = -sign;
        }
        return result;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var temp = components.Length.GetHashCode();
            foreach (var el in components)
            {
                temp = ((temp * 23) << 1) + el.GetHashCode();
            }
            return temp;
        }
    }

    public object Clone() => new Vector<T>(this);
}

