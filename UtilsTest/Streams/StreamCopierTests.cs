using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.Collections;
using Utils.IO;
using Utils.Randomization;

namespace UtilsTest.Streams;

[TestClass]
public class StreamCopierTests
{
    /// <summary>
    /// Stream copier test
    /// </summary>
    [TestMethod]
    public void StreamTest1()
    {
        using MemoryStream target1 = new MemoryStream();
        using MemoryStream target2 = new MemoryStream();
        var r = new Random();
        byte[] reference = r.NextBytes(10, 20);

        StreamCopier copier = new StreamCopier(target1, target2);

        copier.Write(reference, 0, reference.Length);
        copier.Flush();

        Assert.AreEqual(reference.Length, target1.Length);
        Assert.AreEqual(reference.Length, target2.Length);
        Assert.AreEqual(reference.Length, target1.Position);
        Assert.AreEqual(reference.Length, target2.Position);

        byte[] test1 = target1.ToArray();
        byte[] test2 = target2.ToArray();

        var comparer = EnumerableEqualityComparer<byte>.Default;


        Assert.AreEqual(reference, test1, comparer);
        Assert.AreEqual(reference, test2, comparer);
    }
}
