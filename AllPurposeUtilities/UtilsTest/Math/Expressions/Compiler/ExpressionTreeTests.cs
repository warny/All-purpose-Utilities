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
		public void DeclareString()
		{
			Declare declare = new Declare {
				TypeName = "System.String",
				VariableName = "name"
			};

			Context context = new Context();
			var declareExpression = declare.CreateExpression(context).ToExpression();

			Assert.IsInstanceOfType(declareExpression, typeof(ParameterExpression));
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression.Type));
			Assert.AreEqual(context.Variables.Count, 1);
			Assert.AreSame(declareExpression, context.Variables["name"]);
			Assert.AreEqual((declareExpression as ParameterExpression).Name, declare.VariableName);

		}

		[TestMethod]
		public void DeclareInt()
		{
			Declare declare = new Declare {
				TypeName = "System.Int32",
				VariableName = "name"
			};

			Context context = new Context();
			var declareExpression = declare.CreateExpression(context).ToExpression();

			Assert.IsInstanceOfType(declareExpression, typeof(ParameterExpression));
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression.Type));
			Assert.AreEqual(context.Variables.Count, 1);
			Assert.AreSame(declareExpression, context.Variables["name"]);
			Assert.AreEqual((declareExpression as ParameterExpression).Name, declare.VariableName);

		}

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

			Assert.AreEqual(declareExpression.Length, 2);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.IsInstanceOfType(declareExpression[0], typeof(ParameterExpression));
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[0].Type));
			Assert.AreSame(declareExpression[0], context.Variables["name"]);
			Assert.AreEqual((declareExpression[0] as ParameterExpression).Name, declareAndAssign.VariableName);

			Assert.AreSame((declareExpression[1] as BinaryExpression).Left, declareExpression[0]);
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[1].Type));
			Assert.IsInstanceOfType((declareExpression[1] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[1] as BinaryExpression).Right as ConstantExpression).Value, (declareAndAssign.Right as Constant).Value);
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

			Assert.AreEqual(declareExpression.Length, 2);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.IsInstanceOfType(declareExpression[0], typeof(ParameterExpression));
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[0].Type));
			Assert.AreSame(declareExpression[0], context.Variables["name"]);
			Assert.AreEqual((declareExpression[0] as ParameterExpression).Name, declareAndAssign.VariableName);

			Assert.AreSame((declareExpression[1] as BinaryExpression).Left, declareExpression[0]);
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[1].Type));
			Assert.IsInstanceOfType((declareExpression[1] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[1] as BinaryExpression).Right as ConstantExpression).Value, int.Parse((declareAndAssign.Right as Constant).Value));
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

			Assert.AreEqual(declareExpression.Length, 2);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.IsInstanceOfType(declareExpression[0], typeof(ParameterExpression));
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[0].Type));
			Assert.AreSame(declareExpression[0], context.Variables["name"]);
			Assert.AreEqual((declareExpression[0] as ParameterExpression).Name, declareAndAssign.VariableName);

			Assert.AreSame((declareExpression[1] as BinaryExpression).Left, declareExpression[0]);
			Assert.IsTrue(typeof(string).IsAssignableFrom(declareExpression[1].Type));
			Assert.IsInstanceOfType((declareExpression[1] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[1] as BinaryExpression).Right as ConstantExpression).Value, (declareAndAssign.Right as Constant).Value);
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

			Assert.AreEqual(declareExpression.Length, 2);
			Assert.AreEqual(context.Variables.Count, 1);

			Assert.IsInstanceOfType(declareExpression[0], typeof(ParameterExpression));
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[0].Type));
			Assert.AreSame(declareExpression[0], context.Variables["name"]);
			Assert.AreEqual((declareExpression[0] as ParameterExpression).Name, declareAndAssign.VariableName);

			Assert.AreSame((declareExpression[1] as BinaryExpression).Left, declareExpression[0]);
			Assert.IsTrue(typeof(int).IsAssignableFrom(declareExpression[1].Type));
			Assert.IsInstanceOfType((declareExpression[1] as BinaryExpression).Right, typeof(ConstantExpression));
			Assert.AreEqual(((declareExpression[1] as BinaryExpression).Right as ConstantExpression).Value, int.Parse((declareAndAssign.Right as Constant).Value));
		}
	}
}
