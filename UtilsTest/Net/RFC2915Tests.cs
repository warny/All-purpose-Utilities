using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1876;
using Utils.Net.DNS.RFC2915;
using Utils.Objects;

namespace UtilsTest.Net
{
    [TestClass]
    public class RFC2915Tests
    {
        [TestMethod]
        public void ReadNAPTRRecordTest()
        {
            // Example NAPTR Response Data
            var datagram = new byte[]
            {
				// Header
				0x12, 0x34,	// Transaction ID (2 bytes)
				0x81, 0x80, // Flags (2 bytes)
				0x00, 0x01, // Number of Questions (2 bytes)
				0x00, 0x01, // Number of Answer Resource Records (2 bytes)
				0x00, 0x00, // Number of Authority Resource Records (2 bytes)
				0x00, 0x00, // Number of Additional Resource Records (2 bytes)

				// Question section
				0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // "example" in ASCII
				0x03, 0x63, 0x6F, 0x6D, // "com" in ASCII
				0x00, // Null-terminated
				0x00, 0x23, // DNS Question Type (2 bytes) Type: LOC (29)
				0x00, 0x01, // DNS Question Class (2 bytes) Class: IN (Internet)

                0xC0, 0x0C, // Name: Compression pointer to the question
                0x00, 0x23, // Type (2 bytes)
                0x00, 0x01, // Class (2 bytes)
                0x00, 0x00, 0x00, 0x64, // TTL: 100 seconds
                0x00, 0x3B, // RD Length (2 bytes) 59 bytes

                0x00, 0x14, // Priority (2 bytes) 20
                0x00, 0x01, // Weight (2 bytes)// 1

                // Flags (variable length)
                0x01, // Flags length (1 byte)
                0x53, // S

                // Service (variable length)
                0x09, // Service length (8 bytes)
                0x45, 0x32, 0x55, 0x2B, 0x65, 0x6D, 0x61, 0x69, 0x6C, // E2U+email

                // Regexp (variable length)
                0x1D, // Regexp length (17 bytes)
                0x21, 0x5E, 0x2E, 0x2A, 0x24, 0x2E, 0x2A, 0x21, 
                0x73, 0x69, 0x70, 0x3A, 0x69, 0x6E, 0x66, 0x6F, 
                0x40, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, 
                0x2E, 0x63, 0x6F, 0x6D, 0x21, // !^.*$!sip:info@example.com!

                // Replacement (variable length)
                0x14, // Replacement length (20 bytes)
                0x73, 0x69, 0x70, 0x3A, 0x69, 0x6E, 0x66, 0x6F, 
                0x40, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, 
                0x2E, 0x6F, 0x72, 0x67, // sip:info@example.org
            };

            var DNSReader = new DNSPacketReader(typeof(NAPTR));
            DNSHeader header = DNSReader.Read(datagram);

            var naptr = (NAPTR)header.Responses[0].RData;

            Assert.AreEqual(20, naptr.Order);
            Assert.AreEqual(1, naptr.Preference);
            Assert.AreEqual("S", naptr.Flags);
            Assert.AreEqual("E2U+email", naptr.Service);
            Assert.AreEqual("!^.*$.*!sip:info@example.com!", naptr.Regexp);
            Assert.AreEqual("sip:info@example.org", naptr.Replacement);

        }

        [TestMethod]
        public void WriteReadNAPTRTest()
        {
            Random random = new Random();

            var header1 = new DNSHeader();
            header1.Requests.Add(new DNSRequestRecord("NAPTR", "example.com", DNSClass.IN));
            header1.Responses.Add(new DNSResponseRecord("example.com", 3600, new NAPTR
            {
                Order = (ushort)random.Next(0, 65535),
                Flags = random.RandomString(5, 20),
                Service = random.RandomString(5, 20),
                Preference = (ushort)random.Next(0, 65535),
                Regexp = @"\w+",
                Replacement = random.RandomString(5, 20)
            }));

            var datagram = DNSPacketWriter.Default.Write(header1);
            var header2 = DNSPacketReader.Default.Read(datagram);

            Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
        }
    }
}
