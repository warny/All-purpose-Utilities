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
	public class ObjectTest
	{
		[TestMethod]
		public void PropertyCallTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Int32"
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(string), "str")
			};

			lambda.ExpressionTrees.Add(
				new Identifier {
					Name = "Length",
					Left = new Identifier { Name="str" }
				}
			);

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<string, int> length = (Func<string, int>)lambdaExpression.Compile();

			string str = "ceci est une chaine";
			Assert.AreEqual(str.Length, length(str));
		}

		[TestMethod]
		public void FunctionCallTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.String"
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(DateTime), "dt")
			};

			lambda.ExpressionTrees.Add(
				new FunctionCall {
					Name = "ToString",
					Left = new Identifier { Name = "dt" }
				}
			);

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<DateTime, string> toString = (Func<DateTime, string>)lambdaExpression.Compile();

			DateTime dt = DateTime.Now;
			Assert.AreEqual(dt.ToString(), toString(dt));
		}

		[TestMethod]
		public void FunctionWithArgumentCallTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.String"
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(DateTime), "dt")
			};

			FunctionCall functionCall;
			lambda.ExpressionTrees.Add(
				functionCall = new FunctionCall {
					Name = "ToString",
					Left = new Identifier { Name = "dt" }
				}
			);
			functionCall.Arguments.Add(
				new Constant { TypeName = "System.String", Value = "d" }
			);

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<DateTime, string> toString = (Func<DateTime, string>)lambdaExpression.Compile();

			DateTime dt = DateTime.Now;
			Assert.AreEqual(dt.ToString("d"), toString(dt));
		}

	}
}
