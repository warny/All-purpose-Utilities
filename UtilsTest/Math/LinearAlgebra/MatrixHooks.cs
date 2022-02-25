using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
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

		private static Dictionary<string, Vector> TransformToVectors(Table values)
		{
			Dictionary<string, double[]> vectors = new (values.Header.Count);

			foreach (var header in values.Header)
			{
				var vector = new double[values.RowCount];
				vectors.Add(header, vector);


				int i = 0;
				foreach (var row in values.Rows)
				{
					vector[i] = double.Parse(row[header]);
					i++;
				}

			}


			return vectors.ToDictionary(v=> v.Key, v => new Vector(v.Value));
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

		[Given(@"Given these vectors")]
		public void GivenTheseVectors(Table values)
		{
			foreach (var item in TransformToVectors(values))
			{
				context.Add(item.Key, item.Value);
			}
		}

		[When(@"I compute matrix (\w+) \= (\w+) \+ (\w+)")]
		public void IComputeMatrixAddition (string resName, string m1Name, string m2Name)
		{
			var m1 = (Matrix)context[m1Name];
			var m2 = (Matrix)context[m2Name];

			Compute(resName, () => m1 + m2);
		}

		[When(@"I compute matrix (\w+) \= (\d+) \* (\w+)")]
		public void IComputeMatrixMultiplication(string resName, double multiplicator, string name)
		{
			var m = (Matrix)context[name];
			Compute(resName, () => multiplicator * m);
		}

		[When(@"I compute matrix (\w+) \= (\w+) \* (\w+)")]
		public void IComputeMatrixMultiplication(string resName, string m1Name, string m2Name)
		{
			var m1 = (Matrix)context[m1Name];
			var m2 = (Matrix)context[m2Name];

			Compute(resName, () => m1 * m2);
		}

		[When(@"I compute vector (\w+) \= (\w+) \+ (\w+)")]
		public void IComputeVectorAddition(string resName, string v1Name, string v2Name)
		{
			var m1 = (Vector)context[v1Name];
			var m2 = (Vector)context[v2Name];

			Compute(resName, () => m1 + m2);
		}

		[When(@"I compute vector (\w+) \= (\d+) \* (\w+)")]
		public void IComputeVectorMultiplication(string resName, double multiplicator, string name)
		{
			var m = (Vector)context[name];
			Compute(resName, () => multiplicator * m);
		}

		[When(@"I compute vector (\w+) \= (\w+) \* (\w+)")]
		public void IComputeVectorMultiplication(string resName, string m1Name, string v2Name)
		{
			var m1 = (Matrix)context[m1Name];
			var m2 = (Vector)context[v2Name];

			Compute(resName, () => m1 * m2);
		}

		[Then(@"I expect matrix (\w+) equals")]
		public void IExpectMatrixEquals(string name, Table values)
		{
			Matrix m = (Matrix)context[name];
			Matrix expected = TransformToMatrix(values);
			Assert.AreEqual(expected, m);
		}

		[Then(@"I expect vector (\w+) equals")]
		public void IExpectVectorEquals(string name, Table values)
		{
			Vector v = (Vector)context[name];
			var expected = TransformToVectors(values);
			Assert.AreEqual(expected.First(), v);
		}

		[Then(@"I expect (\w+) \= (\w+)")]
		public void IExpectEquality(string m1Name, string m2Name)
		{
			object o1 = context[m1Name];
			object o2 = context[m2Name];

			Assert.AreEqual(o1, o2);
		}

		[Then(@"det\((\w+)\) = (\-?\d+)")]
		public void MatrixDeterminantIsEqualTo(string name, double value)
		{
			Matrix m = (Matrix)context[name];
			Assert.AreEqual(value, m.Determinant);
		}
	}
}