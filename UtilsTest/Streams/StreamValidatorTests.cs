using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.IO;
using Utils.Collections;

namespace UtilsTest.Streams;

[TestClass]
public class StreamValidatorTests
{
    [TestMethod]
    public void ValidateWritesBufferedData()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        byte[] data = { 1, 2, 3, 4 };
        validator.Write(data, 0, data.Length);
        Assert.AreEqual(0, target.Length);
        validator.Validate();
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(data, target.ToArray(), comparer);
    }

    [TestMethod]
    public void DiscardClearsBuffer()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        validator.Write(new byte[] { 1, 2, 3 }, 0, 3);
        validator.Discard();
        validator.Validate();
        Assert.AreEqual(0, target.Length);
    }

    [TestMethod]
    public void MultipleValidationsAppendData()
    {
        using MemoryStream target = new MemoryStream();
        StreamValidator validator = new StreamValidator(target);
        byte[] data1 = { 1, 2 };
        byte[] data2 = { 3, 4 };
        validator.Write(data1, 0, data1.Length);
        validator.Validate();
        validator.Write(data2, 0, data2.Length);
        validator.Validate();
        var expected = new byte[] { 1, 2, 3, 4 };
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(expected, target.ToArray(), comparer);
    }
}
