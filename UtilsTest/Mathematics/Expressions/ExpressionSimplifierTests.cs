using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
    /// <summary>
    /// Protects simplifier contracts that depend on CLR operator types and cannot be expressed in C-syntax scenarios.
    /// </summary>
    [TestClass]
    public class ExpressionSimplifierTests
    {
        /// <summary>
        /// Helper type exposing a non-commutative custom addition operator for canonicalization tests.
        /// </summary>
        private sealed class NonCommutativeAdditive
        {
            /// <summary>
            /// Gets the textual value carried by this instance.
            /// </summary>
            public string Value { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="NonCommutativeAdditive"/> class.
            /// </summary>
            /// <param name="value">Stored textual value.</param>
            public NonCommutativeAdditive(string value)
            {
                Value = value;
            }

            /// <summary>
            /// Combines two values while preserving operand order.
            /// </summary>
            /// <param name="left">Left operand.</param>
            /// <param name="right">Right operand.</param>
            /// <returns>A new instance containing an order-sensitive combination.</returns>
            public static NonCommutativeAdditive operator +(NonCommutativeAdditive left, NonCommutativeAdditive right)
            {
                return new NonCommutativeAdditive($"{left.Value}>{right.Value}");
            }
        }

        /// <summary>
        /// Ensures canonicalization is not applied to non-numeric additive operators.
        /// </summary>
        [TestMethod]
        public void CanonicalOrdering_DoesNotReorder_CustomAddition()
        {
            Expression<Func<NonCommutativeAdditive, NonCommutativeAdditive, NonCommutativeAdditive>> source = (a, b) => b + a;
            var simplifier = new ExpressionSimplifier();
            var left = new NonCommutativeAdditive("L");
            var right = new NonCommutativeAdditive("R");

            var sourceResult = source.Compile()(left, right).Value;
            var simplified = (Expression<Func<NonCommutativeAdditive, NonCommutativeAdditive, NonCommutativeAdditive>>)simplifier.Simplify(source);
            var simplifiedResult = simplified.Compile()(left, right).Value;

            Assert.AreEqual(sourceResult, simplifiedResult);
        }

        /// <summary>
        /// Ensures subtraction canonicalization remains valid for unsigned numeric types.
        /// </summary>
        [TestMethod]
        public void CanonicalOrdering_SupportsUnsignedSubtraction()
        {
            Expression<Func<uint, uint, uint>> source = (a, b) => a + b - a;
            Expression<Func<uint, uint, uint>> expected = (a, b) => b;
            var simplifier = new ExpressionSimplifier();

            var simplified = simplifier.Simplify(source);

            Assert.AreEqual(expected, simplified, ExpressionComparer.Default);
        }

        /// <summary>
        /// <see cref="ExpressionSimplifier.Transform(Expression)"/> (the public
        /// <see cref="ExpressionTransformer"/> contract) and <see cref="ExpressionSimplifier.Simplify(Expression)"/>
        /// (the historical convenience name) call the same code path and must produce structurally
        /// identical results.
        /// </summary>
        [TestMethod]
        public void Transform_And_Simplify_ProduceEquivalentResults()
        {
            Expression<Func<double, double>> source = x => double.Pow(double.Cos(x), 2) + double.Pow(double.Sin(x), 2);
            var simplifier = new ExpressionSimplifier();

            var viaTransform = simplifier.Transform(source);
            var viaSimplify = simplifier.Simplify(source);

            Assert.AreEqual(viaSimplify, viaTransform, ExpressionComparer.Default);
        }
    }
}
