using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Xml.Linq;
using Utils.Net;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC2052;
using Utils.Objects;

namespace UtilsTest.Net
{
	[TestClass]
	public class DNSTests
	{
		[TestMethod]
		public void ReadALLResponse1()
		{
			byte[] datagram = [
				0x1B, 0x0F, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x05, 0x67, 0x6D, 0x61,
				0x69, 0x6C, 0x03, 0x63, 0x6F, 0x6D, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0xC0, 0x0C, 0x00, 0x06, 0x00,
				0x01, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x2D, 0x03, 0x6E, 0x73, 0x31, 0x06, 0x67, 0x6F, 0x6F, 0x67,
				0x6C, 0x65, 0xC0, 0x12, 0x09, 0x64, 0x6E, 0x73, 0x2D, 0x61, 0x64, 0x6D, 0x69, 0x6E, 0xC0, 0x2B,
				0x13, 0x4C, 0x10, 0xD3, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x07, 0x08,
				0x00, 0x00, 0x00, 0x3C
			];

			var DNSReader = DNSPacketReader.Default;
			DNSHeader header = DNSReader.Read(datagram);

            var dnsRequestRecord = header.Requests[0];
            Assert.AreEqual("gmail.com", dnsRequestRecord.Name.Value);
            Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
            Assert.AreEqual("ALL", dnsRequestRecord.Type);

            Assert.IsTrue(header.Responses.Count == 1);
            var dnsResponse = header.Responses[0];

            var SOARecord = (SOA)dnsResponse.RData;
            Assert.AreEqual("gmail.com", dnsResponse.Name.Value);
            Assert.AreEqual("ns1.google.com", SOARecord.MName.Value);
            Assert.AreEqual("dns-admin.google.com", SOARecord.RName.Value);
            Assert.AreEqual(323752147U, SOARecord.Serial);
            Assert.AreEqual(900U, SOARecord.Retry);
            Assert.AreEqual(1800U, SOARecord.Expire);
            Assert.AreEqual(60U, SOARecord.Minimum);

        }

        [TestMethod]
		public void WriteReadRequest()
		{
            var comparer = DNSElementsComparer.Default;

            var packetReader = DNSPacketReader.Default;
			var packetWriter = DNSPacketWriter.Default;
            
			DNSHeader header1 = new DNSHeader();
			header1.Requests.Add(new DNSRequestRecord("A", "google.fr"));
			var datagram = packetWriter.Write(header1);
            DNSHeader header2 = packetReader.Read(datagram);

			Assert.AreEqual(header1, header2, comparer);
			for (int i = 0; i < header1.Requests.Count; i++)
			{
				Assert.AreEqual(header1.Requests[i], header2.Requests[i], comparer);
			}
        }

        [TestMethod]
		public void WriteReadResponse()
		{
			var DNSWriter = DNSPacketWriter.Default;
            var DNSReader = DNSPacketReader.Default;

            DNSHeader header1 = new DNSHeader();
			header1.Requests.Add(new DNSRequestRecord("ALL", "example.com"));
			header1.QrBit = DNSQRBit.Response;
			header1.Responses.Add(new DNSResponseRecord("example.com", 300, new Address() { IPAddress = new IPAddress(new byte[] { 1, 2, 3, 4 }) }));
			header1.Responses.Add(new DNSResponseRecord("example.com", 300, new Address() { IPAddress = new IPAddress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }) }));
			header1.Responses.Add(new DNSResponseRecord("example.com", 300, new MX() { Preference = 5, Exchange = "mail.google.fr" }));
			header1.Responses.Add(new DNSResponseRecord("example.com", 300, new SRV() { Priority = 5, Weight = 15, Server = "service.mail.google.fr", Port = 32534 }));
			header1.Responses.Add(new DNSResponseRecord("example.com", 300, new TXT() { Text = "Ceci est un test" }));

			var datagram = DNSWriter.Write(header1);

            DNSHeader header2 = DNSReader.Read(datagram);

			var comparer = DNSHeadersComparer.Default;
			Assert.AreEqual(header1, header2, comparer);
		}

		[TestMethod]
		public void BuildDNSRequest()
		{
            var packetWriter = DNSPacketWriter.Default;
            
			byte[] dnsRequestBytes =
            [
                //Header section
                0x00, 0x01, // Transaction ID: 1
				0x01, 0x00, // Standard query, Recursion desired
				0x00, 0x01, // Number of questions: 1
				0x00, 0x00, // Number of answer resource records: 0
				0x00, 0x00, // Number of authority resource records: 0
				0x00, 0x00, // Number of additional resource records: 0

				
				// Question QNAME (variable length)
				0x05, 0x67, 0x6d, 0x61, 0x69, 0x6c, // "gmail" in ASCII
				0x03, 0x63, 0x6f, 0x6d, // "com" in ASCII
				0x00, // Null-terminated
				// Question QTYPE (2 bytes)
				0x00, 0xff, // Type: ALL (255)
				// Question QCLASS (2 bytes)
				0x00, 0xFF // Class: ALL
			];

			DNSHeader request = new DNSHeader();
			request.RecursionDesired = true;
			request.Requests.Add(new DNSRequestRecord("ALL", "gmail.com"));

			var constructedRequestBytes = packetWriter.Write(request);

			// alignement du numéro de la demande
			dnsRequestBytes[0] = constructedRequestBytes[0];
			dnsRequestBytes[1] = constructedRequestBytes[1];


			Assert.AreEqual((Bytes)dnsRequestBytes, (Bytes)constructedRequestBytes);
		}


		[TestMethod]
		[Ignore]
		public void SendDNSRequest()
		{
			DNSLookup lookup = new DNSLookup();
			var header = lookup.Request("ALL", "gmail.com");
			Assert.AreEqual(DNSError.Ok, header.ErrorCode);

			var dnsRequestRecord = header.Requests[0];
			Assert.AreEqual("gmail.com", dnsRequestRecord.Name.Value);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("ALL", dnsRequestRecord.Type);

			Assert.IsTrue(header.Responses.Count > 0, "No response from DNS");
			Assert.IsTrue(header.Responses.Any(r => r.RData is Address), "No A record returned from DNS");
			Assert.IsTrue(header.Responses.Any(r => r.RData is MX), "No MX record returned from DNS");
		}

    }
}
