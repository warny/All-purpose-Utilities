using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Math.LinearAlgebra
{

	[TestClass]
	public class MatrixTests
	{

		[TestMethod]
		public void MultiplyMatrixTest2()
		{
			Random r = new Random();
			var m1 = new Matrix(new double[,] {
				{ 1, 2 },
				{ 3, 4 },
			});

			var v2 = new Vector( 5, 7 );

			var result1 = new Vector( 19, 43 );

			var mResult1 = m1 * v2;

			Assert.AreEqual(result1, mResult1);

		}

		[TestMethod]
		public void MultiplyMatrixTest3()
		{
			Random r = new Random();
			double[,] m1values = new double[,] {
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) },
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) },
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) }
				};

			double[,] m2values = new double[,] {
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) },
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) },
					{ r.Next (0,100), r.Next (0,100), r.Next (0,100) }
				};

			var m1 = new Matrix(m1values);
			var m2 = new Matrix(m2values);

			var vectors = m2.ToVectors();

			var mResult = m1 * m2;
			var vResult = vectors.Select(v => m1 * v).ToArray();

			Assert.IsTrue(mResult == vResult);

		}

		/*
		[TestMethod]
		public void TransformMatrixTests()
		{
			System.Drawing.Drawing2D.Matrix Create2D(params Action<System.Drawing.Drawing2D.Matrix>[] actions)
			{
				var result = new System.Drawing.Drawing2D.Matrix(1, 0, 0, 1, 0, 0);
				foreach (var action in actions)
				{
					var tr = new System.Drawing.Drawing2D.Matrix(1, 0, 0, 1, 0, 0);
					action(tr);
					result.Multiply(tr);
				}
				return result;
			}

			var tests = new (string name, System.Drawing.Drawing2D.Matrix D2, Utils.Mathematics.LinearAlgebra.Matrix LA)[] {
				("identitity", Create2D(), Matrix.Transform(1, 0, 0, 1, 0, 0)),
				("translation", Create2D(m=>m.Translate(1,2)), Matrix.Translation(1,2)),
				("rotation", Create2D(m=>m.Rotate(-60)), Matrix.Rotation(System.Math.PI / 3)),
				("homothety", Create2D(m=>m.Scale(1,2)), Matrix.Scaling(1,2)),
				("translation,rotation", Create2D(m=>m.Translate(1,2), m=>m.Rotate(-60)), Matrix.Rotation(System.Math.PI / 3) * Matrix.Translation(1, 2)), //le comportement de Drawing2D.Matrix est très bizarre ici
			};

			int testNumber = 0;
			foreach (var test in tests)
			{
				var components = test.D2.Elements;

				int i = 0;
				for (int x = 0; x < 3; x++)
				{
					for (int y = 0; y < 2; y++)
					{
						Assert.AreEqual(components[i], test.LA[x, y], 0.001, $"La dimension[{x}, {y}] est différente sur le test #{testNumber} {test.name} sur \r\n[{string.Join(", ", test.D2.Elements.Select(x=>x.ToString("0.00")))}] et \r\n{test.LA}");
						i++;
					}
				}
				testNumber++;
			}

		}
		*/
	}
}
