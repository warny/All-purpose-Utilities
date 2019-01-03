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

	}
}
