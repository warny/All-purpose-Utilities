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
		public void InstanciationTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.DateTime"
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(int), "year"),
				Expression.Parameter (typeof(int), "month"),
				Expression.Parameter (typeof(int), "day")
			};

			Instanciation instanciation;
			lambda.ExpressionTrees.Add(
				instanciation = new Instanciation {
					TypeName = "System.DateTime"
				}
			);
			instanciation.Arguments.Add(new Identifier { Name = "year" });
			instanciation.Arguments.Add(new Identifier { Name = "month" });
			instanciation.Arguments.Add(new Identifier { Name = "day" });

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<int, int, int, DateTime> datetime = (Func<int, int, int, DateTime>)lambdaExpression.Compile();

			int year = 1978, month = 4, day = 17;

			Assert.AreEqual(new DateTime(year, month, day), datetime(year, month, day));
		}

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
