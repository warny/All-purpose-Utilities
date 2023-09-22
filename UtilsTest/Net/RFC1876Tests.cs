using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1876;

namespace UtilsTest.Net
{
    [TestClass]

    public class RFC1876Tests
    {

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
				0x00, 0x1D, // Type: LOC (29)

				// DNS Question Class (2 bytes)
				0x00, 0x01, // Class: IN (Internet)

				// Resource Record 1 (Answer)
				0xC0, 0x0C, // Name: Compression pointer to the question
				0x00, 0x1D, // Type: LOC (29)
				0x00, 0x01, // Class: IN (Internet)
				0x00, 0x00, 0x0E, 0x10, // TTL
				0x00, 0x29, // Data Length (45 bytes)

				// Location Data
				0x00, 0x19, // Version and Size
				0x12, 0x12, // Horizontal Precision and Vertical Precision

				// Latitude (45°30'N)
				0x83, 0x83, 0xC7, 0xC0,

				// Longitude (15°15'E)
				0x81, 0x2D, 0x93, 0x20,

				// Altitude (543m)
                0x00, 0x99, 0x47, 0x10,
            };


            DNSHeader header = new DNSHeader(datagram);
			var loc = (LOC)header.Responses[0].RData;
			Assert.AreEqual(45.5, loc.Latitude, loc.HorizontalPrecision / 1852);
            Assert.AreEqual(15.25, loc.Longitude, loc.HorizontalPrecision / 1852);
            Assert.AreEqual(loc.Altitude, 452, 1);
        }
    }
}
