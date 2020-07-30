using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Net;
using Utils.Net;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1886;
using Utils.Net.DNS.RFC2052;

namespace UtilsTest.Net
{
	[TestClass]
	public class DNSTest
	{
		[TestMethod]
		public void ReadAReponse()
		{
			var datagram = new byte[] { 0x3D, 0x20, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x03, 0x77, 0x77, 0x77, 0x06, 0x67, 0x6F, 0x6F, 0x67, 0x6C, 0x65, 0x02, 0x66, 0x72, 0x00, 0x00, 0x01, 0x00, 0xFF, 0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x04, 0x00, 0x04, 0xD8, 0x3A, 0xD6, 0x43 };

			DNSHeader header = new DNSHeader(datagram);

			var dnsRequestRecord = header.Requests[0];
			Assert.AreEqual("www.google.fr", dnsRequestRecord.Name);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("A", dnsRequestRecord.Type);

			Assert.IsTrue(header.Responses.Count == 1);
			var dnsResponse = header.Responses[0];
			var ARecord = (A)dnsResponse.RData;
			Assert.AreEqual("www.google.fr", dnsResponse.Name);
			Assert.AreEqual("216.58.214.67", ARecord.IPAddress.ToString());

		}

		[TestMethod]
		public void ReadALLReponse1()
		{
			var datagram = new byte[] { 0x1B, 0x0F, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x05, 0x67, 0x6D, 0x61, 0x69, 0x6C, 0x03, 0x63, 0x6F, 0x6D, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0xC0, 0x0C, 0x00, 0x06, 0x00, 0x01, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x2D, 0x03, 0x6E, 0x73, 0x31, 0x06, 0x67, 0x6F, 0x6F, 0x67, 0x6C, 0x65, 0xC0, 0x12, 0x09, 0x64, 0x6E, 0x73, 0x2D, 0x61, 0x64, 0x6D, 0x69, 0x6E, 0xC0, 0x2B, 0x13, 0x4C, 0x10, 0xD3, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x03, 0x84, 0x00, 0x00, 0x07, 0x08, 0x00, 0x00, 0x00, 0x3C };

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
		public void ReadMXReponse()
		{
			var datagram = new byte[] { 
				0x78, 0xF4, 0x81, 0x80, 0x00, 0x01, 0x00, 0x05, 0x00, 0x00, 0x00, 0x0A, 0x05, 0x67, 0x6D, 
				0x61, 0x69, 0x6C, 0x03, 0x63, 0x6F, 0x6D, 0x00, 0x00, 0x0F, 0x00, 0xFF, 0xC0, 0x0C, 0x00, 
				0x0F, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, 0x1B, 0x00, 0x05, 0x0D, 0x67, 0x6D, 0x61, 
				0x69, 0x6C, 0x2D, 0x73, 0x6D, 0x74, 0x70, 0x2D, 0x69, 0x6E, 0x01, 0x6C, 0x06, 0x67, 0x6F, 
				0x6F, 0x67, 0x6C, 0x65, 0xC0, 0x12, 0xC0, 0x0C, 0x00, 0x0F, 0x00, 0x01, 0x00, 0x00, 0x0E, 
				0x10, 0x00, 0x09, 0x00, 0x0A, 0x04, 0x61, 0x6C, 0x74, 0x31, 0xC0, 0x29, 0xC0, 0x0C, 0x00, 
				0x0F, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, 0x09, 0x00, 0x14, 0x04, 0x61, 0x6C, 0x74, 
				0x32, 0xC0, 0x29, 0xC0, 0x0C, 0x00, 0x0F, 0x00, 0x01, 0x00, 0x00, 0x0E, 0x10, 0x00, 0x09, 
				0x00, 0x1E, 0x04, 0x61, 0x6C, 0x74, 0x33, 0xC0, 0x29, 0xC0, 0x0C, 0x00, 0x0F, 0x00, 0x01, 
				0x00, 0x00, 0x0E, 0x10, 0x00, 0x09, 0x00, 0x28, 0x04, 0x61, 0x6C, 0x74, 0x34, 0xC0, 0x29, 
				0xC0, 0x29, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x04, 0xAD, 0xC2, 0x4C, 
				0x1B, 0xC0, 0x29, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x10, 0x2A, 0x00, 
				0x14, 0x50, 0x40, 0x0C, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1A, 0xC0, 
				0x50, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x04, 0xD1, 0x55, 0xE9, 0x1A, 
				0xC0, 0x50, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x10, 0x2A, 0x00, 0x14, 
				0x50, 0x40, 0x10, 0x0C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1B, 0xC0, 0x65, 
				0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0xC2, 0x00, 0x04, 0xAC, 0xFD, 0x76, 0x1B, 0xC0, 
				0x65, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x00, 0xC2, 0x00, 0x10, 0x24, 0x04, 0x68, 0x00, 
				0x40, 0x03, 0x0C, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1A, 0xC0, 0x7A, 0x00, 
				0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x04, 0x6C, 0xB1, 0x61, 0x1A, 0xC0, 0x7A, 
				0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x10, 0x24, 0x04, 0x68, 0x00, 0x40, 
				0x08, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1A, 0xC0, 0x8F, 0x00, 0x01, 
				0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x04, 0x8E, 0xFA, 0x6B, 0x1A, 0xC0, 0x8F, 0x00, 
				0x1C, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2B, 0x00, 0x10, 0x26, 0x07, 0xF8, 0xB0, 0x40, 0x0E, 
				0x0C, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1B };

			DNSHeader header = new DNSHeader(datagram);

			var dnsRequestRecord = header.Requests[0];
			Assert.AreEqual("gmail.com", dnsRequestRecord.Name);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("MX", dnsRequestRecord.Type);

			var expectedResponses = new (string Name, ushort Priority, string Exchange)[] {
				("gmail.com", 5 , "gmail-smtp-in.l.google.com"),
				("gmail.com", 10, "alt1.gmail-smtp-in.l.google.com"),
				("gmail.com", 20, "alt2.gmail-smtp-in.l.google.com"),
				("gmail.com", 30, "alt3.gmail-smtp-in.l.google.com"),
				("gmail.com", 40, "alt4.gmail-smtp-in.l.google.com")
			};
			for (int i = 0; i < 5; i++)
			{
				var expectedResponse = expectedResponses[i];
				var response = header.Responses[i];
				var MXResponse = (MX)response.RData;
				Assert.AreEqual(expectedResponse.Name, response.Name);
				Assert.AreEqual(expectedResponse.Priority, MXResponse.Preference);
				Assert.AreEqual(expectedResponse.Exchange, MXResponse.Exchange);
			}

			var expectedAdditionals = new (string Name, string Type, string Adress, uint TTL)[]{
				("gmail-smtp-in.l.google.com", "A", "173.194.76.27", 299),
				("gmail-smtp-in.l.google.com", "AAAA", "2a00:1450:400c:c00::1a", 299),
				("alt1.gmail-smtp-in.l.google.com", "A", "209.85.233.26", 299),
				("alt1.gmail-smtp-in.l.google.com", "AAAA", "2a00:1450:4010:c03::1b", 299),
				("alt2.gmail-smtp-in.l.google.com", "A", "172.253.118.27", 194),
				("alt2.gmail-smtp-in.l.google.com", "AAAA", "2404:6800:4003:c05::1a", 194),
				("alt3.gmail-smtp-in.l.google.com", "A", "108.177.97.26", 299),
				("alt3.gmail-smtp-in.l.google.com", "AAAA", "2404:6800:4008:c00::1a", 299),
				("alt4.gmail-smtp-in.l.google.com", "A", "142.250.107.26", 299),
				("alt4.gmail-smtp-in.l.google.com", "AAAA", "2607:f8b0:400e:c0d::1b", 299)
			};

			for (int i = 0; i < 10; i++)
			{
				var expectedAdditional = expectedAdditionals[i];
				var additionnal = header.Additionnals[i];
				Assert.AreEqual(expectedAdditional.Name, additionnal.Name);
				Assert.AreEqual(expectedAdditional.TTL, additionnal.TTL);
				Assert.AreEqual(expectedAdditional.Type, additionnal.RData.Name);
				Assert.AreEqual(expectedAdditional.Adress, additionnal.RData.ToString());
			}

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
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new A() { IPAddress = new IPAddress(new byte[]{ 1, 2, 3, 4 }) }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new AAAA() { IPAddress = new IPAddress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }) }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new MX() { Preference = 5, Exchange="mail.google.fr" }));
			header1.Responses.Add(new DNSResponseRecord("google.fr", 300, new SRV() { Priority = 5, Weigth=15, Server = "service.mail.google.fr", Port = 32534 }));
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
		public void SendDNSRequest()
		{
			DNSLookup lookup = new DNSLookup();
			var header = lookup.Request("ALL", "gmail.com");
			var dnsRequestRecord = header.Requests[0];
			Assert.AreEqual("gmail.com", dnsRequestRecord.Name);
			Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
			Assert.AreEqual("ALL", dnsRequestRecord.Type);
		}

	}
}
