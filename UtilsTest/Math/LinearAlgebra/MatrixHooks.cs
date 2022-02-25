using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TechTalk.SpecFlow;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Math.LinearAlgebra
{
	[Binding]
	public sealed class MatrixHooks
	{
		readonly ScenarioContext context = ScenarioContext.Current;

		private static Matrix TransformToMatrix(Table values)
		{
			double[,] matrix = new double[values.RowCount, values.Header.Count];
			int i, j;
			i = 0;
			foreach (var row in values.Rows)
			{
				j = 0;
				foreach (var col in row)
				{
					matrix[i, j] = double.Parse(col.Value);
					j++;
				}
				i++;
			}

			return new Matrix(matrix);
		}

		public R Compute<R>(string resultName, Func<R> compute)
		{
			R result;
			try
			{
				result = compute();
			}
			catch (Exception ex)
			{
				context["Exception"] = ex;
				return default(R);
			}
			context[resultName] = result;
			return result;
		}


		[Given(@"(\w+) is a matrix")]
		public void IsAThisMatrix(string name, Table values)
		{
			context.Add(name, TransformToMatrix(values));
		}

		[When(@"I compute (\w+) \= (\w+) \+ (\w+)")]
		public void IComputeMatrixAddition (string resName, string m1Name, string m2Name)
		{
			Matrix m1 = (Matrix)context[m1Name];
			Matrix m2 = (Matrix)context[m2Name];

			Compute(resName, () => m1 + m2);
		}

		[When(@"I compute (\w+) \= (\d+) \* (\w+)")]
		public void IComputeMatrixMultiplication(string resName, double multiplicator, string name)
		{
			Matrix m = (Matrix)context[name];
			Compute(resName, () => multiplicator * m);
		}

		[When(@"I compute (\w+) \= (\w+) \* (\w+)")]
		public void IComputeMatrixMultiplication(string resName, string m1Name, string m2Name)
		{
			Matrix m1 = (Matrix)context[m1Name];
			Matrix m2 = (Matrix)context[m2Name];

			Compute(resName, () => m1 * m2);
		}

		[Then(@"I expect matrix (\w+) equals")]
		public void IExpectMatrixEquals(string name, Table values)
		{
			Matrix m = (Matrix)context[name];
			Matrix expected = TransformToMatrix(values);
			Assert.AreEqual(expected, m);
		}

		[Then(@"I expect (\w+) equals (\w+)")]
		public void IExpectEquality(string m1Name, string m2Name)
		{
			object o1 = context[m1Name];
			object o2 = context[m2Name];

			Assert.AreEqual(o1, o2);
		}

		[Then(@"det\((\w+)\) = (\d+)")]
		public void MatrixDeterminantIsEqualTo(string name, double value)
		{
			Matrix m = (Matrix)context[name];
			Assert.AreEqual(value, m.Determinant);

		}
	}
}