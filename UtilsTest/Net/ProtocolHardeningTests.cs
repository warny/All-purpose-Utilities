using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>Tests strict protocol models introduced for the pass-seven fixes.</summary>
[TestClass]
public class ProtocolHardeningTests
{
    /// <summary>Verifies supported ASCII mailbox and address-literal forms.</summary>
    [TestMethod]
    public void SmtpPath_Parse_AcceptsSupportedAsciiForms()
    {
        Assert.AreEqual("user@example.com", SmtpPath.Parse("user@example.com").Value);
        Assert.AreEqual("user@[192.0.2.1]", SmtpPath.Parse("user@[192.0.2.1]").Value);
        Assert.AreEqual("user@[IPv6:2001:db8::1]", SmtpPath.Parse("user@[IPv6:2001:db8::1]").Value);
        Assert.AreEqual(string.Empty, SmtpPath.Parse(string.Empty).Value);
    }
}
