using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Xml.Linq;
using Utils.Net;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1886;
using Utils.Net.DNS.RFC2052;
using Utils.Objects;

namespace UtilsTest.Net
{
	[TestClass]
	public class DNSTests
	{
		[TestMethod]
		public void ReadALLReponse1()
		{
			var datagram = new byte[] {
				0x1B, 0x0F, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x05, 0x67, 0x6D, 0x61,
				0x69, 0x6C, 0x03, 0x63, 0x6F, 0x6D, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0xC0, 0x0C, 0x00, 0x06, 0x00,
				0x01, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x2D, 0x03, 0x6E, 0x73, 0x31, 0x06, 0x67, 0x6F, 0x6F, 0x67,
				0x6C, 0x65, 0xC0, 0x12, 0x09, 0x64, 0x6E, 0x73, 0x2D, 0x61, 0x64, 0x6D, 0x69, 0x6E, 0xC0, 0x2B,
				0x13, 0x4C, 0x10, 0xD3, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x07, 0x08,
				0x00, 0x00, 0x00, 0x3C
			};

			DNSHeader header = new DNSHeader(datagram);

			var dnsRequestRecord = header.Requests[0];
			Assert.AreEqual("gmail.com", dnsRequestRecord.Name);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("ALL", dnsRequestRecord.Type);

			Assert.IsTrue(header.Responses.Count == 1);
			var dnsResponse = header.Responses[0];

			var SOARecord = (SOA)dnsResponse.RData;
			Assert.AreEqual("gmail.com", dnsResponse.Name);
			Assert.AreEqual("ns1.google.com", SOARecord.MName);
			Assert.AreEqual("dns-admin.google.com", SOARecord.RName);
			Assert.AreEqual(323752147U, SOARecord.Serial);
			Assert.AreEqual(900U, SOARecord.Retry);
			Assert.AreEqual(1800U, SOARecord.Expire);
			Assert.AreEqual(60U, SOARecord.Minimum);

		}

		[TestMethod]
		public void WriteReadRequest()
		{
			DNSHeader header1 = new DNSHeader();
			header1.Requests.Add(new DNSRequestRecord("A", "google.fr"));
			var datagram = header1.ToByteArray();
			var header2 = new DNSHeader(datagram);

			Assert.AreEqual(header1.QrBit, header2.QrBit);
			Assert.AreEqual(header1.RecursionDesired, header2.RecursionDesired);
			Assert.AreEqual(header1.RecursionPossible, header2.RecursionPossible);
			Assert.AreEqual(header1.ErrorCode, header2.ErrorCode);
			Assert.AreEqual(header1.OpCode, header2.OpCode);
			Assert.AreEqual(header1.Requests[0].Name, header2.Requests[0].Name);
			Assert.AreEqual(header1.Requests[0].Type, header2.Requests[0].Type);
			Assert.AreEqual(header1.Requests[0].Class, header2.Requests[0].Class);
			Assert.AreEqual(header1.Requests[0].DNSType, header2.Requests[0].DNSType);
		}

		[TestMethod]
		public void WriteReadResponse()
		{
			DNSHeader header1 = new DNSHeader();
			header1.Requests.Add(new DNSRequestRecord("ALL", "google.fr"));
			header1.QrBit = DNSQRBit.Response;
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new A() { IPAddress = new IPAddress(new byte[] { 1, 2, 3, 4 }) }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new AAAA() { IPAddress = new IPAddress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }) }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new MX() { Preference = 5, Exchange = "mail.google.fr" }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new SRV() { Priority = 5, Weight = 15, Server = "service.mail.google.fr", Port = 32534 }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new TXT() { Text = "Ceci est un test" }));
			var datagram = header1.ToByteArray();
			var header2 = new DNSHeader(datagram);

			Assert.AreEqual(header1.QrBit, header2.QrBit);
			Assert.AreEqual(header1.RecursionDesired, header2.RecursionDesired);
			Assert.AreEqual(header1.RecursionPossible, header2.RecursionPossible);
			Assert.AreEqual(header1.ErrorCode, header2.ErrorCode);
			Assert.AreEqual(header1.OpCode, header2.OpCode);
			Assert.AreEqual(header1.Requests[0].Name, header2.Requests[0].Name);
			Assert.AreEqual(header1.Requests[0].Type, header2.Requests[0].Type);
			Assert.AreEqual(header1.Requests[0].Class, header2.Requests[0].Class);
			Assert.AreEqual(header1.Requests[0].DNSType, header2.Requests[0].DNSType);

			for (int i = 0; i < header1.Responses.Count; i++)
			{
				var response1 = header1.Responses[i];
				var response2 = header2.Responses[i];

				Assert.AreEqual(response1.Name, response2.Name);
				Assert.AreEqual(response1.TTL, response2.TTL);
				Assert.AreEqual(response1.RData.ToString(), response2.RData.ToString());
			}
		}

		[TestMethod]
		public void BuildDNSRequest()
		{
			byte[] dnsRequestBytes = new byte[]
			{
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
			};

			DNSHeader request = new DNSHeader();
			request.RecursionDesired = true;
			request.Requests.Add(new DNSRequestRecord("ALL", "gmail.com"));

			var constructedRequestBytes = request.ToByteArray();

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
			Assert.AreEqual("gmail.com", dnsRequestRecord.Name);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("ALL", dnsRequestRecord.Type);

			Assert.IsTrue(header.Responses.Count > 0, "No response from DNS");
			Assert.IsTrue(header.Responses.Any(r => r.RData is A), "No A record returned from DNS");
			Assert.IsTrue(header.Responses.Any(r => r.RData is MX), "No MX record returned from DNS");
		}

    }
}
