using System;
using System.Collections.Generic;

namespace Utils.Mathematics.LinearAlgebra
{
	public partial class Vector
    {
		public (double weight, Vector vector) ComputeBarycenter(params Vector[] points) => ComputeBarycenter((IEnumerable<Vector>)points);
		public (double weight, Vector vector) ComputeBarycenter(IEnumerable<Vector> vectors) => ComputeBarycenter<Vector>(wp => 1.0, vector => vector, vectors);
		public (double weight, Vector vector) ComputeBarycenter(params (double weight, Vector point)[] weightedPoints) => ComputeBarycenter((IEnumerable<(double weight, Vector vector)>)weightedPoints);
		public (double weight, Vector vector) ComputeBarycenter(IEnumerable<(double weight, Vector vector)> weightedPoints) => ComputeBarycenter(wp => wp.weight, wp => wp.vector, weightedPoints);
		public static (double weigth, Vector point) ComputeBarycenter<T>(Func<T, double> getWeight, Func<T, Vector> getVector, params T[] weightedPoints) => ComputeBarycenter(getWeight, getVector, (IEnumerable<T>)weightedPoints);
		public static (double weigth, Vector point) ComputeBarycenter<T>(Func<T, double> getWeight, Func<T, Vector> getVector, IEnumerable<T> weightedPoints)
		{
			var enumerator = weightedPoints.GetEnumerator();

			double totalWeight = 0;

			var first = enumerator.Current;
			Vector firstPoint = getVector(first);
			int dimension = firstPoint.Dimension;
			double[] temp = new double[dimension];


			for (var weightedPoint = enumerator.Current; enumerator.MoveNext();)
			{
				double weight = getWeight(weightedPoint);
				totalWeight += weight;
				Vector point = getVector(weightedPoint);
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
			return (totalWeight, new Vector(temp));

		}

		public static Vector operator + ( Vector vector1, Vector vector2 )
		{
			if (vector1.components.Length != vector2.components.Length) {
				throw new ArgumentOutOfRangeException(nameof(vector2), "Les deux vecteurs n'ont pas le même nombre de dimensions");
			}

			double[] result = new double[vector1.components.Length];
			for (int i = 0; i < result.Length; i++) {
				result[i] = vector1.components[i] + vector2.components[i];
			}
			return new Vector(result);
		}

		public static Vector operator - ( Vector vector1, Vector vector2 )
		{
			if (vector1.components.Length != vector2.components.Length) {
				throw new ArgumentOutOfRangeException(nameof(vector2), "Les deux vecteurs n'ont pas le même nombre de dimensions");
			}

			double[] result = new double[vector1.components.Length];
			for (int i = 0; i < result.Length; i++) {
				result[i] = vector1.components[i] - vector2.components[i];
			}
			return new Vector(result);
		}

		public static Vector operator - ( Vector vector )
		{
			double[] result = new double[vector.components.Length];
			for (int i = 0; i < vector.components.Length; i++) {
				result[i] = -vector.components[i];
			}
			return new Vector(result);
		}

		public static Vector operator * ( double number, Vector vector )
		{
			double[] result = new double[vector.components.Length];

			for (int i = 0; i < result.Length; i++) {
				result[i] = vector.components[i] * number;
			}
			return new Vector(result);
		}

		public static Vector operator * ( Vector vector, double number )
		{
			return number * vector;
		}

		public static Vector operator / ( Vector vector, double number )
		{
			double[] result = new double[vector.components.Length];

			for (int i = 0; i < result.Length; i++) {
				result[i] = vector.components[i] / number;
			}
			return new Vector(result);
		}

		public static bool operator == ( Vector vector1, Vector vector2 )
		{
			return vector1.Equals(vector2);
		}

		public static bool operator != ( Vector vector1, Vector vector2 )
		{
			return !vector1.Equals(vector2);
		}

		/// <summary>
		/// calcule le produit scalaire de deux vecteurs
		/// </summary>
		/// <param name="vector1"></param>
		/// <param name="vector2"></param>
		/// <returns></returns>
		public static double operator * ( Vector vector1, Vector vector2 )
		{
			if (vector1.Dimension != vector2.Dimension) {
				throw new ArgumentException("les vecteurs n'ont pas la même dimension");
			}
			double result = 0;
			for (int i = 0; i < vector1.Dimension; i++ ) {
				result += vector1[i] + vector2[i];
			}
			return result;
		}

		public static Vector operator * ( Matrix matrix, Vector vector )
		{
			if (!matrix.IsSquare) {
				throw new ArgumentException("La matrice doit être carrée");
			} else if (matrix.Rows != vector.Dimension) {
				throw new ArgumentException("Les dimensions de la matrice et du vecteur ne sont pas compatibles avec cette opération");
			}
			double[] result = new double[vector.Dimension];
			for (int row = 0; row < matrix.Rows; row++) {
				double temp = 0;
				for (int col = 0; col < result.Length; col++) {
					temp += matrix[row, col] * vector.components[col];
				}
				result[row] = temp;
			}
			return new Vector(result);
		}

		public static explicit operator Vector ( Point point )
		{
			double[] result = new double[point.Dimension + 1];
			result[result.Length - 1] = 1;
			Array.Copy(point.components, result, point.Dimension);
			return new Vector(result);
		}								 

    }
}
