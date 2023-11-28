using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Point<T> :
	IAdditionOperators<Point<T>, Vector<T>, Point<T>>,
	ISubtractionOperators<Point<T>, Point<T>, Vector<T>>,
	IEqualityOperators<Point<T>, Point<T>, bool>
	where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
{
	
	public static (T weight, Point<T> point) ComputeBarycenter(params Point<T>[] points)
        => ComputeBarycenter((IEnumerable<Point<T>>)points);
	public static (T weight, Point<T> point) ComputeBarycenter<T>(IEnumerable<Point<T>> points)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
        => ComputeBarycenter<T, Point<T>>(wp => T.One, point => point, points);
    public (T weight, Point<T> vector) ComputeBarycenter(params (T weight, Point<T> point)[] weightedPoints) 
		=> ComputeBarycenter((IEnumerable<(T weight, Point<T> vector)>)weightedPoints);
    public static (T weight, Point<T> point) ComputeBarycenter(IEnumerable<(T weight, Point<T> point)> weightedPoints)
        => ComputeBarycenter(wp => wp.weight, wp => wp.point, weightedPoints);
    public static (T weigth, Point<T> point) ComputeBarycenter<T, TW>(Func<TW, T> getWeight, Func<TW, Point<T>> getVector, params TW[] weightedPoints)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
        => ComputeBarycenter(getWeight, getVector, (IEnumerable<TW>)weightedPoints);
    public static (T weigth, Point<T> point) ComputeBarycenter<T, TW>(Func<TW, T> getWeight, Func<TW, Point<T>> getVector, IEnumerable<TW> weightedPoints)
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, IRootFunctions<T>
    {
        var enumerator = weightedPoints.GetEnumerator();

		T totalWeight = T.Zero;

		var first = enumerator.Current;
		Point<T> firstPoint = getPoint(first);
		int dimension = firstPoint.Dimension;
		T[] temp = new T[dimension];


		for (var weightedPoint = enumerator.Current; enumerator.MoveNext();)
		{
			T weight = getWeight(weightedPoint);
			totalWeight += weight;
			Point<T> point = getPoint(weightedPoint);
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
		return (totalWeight, new Point<T>(temp));

	}
	public static Vector<T> operator -(Point<T> point1, Point<T> point2)
	{
		if (point1.Dimension != point2.Dimension)
		{
			throw new ArgumentOutOfRangeException(nameof(point2), "Les deux points n'ont pas la même dimension");
		}

		T[] result = new T[point1.Dimension];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = point1[i] - point2[i];
		}
		return new Vector<T>(result);

	}

	public static Point<T> operator +(Point<T> point1, Vector<T> vector2)
	{
		if (point1.Dimension != vector2.Dimension)
		{
			throw new ArgumentOutOfRangeException(nameof(vector2), "Le point et le vecteur n'ont pas la même dimension");
		}
		T[] result = new T[point1.Dimension + 1];
		for (int i = 0; i < result.Length - 1; i++)
		{
			result[i] = point1[i] - vector2[i];
		}
		result[^1] = T.One;
		return new Point<T>(result);
	}

	public static bool operator ==(Point<T> point1, Point<T> point2) => point1.Equals(point2);

	public static bool operator !=(Point<T> point1, Point<T> point2) => !point1.Equals(point2);

	public static explicit operator Point<T>(Vector<T> vector)
	{
		T[] result = new T[vector.Dimension + 1];
		result[^1] = T.One;
		Array.Copy(vector.components, result, vector.Dimension);
		return new Point<T>(result);
	}

}
