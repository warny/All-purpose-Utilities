using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Mathematics.Expressions.Compiler;

namespace UtilsTest.Math.Expressions.Compiler
{
	[TestClass]
	public class Constants
	{
		[TestMethod]
		public void ConstantString()
		{
			Constant constant = new Constant {
				TypeName = "System.String",
				Value = "test"
			};

			var constantExpression = constant.CreateExpression(
				new ParameterExpression[0],
				new Utils.Lists.IndexedList<string, LabelTarget>(l => l.Name),
				out var declaredVariables
			).ToExpression();

			Assert.IsInstanceOfType(constantExpression, typeof(ConstantExpression));
			Assert.IsTrue(constantExpression.Type.IsAssignableFrom(typeof(string)), $"Le type doit être {nameof(String)} au lieu de {constantExpression.Type.FullName}");
			Assert.AreEqual((constantExpression as ConstantExpression).Value, constant.Value);
			Assert.IsTrue(declaredVariables.IsNullOrEmpty(), "Aucune variable ne devrait être déclarée");
		}

		[TestMethod]
		public void ConstantInt32()
		{
			Constant constant = new Constant {
				TypeName = "System.Int32",
				Value = "10"
			};

			var constantExpression = constant.CreateExpression(
				new ParameterExpression[0],
				new Utils.Lists.IndexedList<string, LabelTarget>(l => l.Name),
				out var declaredVariables
			).ToExpression();

			Assert.IsInstanceOfType(constantExpression, typeof(ConstantExpression));
			Assert.IsTrue(constantExpression.Type.IsAssignableFrom(typeof(int)), $"Le type doit être {nameof(Int32)} au lieu de {constantExpression.Type.FullName}");
			Assert.AreEqual((constantExpression as ConstantExpression).Value, int.Parse(constant.Value));
			Assert.IsTrue(declaredVariables.IsNullOrEmpty(), "Aucune variable ne devrait être déclarée");
		}

		[TestMethod]
		public void ConstantDouble()
		{
			Constant constant = new Constant {
				TypeName = "System.Double",
				Value = "10"
			};

			var constantExpression = constant.CreateExpression(
				new ParameterExpression[0],
				new Utils.Lists.IndexedList<string, LabelTarget>(l => l.Name),
				out var declaredVariables
			).ToExpression();

			Assert.IsInstanceOfType(constantExpression, typeof(ConstantExpression));
			Assert.IsTrue(constantExpression.Type.IsAssignableFrom(typeof(double)), $"Le type doit être {nameof(Double)} au lieu de {constantExpression.Type.FullName}");
			Assert.AreEqual((constantExpression as ConstantExpression).Value, double.Parse(constant.Value));
			Assert.IsTrue(declaredVariables.IsNullOrEmpty(), "Aucune variable ne devrait être déclarée");
		}

	}
}
