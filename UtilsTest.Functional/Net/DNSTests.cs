using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Net;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net
{
    /// <summary>
    /// Tests DNS lookups that require external DNS infrastructure.
    /// </summary>
    [TestClass]
    public class DNSTests
    {
        [TestMethod]
        [Ignore]
        public void SendDNSRequest()
        {
            DNSLookup lookup = new DNSLookup();
            var header = lookup.Request("ALL", "gmail.com");
            Assert.AreEqual(DNSError.Ok, header.ErrorCode);

            var dnsRequestRecord = header.Requests[0];
            Assert.AreEqual("gmail.com", dnsRequestRecord.Name.Value);
            Assert.AreEqual(DNSClassId.ALL, dnsRequestRecord.Class);
            Assert.AreEqual("ALL", dnsRequestRecord.Type);

            Assert.IsTrue(header.Responses.Count > 0, "No response from DNS");
            Assert.IsTrue(header.Responses.Any(r => r.RData is Address), "No A record returned from DNS");
            Assert.IsTrue(header.Responses.Any(r => r.RData is MX), "No MX record returned from DNS");
        }
    }
}
