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
		public void LambdaTestAddition()
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

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<double, double, double> addition = (Func<double, double, double>)lambdaExpression.Compile();

			Random rnd = new Random();
			double x = rnd.NextDouble();
			double y = rnd.NextDouble();

			Assert.AreEqual(x + y, addition(x, y));
		}

		[TestMethod]
		public void LambdaTestStaticVariable()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Double"
			};
			ParameterExpression[] parameters = { };
			lambda.ExpressionTrees.Add(
				new Identifier {
					Name = "PI",
					Left = new Identifier {
						Name = "Math",
						Left = new Identifier { Name = "System" }
					}
				}
			);
			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<double> pi = (Func<double>)lambdaExpression.Compile();

			Assert.AreEqual(System.Math.PI, pi());
		}

		[TestMethod]
		public void LambdaTestStaticFunction()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Double"
			};
			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(double), "angle")
			};

			lambda.ExpressionTrees.Add(
				new FunctionCall {
					Name = "Cos",
					Left = new Identifier {
						Name = "Math",
						Left = new Identifier {
							Name = "System",
						}
					},
					Arguments = {
						new Identifier { Name = "angle" }
					}
				}
			);
			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<double, double> cos = (Func<double, double>)lambdaExpression.Compile();

			Random rnd = new Random();
			double angle = rnd.NextDouble();

			Assert.AreEqual(System.Math.Cos(angle), cos(angle));
		}				   
	}
}
