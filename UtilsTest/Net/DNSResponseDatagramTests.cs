using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1876;

namespace UtilsTest.Net
{
    [TestClass]

    public class DNSResponseDatagramTests
    {
        [TestMethod]
        public void ReadMXReponse()
        {
            var datagram = new byte[] {
				// Transaction ID (2 bytes)
				0x78, 0xF4,

				// Flags (2 bytes)
				0x81, 0x80,

				// Number of Questions (2 bytes)
				0x00, 0x01,

				// Number of Answer Resource Records (2 bytes)
				0x00, 0x05,

				// Number of Authority Resource Records (2 bytes)
				0x00, 0x00,

				// Number of Additional Resource Records (2 bytes)
				0x00, 0x0A,

				// DNS Question (QNAME)
				0x05, 0x67, 0x6D, 0x61, 0x69, 0x6C, // "gmail" in ASCII
				0x03, 0x63, 0x6F, 0x6D, // "com" in ASCII
				0x00, // Null-terminated

				// DNS Question Type (2 bytes)
				0x00, 0xFF, // Type: ALL (255)

				// DNS Question Class (2 bytes)
				0xC0, 0x0C, // Class: IN (Internet)

				// Additional Resource Record 1
				0x00, 0x0F, // Type: 15 (Opt-Out)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x1B, // Data Length
				0x00, 0x05, // Data: 5 (Payload Size)
				0x0D, 0x67, 0x6D, 0x61, 0x69, 0x6C, 0x2D, 0x73, 0x6D, 0x74, 0x70, 0x2D, 0x69, 0x6E, // "gmail-smtpp-in" in ASCII

				// Additional Resource Record 2
				0x00, 0x0F, // Type: 15 (Opt-Out)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x09, // Data Length
				0x00, 0x0A, // Data: 10 (Payload Size)
				0x04, 0x61, 0x6C, 0x74, 0x31, // "alt1" in ASCII

				// Additional Resource Record 3
				0x00, 0x0F, // Type: 15 (Opt-Out)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x09, // Data Length
				0x00, 0x14, // Data: 20 (Payload Size)
				0x04, 0x61, 0x6C, 0x74, 0x32, // "alt2" in ASCII

				// Additional Resource Record 4
				0x00, 0x0F, // Type: 15 (Opt-Out)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x09, // Data Length
				0x00, 0x1E, // Data: 30 (Payload Size)
				0x04, 0x61, 0x6C, 0x74, 0x33, // "alt3" in ASCII

				// Additional Resource Record 5
				0x00, 0x0F, // Type: 15 (Opt-Out)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x09, // Data Length
				0x00, 0x28, // Data: 40 (Payload Size)
				0x04, 0x61, 0x6C, 0x74, 0x34, // "alt4" in ASCII

				// Additional Resource Record 6 (Root DNSKEY Record)
				0xC0, 0x29, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x04, // Data Length
				0xAD, 0xC2, 0x4C, 0x1B, // Data

				// Additional Resource Record 7 (Root DS Record)
				0xC0, 0x29, // Type: DS (43)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x10, // Data Length
				0x2A, 0x00, 0x14, 0x50, 0x40, 0x0C, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Data

				// Additional Resource Record 8 (. DNSKEY Record)
				0xC0, 0x50, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x04, // Data Length
				0xD1, 0x55, 0xE9, 0x1A, // Data

				// Additional Resource Record 9 (. DS Record)
				0xC0, 0x50, // Type: DS (43)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				 0x00, 0x10, // Data Length
				0x2A, 0x00, 0x14, 0x50, 0x40, 0x10, 0x0C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Data

				// Additional Resource Record 10 (Root DNSKEY Record)
				0xC0, 0x65, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x00, 0xC2, // TTL
				0x00, 0x04, // Data Length
				0xAC, 0xFD, 0x76, 0x1B, // Data

				// Additional Resource Record 11 (Root DS Record)
				0xC0, 0x65, // Type: DS (43)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x00, 0xC2, // TTL
				0x00, 0x10, // Data Length
				0x24, 0x04, 0x68, 0x00, 0x40, 0x03, 0x0C, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Data

				// Additional Resource Record 12 (. DNSKEY Record)
				0xC0, 0x7A, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x04, // Data Length
				0x6C, 0xB1, 0x61, 0x1A, // Data

				// Additional Resource Record 13 (. DS Record)
				0xC0, 0x7A, // Type: DS (43)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x10, // Data Length
				0x24, 0x04, 0x68, 0x00, 0x40, 0x08, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Data

				// Additional Resource Record 14 (Root DNSKEY Record)
				0xC0, 0x8F, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x04, // Data Length
				0x8E, 0xFA, 0x6B, 0x1A, // Data

				// Additional Resource Record 15 (Root DS Record)
				0xC0, 0x8F, // Type: DS (43)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x01, 0x2B, // TTL
				0x00, 0x10, // Data Length
				0x26, 0x07, 0xF8, 0xB0, 0x40, 0x0E, 0x0C, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Data

				// Additional Resource Record 16 (. DNSKEY Record)
				0xC0, 0x65, // Type: DNSKEY (257)
				0x00, 0x01, // Class: 1 (Internet)
				0x00, 0x00, 0x00, 0xC2, // TTL
				0x00, 0x04, // Data Length
				0x6C, 0xB1, 0x61, 0x1A, // Data
			};
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
        public void ReadLOCResponse()
		{
            var datagram = new byte[] {
				// Transaction ID (2 bytes)
				0x12, 0x34,

				// Flags (2 bytes)
				0x81, 0x80,

				// Number of Questions (2 bytes)
				0x00, 0x01,

				// Number of Answer Resource Records (2 bytes)
				0x00, 0x01,

				// Number of Authority Resource Records (2 bytes)
				0x00, 0x00,

				// Number of Additional Resource Records (2 bytes)
				0x00, 0x00,

				// DNS Question (QNAME)
				0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // "example" in ASCII
				0x03, 0x63, 0x6F, 0x6D, // "com" in ASCII
				0x00, // Null-terminated

				// DNS Question Type (2 bytes)
				0x00, 0x15, // Type: LOC (26)

				// DNS Question Class (2 bytes)
				0x00, 0x01, // Class: IN (Internet)

				// Resource Record 1 (Answer)
				0xC0, 0x0C, // Name: Compression pointer to the question
				0x00, 0x15, // Type: LOC (26)
				0x00, 0x01, // Class: IN (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x2D, // Data Length (45 bytes)

				// Location Data
				0x00, 0x00, 0x00, 0x00, // Version and Size
				0x00, 0x00, 0x00, 0x00, // Horizontal Precision and Vertical Precision

				// Latitude (45°18'N)
				0x00, 0x00, 0x19, 0x99,

				// Longitude (15°16'E)
				0x00, 0x00, 0x0F, 0xA0,

				// Altitude (543m)
				0x00, 0x02, 0x1F, 0x3B,
			};


            DNSHeader header = new DNSHeader(datagram);
			var loc = (LOC)header.Responses[0].RData;
			Assert.AreEqual(loc.Latitude, 45.3, 0.5);
            Assert.AreEqual(loc.Longitude, 15.26, 0.5);
            Assert.AreEqual(loc.Altitude, 543, 1);
        }
    }
}
