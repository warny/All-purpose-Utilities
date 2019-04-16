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
	public class ExpressionTreeTests
	{
		[TestMethod]
		public void DeclareAndAssignString()
		{
			DeclareAndAssign declareAndAssign = new DeclareAndAssign {
				TypeName = "System.String",
				VariableName = "name",
				Right = new Constant {
					TypeName = "System.String",
					Value = "value"
				}
			};

			Context context = new Context();
			var declareExpression = declareAndAssign.CreateExpression(context);

			Assert.AreEqual(declareExpression.Length, 1);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.AreSame((declareExpression[0] as BinaryExpression).Left, context.Variables["name"]);
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[0].Type));
			Assert.IsInstanceOfType((declareExpression[0] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[0] as BinaryExpression).Right as ConstantExpression).Value, (declareAndAssign.Right as Constant).Value);
		}

		[TestMethod]
		public void DeclareAndAssignInt32()
		{
			DeclareAndAssign declareAndAssign = new DeclareAndAssign {
				TypeName = "System.Int32",
				VariableName = "name",
				Right = new Constant {
					TypeName = "System.Int32",
					Value = "10"
				}
			};

			Context context = new Context();
			var declareExpression = declareAndAssign.CreateExpression(context);

			Assert.AreEqual(declareExpression.Length, 1);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.AreSame((declareExpression[0] as BinaryExpression).Left, context.Variables["name"]);
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[0].Type));
			Assert.IsInstanceOfType((declareExpression[0] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[0] as BinaryExpression).Right as ConstantExpression).Value, int.Parse((declareAndAssign.Right as Constant).Value));
		}

		[TestMethod]
		public void DeclareAndAssignVarString()
		{
			DeclareAndAssign declareAndAssign = new DeclareAndAssign {
				TypeName = "var",
				VariableName = "name",
				Right = new Constant {
					TypeName = "System.String",
					Value = "value"
				}
			};

			Context context = new Context();
			var declareExpression = declareAndAssign.CreateExpression(context);

			Assert.AreEqual(declareExpression.Length, 1);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.AreSame((declareExpression[0] as BinaryExpression).Left, context.Variables["name"]);
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[0].Type));
			Assert.IsInstanceOfType((declareExpression[0] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[0] as BinaryExpression).Right as ConstantExpression).Value, (declareAndAssign.Right as Constant).Value);
		}

		[TestMethod]
		public void DeclareAndAssignVarInt32()
		{
			DeclareAndAssign declareAndAssign = new DeclareAndAssign {
				TypeName = "var",
				VariableName = "name",
				Right = new Constant {
					TypeName = "System.Int32",
					Value = "10"
				}
			};

			Context context = new Context();
			var declareExpression = declareAndAssign.CreateExpression(context);

			Assert.AreEqual(declareExpression.Length, 1);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.AreSame((declareExpression[0] as BinaryExpression).Left, context.Variables["name"]);
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[0].Type));
			Assert.IsInstanceOfType((declareExpression[0] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[0] as BinaryExpression).Right as ConstantExpression).Value, int.Parse((declareAndAssign.Right as Constant).Value));
		}
	}
}
