using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Math.Expressions
{
	[TestClass]
	public class ExpressionDerivationTest
	{
		ExpressionDerivation derivation = new ExpressionDerivation("x");

		[TestMethod]
		public void SimpleExpressionTest1()
		{
			Expression<Func<double, double>> expression = x => x;
			Expression<Func<double, double>> derivateTarget = x => 1;
			var derivate = derivation.Derivate(expression);
			Assert.IsTrue(ExpressionUtils.Equals(derivateTarget, derivate));
		}


		[TestMethod]
		public void SimpleExpressionTest2()
		{
			Expression<Func<double, double>> expression = x => x*x;
			Expression<Func<double, double>> derivateTarget = x => 2 * x;
			var derivate = derivation.Derivate(expression);
			Assert.IsTrue(ExpressionUtils.Equals(derivateTarget, derivate));
		}

		[TestMethod]
		public void SimpleExpressionTest3()
		{
			Expression<Func<double, double>> expression = x => x*x*x;
			Expression<Func<double, double>> derivateTarget = y => 3 * y * y;
			var derivate = derivation.Derivate(expression);
			Assert.IsTrue(ExpressionUtils.Equals(derivateTarget, derivate));
		}
	}
}
