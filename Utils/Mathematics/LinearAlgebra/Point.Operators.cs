using System;
using System.Collections.Generic;
using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Point :
	IAdditionOperators<Point, Vector, Point>,
	ISubtractionOperators<Point, Point, Vector>,
	IEqualityOperators<Point, Point, bool>
{

	public (double weight, Point point) ComputeBarycenter(params Point[] points) => ComputeBarycenter((IEnumerable<Point>)points);
	public (double weight, Point point) ComputeBarycenter(IEnumerable<Point> points) => ComputeBarycenter<Point>(wp => 1.0, point => point, points);
	public (double weight, Point point) ComputeBarycenter(params (double weight, Point point)[] weightedPoints) => ComputeBarycenter((IEnumerable<(double weight, Point point)>)weightedPoints);
	public (double weight, Point point) ComputeBarycenter(IEnumerable<(double weight, Point point)> weightedPoints) => ComputeBarycenter(wp => wp.weight, wp => wp.point, weightedPoints);
	public static (double weigth, Point point) ComputeBarycenter<T>(Func<T, double> getWeight, Func<T, Point> getPoint, params T[] weightedPoints) => ComputeBarycenter(getWeight, getPoint, (IEnumerable<T>)weightedPoints);
	public static (double weigth, Point point) ComputeBarycenter<T>(Func<T, double> getWeight, Func<T, Point> getPoint, IEnumerable<T> weightedPoints)
	{
		var enumerator = weightedPoints.GetEnumerator();

		double totalWeight = 0;

		var first = enumerator.Current;
		Point firstPoint = getPoint(first);
		int dimension = firstPoint.Dimension;
		double[] temp = new double[dimension];


		for (var weightedPoint = enumerator.Current; enumerator.MoveNext();)
		{
			double weight = getWeight(weightedPoint);
			totalWeight += weight;
			Point point = getPoint(weightedPoint);
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
		return (totalWeight, new Point(temp));

	}

	public static Vector operator -(Point point1, Point point2)
	{
		if (point1.Dimension != point2.Dimension)
		{
			throw new ArgumentOutOfRangeException(nameof(point2), "Les deux points n'ont pas la même dimension");
		}

		double[] result = new double[point1.Dimension];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = point1[i] - point2[i];
		}
		return new Vector(result);

	}

	public static Point operator +(Point point1, Vector vector2)
	{
		if (point1.Dimension != vector2.Dimension)
		{
			throw new ArgumentOutOfRangeException(nameof(vector2), "Le point et le vecteur n'ont pas la même dimension");
		}
		double[] result = new double[point1.Dimension + 1];
		for (int i = 0; i < result.Length - 1; i++)
		{
			result[i] = point1[i] - vector2[i];
		}
		result[^1] = 1;
		return new Point(result);
	}

	public static bool operator ==(Point point1, Point point2) => point1.Equals(point2);

	public static bool operator !=(Point point1, Point point2) => !point1.Equals(point2);

	public static explicit operator Point(Vector vector)
	{
		double[] result = new double[vector.Dimension + 1];
		result[^1] = 1;
		Array.Copy(vector.components, result, vector.Dimension);
		return new Point(result);
	}

}
