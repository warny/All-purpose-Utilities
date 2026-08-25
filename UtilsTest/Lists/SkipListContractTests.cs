using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;

namespace UtilsTest.Lists;

/// <summary>
/// Verifies collection-copy, comparer, live-view, and exact adaptive-topology contracts.
/// </summary>
[TestClass]
public class SkipListContractTests
{
    /// <summary>
    /// Verifies every copy surface validates all arguments before changing the destination.
    /// </summary>
    [TestMethod]
    public void CopyTo_InvalidArguments_DoNotPartiallyWriteOnAnySurface()
    {
        SkipList<int> list = new(2) { 1, 2, 3 };
        AssertCopyPreflight(list.Count, list.CopyTo, -1);

        SkipListDictionary<int, string> dictionary = new(2)
        {
            [1] = "one",
            [2] = "two",
            [3] = "three"
        };
        AssertCopyPreflight(dictionary.Count, dictionary.CopyTo, new KeyValuePair<int, string>(-1, "unchanged"));
        AssertCopyPreflight(dictionary.Keys.Count, dictionary.Keys.CopyTo, -1);
        AssertCopyPreflight(dictionary.Values.Count, dictionary.Values.CopyTo, "unchanged");
    }

    /// <summary>
    /// Verifies successful copies at an offset and the empty-collection end-index boundary.
    /// </summary>
    [TestMethod]
    public void CopyTo_ValidBoundaries_CopyInSortedOrder()
    {
        SkipList<int> list = new(2) { 3, 1, 2 };
        int[] destination = [-1, -1, -1, -1, -1];
        list.CopyTo(destination, 1);
        CollectionAssert.AreEqual(new[] { -1, 1, 2, 3, -1 }, destination);

        SkipList<int> empty = new();
        empty.CopyTo(destination, destination.Length);

        SkipListDictionary<int, string> dictionary = new() { [2] = "two", [1] = "one" };
        KeyValuePair<int, string>[] entries = new KeyValuePair<int, string>[3];
        dictionary.CopyTo(entries, 1);
        CollectionAssert.AreEqual(new[] { 1, 2 }, entries.Skip(1).Select(entry => entry.Key).ToArray());

        string[] values = ["before", "", "", "after"];
        dictionary.Values.CopyTo(values, 1);
        CollectionAssert.AreEqual(new[] { "before", "one", "two", "after" }, values);
    }

    /// <summary>
    /// Verifies custom comparers are exposed by identity and default comparers are exposed directly.
    /// </summary>
    [TestMethod]
    public void Comparer_ExposesConfiguredOrDefaultInstance()
    {
        IComparer<string> custom = StringComparer.OrdinalIgnoreCase;

        Assert.AreSame(custom, new SkipList<string>(custom).Comparer);
        Assert.AreSame(custom, new SkipListDictionary<string, int>(custom).Comparer);
        Assert.AreSame(Comparer<int>.Default, new SkipList<int>().Comparer);
        Assert.AreSame(Comparer<int>.Default, new SkipListDictionary<int, string>().Comparer);
    }

    /// <summary>
    /// Verifies dictionary key and value views have stable identity and remain live across mutations.
    /// </summary>
    [TestMethod]
    public void KeysAndValues_AreStableLiveReadOnlyViews()
    {
        SkipListDictionary<int, string> dictionary = new();
        ICollection<int> keys = dictionary.Keys;
        ICollection<string> values = dictionary.Values;

        Assert.AreSame(keys, dictionary.Keys);
        Assert.AreSame(values, dictionary.Values);
        Assert.IsTrue(keys.IsReadOnly);
        Assert.IsTrue(values.IsReadOnly);

        dictionary.Add(2, "two");
        dictionary[1] = "one";
        CollectionAssert.AreEqual(new[] { 1, 2 }, keys.ToArray());
        CollectionAssert.AreEqual(new[] { "one", "two" }, values.ToArray());

        dictionary[2] = "TWO";
        Assert.IsTrue(dictionary.Remove(1));
        CollectionAssert.AreEqual(new[] { 2 }, keys.ToArray());
        CollectionAssert.AreEqual(new[] { "TWO" }, values.ToArray());
    }

    /// <summary>
    /// Verifies exact insertion and lookup-driven topology for each supported threshold scenario.
    /// </summary>
    /// <param name="threshold">The traversal count that must be exceeded before promotion eligibility.</param>
    /// <param name="insertionSignature">The expected signature after insertion.</param>
    /// <param name="lookupSignature">The expected signature after deterministic lookups.</param>
    [DataTestMethod]
    [DataRow(2, "0,3,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", "0,9,23|0,3,6,9,12,15,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23")]
    [DataRow(3, "0,4,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", "0,16,23|0,4,8,12,16,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23")]
    [DataRow(10, "0,11,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23", "0,11,23|0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23")]
    public void Threshold_ProducesExactDeterministicTopology(int threshold, string insertionSignature, string lookupSignature)
    {
        SkipList<int> list = new(threshold);
        foreach (int value in Enumerable.Range(0, 24))
        {
            Assert.IsTrue(list.Add(value));
        }
        list.ValidateInvariants();
        Assert.AreEqual(insertionSignature, list.GetStructureSignature());

        foreach (int value in new[] { 23, 17, 11, 5, 23, 17, 11, 5 })
        {
            Assert.IsTrue(list.Contains(value));
        }
        list.ValidateInvariants();
        Assert.AreEqual(lookupSignature, list.GetStructureSignature());
    }

    /// <summary>
    /// Asserts BCL-compatible failures and unchanged prefilled destinations for one copy surface.
    /// </summary>
    /// <typeparam name="T">The copied element type.</typeparam>
    /// <param name="count">The source collection count.</param>
    /// <param name="copy">The copy operation under test.</param>
    /// <param name="sentinel">The value used to prefill and verify the destination.</param>
    private static void AssertCopyPreflight<T>(int count, Action<T[], int> copy, T sentinel)
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => copy(null!, 0));

        T[] negativeIndex = Enumerable.Repeat(sentinel, count + 1).ToArray();
        T[] negativeSnapshot = negativeIndex.ToArray();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => copy(negativeIndex, -1));
        CollectionAssert.AreEqual(negativeSnapshot, negativeIndex);

        T[] insufficient = Enumerable.Repeat(sentinel, count).ToArray();
        T[] insufficientSnapshot = insufficient.ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => copy(insufficient, 1));
        CollectionAssert.AreEqual(insufficientSnapshot, insufficient);

        T[] indexPastEnd = Enumerable.Repeat(sentinel, count).ToArray();
        T[] indexPastEndSnapshot = indexPastEnd.ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => copy(indexPastEnd, indexPastEnd.Length + 1));
        CollectionAssert.AreEqual(indexPastEndSnapshot, indexPastEnd);
    }
}
