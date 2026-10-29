using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R2 follow-up: the validation of the whole R0–R2 catalog is preserved by the linear-time validation of shared DAGs.
/// For every sample, the fast unfolded walk and the exact shared measurement agree on depth and logical size (exact
/// acceptance boundaries), and a 16-level doubling DAG built over the sample (decided by the shared measurement) has the
/// depth and size the unfolded tree would have.
/// </summary>
[TestClass]
[TestCategory("ETC-R2")]
public class ExpressionShapeValidationCorpusTests
{
    /// <summary>Validates <paramref name="root"/> and returns the refusal, or <see langword="null"/> when accepted.</summary>
    /// <param name="root">The input.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The node limit.</param>
    /// <returns>The refusal, or <see langword="null"/>.</returns>
    private static ExpressionResourceLimitException? TryValidate(Expression root, int maxDepth, long maxNodes)
    {
        try
        {
            ExpressionShapeValidator.Validate(root, maxDepth, maxNodes);
            return null;
        }
        catch (ExpressionResourceLimitException exception)
        {
            return exception;
        }
    }

    /// <summary>Asserts the exact acceptance boundaries of <paramref name="root"/> (see <c>ExpressionSharedDagValidationTests</c>).</summary>
    /// <param name="root">The input.</param>
    /// <param name="depth">Its depth.</param>
    /// <param name="logical">Its logical size.</param>
    /// <param name="label">The case name.</param>
    private static void AssertBoundaries(Expression root, int depth, long logical, string label)
    {
        Assert.IsNull(TryValidate(root, depth, logical), label + ": exact limits accepted");
        if (depth > 1)
        {
            Assert.AreEqual(ExpressionResourceLimitKind.Depth, TryValidate(root, depth - 1, long.MaxValue)?.Kind, label + ": depth");
        }

        if (logical > 1)
        {
            ExpressionResourceLimitException? tooLarge = TryValidate(root, int.MaxValue, logical - 1);
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, tooLarge?.Kind, label + ": nodes");
            Assert.AreEqual(logical, tooLarge!.Observed, label + ": observed size");
        }
    }

    /// <summary>Every catalog sample, and a doubling DAG over it, validates with the exact depth and logical size on both decision paths.</summary>
    [TestMethod]
    public void EveryCatalogSample_ValidatesIdenticallyOnBothPaths()
    {
        const int Levels = 16;
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All)
        {
            Expression tree = sample.Build();
            ExpressionShape shape = ExpressionShapeValidator.Measure(tree);
            Assert.IsFalse(shape.Truncated, sample.Name);
            AssertBoundaries(tree, shape.Depth, shape.LogicalNodes, sample.Name);

            Expression dag = tree;
            long logical = shape.LogicalNodes;
            for (int i = 0; i < Levels; i++)
            {
                dag = Expression.Block(typeof(void), dag, dag);
                logical = 2 * logical + 1;
            }

            ExpressionShape dagShape = ExpressionShapeValidator.Measure(dag);
            Assert.AreEqual(shape.Depth + Levels, dagShape.Depth, sample.Name);
            Assert.AreEqual(logical, dagShape.LogicalNodes, sample.Name);
            Assert.AreEqual(shape.DistinctNodes + Levels, dagShape.DistinctNodes, sample.Name);
            Assert.IsTrue(logical > 65_536, sample.Name + ": the DAG must exceed the fast walk's budget");
            AssertBoundaries(dag, dagShape.Depth, logical, sample.Name + " DAG");
        }
    }
}
