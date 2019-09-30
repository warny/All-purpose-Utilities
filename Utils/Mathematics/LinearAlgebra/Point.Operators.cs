using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra
{
    public partial class Point
    {

		public static Vector operator - ( Point point1, Point point2 )
		{
			if (point1.Dimension != point2.Dimension) {
				throw new ArgumentOutOfRangeException("Les deux points n'ont pas la même dimension");
			}

			double[] result = new double[point1.Dimension];
			for (int i = 0; i < result.Length; i++) {
				result[i] = point1[i] - point2[i];
			}
			return new Vector(result);

		}

		public static Point operator + ( Point point1, Vector vector2 )
		{
			if (point1.Dimension != vector2.Dimension) {
				throw new ArgumentOutOfRangeException("Le point et le vecteur n'ont pas la même dimension");
			}
			double[] result = new double[point1.Dimension + 1];
			for (int i = 0; i < result.Length - 1; i++) {
				result[i] = point1[i] - vector2[i];
			}
			result[result.Length - 1] = 1;
			return new Point() { components = result };
		}

		public static bool operator == ( Point point1, Point point2 )
		{
			return point1.Equals(point2);
		}

		public static bool operator != ( Point point1, Point point2 )
		{
			return !point1.Equals(point2);
		}

		public static Point operator * ( Matrix matrix, Point point )
		{
			if (!matrix.IsSquare) {
				throw new ArgumentException("La matrice doit être carrée");
			} else if (!(matrix.Rows == point.Dimension || (matrix.Rows - 1 == point.Dimension && matrix.IsNormalSpace))) {
				throw new ArgumentException("Les dimensions de la matrice et du point ne sont pas compatibles avec cette opération");
			}
			double[] result = new double[point.Dimension + 1];
			result[result.Length - 1] = 1;
			for (int row = 0; row < matrix.Rows; row++) {
				double temp = 0;
				for (int col = 0; col < matrix.Columns; col++) {
					temp += matrix[row, col] * point.components[col];
				}
				result[row] = temp;
			}
			if (result[result.Length - 1] != 1) {
				throw new Exception("Erreur lors de l'application de la matrice de transformation");
			}
			return new Point() { components = result };
		}

		public static explicit operator Point (Vector vector) {
			double[] result = new double[vector.Dimension + 1];
			result[result.Length - 1] = 1;
			Array.Copy(vector.components, result, vector.Dimension);
			return new Point() { components = result };
		}								 

    }
}
