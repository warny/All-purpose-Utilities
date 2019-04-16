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
	public class LoopTests
	{
		[TestMethod]
		public void ForTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Int32"				
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(System.Int32), "x")
			};

			var resultVariable = new DeclareAndAssign() {
				TypeName = "var",
				VariableName = "result",
				Right = new Constant() {
					TypeName = "System.Int32",
					Value = "0"
				}
			};

			var @for = new For() {
				Initializer = new DeclareAndAssign() {
					TypeName = "var",
					VariableName = "i",
					Right = new Constant() {
						TypeName = "System.Int32",
						Value = "0"
					}
				},
				Test = new BinaryOperator() {
					Left = new Identifier { Name = "i" },
					Operator = Expression.LessThan,
					Right = new Identifier { Name = "x" }
				},
				Stepper = new UnaryOperator() {
					Expression = new Identifier { Name = "i" },
					Operator = Expression.PostIncrementAssign
				},
				Body = new BinaryOperator() {
					Left = new Identifier { Name = "result" },
					Operator = Expression.AddAssign,
					Right = new Identifier { Name = "i" }
				}
			};

			lambda.ExpressionTrees.Add(resultVariable);
			lambda.ExpressionTrees.Add(@for);
			lambda.ExpressionTrees.Add(new ReturnValue() {
				Expression = new Identifier { Name = "result" }
			});

			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<int, int> add = (Func<int, int>)lambdaExpression.Compile();

			var rnd = new Random();
			var x = rnd.Next(10, 20);
			Assert.AreEqual(x * (x - 1) / 2, add(x));
		}

		[TestMethod]
		public void ForEachTest()
		{
			Lambda lambda = new Lambda() {
				ReturnType = "System.Int32"
			};

			ParameterExpression[] parameters = {
				Expression.Parameter (typeof(IEnumerable<int>), "enum")
			};

			var resultVariable = new DeclareAndAssign() {
				TypeName = "var",
				VariableName = "result",
				Right = new Constant() {
					TypeName = "System.Int32",
					Value = "0"
				}
			};

			var forEach = new ForEach() {
				TypeName = "System.Int32",
				VariableName = "s",
				EnumerableVariable = new Identifier { Name = "enum" },
				Body = new BinaryOperator {
					Left = new Identifier { Name = "result" },
					Operator = Expression.Assign,
					Right = new BinaryOperator {
						Left = new Identifier { Name = "result" },
						Operator = Expression.Add,
						Right = new Identifier { Name = "s" }
					}
				}
			};

			lambda.ExpressionTrees.Add(resultVariable);
			lambda.ExpressionTrees.Add(forEach);
			lambda.ExpressionTrees.Add(new ReturnValue() {
				Expression = new Identifier { Name = "result" }
			});


			var lambdaExpression = (LambdaExpression)lambda.CreateLambda(parameters);
			Func<int[], int> add = (Func<int[], int>)lambdaExpression.Compile();

			var strings = new []{ 1, 2, 3 };

			Assert.AreEqual(6, add(strings));

		}

	}
}
