using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Vecteur
/// </summary>
public sealed partial class Vector : IEquatable<Vector>, IEquatable<double[]>
{
    /// <summary>
    /// composantes du vecteur
    /// </summary>
    internal readonly double[] components;

    /// <summary>
    /// Longueur du vecteur	(calculée à la demande)
    /// </summary>
    private double? length;

    /// <summary>
    /// constructeur par dimensions
    /// </summary>
    /// <param name="dimensions"></param>
    private Vector(int dimensions)
    {
        components = new double[dimensions];
        length = 0;
    }

    /// <summary>
    /// constructeur par valeurs
    /// </summary>
    /// <param name="components"></param>
    public Vector(params double[] components)
    {
        if (components.Length == 0) throw new ArgumentException("La dimension du vecteur ne peut pas être 0", nameof(components));
        this.components = new double[components.Length];
        Array.Copy(components, this.components, components.Length);
    }

    public Vector(Vector vector)
    {
        components = new double[vector.components.Length];
        Array.Copy(vector.components, components, vector.components.Length);
    }

    /// <summary>
    /// Retourne ou défini la valeur de la dimension indiquée
    /// </summary>
    /// <param name="dimension"></param>
    /// <returns></returns>
    public double this[int dimension] => this.components[dimension];

    /// <summary>
    /// dimension du vecteur
    /// </summary>
    public int Dimension => this.components.Length;

    /// <summary>
    /// Longueur du vecteur
    /// </summary>
    public double Length
    {
        get
        {
            if (length is not null) return length.Value;
            double temp = 0;
            for (int i = 0; i < this.components.Length; i++)
            {
                temp += Math.Pow(this.components[i], 2);
            }
            length = Math.Sqrt(temp);
            return length.Value;
        }
    }

    public Vector Normalize() => this / Length;

    public override bool Equals(object obj) => obj switch
    {
        Vector v => Equals(v),
        double[] a => Equals(a),
        _ => false,
    };

    public bool Equals(Vector other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(other.components);
    }

    public bool Equals(double[] other)
    {
        if (other is null) return false;
        return Arrays.ArrayEqualityComparers.Double.Equals(this.components, other);
    }

    public override string ToString()
        => $"({string.Join(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator, components)})";

    /// <summary>
    /// Converti un vecteur pour l'utiliser dans un espace normal
    /// </summary>
    /// <returns></returns>
    public Vector ToNormalSpace()
    {
        Vector result = new Vector(Dimension + 1);
        Array.Copy(components, result.components, Dimension);
        result.components[Dimension] = 1;
        return result;
    }

    /// <summary>
    /// Converti un vecteur utilisable dans un espace normal en vecteur utilisable en espace cartésien
    /// </summary>
    /// <returns></returns>
    public Vector FromNormalSpace()
    {
        var temp = this;
        if (temp[temp.Dimension - 1] != 1)
        {
            temp /= temp[temp.Dimension - 1];
        }
        Vector result = new Vector(Dimension - 1);
        Array.Copy(temp.components, result.components, Dimension - 1);
        return result;
    }

    /// <summary>
    /// renvoi le produit vectoriel de (n-1) vecteurs de dimension n
    /// </summary>
    /// <param name="vectors">vecteurs de dimension n</param>
    /// <returns>vecteur normal</returns>
    /// <exception cref="ArgumentException">Renvoie une exception si les vecteurs ne sont pas tous de dimension n</exception>
    public static Vector Product(params Vector[] vectors)
    {
        int dimensions = vectors.Length + 1;
        foreach (var vector in vectors)
        {
            if (vector.components.Length != dimensions)
            {
                throw new ArgumentException(string.Format("Tous les vecteurs ne sont pas de dimension {0}", dimensions), "vectors");
            }
        }

        double[] result = new double[dimensions];
        var columns = Enumerable.Range(0, dimensions);
        double sign = 1;
        foreach (var column in columns)
        {
            var nextColumns = columns.Where(c => c != column);
            result[column] = sign * ComputeProduct(1, nextColumns, vectors);
            sign = -sign;
        }

        return new Vector(result);
    }

    /// <summary>
    /// Calcul recursif du produit vectoriel de n-1 vecteurs dans un espace n
    /// </summary>
    /// <param name="recurence"></param>
    /// <param name="columns"></param>
    /// <param name="vectors"></param>
    /// <returns></returns>
    private static double ComputeProduct(int recurence, IEnumerable<int> columns, Vector[] vectors)
    {
        double result = 0;
        double sign = 1;
        foreach (int column in columns)
        {
            double temp = sign;
            if (recurence > 0)
            {
                temp *= vectors[recurence - 1].components[column];

                var nextColumns = columns.Where(c => c != column);
                if (temp != 0 && nextColumns.Any())
                {
                    temp *= ComputeProduct(recurence + 1, nextColumns, vectors);
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
}

