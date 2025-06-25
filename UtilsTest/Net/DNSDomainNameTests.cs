using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;

namespace UtilsTest.Net;

[TestClass]
public class DNSDomainNameTests
{
    [TestMethod]
    public void AppendConcatenatesLabels()
    {
        var domain = new DNSDomainName("www");
        var combined = domain.Append("example.com");
        Assert.AreEqual("www.example.com", combined.Value);
    }

    [TestMethod]
    public void EqualityAgainstStringAndStruct()
    {
        var d1 = new DNSDomainName("mail.example.com");
        var d2 = new DNSDomainName("mail.example.com");

        Assert.IsTrue(d1.Equals("mail.example.com"));
        Assert.IsTrue(d1.Equals(d2));
        Assert.AreEqual(d1.GetHashCode(), d2.GetHashCode());
    }
}
