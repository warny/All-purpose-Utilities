using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;

namespace UtilsTest.Lists;

/// <summary>
/// Verifies content-version enumeration and exception-safe adaptive maintenance.
/// </summary>
[TestClass]
public class SkipListSafetyTests
{
    /// <summary>
    /// Verifies that insertion invalidates an active enumerator, including before its first move.
    /// </summary>
    [TestMethod]
    public void Enumerator_NewInsertion_InvalidatesBeforeAndAfterFirstMove()
    {
        SkipList<int> list = CreateList(1, 2, 3);
        using IEnumerator<int> beforeFirstMove = list.GetEnumerator();
        Assert.IsTrue(list.Add(4));
        Assert.ThrowsException<InvalidOperationException>(() => beforeFirstMove.MoveNext());

        using IEnumerator<int> afterFirstMove = list.GetEnumerator();
        Assert.IsTrue(afterFirstMove.MoveNext());
        Assert.IsTrue(list.Add(5));
        Assert.ThrowsException<InvalidOperationException>(() => afterFirstMove.MoveNext());
        list.ValidateInvariants();
    }

    /// <summary>
    /// Verifies that comparer-equal duplicate insertion does not invalidate enumeration.
    /// </summary>
    [TestMethod]
    public void Enumerator_ComparerEqualDuplicate_DoesNotInvalidate()
    {
        SkipList<string> list = new(StringComparer.OrdinalIgnoreCase, 2);
        Assert.IsTrue(list.Add("alpha"));
        Assert.IsTrue(list.Add("beta"));
        using IEnumerator<string> enumerator = list.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        Assert.IsFalse(list.Add("ALPHA"));

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("beta", enumerator.Current);
        Assert.IsFalse(enumerator.MoveNext());
        list.ValidateInvariants();
    }

    /// <summary>
    /// Verifies successful removal invalidates enumeration while a missing removal does not.
    /// </summary>
    [TestMethod]
    public void Enumerator_Remove_OnlySuccessfulRemovalInvalidates()
    {
        SkipList<int> list = CreateList(1, 2, 3);
        using IEnumerator<int> unaffected = list.GetEnumerator();
        Assert.IsTrue(unaffected.MoveNext());
        Assert.IsFalse(list.Remove(99));
        Assert.IsTrue(unaffected.MoveNext());

        using IEnumerator<int> invalidated = list.GetEnumerator();
        Assert.IsTrue(invalidated.MoveNext());
        Assert.IsTrue(list.Remove(2));
        Assert.ThrowsException<InvalidOperationException>(() => invalidated.MoveNext());
        list.ValidateInvariants();
    }

    /// <summary>
    /// Verifies clearing non-empty content invalidates enumeration while clearing an empty list is a no-op.
    /// </summary>
    [TestMethod]
    public void Enumerator_Clear_OnlyNonEmptyClearInvalidates()
    {
        SkipList<int> list = CreateList(1, 2);
        using IEnumerator<int> invalidated = list.GetEnumerator();
        list.Clear();
        Assert.ThrowsException<InvalidOperationException>(() => invalidated.MoveNext());
        list.ValidateInvariants();

        using IEnumerator<int> unaffected = list.GetEnumerator();
        list.Clear();
        Assert.IsFalse(unaffected.MoveNext());
        list.ValidateInvariants();
    }

    /// <summary>
    /// Verifies successful lookup-driven maintenance changes topology without invalidating enumeration.
    /// </summary>
    [TestMethod]
    public void Enumerator_LookupPromotion_DoesNotInvalidate()
    {
        SkipList<int> list = new(2);
        foreach (int value in Enumerable.Range(0, 120))
        {
            Assert.IsTrue(list.Add(value));
        }
        list.ValidateInvariants();
        using IEnumerator<int> enumerator = list.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        string before = list.GetStructureSignature();

        bool changed = ExerciseLookupsUntilTopologyChanges(list, before);

        Assert.IsTrue(changed, "The deterministic lookup scenario must exercise adaptive maintenance.");
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(1, enumerator.Current);
        list.ValidateInvariants();
        CollectionAssert.AreEqual(Enumerable.Range(0, 120).ToArray(), list.ToArray());
    }

    /// <summary>
    /// Verifies comparer exceptions from every mutating and lookup path preserve content and topology.
    /// </summary>
    /// <param name="operation">The public operation whose traversal is tested.</param>
    [DataTestMethod]
    [DataRow(SearchOperation.Contains)]
    [DataRow(SearchOperation.TryGet)]
    [DataRow(SearchOperation.Add)]
    [DataRow(SearchOperation.Remove)]
    public void ComparerException_SearchPlanIsAbandoned(SearchOperation operation)
    {
        PromotingSearchScenario scenario = FindPromotingSearchScenario();
        ControlledComparer comparer = new();
        SkipList<int> list = CreateComparisonList(comparer);
        list.ValidateInvariants();
        string before = list.GetStructureSignature();
        int[] contentBefore = list.ToArray();
        int countBefore = list.Count;
        comparer.ThrowOnComparisonNumber = comparer.ComparisonCount + scenario.ComparisonCount;

        ComparerTestException exception = Assert.ThrowsException<ComparerTestException>(
            () => Execute(operation, list, scenario.Target));

        Assert.AreSame(comparer.Exception, exception);
        comparer.ThrowOnComparisonNumber = null;
        Assert.AreEqual(before, list.GetStructureSignature());
        Assert.AreEqual(countBefore, list.Count);
        CollectionAssert.AreEqual(contentBefore, list.ToArray());
        list.ValidateInvariants();
    }

