using System;
using System.Collections.Generic;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

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
	public (T weight, Vector<T> vector) ComputeBarycenter(params Vector<T>[] points)
		=> ComputeBarycenter((IEnumerable<Vector<T>>)points);
	public (T weight, Vector<T> vector) ComputeBarycenter(IEnumerable<Vector<T>> vectors)
		=> ComputeBarycenter(wp => T.One, vector => vector, vectors);
	public (T weight, Vector<T> vector) ComputeBarycenter(params (T weight, Vector<T> point)[] weightedPoints)
		=> ComputeBarycenter((IEnumerable<(T weight, Vector<T> vector)>)weightedPoints);
	public (T weight, Vector<T> vector) ComputeBarycenter(IEnumerable<(T weight, Vector<T> vector)> weightedPoints)
		=> ComputeBarycenter(wp => wp.weight, wp => wp.vector, weightedPoints);
	public static (T weigth, Vector<T> point) ComputeBarycenter<TW>(Func<TW, T> getWeight, Func<TW, Vector<T>> getVector, params TW[] weightedPoints)
		=> ComputeBarycenter(getWeight, getVector, (IEnumerable<TW>)weightedPoints);
	public static (T weigth, Vector<T> point) ComputeBarycenter<TW>(Func<TW, T> getWeight, Func<TW, Vector<T>> getVector, IEnumerable<TW> weightedPoints)
	{
		var enumerator = weightedPoints.GetEnumerator();

		T totalWeight = T.Zero;

		var first = enumerator.Current;
		Vector<T> firstPoint = getVector(first);
		int dimension = firstPoint.Dimension;
		T[] temp = new T[dimension];


		for (var weightedPoint = enumerator.Current; enumerator.MoveNext();)
		{
			T weight = getWeight(weightedPoint);
			totalWeight += weight;
			Vector<T> point = getVector(weightedPoint);
			if (dimension != point.Dimension) throw new InvalidOperationException("Tous les points doivent avoir la même dimension");
			for (int i = 0; i < dimension; i++)
			{
				temp[i] += point[i];
			}
		}

		for (int i = 0; i < dimension; i++)
		{
			temp[i] /= totalWeight;
		}
		return (totalWeight, new Vector<T>(temp));

	}

	public static Vector<T> operator +(Vector<T> vector1, Vector<T> vector2)
	{
		if (vector1.components.Length != vector2.components.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(vector2), "Les deux vecteurs n'ont pas le même nombre de dimensions");
		}

		T[] result = new T[vector1.components.Length];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = vector1.components[i] + vector2.components[i];
		}
		return new Vector<T>(result);
	}

	public static Vector<T> operator -(Vector<T> vector1, Vector<T> vector2)
	{
		if (vector1.components.Length != vector2.components.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(vector2), "Les deux vecteurs n'ont pas le même nombre de dimensions");
		}

		T[] result = new T[vector1.components.Length];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = vector1.components[i] - vector2.components[i];
		}
		return new Vector<T>(result);
	}

	public static Vector<T> operator -(Vector<T> vector)
	{
		T[] result = new T[vector.components.Length];
		for (int i = 0; i < vector.components.Length; i++)
		{
			result[i] = -vector.components[i];
		}
		return new Vector<T>(result);
	}

	public static Vector<T> operator *(T number, Vector<T> vector)
	{
		T[] result = new T[vector.components.Length];

		for (int i = 0; i < result.Length; i++)
		{
			result[i] = vector.components[i] * number;
		}
		return new Vector<T>(result);
	}

	public static Vector<T> operator *(Vector<T> vector, T number)
	{
		return number * vector;
	}

	public static Vector<T> operator /(Vector<T> vector, T number)
	{
		T[] result = new T[vector.components.Length];

		for (int i = 0; i < result.Length; i++)
		{
			result[i] = vector.components[i] / number;
		}
		return new Vector<T>(result);
	}

        public static bool operator ==(Vector<T>? vector1, Vector<T>? vector2) 
		=> (vector1 is not null && vector1.Equals(vector2)) || vector2 is null;

        public static bool operator !=(Vector<T>? vector1, Vector<T>? vector2) 
		=> !(vector1 == vector2);

        /// <summary>
        /// calcule le produit scalaire de deux vecteurs
        /// </summary>
        /// <param name="vector1"></param>
        /// <param name="vector2"></param>
        /// <returns></returns>
        public static T operator *(Vector<T> vector1, Vector<T> vector2)
	{
		if (vector1.Dimension != vector2.Dimension)
		{
			throw new ArgumentException("les vecteurs n'ont pas la même dimension");
		}
		T result = T.Zero;
		for (int i = 0; i < vector1.Dimension; i++)
		{
			result += vector1[i] + vector2[i];
		}
		return result;
	}

	public static Vector<T> operator +(Vector<T> value) => new(value);
}
