using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Linq;
using Utils.Network;

namespace UtilsTest.Net;

[TestClass]
public class IpRangeTests
{
    [TestMethod]
    public void ConstructorWithTwoAddressesComputesMask()
    {
        var start = IPAddress.Parse("192.168.1.0");
        var end = IPAddress.Parse("192.168.1.255");

        var range = new IpRange(start, end);

        Assert.AreEqual(start, range.Start);
        Assert.AreEqual(end, range.End);
        Assert.AreEqual(24, range.Mask);
    }

    [TestMethod]
    public void ConstructorWithTwoAddressesNoMask()
    {
        var range = new IpRange(IPAddress.Parse("192.168.1.10"), IPAddress.Parse("192.168.1.20"));
        Assert.IsNull(range.Mask);
    }

    [TestMethod]
    public void ConstructorWithPrefix()
    {
        var range = new IpRange(IPAddress.Parse("10.0.0.0"), 16);
        Assert.AreEqual(IPAddress.Parse("10.0.0.0"), range.Start);
        Assert.AreEqual(IPAddress.Parse("10.0.255.255"), range.End);
        Assert.AreEqual(16, range.Mask);
    }

    [TestMethod]
    public void ParseHyphenNotation()
    {
        var range = IpRange.Parse("192.168.0.0-192.168.0.255");
        Assert.AreEqual(24, range.Mask);
    }

    [TestMethod]
    public void ParseCidrNotation()
    {
        var range = IpRange.Parse("192.168.2.0/24");
        Assert.AreEqual(IPAddress.Parse("192.168.2.255"), range.End);
    }

    [TestMethod]
    public void TryParseSucceeds()
    {
        bool ok = IpRange.TryParse("192.168.3.0/24", null, out var range);
        Assert.IsTrue(ok);
        Assert.AreEqual(24, range.Mask);
    }

    [TestMethod]
    public void EnumeratesAllAddresses()
    {
        var range = new IpRange(IPAddress.Parse("192.168.1.1"), IPAddress.Parse("192.168.1.3"));
        var list = range.ToList();
        var expected = new[]
        {
            IPAddress.Parse("192.168.1.1"),
            IPAddress.Parse("192.168.1.2"),
            IPAddress.Parse("192.168.1.3")
        };
        CollectionAssert.AreEqual(expected, list);
    }

    [TestMethod]
    public void ContainsReturnsTrueForAddressInsideRange()
    {
        var range = new IpRange(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("192.168.0.255"));
        Assert.IsTrue(range.Contains(IPAddress.Parse("192.168.0.10")));
    }

    [TestMethod]
    public void ContainsReturnsFalseForAddressOutsideRange()
    {
        var range = new IpRange(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("192.168.0.10"));
        Assert.IsFalse(range.Contains(IPAddress.Parse("192.168.1.1")));
    }

    [TestMethod]
    public void EqualityMembersCompareStartAndEnd()
    {
        var r1 = new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"));
        var r2 = new IpRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"));

        Assert.IsTrue(r1.Equals(r2));
        Assert.IsTrue(r1 == r2);
        Assert.AreEqual(r1.HashCode, r2.HashCode);
        Assert.IsFalse(r1 != r2);
    }

    [TestMethod]
    public void StaticPrivateRangesAreCorrect()
    {
        Assert.AreEqual(IPAddress.Parse("10.0.0.0"), IpRange.Private10.Start);
        Assert.AreEqual(IPAddress.Parse("10.255.255.255"), IpRange.Private10.End);

        Assert.AreEqual(IPAddress.Parse("172.16.0.0"), IpRange.Private172.Start);
        Assert.AreEqual(IPAddress.Parse("172.31.255.255"), IpRange.Private172.End);

        Assert.AreEqual(IPAddress.Parse("192.168.0.0"), IpRange.Private192.Start);
        Assert.AreEqual(IPAddress.Parse("192.168.255.255"), IpRange.Private192.End);
    }

    [TestMethod]
    public void LoopbackRangeContainsLocalhost()
    {
        Assert.IsTrue(IpRange.Loopback.Contains(IPAddress.Loopback));
        Assert.IsTrue(IpRange.IPv6Loopback.Contains(IPAddress.IPv6Loopback));
    }
}
