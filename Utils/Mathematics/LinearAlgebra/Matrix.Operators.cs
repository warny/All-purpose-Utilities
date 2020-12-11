using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra
{
	public partial class Matrix
	{
		public static Matrix operator + (Matrix matrix1, Matrix matrix2) 
		{
			if (matrix1.components.GetLength(0) != matrix2.components.GetLength(0) || matrix1.components.GetLength(1) != matrix2.components.GetLength(1)) {
				throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
			}
			var result = new double[matrix1.components.GetLength(0), matrix1.components.GetLength(1)];
			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					result[i, j] = matrix1.components[i, j] + matrix2.components[i, j];
				}
			}
			return new Matrix(result);
		}

		public static Matrix operator - ( Matrix matrix1, Matrix matrix2 )
		{
			if (matrix1.components.GetLength(0) != matrix2.components.GetLength(0) || matrix1.components.GetLength(1) != matrix2.components.GetLength(1)) {
				throw new ArgumentException("Les matrices n'ont pas les mêmes dimensions");
			}
			var result = new double[matrix1.components.GetLength(0), matrix1.components.GetLength(1)];
			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					result[i, j] = matrix1.components[i, j] - matrix2.components[i, j];
				}
			}
			return new Matrix(result);
		}

		public static Matrix operator - ( Matrix matrix )
		{
			var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];
			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					result[i, j] = -matrix.components[i, j];
				}
			}
			return new Matrix(result);
		}

		public static Matrix operator * ( double number, Matrix matrix )
		{
			var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];

			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					result[i, j] = Math.Round(number * matrix.components[i, j], 15);
				}
			}
			return new Matrix(result);
		}

		public static Matrix operator *(Matrix matrix, double number) => number * matrix;

		public static Matrix operator / ( Matrix matrix, double number )
		{
			var result = new double[matrix.components.GetLength(0), matrix.components.GetLength(1)];

			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					result[i, j] = matrix.components[i, j] / number;
				}
			}
			return new Matrix(result);
		}

		public static Matrix operator * ( Matrix matrix1, Matrix matrix2 )
		{
			if (matrix1.components.GetLength(0) != matrix2.components.GetLength(1)) {
				throw new ArgumentException("Les matrices n'ont pas de dimensions compatibles");
			}
			var depth = matrix1.components.GetLength(0);
			var result = new double[matrix1.components.GetLength(1), matrix2.components.GetLength(0)];

			for (int i = 0; i < result.GetLength(0); i++) {
				for (int j = 0; j < result.GetLength(1); j++) {
					double temp = 0;
					for (int k = 0; k < depth; k++) {
						temp += matrix1.components[i, k] * matrix2.components[k, j];
					}
					result[i, j] = temp;
				}
			}

			return new Matrix(result);
		}

		public static bool operator ==(Matrix matrix1, Matrix matrix2) => matrix1.Equals(matrix2);
		public static bool operator !=(Matrix matrix1, Matrix matrix2) => !matrix1.Equals(matrix2);
		public static bool operator ==(Matrix matrix1, object obj) => matrix1.Equals(obj);
		public static bool operator !=(Matrix matrix1, object obj) => !matrix1.Equals(obj);
	}
}
