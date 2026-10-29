using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R2, checkpoint B: the lexical binding model (binder frames for lambda, conversion-lambda, block and catch
/// declarations) and the label bijection, checked together on structural equality (public and raw comparers), hashing and
/// the canonical order (roadmap §5.2). Positive pairs are alpha-equivalent and must agree on every contract; negative pairs
/// must be unequal (their hashes may collide).
/// </summary>
[TestClass]
[TestCategory("ETC-R2")]
public class ExpressionTreeR2BindingTests
{
    /// <summary>A free parameter shared by both sides of the pairs.</summary>
    private static readonly ParameterExpression Free = Expression.Parameter(typeof(double), "free");

    /// <summary>Creates a fresh <see cref="double"/> variable.</summary>
    /// <param name="name">Its name (never significant).</param>
    /// <returns>A new variable.</returns>
    private static ParameterExpression V(string name = "v") => Expression.Variable(typeof(double), name);

    /// <summary>
    /// Asserts that <paramref name="x"/> and <paramref name="y"/> are equal in both directions on the public and raw comparers,
    /// hash identically on both, can be retrieved from a <see cref="HashSet{T}"/> and a <see cref="Dictionary{TKey, TValue}"/>
    /// keyed by the other, and tie in the canonical order.
    /// </summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <param name="message">The case name.</param>
    private static void AssertEquivalent(Expression x, Expression y, string message)
    {
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(x, y), $"{message}: raw x = y");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(y, x), $"{message}: raw y = x");
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(x), ExpressionComparer.StructuralHashRaw(y), $"{message}: raw hash");
        Assert.IsTrue(ExpressionComparer.Default.Equals(x, y), $"{message}: public x = y");
        Assert.IsTrue(ExpressionComparer.Default.Equals(y, x), $"{message}: public y = x");
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(x), ExpressionComparer.Default.GetHashCode(y), $"{message}: public hash");

        var set = new HashSet<Expression>(ExpressionComparer.Default) { x };
        Assert.IsTrue(set.Contains(y), $"{message}: HashSet");
        var dictionary = new Dictionary<Expression, int>(ExpressionComparer.Default) { [y] = 1 };
        Assert.IsTrue(dictionary.ContainsKey(x), $"{message}: Dictionary");

        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(x, y, []), $"{message}: canonical x vs y");
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(y, x, []), $"{message}: canonical y vs x");
    }

    /// <summary>Asserts that <paramref name="x"/> and <paramref name="y"/> are unequal in both directions on the public and raw comparers.</summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <param name="message">The case name.</param>
    private static void AssertDistinct(Expression x, Expression y, string message)
    {
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(x, y), $"{message}: raw x = y");
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(y, x), $"{message}: raw y = x");
        Assert.IsFalse(ExpressionComparer.Default.Equals(x, y), $"{message}: public x = y");
        Assert.IsFalse(ExpressionComparer.Default.Equals(y, x), $"{message}: public y = x");
    }

    // ------------------------------------------------------------------------------------------
    // Positive binding pairs.
    // ------------------------------------------------------------------------------------------

    /// <summary>Two blocks with separately allocated, positionally equivalent locals (different names) are equivalent.</summary>
    [TestMethod]
    public void Blocks_WithAlphaRenamedLocals_AreEquivalent()
    {
        Expression Build(string first, string second)
        {
            ParameterExpression a = V(first);
            ParameterExpression b = V(second);
            return Expression.Block([a, b], Expression.Assign(a, Free), Expression.Assign(b, Expression.Add(a, Expression.Constant(2.0))), Expression.Subtract(b, a));
        }

        AssertEquivalent(Build("a", "b"), Build("x", "y"), "alpha-renamed block locals");
    }

    /// <summary>Two catch handlers with differently named variables and equivalent filters and bodies are equivalent.</summary>
    [TestMethod]
    public void Catches_WithAlphaRenamedVariables_AreEquivalent()
    {
        Expression Build(string name)
        {
            ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), name);
            return Expression.TryCatch(
                Free,
                Expression.Catch(
                    exception,
                    Expression.Condition(Expression.Equal(Expression.Property(exception, nameof(Exception.Message)), Expression.Constant("x")), Expression.Constant(1.0), Expression.Constant(2.0)),
                    Expression.NotEqual(exception, Expression.Constant(null, typeof(InvalidOperationException)))));
        }

        AssertEquivalent(Build("ex"), Build("error"), "alpha-renamed catch variables");
    }

    /// <summary>A lambda capturing a variable of its enclosing block is equivalent to the same shape with renamed instances.</summary>
    [TestMethod]
    public void LambdaCapturingBlockVariable_IsEquivalent()
    {
        Expression Build(string local, string parameter)
        {
            ParameterExpression captured = V(local);
            ParameterExpression x = Expression.Parameter(typeof(double), parameter);
            return Expression.Block(
                [captured],
                Expression.Assign(captured, Free),
                Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Multiply(x, captured), x), Expression.Constant(3.0)));
        }

        AssertEquivalent(Build("c", "x"), Build("k", "y"), "lambda capturing an enclosing block variable");
    }

    /// <summary>Nested blocks with alpha-renamed locals, including an inner block shadowing the outer variable instance, are equivalent.</summary>
    [TestMethod]
    public void NestedBlocks_WithAlphaRenamedAndShadowingLocals_AreEquivalent()
    {
        ParameterExpression shared = V("shared");
        Expression shadowing = Expression.Block([shared], Expression.Assign(shared, Free), Expression.Block([shared], Expression.Assign(shared, Expression.Constant(1.0)), shared));
        ParameterExpression outer = V("outer");
        ParameterExpression inner = V("inner");
        Expression renamed = Expression.Block([outer], Expression.Assign(outer, Free), Expression.Block([inner], Expression.Assign(inner, Expression.Constant(1.0)), inner));

        AssertEquivalent(shadowing, renamed, "nested blocks, shadowing instance vs renamed instances");
    }

    /// <summary>Two coalesce nodes with equivalent conversion lambdas (different parameter instances) are equivalent.</summary>
    [TestMethod]
    public void ConversionLambdas_WithRenamedParameters_AreEquivalent()
    {
        ParameterExpression nullable = Expression.Parameter(typeof(double?), "n");
        Expression Build(string name)
        {
            ParameterExpression value = Expression.Parameter(typeof(double), name);
            return Expression.Coalesce(nullable, Expression.Constant(0.0), Expression.Lambda<Func<double, double>>(Expression.Add(value, Expression.Constant(1.0)), value));
        }

        AssertEquivalent(Build("a"), Build("b"), "conversion lambdas");
    }

    /// <summary>Runtime variables reference corresponding bound variables (by position) and the same free variable (by identity).</summary>
    [TestMethod]
    public void RuntimeVariables_ResolveThroughTheBindingModel()
    {
        Expression Build(string name)
        {
            ParameterExpression local = V(name);
            return Expression.Block([local], Expression.Assign(local, Free), Expression.RuntimeVariables(local, Free));
        }

        AssertEquivalent(Build("a"), Build("b"), "runtime variables");
        ParameterExpression first = V("a");
        ParameterExpression second = V("b");
        AssertDistinct(
            Expression.Block([first, second], Expression.RuntimeVariables(first)),
            Expression.Block([first, second], Expression.RuntimeVariables(second)),
            "runtime variables at different positions");
    }

    // ------------------------------------------------------------------------------------------
    // Negative binding pairs.
    // ------------------------------------------------------------------------------------------

    /// <summary>Two distinct free variables with the same name and type are not equivalent.</summary>
    [TestMethod]
    public void SameNameDistinctFreeVariables_AreDistinct()
    {
        ParameterExpression first = Expression.Parameter(typeof(double), "same");
        ParameterExpression second = Expression.Parameter(typeof(double), "same");

        AssertDistinct(Expression.Block(first), Expression.Block(second), "same-name free variables");
    }

    /// <summary>Blocks declaring variables of different types are not equivalent, even when unused.</summary>
    [TestMethod]
    public void DifferentDeclarationTypes_AreDistinct()
    {
        AssertDistinct(
            Expression.Block([Expression.Variable(typeof(double), "a")], Free),
            Expression.Block([Expression.Variable(typeof(int), "a")], Free),
            "different declaration types");
    }

    /// <summary>A reference bound by the block on one side and free on the other is not equivalent.</summary>
    [TestMethod]
    public void BoundVersusFreeReference_IsDistinct()
    {
        ParameterExpression local = V("free");

        AssertDistinct(Expression.Block([local], local), Expression.Block([V("free")], Free), "bound vs free");
    }

    /// <summary>Swapping declaration positions while changing the use sites accordingly does not make two different blocks equivalent.</summary>
    [TestMethod]
    public void SwappedDeclarationPositions_AreDistinct()
    {
        ParameterExpression a = V("a");
        ParameterExpression b = V("b");

        AssertDistinct(
            Expression.Block([a, b], Expression.Assign(a, Free), Expression.Subtract(a, b)),
            Expression.Block([b, a], Expression.Assign(a, Free), Expression.Subtract(a, b)),
            "swapped declaration positions");
    }

    /// <summary>A variable bound by a catch handler and one bound by a block at the same place are not equivalent.</summary>
    [TestMethod]
    public void DifferentBinderKinds_AreDistinct()
    {
        ParameterExpression caught = Expression.Variable(typeof(Exception), "e");
        ParameterExpression local = Expression.Variable(typeof(Exception), "e");

        AssertDistinct(
            Expression.TryCatch(Expression.Constant(1.0), Expression.Catch(caught, Expression.Block(Expression.Constant(2.0), Expression.Condition(Expression.Equal(caught, Expression.Constant(null, typeof(Exception))), Free, Free)))),
            Expression.TryCatch(Expression.Constant(1.0), Expression.Catch(typeof(Exception), Expression.Block([local], Expression.Constant(2.0), Expression.Condition(Expression.Equal(local, Expression.Constant(null, typeof(Exception))), Free, Free)))),
            "catch-bound vs block-bound");
    }

    /// <summary>
    /// Shadowing: one instance declared by two nested blocks means two variables. Compared with two distinct instances, the
    /// inner reference matches the inner declaration, never the outer one.
    /// </summary>
    [TestMethod]
    public void ShadowingWithReusedInstance_ResolvesInnermost()
    {
        ParameterExpression shared = V("v");
        Expression shadowing = Expression.Block([shared], Expression.Block([shared], shared));
        ParameterExpression outer = V("a");
        ParameterExpression inner = V("b");

        AssertEquivalent(shadowing, Expression.Block([outer], Expression.Block([inner], inner)), "inner reference to inner declaration");
        AssertDistinct(shadowing, Expression.Block([outer], Expression.Block([inner], outer)), "inner reference is not the outer declaration");
    }

    /// <summary>A catch variable is not in scope in the protected body or in a sibling handler: a same-instance reference there is free.</summary>
    [TestMethod]
    public void CatchVariableScope_IsFilterAndBodyOnly()
    {
        ParameterExpression shared = Expression.Variable(typeof(Exception), "e");
        ParameterExpression renamed = Expression.Variable(typeof(Exception), "f");
        Expression Body(ParameterExpression e) => Expression.Condition(Expression.Equal(e, Expression.Constant(null, typeof(Exception))), Expression.Constant(1.0), Expression.Constant(2.0));

        // In the protected body and in the sibling handler, 'shared' is free: renaming it there changes the meaning.
        AssertDistinct(
            Expression.TryCatch(Body(shared), Expression.Catch(shared, Body(shared)), Expression.Catch(typeof(ArgumentException), Body(shared))),
            Expression.TryCatch(Body(renamed), Expression.Catch(renamed, Body(renamed)), Expression.Catch(typeof(ArgumentException), Body(renamed))),
            "free outside the handler");
        AssertEquivalent(
            Expression.TryCatch(Body(shared), Expression.Catch(shared, Body(shared)), Expression.Catch(typeof(ArgumentException), Body(shared))),
            Expression.TryCatch(Body(shared), Expression.Catch(renamed, Body(renamed)), Expression.Catch(typeof(ArgumentException), Body(shared))),
            "bound inside the handler only");
    }

    // ------------------------------------------------------------------------------------------
    // Label bijection.
    // ------------------------------------------------------------------------------------------

    /// <summary>Builds <c>{ if (free &gt; 0) goto first(free); second-statement; label first: default }</c> with the given target.</summary>
    /// <param name="target">The label target.</param>
    /// <returns>The block.</returns>
    private static Expression ForwardJump(LabelTarget target)
        => Expression.Block(
            Expression.IfThen(Expression.GreaterThan(Free, Expression.Constant(0.0)), Expression.Return(target, Free)),
            Expression.Label(target, Expression.Constant(-1.0)));

    /// <summary>Two alpha-renamed labels (different names, same type) with a forward jump before the definition are equivalent.</summary>
    [TestMethod]
    public void AlphaRenamedLabels_WithForwardJump_AreEquivalent()
    {
        AssertEquivalent(ForwardJump(Expression.Label(typeof(double), "exit")), ForwardJump(Expression.Label(typeof(double), "done")), "alpha-renamed labels");
    }

    /// <summary>Labels of different types never correspond.</summary>
    [TestMethod]
    public void LabelsOfDifferentTypes_AreDistinct()
    {
        LabelTarget voidTarget = Expression.Label("end");
        LabelTarget objectTarget = Expression.Label(typeof(object), "end");

        AssertDistinct(Expression.Block(Expression.Goto(voidTarget), Expression.Label(voidTarget)), Expression.Block(Expression.Goto(objectTarget, Expression.Constant(null)), Expression.Label(objectTarget, Expression.Constant(null))), "label types");
    }

    /// <summary>Several jumps to one shared target are not equivalent to the same jumps to two distinct targets with identical names and types.</summary>
    [TestMethod]
    public void SharedTargetVersusDistinctTargets_AreDistinct()
    {
        LabelTarget shared = Expression.Label("same");
        LabelTarget first = Expression.Label("same");
        LabelTarget second = Expression.Label("same");
        Expression Build(LabelTarget jump1, LabelTarget jump2, LabelTarget definition1, LabelTarget definition2)
            => Expression.Block(
                Expression.IfThen(Expression.GreaterThan(Free, Expression.Constant(0.0)), Expression.Goto(jump1)),
                Expression.IfThen(Expression.LessThan(Free, Expression.Constant(0.0)), Expression.Goto(jump2)),
                Expression.Label(definition1),
                Expression.Label(definition2));

        // Left: both jumps go to 'shared', defined once; the second definition is another target. Right: one target per jump.
        LabelTarget other = Expression.Label("same");
        AssertDistinct(Build(shared, shared, shared, other), Build(first, second, first, second), "two left jumps, one right target vs two");
        AssertEquivalent(Build(shared, shared, shared, other), Build(first, first, first, second), "same sharing pattern");
    }

    /// <summary>Two left targets corresponding to one right target violate the bijection.</summary>
    [TestMethod]
    public void TwoLeftTargetsToOneRight_ViolateTheBijection()
    {
        LabelTarget a = Expression.Label("a");
        LabelTarget b = Expression.Label("b");
        LabelTarget c = Expression.Label("c");

        AssertDistinct(
            Expression.Block(Expression.Goto(a), Expression.Label(b)),
            Expression.Block(Expression.Goto(c), Expression.Label(c)),
            "two left targets, one right target");
    }

    /// <summary>A loop's break and continue labels are distinct identities: a jump to the break label never matches a jump to the continue label.</summary>
    [TestMethod]
    public void LoopBreakAndContinue_AreDistinguished()
    {
        Expression Build(bool jumpToBreak)
        {
            LabelTarget breakLabel = Expression.Label("break");
            LabelTarget continueLabel = Expression.Label("continue");
            return Expression.Loop(
                Expression.IfThenElse(Expression.GreaterThan(Free, Expression.Constant(0.0)), Expression.Goto(jumpToBreak ? breakLabel : continueLabel), Expression.Goto(breakLabel)),
                breakLabel,
                continueLabel);
        }

        AssertEquivalent(Build(true), Build(true), "same jump");
        AssertDistinct(Build(true), Build(false), "break vs continue");
    }

    /// <summary>
    /// Label scopes are per lambda body: two nested lambdas reusing one target instance are equivalent to two nested lambdas
    /// with their own targets, and a correspondence established in one body never constrains the other.
    /// </summary>
    [TestMethod]
    public void LabelsInIndependentLambdaBodies_DoNotLeak()
    {
        Expression Lambda(LabelTarget target) => Expression.Lambda<Func<double>>(ForwardJump(target));
        LabelTarget shared = Expression.Label(typeof(double), "shared");
        Expression reused = Expression.NewArrayInit(typeof(Func<double>), Lambda(shared), Lambda(shared));
        Expression separate = Expression.NewArrayInit(typeof(Func<double>), Lambda(Expression.Label(typeof(double), "first")), Lambda(Expression.Label(typeof(double), "second")));

        AssertEquivalent(reused, separate, "one target reused in two lambda bodies vs one target per body");
    }

    /// <summary>The same target reused in two subtrees of one body is one identity there: it does not match two distinct targets.</summary>
    [TestMethod]
    public void SameTargetInOneBody_IsOneIdentity()
    {
        LabelTarget shared = Expression.Label(typeof(double), "shared");
        Expression reused = Expression.Block(Expression.Block(Expression.Return(shared, Free), Expression.Label(shared, Free)));
        LabelTarget jump = Expression.Label(typeof(double), "jump");
        LabelTarget definition = Expression.Label(typeof(double), "definition");
        Expression split = Expression.Block(Expression.Block(Expression.Return(jump, Free), Expression.Label(definition, Free)));

        AssertDistinct(reused, split, "one shared target vs two targets");
    }

    /// <summary>
    /// The commutative fallback of ordinary additions keeps the label correspondences consistent: two blocks swapped across an
    /// addition are equivalent and hash identically, and a failed positional attempt leaves no correspondence behind.
    /// </summary>
    [TestMethod]
    public void CommutativeOperands_WithLabels_StayConsistent()
    {
        Expression Operand(LabelTarget target, double value)
            => Expression.Block(Expression.Return(target, Expression.Constant(value)), Expression.Label(target, Expression.Constant(0.0)));

        LabelTarget a = Expression.Label(typeof(double), "a");
        LabelTarget b = Expression.Label(typeof(double), "b");
        LabelTarget c = Expression.Label(typeof(double), "c");
        LabelTarget d = Expression.Label(typeof(double), "d");
        Expression left = Expression.Add(Operand(a, 1.0), Operand(b, 2.0));
        Expression right = Expression.Add(Operand(c, 2.0), Operand(d, 1.0));

        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(left, right));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(right, left));
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(left), ExpressionComparer.StructuralHashRaw(right));
    }

    // ------------------------------------------------------------------------------------------
    // Cross-contract matrix (equality, hash and canonical order together).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Deterministic matrix of binding shapes under nested lambdas: each case is built twice with fresh instances and names
    /// (alpha-equivalent, so equal on every contract), and every two different cases are unequal with an antisymmetric,
    /// non-tying canonical order.
    /// </summary>
    [TestMethod]
    public void BindingMatrix_AllContractsAgree()
    {
        Func<int, Expression>[] cases =
        [
            // Block local captured by a nested lambda.
            n =>
            {
                ParameterExpression local = V($"l{n}");
                ParameterExpression x = Expression.Parameter(typeof(double), $"x{n}");
                return Expression.Lambda<Func<double, double>>(Expression.Block([local], Expression.Assign(local, x), Expression.Invoke(Expression.Lambda<Func<double>>(local))), x);
            },

            // Catch variable inside a lambda, with an outer lambda parameter captured in the handler.
            n =>
            {
                ParameterExpression x = Expression.Parameter(typeof(double), $"x{n}");
                ParameterExpression e = Expression.Variable(typeof(Exception), $"e{n}");
                return Expression.Lambda<Func<double, double>>(Expression.TryCatch(x, Expression.Catch(e, Expression.Condition(Expression.Equal(e, Expression.Constant(null, typeof(Exception))), x, Expression.Constant(0.0)))), x);
            },

            // Shadowing: the inner block redeclares the outer variable instance.
            n =>
            {
                ParameterExpression shared = V($"s{n}");
                ParameterExpression x = Expression.Parameter(typeof(double), $"x{n}");
                return Expression.Lambda<Func<double, double>>(Expression.Block([shared], Expression.Assign(shared, x), Expression.Block([shared], Expression.Assign(shared, Expression.Constant(1.0)), shared)), x);
            },

            // Forward jump to a label defined later in a block.
            n =>
            {
                ParameterExpression x = Expression.Parameter(typeof(double), $"x{n}");
                LabelTarget target = Expression.Label(typeof(double), $"t{n}");
                return Expression.Lambda<Func<double, double>>(Expression.Block(Expression.Return(target, x), Expression.Label(target, Expression.Constant(0.0))), x);
            },

            // Free variable used inside a block.
            n => Expression.Lambda<Func<double>>(Expression.Block([V($"u{n}")], Free)),
        ];

        for (int i = 0; i < cases.Length; i++)
        {
            AssertEquivalent(cases[i](1), cases[i](2), $"case {i}");
            for (int j = 0; j < cases.Length; j++)
            {
                if (i == j) continue;
                Expression x = cases[i](1);
                Expression y = cases[j](2);
                Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(x, y), $"cases {i} and {j}");
                int forward = ExpressionCanonicalOrder.Compare(x, y, []);
                Assert.AreNotEqual(0, forward, $"cases {i} and {j} must not tie");
                Assert.AreEqual(-Math.Sign(forward), Math.Sign(ExpressionCanonicalOrder.Compare(y, x, [])), $"cases {i} and {j}: antisymmetry");
            }
        }
    }

    /// <summary>
    /// Canonical keys of block-local terms do not depend on the variable instances, enclosing scopes included: the keys of two
    /// alpha-equivalent blocks under an enclosing scope snapshot tie, and the enclosing variable is keyed as bound.
    /// </summary>
    [TestMethod]
    public void CanonicalKeys_BlockLocalTerms_IgnoreVariableInstances()
    {
        ParameterExpression enclosing = Expression.Parameter(typeof(double), "outer");
        Expression Build(string name)
        {
            ParameterExpression local = V(name);
            return Expression.Block([local], Expression.Assign(local, enclosing), Expression.Multiply(local, enclosing));
        }

        IReadOnlyList<ParameterExpression[]> scopes = [[enclosing]];
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(Build("a"), Build("b"), scopes));
        ExpressionCanonicalOrder.KeyNode[] keys = ExpressionCanonicalOrder.BuildKeys([Build("a"), Build("b")], scopes);
        Assert.AreEqual(0, keys[0].CompareTo(keys[1]), "Sibling keys built in one batch do not share label or scope state.");
    }

    /// <summary>
    /// The R2 families carry their metadata in equality and hashing: a different switch comparison, jump kind, catch test type,
    /// finally versus fault, or block result type makes two otherwise identical trees unequal; debug information is not
    /// semantic and never does.
    /// </summary>
    [TestMethod]
    public void R2Metadata_ParticipatesInEquality_ExceptDebugInfo()
    {
        LabelTarget target = Expression.Label(typeof(double), "t");
        AssertDistinct(
            Expression.Block(Expression.Return(target, Free), Expression.Label(target, Free)),
            Expression.Block(Expression.Goto(target, Free), Expression.Label(target, Free)),
            "jump kind");
        AssertDistinct(
            Expression.TryCatch(Free, Expression.Catch(typeof(InvalidOperationException), Free)),
            Expression.TryCatch(Free, Expression.Catch(typeof(ArgumentException), Free)),
            "catch test type");
        AssertDistinct(Expression.TryFinally(Free, Expression.Empty()), Expression.TryFault(Free, Expression.Empty()), "finally vs fault");
        AssertDistinct(Expression.Block(typeof(object), Expression.Constant("x")), Expression.Block(typeof(string), Expression.Constant("x")), "block result type");
        AssertDistinct(
            Expression.Switch(Free, Free, typeof(ExpressionTreeR2Tests).GetMethod(nameof(ExpressionTreeR2Tests.NearlyEqual)), Expression.SwitchCase(Free, Expression.Constant(1.0))),
            Expression.Switch(Free, Free, (System.Reflection.MethodInfo?)null, Expression.SwitchCase(Free, Expression.Constant(1.0))),
            "switch comparison");
        AssertDistinct(
            Expression.Switch(Free, Free, Expression.SwitchCase(Free, Expression.Constant(1.0)), Expression.SwitchCase(Expression.Constant(0.0), Expression.Constant(2.0))),
            Expression.Switch(Free, Free, Expression.SwitchCase(Expression.Constant(0.0), Expression.Constant(2.0)), Expression.SwitchCase(Free, Expression.Constant(1.0))),
            "case order");

        AssertEquivalent(
            Expression.Block(Expression.DebugInfo(Expression.SymbolDocument("a.cs"), 1, 1, 1, 2), Free),
            Expression.Block(Expression.DebugInfo(Expression.SymbolDocument("b.cs"), 7, 3, 9, 4), Free),
            "debug information is not semantic");
    }
}
