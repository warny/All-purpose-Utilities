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
        ControlledComparer comparer = new();
        SkipList<int> list = new(comparer, 2);
        foreach (int value in Enumerable.Range(0, 120))
        {
            Assert.IsTrue(list.Add(value));
        }
        list.ValidateInvariants();
        string before = list.GetStructureSignature();
        int[] contentBefore = list.ToArray();
        int countBefore = list.Count;
        comparer.ThrowOnComparisonNumber = comparer.ComparisonCount + 4;

        ComparerTestException exception = Assert.ThrowsException<ComparerTestException>(() => Execute(operation, list));

        Assert.AreSame(comparer.Exception, exception);
        comparer.ThrowOnComparisonNumber = null;
        Assert.AreEqual(before, list.GetStructureSignature());
        Assert.AreEqual(countBefore, list.Count);
        CollectionAssert.AreEqual(contentBefore, list.ToArray());
        list.ValidateInvariants();
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
    private static void Execute(SearchOperation operation, SkipList<int> list)
    {
        switch (operation)
        {
            case SearchOperation.Contains:
                list.Contains(119);
                break;
            case SearchOperation.TryGet:
                list.TryGet(119, out _);
                break;
            case SearchOperation.Add:
                list.Add(121);
                break;
            case SearchOperation.Remove:
                list.Remove(119);
                break;
        }
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
