using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R0: executable baseline of the expression-tree coverage matrix. For every catalog sample, every
/// <see cref="ExpressionContract"/> is run and its classified outcome is compared with the row recorded here, which
/// is the observed behavior of <c>master</c> at <c>b5c05dc3</c>, updated by ETC R1a for the rows of ETC-08, ETC-09 and
/// ETC-13 (<c>TypeIs</c>, <c>TypeEqual</c>, <c>New*</c>, <c>NewArray*</c>, <c>Block*</c>, <c>Loop</c>, <c>Throw.Rethrow</c>),
/// and by ETC R1b (rows marked <c>// ETC R1b</c>) for the traversal of member access, type tests, creations and array
/// creations (ETC-01/ETC-10, C-03) and the faithful unary and lambda reconstruction (ETC-04/ETC-05, C-01).
/// </summary>
/// <remarks>
/// <para>
/// This table is a characterization, not a specification: rows showing <c>Corrupts</c>, <c>Drops</c>,
/// <c>Throws:*</c>, <c>Partial</c> or <c>Unbound</c> pin defects registered in
/// <c>Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md</c> (findings ETC-01 to ETC-15). A correction
/// round (R1 to R4) changes a row only together with the finding it resolves and the compatibility-register entry
/// approving the new contract; it never changes a row merely to make a test pass.
/// </para>
/// <para>
/// Column order: generic traversal | exact simplifier | derived simplifier | substitution | equality | hashing.
/// The outcome vocabulary is documented on <see cref="ExpressionContractProbe"/>.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("ETC-R0")]
public class ExpressionTreeCoverageBaselineTests
{
    /// <summary>The recorded outcome row of every catalog sample.</summary>
    private static readonly IReadOnlyDictionary<string, string> Baseline = new Dictionary<string, string>
    {
        ["Add"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AddChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Subtract"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["SubtractChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Multiply"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["MultiplyChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Divide"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Modulo"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Power"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["And"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Or"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ExclusiveOr"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["LeftShift"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["RightShift"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Equal"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["NotEqual"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["GreaterThan"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["GreaterThanOrEqual"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["LessThan"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["LessThanOrEqual"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AndAlso"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["OrElse"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Coalesce"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ArrayIndex"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Assign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AddAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AddAssignChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["SubtractAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["SubtractAssignChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["MultiplyAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["MultiplyAssignChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["DivideAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ModuloAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["PowerAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AndAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["OrAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ExclusiveOrAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["LeftShiftAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["RightShiftAssign"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["PreIncrementAssign"] = "Rebuilt | Rebuilt | Rebuilt | Rebuilt | Equal | Consistent",
        ["PreDecrementAssign"] = "Rebuilt | Rebuilt | Rebuilt | Rebuilt | Equal | Consistent",
        ["PostIncrementAssign"] = "Rebuilt | Rebuilt | Rebuilt | Rebuilt | Equal | Consistent",
        ["PostDecrementAssign"] = "Rebuilt | Rebuilt | Rebuilt | Rebuilt | Equal | Consistent",
        ["Negate"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["NegateChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["UnaryPlus"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Not"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["OnesComplement"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Increment"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Decrement"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["IsTrue"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["IsFalse"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Convert"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ConvertChecked"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["TypeAs"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Unbox"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["ArrayLength"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Quote"] = "Rewrites | Rewrites | Rewrites | Opaque | Equal | Consistent",
        ["Throw"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent", // ETC R1b
        ["Constant"] = "Same | Rebuilt | Rebuilt | Same | Equal | Consistent",
        ["Parameter"] = "Same | Same | Same | Rewrites | Equal | Consistent",
        ["Default"] = "Same | Same | Same | Same | Unequal | Consistent",
        ["Call"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["MemberAccess"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent", // ETC R1b
        ["Invoke"] = "Rewrites | Rewrites | Rewrites | Partial | Equal | Consistent",
        ["Lambda"] = "Rewrites | Rewrites | Rewrites | Opaque | Equal | Consistent",
        ["Conditional"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent",
        ["TypeIs"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["TypeEqual"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["New"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["NewArrayInit"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["NewArrayBounds"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["MemberInit"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["ListInit"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Index"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Block"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Try"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Switch"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Loop"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Goto"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Label"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["RuntimeVariables"] = "Same | Same | Same | Opaque | Unequal | Consistent",
        ["DebugInfo"] = "Same | Same | Same | Same | Unequal | Consistent",
        ["Dynamic"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Extension"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Add.CustomMethod"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Add.Lifted"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Equal.LiftedToNull"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Coalesce.Conversion"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["AddAssign.CustomMethod"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["Negate.CustomMethod"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent", // ETC R1b
        ["Convert.CustomMethod"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent", // ETC R1b
        ["PreIncrementAssign.CustomMethod"] = "Rebuilt | Rebuilt | Rebuilt | Rebuilt | Equal | Consistent", // ETC R1b
        ["Throw.Rethrow"] = "Same | Same | Same | Same | Equal | Consistent",
        ["Call.InstanceReceiver"] = "Rewrites | Rewrites | Rewrites | Rewrites | Equal | Consistent",
        ["MemberAccess.Static"] = "Same | Same | Same | Same | Equal | Consistent",
        ["Invoke.Delegate"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent",
        ["Lambda.Metadata"] = "Rewrites | Rewrites | Rewrites | Opaque | Equal | Consistent", // ETC R1b
        ["New.Members"] = "Rewrites | Rewrites | Rewrites | Rewrites | Unequal | Consistent", // ETC R1b
        ["New.ValueTypeDefault"] = "Same | Same | Same | Same | Unequal | Consistent",
        ["Block.TypedResult"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Block.Shadowing"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Block.DebugInfo"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Block.GotoLabel"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Block.RuntimeVariables"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Try.CatchVariableFilter"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Try.Finally"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Try.Fault"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Try.CatchFinally"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Switch.ComparisonMethod"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Goto.Goto"] = "Same | Same | Same | Same | Unequal | Consistent",
        ["MemberInit.MemberMemberBinding"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["MemberInit.MemberListBinding"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["ListInit.ExplicitAddMethod"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Index.Indexer"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Extension.Irreducible"] = "Opaque | Opaque | Opaque | Opaque | Unequal | Consistent",
        ["Quote.CapturedOuterParameter"] = "Rewrites | Rewrites | Rewrites | Opaque | Equal | Consistent",
    };

    /// <summary>Every catalog sample has a recorded baseline row, and every recorded row belongs to a sample.</summary>
    [TestMethod]
    public void Baseline_CoversExactlyTheCatalog()
    {
        CollectionAssert.AreEquivalent(
            ExpressionNodeCatalog.All.Select(sample => sample.Name).ToArray(),
            Baseline.Keys.ToArray());
    }

    /// <summary>Every sample still produces its recorded outcome for every contract.</summary>
    [TestMethod]
    public void EverySample_MatchesItsRecordedBaseline()
    {
        var differences = new List<string>();
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All)
        {
            string observed = ExpressionContractProbe.ObserveRow(sample);
            if (!Baseline.TryGetValue(sample.Name, out string? expected) || expected != observed)
            {
                differences.Add($"{sample.Name}: expected [{expected}] observed [{observed}]");
            }
        }

        Assert.AreEqual(0, differences.Count, Environment.NewLine + string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    /// The public comparer never reports two equal trees with different hash codes on the catalog: wherever it does
    /// not throw, <c>Equals(x, y)</c> implies <c>GetHashCode(x) == GetHashCode(y)</c>.
    /// </summary>
    [TestMethod]
    public void Hashing_IsNeverInconsistentWithEquality()
    {
        string[] mismatches = ExpressionNodeCatalog.All
            .Where(sample => ExpressionContractProbe.Observe(sample, ExpressionContract.Hashing) == "Mismatch")
            .Select(sample => sample.Name)
            .ToArray();

        Assert.AreEqual(0, mismatches.Length, string.Join(", ", mismatches));
    }
}
