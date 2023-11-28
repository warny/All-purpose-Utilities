using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.LinearAlgebra;

public partial class Matrix<T>
{
    private static void LinearTransformation(IEnumerable<Matrix<T>> matrices, int row1, T[] transformations)
	{
		if (transformations[row1] == T.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(row1), $"La transformation de la ligne {row1} ne peut pas annuler sa valeur");
		}
		foreach (Matrix<T> matrix in matrices)
		{
			int dimensionX = matrix.Rows;
			int dimensionY = matrix.Rows;
			for (int col = 0; col < dimensionX; col++)
			{
				T temp = T.Zero;
				for (int row = 0; row < dimensionY; row++)
				{
					temp += matrix[row, col] * transformations[row];
				}
				matrix.components[row1, col] = temp;
			}
			var isSquare = matrix.IsSquare;
			matrix.isTriangularised = isSquare ? (bool?)null : false;
			matrix.isIdentity = isSquare ? (bool?)null : false;
			matrix.isDiagonalized = isSquare ? (bool?)null : false;
		}
	}

	private static void PermuteTransformation(IEnumerable<Matrix<T>> matrices, int row1, int row2)
	{
		if (row1 == row2) return;
		foreach (Matrix<T> matrix in matrices)
		{
			int dimensionX = matrix.Rows;
			for (int col = 0; col < dimensionX; col++)
			{
				T temp = matrix[row1, col];
				matrix.components[row1, col] = matrix[row2, col];
				matrix.components[row2, col] = temp;
			}
			var isSquare = matrix.IsSquare;
			matrix.isTriangularised = isSquare ? (bool?)null : false;
			matrix.isIdentity = isSquare ? (bool?)null : false;
			matrix.isDiagonalized = isSquare ? (bool?)null : false;
		}
	}

	/// <summary>
	/// Calcule le déterminant partiel de la matrice
	/// </summary>
	/// <param name="recurrence"></param>
	/// <param name="columns"></param>
	/// <returns></returns>
	private T ComputeDeterminant(int recurrence, ComputeColumns<T> columns)
	{
		T sign = T.One;
		T result = T.Zero;
		foreach (int column in columns)
		{
			T temp = sign * this.components[recurrence, column];

			var nextColumns = columns.NextLevel(column);
			if (temp != T.Zero && nextColumns.Level > 0)
			{
				T subDeterminant;
				if (columns.Level <= 2 || columns.Level >= columns.ColumnsCount - 2)
				{
					subDeterminant = ComputeDeterminant(recurrence + 1, nextColumns);
				}
				else if (!nextColumns.TryGetCache(out subDeterminant))
				{
					subDeterminant = ComputeDeterminant(recurrence + 1, nextColumns);
					nextColumns.SetCache(subDeterminant);
				}
				temp *= subDeterminant;
			}
			result += temp;
			sign = -sign;
		}
		return result;
	}

	public Matrix<T>[] Diagonalize()
	{
		if (!this.IsSquare)
		{
			throw new Exception("la matrice n'est pas une matrice carrée");
		}
		if (this.IsDiagonalized)
		{
			return [new(this), Identity(this.Rows)];
		}

		Matrix<T> start = new Matrix<T>(this);
		Matrix<T> result = Identity(this.Rows);
		Matrix<T>[] matrices = [start, result];

		for (int column = 0; column < this.Rows; column++)
		{
			//vérifie qu'à la ligne correspondant à la colonne considérée, on ait une valeur non nulle, sinon, on inverse avec une ligne suivante qui soit dans ce cas
			T max = T.Zero;
			int maxLine = 0;
			for (int j = column; j < this.Rows; j++)
			{
				T value = T.Abs(this[j, column]);
				if (max < value)
				{
					max = value;
					maxLine = j;
				}
			}
			if (max == T.Zero)
			{
				throw new Exception("La matrice n'est pas diagonalisable");
			}
			PermuteTransformation(matrices, column, maxLine);

			//ensuite, on essaye d'annuler les colonnes différentes de la colonne considérée
			T[] transformations = Enumerable.Repeat(T.Zero, this.Rows).ToArray();
			for (int row = column + 1; row < this.Rows; row++)
			{
				transformations[row] = T.One;
				transformations[column] = -start[row, column] / start[column, column];
				LinearTransformation(matrices, row, transformations);
				transformations[row] = T.Zero;
			}
		}

		return matrices;
	}

	public Matrix<T> Invert()
	{
		if (!this.IsSquare)
		{
			throw new InvalidOperationException("la matrice n'est pas une matrice carrée");
		}
		if (this.IsIdentity)
		{
			return new Matrix<T>(this);
		}

		var matrices = this.Diagonalize();
		var start = matrices[0];
		var result = matrices[1];

		T[] transformations = Enumerable.Repeat(T.Zero, this.Rows).ToArray();
		for (int j = this.Rows - 1; j >= 0; j--)
		{
			transformations[j] = T.One / start[j, j];
			LinearTransformation(matrices, j, transformations);
			transformations[j] = T.One;
			for (int i = j + 1; i < this.Rows; i++)
			{
				transformations[i] = -start[j, i] / start[i, i];
			}
			LinearTransformation(matrices, j, transformations);

		}
		return result;
	}

	private class ComputeColumns<T> : IEnumerable, IEquatable<ComputeColumns<T>>
    where T : struct,
        IFloatingPoint<T>,
        IAdditionOperators<T, T, T>,
        ISubtractionOperators<T, T, T>,
        IMultiplyOperators<T, T, T>,
        IDivisionOperators<T, T, T>,
        IEquatable<T>,
        IEqualityOperators<T, T, bool>
    {
        public int ColumnsCount { get; }
		private readonly int[] columns;
		private Dictionary<ComputeColumns<T>, T> cache { get; }
		public int Level => columns.Length;

		private ComputeColumns(int[] columns, Dictionary<ComputeColumns<T>, T> cache, int columnsCount)
		{
			this.columns = columns;
			this.cache = cache;
			this.ColumnsCount = columnsCount;
		}

		public ComputeColumns(int columnsCount)
		{
			this.columns = Enumerable.Range(0, columnsCount).ToArray();
			this.cache = [];
			this.ColumnsCount = columnsCount;
		}

		public ComputeColumns<T> NextLevel(int columnToRemove)
		{
			return new ComputeColumns<T>(columns.Where(c => c != columnToRemove).ToArray(), this.cache, this.ColumnsCount);
		}

		public bool TryGetCache(out T value)
		{
			return cache.TryGetValue(this, out value);
		}

		public void SetCache(T value)
		{
			cache.Add(this, value);
		}

		IEnumerator IEnumerable.GetEnumerator() => columns.GetEnumerator();

		public override int GetHashCode() => ObjectUtils.ComputeHash(this.columns);
		public override bool Equals(object obj) => obj is ComputeColumns<T> other && Equals(other);
        public bool Equals(ComputeColumns<T> other) => ReferenceEquals(this, other) || Enumerable.SequenceEqual(columns, other.columns);
        public override string ToString() => string.Join(", ", columns);

    }
}
