using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra
{
    public partial class Vector
    {
		public static Vector operator + ( Vector vector1, Vector vector2 )
		{
			if (vector1.components.Length != vector2.components.Length) {
				throw new ArgumentOutOfRangeException("Les deux vecteurs n'ont pas le même nombre de dimensions");
			}

			double[] result = new double[vector1.components.Length];
			for (int i = 0; i < result.Length; i++) {
				result[i] = vector1.components[i] + vector2.components[i];
			}
			return new Vector() { components = result };
		}

		public static Vector operator - ( Vector vector1, Vector vector2 )
		{
			if (vector1.components.Length != vector2.components.Length) {
				throw new ArgumentOutOfRangeException("Les deux vecteurs n'ont pas le même nombre de dimensions");
			}

			double[] result = new double[vector1.components.Length];
			for (int i = 0; i < result.Length; i++) {
				result[i] = vector1.components[i] - vector2.components[i];
			}
			return new Vector() { components = result };
		}

		public static Vector operator - ( Vector vector )
		{
			double[] result = new double[vector.components.Length];
			for (int i = 0; i < vector.components.Length; i++) {
				result[i] = -vector.components[i];
			}
			return new Vector() { components = result };
		}

		public static Vector operator * ( double number, Vector vector )
		{
			double[] result = new double[vector.components.Length];

			for (int i = 0; i < result.Length; i++) {
				result[i] = vector.components[i] * number;
			}
			return new Vector() { components = result };
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
			return new Vector() { components = result };
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
			return new Vector() { components = result };
		}

		public static explicit operator Vector ( Point point )
		{
			double[] result = new double[point.Dimension + 1];
			result[result.Length - 1] = 1;
			Array.Copy(point.components, result, point.Dimension);
			return new Vector() { components = result };
		}								 

    }
}
