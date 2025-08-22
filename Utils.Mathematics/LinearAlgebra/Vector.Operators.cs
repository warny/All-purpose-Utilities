using System.Collections.Generic;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Provides operator overloads and barycenter computations for <see cref="Vector{T}"/>.
/// </summary>
public partial class Vector<T> :
    IAdditionOperators<Vector<T>, Vector<T>, Vector<T>>,
    ISubtractionOperators<Vector<T>, Vector<T>, Vector<T>>,
    IMultiplyOperators<Vector<T>, T, Vector<T>>,
    IDivisionOperators<Vector<T>, T, Vector<T>>,
    IMultiplyOperators<Vector<T>, Vector<T>, T>,
    IEqualityOperators<Vector<T>, Vector<T>, bool>,
    IUnaryNegationOperators<Vector<T>, Vector<T>>,
    IUnaryPlusOperators<Vector<T>, Vector<T>>
{
    /// <summary>
    /// Computes the barycenter of the provided points with equal weights.
    /// </summary>
    /// <param name="points">Points to compute the barycenter of.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public (T weight, Vector<T> vector) ComputeBarycenter(params Vector<T>[] points)
        => ComputeBarycenter((IEnumerable<Vector<T>>)points);

    /// <summary>
    /// Computes the barycenter of the provided points with equal weights.
    /// </summary>
    /// <param name="vectors">Points to compute the barycenter of.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public (T weight, Vector<T> vector) ComputeBarycenter(IEnumerable<Vector<T>> vectors)
        => ComputeBarycenter(wp => T.One, vector => vector, vectors);

    /// <summary>
    /// Computes the barycenter of the provided weighted points.
    /// </summary>
    /// <param name="weightedPoints">Weighted points.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public (T weight, Vector<T> vector) ComputeBarycenter(params (T weight, Vector<T> point)[] weightedPoints)
        => ComputeBarycenter((IEnumerable<(T weight, Vector<T> vector)>)weightedPoints);

    /// <summary>
    /// Computes the barycenter of the provided weighted points.
    /// </summary>
    /// <param name="weightedPoints">Weighted points.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public (T weight, Vector<T> vector) ComputeBarycenter(IEnumerable<(T weight, Vector<T> vector)> weightedPoints)
        => ComputeBarycenter(wp => wp.weight, wp => wp.vector, weightedPoints);

    /// <summary>
    /// Computes the barycenter of the provided weighted points using selector functions.
    /// </summary>
    /// <typeparam name="TW">Type of the weighted point.</typeparam>
    /// <param name="getWeight">Function to extract the weight from the point.</param>
    /// <param name="getVector">Function to extract the vector from the point.</param>
    /// <param name="weightedPoints">Collection of weighted points.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public static (T weigth, Vector<T> point) ComputeBarycenter<TW>(Func<TW, T> getWeight, Func<TW, Vector<T>> getVector, params TW[] weightedPoints)
        => ComputeBarycenter(getWeight, getVector, (IEnumerable<TW>)weightedPoints);

    /// <summary>
    /// Computes the barycenter of the provided weighted points using selector functions.
    /// </summary>
    /// <typeparam name="TW">Type of the weighted point.</typeparam>
    /// <param name="getWeight">Function to extract the weight from the point.</param>
    /// <param name="getVector">Function to extract the vector from the point.</param>
    /// <param name="weightedPoints">Collection of weighted points.</param>
    /// <returns>The total weight and the barycenter vector.</returns>
    public static (T weigth, Vector<T> point) ComputeBarycenter<TW>(Func<TW, T> getWeight, Func<TW, Vector<T>> getVector, IEnumerable<TW> weightedPoints)
    {
        using var enumerator = weightedPoints.GetEnumerator();
        if (!enumerator.MoveNext()) throw new ArgumentException("At least one point is required", nameof(weightedPoints));

        TW first = enumerator.Current;
        Vector<T> firstPoint = getVector(first);
        int dimension = firstPoint.Dimension;
        T[] temp = new T[dimension];

        T weight = getWeight(first);
        T totalWeight = weight;
        for (int i = 0; i < dimension; i++)
        {
            temp[i] = firstPoint[i] * weight;
        }

        while (enumerator.MoveNext())
        {
            var weightedPoint = enumerator.Current;
            weight = getWeight(weightedPoint);
            totalWeight += weight;
            Vector<T> point = getVector(weightedPoint);
            if (dimension != point.Dimension) throw new InvalidOperationException("All points must have the same dimension.");
            for (int i = 0; i < dimension; i++)
            {
                temp[i] += point[i] * weight;
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            temp[i] /= totalWeight;
        }
        return (totalWeight, new Vector<T>(temp));

    }

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>The component-wise sum.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when vectors do not have the same dimension.</exception>
    public static Vector<T> operator +(Vector<T> vector1, Vector<T> vector2)
    {
        if (vector1.components.Length != vector2.components.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(vector2), "Both vectors must have the same number of dimensions.");
        }

        T[] result = new T[vector1.components.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = vector1.components[i] + vector2.components[i];
        }
        return new Vector<T>(result);
    }

    /// <summary>
    /// Subtracts one vector from another component-wise.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>The component-wise difference.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when vectors do not have the same dimension.</exception>
    public static Vector<T> operator -(Vector<T> vector1, Vector<T> vector2)
    {
        if (vector1.components.Length != vector2.components.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(vector2), "Both vectors must have the same number of dimensions.");
        }

        T[] result = new T[vector1.components.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = vector1.components[i] - vector2.components[i];
        }
        return new Vector<T>(result);
    }

    /// <summary>
    /// Negates all components of the vector.
    /// </summary>
    /// <param name="vector">Vector to negate.</param>
    /// <returns>The negated vector.</returns>
    public static Vector<T> operator -(Vector<T> vector)
    {
        T[] result = new T[vector.components.Length];
        for (int i = 0; i < vector.components.Length; i++)
        {
            result[i] = -vector.components[i];
        }
        return new Vector<T>(result);
    }

    /// <summary>
    /// Multiplies a vector by a scalar.
    /// </summary>
    /// <param name="number">Scalar value.</param>
    /// <param name="vector">Vector to multiply.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector<T> operator *(T number, Vector<T> vector)
    {
        T[] result = new T[vector.components.Length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = vector.components[i] * number;
        }
        return new Vector<T>(result);
    }

    /// <summary>
    /// Multiplies a vector by a scalar.
    /// </summary>
    /// <param name="vector">Vector to multiply.</param>
    /// <param name="number">Scalar value.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector<T> operator *(Vector<T> vector, T number)
    {
        return number * vector;
    }

    /// <summary>
    /// Divides a vector by a scalar.
    /// </summary>
    /// <param name="vector">Vector to divide.</param>
    /// <param name="number">Scalar value.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector<T> operator /(Vector<T> vector, T number)
    {
        T[] result = new T[vector.components.Length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = vector.components[i] / number;
        }
        return new Vector<T>(result);
    }

    /// <summary>
    /// Checks if two vectors are equal.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>True if vectors are equal; otherwise, false.</returns>
    public static bool operator ==(Vector<T>? vector1, Vector<T>? vector2)
        => (vector1 is not null && vector1.Equals(vector2)) || vector2 is null;

    /// <summary>
    /// Checks if two vectors are not equal.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>True if vectors are different; otherwise, false.</returns>
    public static bool operator !=(Vector<T>? vector1, Vector<T>? vector2)
        => !(vector1 == vector2);

    /// <summary>
    /// Calculates the dot product of two vectors.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>The dot product.</returns>
    /// <exception cref="ArgumentException">Thrown when vectors do not share the same dimension.</exception>
    public static T operator *(Vector<T> vector1, Vector<T> vector2)
    {
        if (vector1.Dimension != vector2.Dimension)
        {
            throw new ArgumentException("Vectors must have the same dimension.");
        }
        T result = T.Zero;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            result += vector1[i] * vector2[i];
        }
        return result;
    }

    /// <summary>
    /// Returns a copy of the vector (unary plus).
    /// </summary>
    /// <param name="value">Vector to copy.</param>
    /// <returns>A new vector equal to the input.</returns>
    public static Vector<T> operator +(Vector<T> value) => new(value);
}

