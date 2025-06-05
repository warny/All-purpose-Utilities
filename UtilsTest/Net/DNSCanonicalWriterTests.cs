using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net
{
    [TestClass]
    public class DNSCanonicalWriterTests
    {
        [TestMethod]
        public void CanonicalWriterProducesLowercaseNamesWithoutCompression()
        {
            // Build a DNS header with repeated names using various casing
            DNSHeader header = new DNSHeader();
            header.Requests.Add(new DNSRequestRecord("A", "EXAMPLE.COM"));
            header.QrBit = DNSQRBit.Response;
            header.Responses.Add(new DNSResponseRecord("EXAMPLE.COM", 300, new Address { IPAddress = IPAddress.Parse("1.2.3.4") }));
            header.Responses.Add(new DNSResponseRecord("ExAmPlE.CoM", 300, new Address { IPAddress = IPAddress.Parse("5.6.7.8") }));

            // Write using the canonical writer
            byte[] canonicalBytes = DNSCanonicalWriter.Default.Write(header);

            // The canonical format should never contain compression pointers (0xC0)
            CollectionAssert.DoesNotContain(canonicalBytes, (byte)0xC0);

            // Reading back should yield lower case domain names
            DNSHeader decoded = DNSPacketReader.Default.Read(canonicalBytes);
            Assert.AreEqual("example.com", decoded.Requests[0].Name.Value);
            Assert.AreEqual("example.com", decoded.Responses[0].Name.Value);
            Assert.AreEqual("example.com", decoded.Responses[1].Name.Value);
        }
    }
}
