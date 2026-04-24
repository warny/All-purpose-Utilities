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

        [TestMethod]
        public void SimpleExpressionTest1()
        {
            (Expression<Func<double, double>> Expression, Expression<Func<double, double>> Expected)[] Test = [
                (x => double.Pow(double.Cos(x), 2) + double.Pow(double.Sin(x), 2), x => 1),
                (x => double.Sin(x) / double.Cos(x), x => double.Tan(x)),
                (x => double.Cos(x) / double.Sin(x), x => 1 / double.Tan(x)),
                (x => double.Cos(x) * double.Tan(x), x => double.Sin(x)),
                (x => double.Tan(x) * double.Cos(x), x => double.Sin(x)),
                (x => double.Sin(x) / double.Tan(x), x => double.Cos(x)),
                (x => 3 * x + 2 * x, x => 5 * x),
                (x => 3 * x + x * 2, x => 5 * x),
                (x => x + 2 * x, x => 3 * x),
                (x => x - 2 * x, x => -x),
                (x => 2 * x - 2 * x, x => 0),
                (x => -2 * x + 2 * x, x => 0)
            ];

            var simplifier = new ExpressionSimplifier();
            foreach (var test in Test)
            {
                var simplified = simplifier.Simplify(test.Expression);
                Assert.AreEqual(test.Expected, simplified, ExpressionComparer.Default);
            }
        }

        /// <summary>
        /// Ensures commutative and associative operations are rewritten into a deterministic canonical order.
        /// </summary>
        [TestMethod]
        public void CanonicalOrdering_ForCommutativeAndAssociativeExpressions()
        {
            var simplifier = new ExpressionSimplifier();

            (Expression Expression, Expression Expected)[] tests =
            {
                (
                    (Expression<Func<double, double, double>>)((a, b) => b + a),
                    (Expression<Func<double, double, double>>)((a, b) => a + b)
                ),
                (
                    (Expression<Func<double, double, double>>)((A, a) => a + A),
                    (Expression<Func<double, double, double>>)((A, a) => A + a)
                ),
                (
                    (Expression<Func<double, double, double, double, double>>)((a, b, c, d) => (d + c) * (b + a)),
                    (Expression<Func<double, double, double, double, double>>)((a, b, c, d) => (a + b) * (c + d))
                ),
                (
                    (Expression<Func<double, double, double>>)((a, b) => b - a),
                    (Expression<Func<double, double, double>>)((a, b) => -a + b)
                ),
                (
                    (Expression<Func<double, double, double>>)((a, b) => a + b - a),
                    (Expression<Func<double, double, double>>)((a, b) => b)
                ),
                (
                    (Expression<Func<double, double, double>>)((x, y) => double.Cos(y) + double.Cos(x)),
                    (Expression<Func<double, double, double>>)((x, y) => double.Cos(x) + double.Cos(y))
                ),
                (
                    (Expression<Func<double, double>>)(x => double.Sin(x) + double.Cos(x)),
                    (Expression<Func<double, double>>)(x => double.Cos(x) + double.Sin(x))
                ),
                (
                    (Expression<Func<double, double, double>>)((x, y) => double.Pow(double.Cos(x), 2) + double.Pow(double.Cos(y), 2) + double.Pow(double.Sin(x), 2) + double.Pow(double.Sin(y), 2)),
                    (Expression<Func<double, double, double>>)((x, y) => 2)
                ),
                (
                    (Expression<Func<double, double, double, double>>)((a, b, c) => (a + b) + c),
                    (Expression<Func<double, double, double, double>>)((a, b, c) => a + (b + c))
                ),
                (
                    (Expression<Func<double, double, double, double>>)((a, b, c) => (c + b) - a),
                    (Expression<Func<double, double, double, double>>)((a, b, c) => -a + (b + c))
                ),
                (
                    (Expression<Func<double, double, double, double>>)((a, b, c) => (c - b) - a),
                    (Expression<Func<double, double, double, double>>)((a, b, c) => -a + (-b + c))
                ),
                (
                    (Expression<Func<double, double, double, double>>)((a, b, c) => (a / b) / c),
                    (Expression<Func<double, double, double, double>>)((a, b, c) => a / (b * c))
                ),
            };

            foreach (var (source, expected) in tests)
            {
                var simplified = simplifier.Simplify(source);
                Assert.AreEqual(expected, simplified, ExpressionComparer.Default);
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
    }
}
