using System;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
	[TestClass]
	public class ExpressionDerivationTests
	{
		ExpressionDerivation derivation = new ExpressionDerivation("x");

		[TestMethod]
		public void SimpleExpressionTest1()
		{
			Expression<Func<double, double>> expression = x => x;
			Expression<Func<double, double>> derivateTarget = x => 1;
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}


		[TestMethod]
		public void SimpleExpressionTest2()
		{
			Expression<Func<double, double>> expression = x => x*x;
			Expression<Func<double, double>> derivateTarget = x => 2 * x;
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void SimpleExpressionTest3()
		{
			Expression<Func<double, double>> expression = x => x*x*x;
			Expression<Func<double, double>> derivateTarget = y => 3 * y * y;
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void SimpleExpressionTest4()
		{
			Expression<Func<double, double>> expression = x => System.Math.Cos(x);
			Expression<Func<double, double>> derivateTarget = x => -System.Math.Sin(x);
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void SimpleExpressionTest5()
		{
			Expression<Func<double, double>> expression = x => System.Math.Sin(x);
			Expression<Func<double, double>> derivateTarget = x => System.Math.Cos(x);
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void SimpleExpressionTest6()
		{
			Expression<Func<double, double>> expression = x => System.Math.Exp(x);
			Expression<Func<double, double>> derivateTarget = x => System.Math.Exp(x);
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void SimpleExpressionTest7()
		{
			Expression<Func<double, double>> expression = x => System.Math.Exp(x*x);
			Expression<Func<double, double>> derivateTarget = x => 2 * x * System.Math.Exp(x*x);
			var derivate = derivation.Derivate(expression);
			Assert.AreEqual(derivateTarget, derivate, ExpressionComparer.Default);
		}

		[TestMethod]
		public void ExpressionsTests()
		{
			var parameters = new ParameterExpression[] {
				Expression.Parameter(typeof(double), "x"),
			};

			var tests = new (string function, string derivative)[]
			{
				("1", "0"),
				("Exp(x)", "Exp(x)"),
				("x", "1"),
				("x^2", "2*x"),
				("x^3", "3*x^2"),
				("x^3 + x^2 + x+1 ", "3*x^2 + 2*x + 1"),
				("Cos(x)", "0-Sin(x)"),
				("Sin(x)", "Cos(x)"),
				("(Sin(x)) * (Cos(x))", "(Cos(x))^2-(Sin(x))^2"),
				("Exp(x^2)", "2*x*Exp(x^2)"),
			};

			MathExpressionParser parser = new MathExpressionParser();

			foreach (var test in tests)
			{
				var function = parser.ParseExpression(test.function, parameters);
				var derivative = parser.ParseExpression(test.derivative, parameters);

				var result = derivation.Derivate(function);

				Assert.AreEqual(derivative, result, ExpressionComparer.Default);
			}
		}

	}
}
