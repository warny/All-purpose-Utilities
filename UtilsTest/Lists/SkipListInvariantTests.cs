using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;

namespace UtilsTest.Lists;

/// <summary>
/// Exercises the internal structural invariant validator for <see cref="SkipList{T}"/>.
/// </summary>
[TestClass]
public class SkipListInvariantTests
{
    /// <summary>
    /// Verifies the empty, single-element, removed, and cleared states.
    /// </summary>
    [TestMethod]
    public void ValidateInvariants_EmptySingleRemoveAndClear_Succeeds()
    {
        SkipList<int> list = new(2);
        list.ValidateInvariants();

        Assert.IsTrue(list.Add(42));
        list.ValidateInvariants();
        Assert.IsTrue(list.Remove(42));
        list.ValidateInvariants();

        foreach (int value in Enumerable.Range(0, 40))
        {
            Assert.IsTrue(list.Add(value));
        }
        list.ValidateInvariants();
        list.Clear();
        list.ValidateInvariants();
        Assert.AreEqual(0, list.Count);
    }

    /// <summary>
    /// Verifies every insertion in ascending, descending, and mixed deterministic sequences.
    /// </summary>
    /// <param name="threshold">The adaptive promotion threshold to exercise.</param>
    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(10)]
    public void ValidateInvariants_AfterEachInsertion_Succeeds(int threshold)
    {
        ValidateInsertionSequence(new SkipList<int>(threshold), Enumerable.Range(0, 80));
        ValidateInsertionSequence(new SkipList<int>(threshold), Enumerable.Range(0, 80).Reverse());
        ValidateInsertionSequence(new SkipList<int>(threshold),
            [37, 4, 71, 12, 55, 1, 79, 23, 48, 9, 63, 31, 16, 68, 42, 6, 75, 27, 51, 19]);
    }

    /// <summary>
    /// Verifies structural integrity while removing left, right, and middle elements.
    /// </summary>
    [TestMethod]
    public void ValidateInvariants_AfterBoundaryAndMiddleRemovals_Succeeds()
    {
        ValidateRemovalSequence(Enumerable.Range(0, 80));
        ValidateRemovalSequence(Enumerable.Range(0, 80).Reverse());
        ValidateRemovalSequence(Enumerable.Range(0, 80).OrderBy(value => value % 2).ThenBy(value => value));
    }

    /// <summary>
    /// Verifies rejected duplicates preserve a valid structure under default and custom comparers.
    /// </summary>
    [TestMethod]
    public void ValidateInvariants_AfterDuplicateAttempts_Succeeds()
    {
        SkipList<int> integers = new(2);
        Assert.IsTrue(integers.Add(7));
        Assert.IsFalse(integers.Add(7));
        integers.ValidateInvariants();
        Assert.AreEqual(1, integers.Count);

        SkipList<string> strings = new(StringComparer.OrdinalIgnoreCase, 2);
        Assert.IsTrue(strings.Add("alpha"));
        Assert.IsFalse(strings.Add("ALPHA"));
        strings.ValidateInvariants();
        Assert.AreEqual(1, strings.Count);
        CollectionAssert.AreEqual(new[] { "alpha" }, strings.ToArray());
    }

    /// <summary>
    /// Verifies lookup-driven promotions from both <see cref="SkipList{T}.Contains"/> and
    /// <see cref="SkipList{T}.TryGet"/> preserve every structural invariant.
    /// </summary>
    [TestMethod]
    public void ValidateInvariants_AfterLookupDrivenPromotions_Succeeds()
    {
        SkipList<int> list = new(2);
        foreach (int value in Enumerable.Range(0, 120))
        {
            Assert.IsTrue(list.Add(value));
        }

        for (int pass = 0; pass < 4; pass++)
        {
            foreach (int value in Enumerable.Range(0, 120))
            {
                Assert.IsTrue(list.Contains(value));
                list.ValidateInvariants();
                Assert.IsTrue(list.TryGet(value, out int found));
                Assert.AreEqual(value, found);
                list.ValidateInvariants();
            }
        }
    }

    /// <summary>
    /// Compares a long, fixed-seed operation sequence with a <see cref="SortedSet{T}"/> model.
    /// </summary>
    [TestMethod]
    public void ValidateInvariants_DeterministicModelScenario_MatchesSortedSet()
    {
        const int seed = 54205;
        Random random = new(seed);
        SkipList<int> list = new(3);
        SortedSet<int> model = [];

        for (int operationIndex = 0; operationIndex < 2000; operationIndex++)
        {
            int value = random.Next(-250, 251);
            switch (random.Next(100))
            {
                case < 35:
                    Assert.AreEqual(model.Add(value), list.Add(value), $"Add mismatch at operation {operationIndex}.");
                    break;
                case < 60:
                    Assert.AreEqual(model.Remove(value), list.Remove(value), $"Remove mismatch at operation {operationIndex}.");
                    break;
                case < 78:
                    Assert.AreEqual(model.Contains(value), list.Contains(value), $"Contains mismatch at operation {operationIndex}.");
                    break;
                case < 96:
                    bool expected = model.TryGetValue(value, out int expectedValue);
                    Assert.AreEqual(expected, list.TryGet(value, out int actualValue), $"TryGet mismatch at operation {operationIndex}.");
                    if (expected)
                    {
                        Assert.AreEqual(expectedValue, actualValue);
                    }
                    break;
                default:
                    model.Clear();
                    list.Clear();
                    break;
            }

            list.ValidateInvariants();
            Assert.AreEqual(model.Count, list.Count, $"Count mismatch at operation {operationIndex}.");
            CollectionAssert.AreEqual(model.ToArray(), list.ToArray(), $"Sequence mismatch at operation {operationIndex}.");
        }
    }

    /// <summary>
    /// Adds a sequence and checks both structural invariants and the sorted reference model after each item.
    /// </summary>
    /// <param name="list">The skip list to populate.</param>
    /// <param name="values">The deterministic insertion sequence.</param>
    private static void ValidateInsertionSequence(SkipList<int> list, IEnumerable<int> values)
    {
        SortedSet<int> expected = [];
        foreach (int value in values)
        {
            Assert.AreEqual(expected.Add(value), list.Add(value));
            list.ValidateInvariants();
            Assert.AreEqual(expected.Count, list.Count);
            CollectionAssert.AreEqual(expected.ToArray(), list.ToArray());
        }
    }

    /// <summary>
    /// Builds a multi-level list and validates it after every removal in the supplied order.
    /// </summary>
    /// <param name="removalOrder">The deterministic order in which values are removed.</param>
    private static void ValidateRemovalSequence(IEnumerable<int> removalOrder)
    {
        int[] values = Enumerable.Range(0, 80).ToArray();
        SkipList<int> list = new(2);
        SortedSet<int> expected = new(values);
        foreach (int value in values)
        {
            Assert.IsTrue(list.Add(value));
        }
        list.ValidateInvariants();

        foreach (int value in removalOrder)
        {
            Assert.AreEqual(expected.Remove(value), list.Remove(value));
            list.ValidateInvariants();
            Assert.AreEqual(expected.Count, list.Count);
            CollectionAssert.AreEqual(expected.ToArray(), list.ToArray());
        }
    }
}
