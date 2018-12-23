using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions.Compiler;

namespace UtilsTest.Math.Expressions.Compiler
{
	[TestClass]
	public class LambdaTests
	{
		[TestMethod]
		public void LambdaTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Double"
			};
			ParameterExpression[] parameters = {
				Expression.Parameter(typeof(double), "x"),
				Expression.Parameter(typeof(double), "y")
			};
			lambda.ExpressionTrees.Add(new ComputeOperator {
				Operator = Expression.Add,
				Left = new Identifier { Name = "x" },
				Right = new Identifier { Name = "y" }
			});

			var lambdaExpression = (LambdaExpression) lambda.CreateLambda(parameters);
			Func<double, double, double> addition = (Func<double, double, double>)lambdaExpression.Compile();

			Random rnd = new Random();
			double x = rnd.NextDouble();
			double y = rnd.NextDouble();

			Assert.AreEqual(x + y, addition(x, y));
		}
	}
}