    /// <summary>
    /// Finds a lookup that provably commits adaptive maintenance on an identically constructed list.
    /// </summary>
    /// <returns>The lookup target and the number of comparisons required to complete its traversal.</returns>
    private static PromotingSearchScenario FindPromotingSearchScenario()
    {
        foreach (int target in Enumerable.Range(0, 120))
        {
            ControlledComparer comparer = new();
            SkipList<int> list = CreateComparisonList(comparer);
            string before = list.GetStructureSignature();
            int comparisonsBefore = comparer.ComparisonCount;

            bool found = list.Contains(target);
            int comparisonCount = comparer.ComparisonCount - comparisonsBefore;

            Assert.IsTrue(found);
            if (list.GetStructureSignature() != before)
            {
                Assert.IsTrue(comparisonCount > 1,
                    "The comparer must throw after at least one successful comparison.");
                list.ValidateInvariants();
                return new PromotingSearchScenario(target, comparisonCount);
            }
        }

        Assert.Fail("The deterministic scenario must contain a lookup that commits adaptive maintenance.");
        return null!;
    }

    /// <summary>Creates the deterministic multi-level list used by comparer exception tests.</summary>
    /// <param name="comparer">The controlled comparer to associate with the list.</param>
    /// <returns>A list containing the integers from zero through 119.</returns>
    private static SkipList<int> CreateComparisonList(ControlledComparer comparer)
    {
        SkipList<int> list = new(comparer, 2);
        foreach (int value in Enumerable.Range(0, 120))
        {
            Assert.IsTrue(list.Add(value));
        }
        return list;
    }

    /// <summary>Creates a threshold-two list containing the supplied values.</summary>
    /// <param name="values">Values to insert.</param>
    /// <returns>The populated list.</returns>
    private static SkipList<int> CreateList(params int[] values)
    {
        SkipList<int> list = new(2);
        foreach (int value in values)
        {
            Assert.IsTrue(list.Add(value));
        }
        return list;
    }

    /// <summary>Executes deterministic lookups until at least one promotion is committed.</summary>
    /// <param name="list">The list to search.</param>
    /// <param name="initialSignature">The topology before searching.</param>
    /// <returns><see langword="true"/> when the topology changes.</returns>
    private static bool ExerciseLookupsUntilTopologyChanges(SkipList<int> list, string initialSignature)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            foreach (int value in Enumerable.Range(0, 120))
            {
                Assert.IsTrue(list.Contains(value));
                Assert.IsTrue(list.TryGet(value, out int found));
                Assert.AreEqual(value, found);
                if (list.GetStructureSignature() != initialSignature)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Executes the requested public operation against a comparison path of several nodes.</summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="list">The target list.</param>
    /// <param name="target">The value whose traversal provably plans adaptive maintenance.</param>
    private static void Execute(SearchOperation operation, SkipList<int> list, int target)
    {
        switch (operation)
        {
            case SearchOperation.Contains:
                list.Contains(target);
                break;
            case SearchOperation.TryGet:
                list.TryGet(target, out _);
                break;
            case SearchOperation.Add:
                list.Add(target);
                break;
            case SearchOperation.Remove:
                list.Remove(target);
                break;
        }
    }

    /// <summary>Describes a lookup known to commit at least one adaptive promotion when it succeeds.</summary>
    private sealed class PromotingSearchScenario
    {
        /// <summary>Initializes a promoting lookup scenario.</summary>
        /// <param name="target">The lookup target.</param>
        /// <param name="comparisonCount">The number of comparisons in the complete traversal.</param>
        public PromotingSearchScenario(int target, int comparisonCount)
        {
            Target = target;
            ComparisonCount = comparisonCount;
        }

        /// <summary>Gets the lookup target.</summary>
        public int Target { get; }

        /// <summary>Gets the number of comparisons in the complete traversal.</summary>
        public int ComparisonCount { get; }
    }

    /// <summary>Identifies a public traversal operation used by exception-safety tests.</summary>
    public enum SearchOperation
    {
        /// <summary>A membership lookup.</summary>
        Contains,
        /// <summary>A stored-item lookup.</summary>
        TryGet,
        /// <summary>An insertion lookup.</summary>
        Add,
        /// <summary>A removal lookup.</summary>
        Remove
    }

    /// <summary>Comparer that throws its stable exception at a configured comparison count.</summary>
    private sealed class ControlledComparer : IComparer<int>
    {
        /// <summary>Gets the number of comparisons performed.</summary>
        public int ComparisonCount { get; private set; }

        /// <summary>Gets or sets the absolute comparison number at which to throw.</summary>
        public int? ThrowOnComparisonNumber { get; set; }

        /// <summary>Gets the stable exception propagated by the comparer.</summary>
        public ComparerTestException Exception { get; } = new("Controlled comparer failure.");

        /// <inheritdoc />
        public int Compare(int x, int y)
        {
            ComparisonCount++;
            if (ComparisonCount == ThrowOnComparisonNumber)
            {
                throw Exception;
            }
            return x.CompareTo(y);
        }
    }

    /// <summary>Represents the deterministic exception supplied by the controlled comparer.</summary>
    private sealed class ComparerTestException : Exception
    {
        /// <summary>Initializes the test exception.</summary>
        /// <param name="message">The diagnostic message.</param>
        public ComparerTestException(string message) : base(message) { }
    }
}
