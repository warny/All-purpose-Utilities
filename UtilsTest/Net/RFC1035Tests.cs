using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS;

namespace UtilsTest.Net
{
    [TestClass]
    public class RFC1035Tests
    {
        [TestMethod]
        public void ReadAReponse()
        {
            var datagram = new byte[] {
                // Header Section
                0x3D, 0x20,     // 0x00 Transaction ID
                0x81, 0x80,     // 0x02 Flags (e.g., response, authoritative answer)
                0x00, 0x01,     // 0x04 Question Count (1)
                0x00, 0x01,     // 0x06 Answer Record Count (1)
                0x00, 0x00,     // 0x08 Authority Record Count (0)
                0x00, 0x00,     // 0x0A Additional Record Count (0)
                
                // Question Section
                0x04, 0x74, 0x65, 0x73, 0x74,       // 0x0C QNAME: length 4 "test"
                0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // 0x11 QNAME: length 7 "example"
                0x03, 0x63, 0x6F, 0x6D, // 0x19 QNAME: length 3 "com"
                0x00,                   // 0x1D QNAME: End of Name (null terminator)
                0x00, 0x01,             // 0x1E QTYPE: A records 
                0x00, 0xFF,             // 0x20 QCLASS: All classes (IN, CH, etc.)
                
                // Answer section
                0xC0, 0x0C,             // Pointer to "test.example.com"
                0x00, 0x01,             // TYPE: A (IPV4 Address)
                0x00, 0x01,             // CLASS: IN (Internet)
                0x00, 0x00, 0x01, 0x04, // TTL (Time to Live)
                0x00, 0x04,             // RDLENGTH (Data Length) - Longueur d'adresse IPv4
                0x0A, 0x0B, 0x0C, 0x0D  // Address: 10.11.12.13
            };

            var DNSReader = DNSPacketReader.Default;
            DNSHeader header = DNSReader.Read(datagram);

            var dnsRequestRecord = header.Requests[0];
            Assert.AreEqual("test.example.com", dnsRequestRecord.Name.Value);
            Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
            Assert.AreEqual("A", dnsRequestRecord.Type);

            Assert.IsTrue(header.Responses.Count == 1);
            var dnsResponse = header.Responses[0];
            var ARecord = (Address)dnsResponse.RData;
            Assert.AreEqual("test.example.com", dnsResponse.Name.Value);
            Assert.AreEqual("10.11.12.13", ARecord.IPAddress.ToString());

        }

        [TestMethod]
        public void ReadCNameReponse()
        {
            var datagram = new byte[] { 
                // Header Section
                0x87, 0xC3,             // 0x00 Transaction ID
                0x81, 0x80,             // 0x02 Flags (e.g., response, authoritative answer)
                0x00, 0x01,             // 0x04 Question Count (1)
                0x00, 0x02,             // 0x06 Answer Record Count (2)
                0x00, 0x00,             // 0x08 Authority Record Count (0)
                0x00, 0x00,             // 0x0A Additional Record Count (1) - Ajout d'un enregistrement additionnel

                // Question Section
                0x04, 0x74, 0x65, 0x73, 0x74,       // 0x0C QNAME: length 4 "test"
                0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // 0x11 QNAME: length 7 "example"
                0x03, 0x63, 0x6F, 0x6D, // 0x19 QNAME: length 3 "com"
                0x00,                   // 0x1D QNAME: End of Name (null terminator)
                0x00, 0xFF,             // 0x1E QTYPE: All records (A, AAAA, etc.)
                0x00, 0xFF,             // 0x20 QCLASS: All classes (IN, CH, etc.)

                // Answer Section
                0xC0, 0x0C,             // 0x22 NAME: Pointer to "test.example.com"
                0x00, 0x05,             // 0x24 TYPE: CNAME (Canonical Name)
                0x00, 0x01,             // 0x26 CLASS: IN (Internet)
                0x00, 0x00, 0x0E, 0x10, // 0x28 TTL (Time to Live)
                0x00, 0x08,             // 0x2C RDLENGTH (Data Length)
                0x05, 0x63, 0x6E, 0x61, 0x6D, 0x65, // 0x2E QNAME: Length 5 "cname"
                0xC0, 0x11,             // 0x24 POINTER to "example.com"

                // Additional Records
                0xC0, 0x2E,             // 0x26 TYPE: Pointer to "cname.example.com"
                0x00, 0x01,             // 0x28 TYPE: A (IPV4 Address)
                0x00, 0x01,             // 0x2A CLASS: IN (Internet)
                0x00, 0x01, 0x00, 0x00, // 0x2C TTL (Time to Live)
                0x00, 0x04,             // 0x30 RDLENGTH (Data Length) - Longueur d'adresse IPv4
                0x0A, 0x0B, 0x0C, 0x0D  // 0x32 Address: 10.11.12.13
            };
            var DNSReader = DNSPacketReader.Default;
            DNSHeader header = DNSReader.Read(datagram);

            var dnsRequestRecord = header.Requests[0];
            Assert.AreEqual("test.example.com", dnsRequestRecord.Name.Value);
            Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
            Assert.AreEqual("ALL", dnsRequestRecord.Type);

            Assert.AreEqual(2, header.Responses.Count);
            var dnsResponse0 = header.Responses[0];
            var CNameRecord0 = (CNAME)dnsResponse0.RData;
            Assert.AreEqual("test.example.com", dnsResponse0.Name.Value);
            Assert.AreEqual("cname.example.com", CNameRecord0.CName.Value);

            var dnsResponse1 = header.Responses[1];
            var ARecord1 = (Address)dnsResponse1.RData;
            Assert.AreEqual("cname.example.com", dnsResponse1.Name.Value);
            Assert.AreEqual("10.11.12.13", ARecord1.IPAddress.ToString());
        }


        [TestMethod]
        public void ReadMXReponse()
        {
            byte[] datagram = new byte[] {
                // Header Section
                0x00, 0x00,         // 0x00 Transaction ID (à remplir)
                0x81, 0x80,         // 0x02 Flags (e.g., response, authoritative answer)
                0x00, 0x01,         // 0x04 Question Count (1)
                0x00, 0x02,         // 0x06 Answer Record Count (2)
                0x00, 0x00,         // 0x08 Authority Record Count (0)
                0x00, 0x02,         // 0x0A Additional Record Count (2)

                // Question Section
                0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // 0x0C QNAME: length 7 "example"
                0x03, 0x63, 0x6F, 0x6D, // 0x14 QNAME: length 3 "com"
                0x00,                   // 0x18 QNAME: End of Name (null terminator)
                0x00, 0x0F,             // 0x1E QTYPE: A records 
                0x00, 0xFF,             // 0x20 QCLASS: All classes (IN, CH, etc.)

                // Réponse pour mx1.example.com (premier serveur MX)
                0xC0, 0x0C,         // 0x22 Pointer to domain name (0x0C)
                0x00, 0x0F,         // 0x24 Answer Type (15, MX)
                0x00, 0x01,         // 0x26 Answer Class (1, IN)
                0x00, 0x00, 0x0E, 0x10, // 0x28 Time to Live (3600 seconds)
                0x00, 0x15,         // 0x2C Data Length (21 bytes)
                0x00, 0x05,         // 0x2E Priority (5)
                // Nom de serveur pour mx1.example.com
                0x03, 0x6D, 0x78, 0x31, // 0x30 mx1
                0xC0, 0x0C,         // 0x34 Pointer to domain name (example.com)

                // Réponse pour mx2.example.com (deuxième serveur MX)
                0xC0, 0x0C,         // 0x36 Pointer to domain name (0x0C)
                0x00, 0x0F,         // 0x38 Answer Type (15, MX)
                0x00, 0x01,         // 0x3A Answer Class (1, IN)
                0x00, 0x00, 0x0E, 0x10, // 0x3C Time to Live (3600 seconds)
                0x00, 0x15,         // 0x40 Data Length (21 bytes)
                0x00, 0x0A,         // 0x42 Priority (10)
                // Nom de serveur pour mx2.example.com
                0x03, 0x6D, 0x78, 0x32, // 0x44 mx2
                0xC0, 0x0C,         // 0x48 Pointer to domain name (example.com)

                // Enregistrement additionnel pour mx1.example.com
                // Nom de serveur : mx1.example.com
                0xC0, 0x2B,         // 0x4A Pointer to domain name (mx1.example.com)
                0x00, 0x01,         // 0x4C Answer Type (1, A)
                0x00, 0x01,         // 0x4E Answer Class (1, IN)
                0x00, 0x00, 0x0E, 0x10, // 0x50 Time to Live (3600 seconds)
                0x00, 0x04,         // 0x54 Data Length (4 bytes)
                0x0A, 0x0B, 0x0C, 0x0D, // 0x56 Adresse IP pour mx1.example.com : 10.11.12.13

                // Enregistrement additionnel pour mx2.example.com
                // Nom de serveur : mx2.example.com
                0xC0, 0x3F,         // 0x5A Pointer to domain name (mx2.example.com)
                0x00, 0x01,         // 0x5C Answer Type (1, A)
                0x00, 0x01,         // 0x5E Answer Class (1, IN)
                0x00, 0x00, 0x0E, 0x10, // 0x60 Time to Live (3600 seconds)
                0x00, 0x04,         // 0x62 Data Length (4 bytes)
                0x0A, 0x15, 0x16, 0x17  // 0x64 Adresse IP pour mx2.example.com : 10.21.22.23

            };
            var DNSReader = DNSPacketReader.Default;
            DNSHeader header = DNSReader.Read(datagram);

            var dnsRequestRecord = header.Requests[0];
            Assert.AreEqual("example.com", dnsRequestRecord.Name.Value);
            Assert.AreEqual(DNSClass.ALL, dnsRequestRecord.Class);
            Assert.AreEqual("MX", dnsRequestRecord.Type);

            var expectedResponses = new (string Name, ushort Priority, string Exchange)[] {
                ("example.com", 5 , "mx1.example.com"),
                ("example.com", 10, "mx2.example.com"),
            };
            for (int i = 0; i < expectedResponses.Length; i++)
            {
                var expectedResponse = expectedResponses[i];
                var response = header.Responses[i];
                var MXResponse = (MX)response.RData;
                Assert.AreEqual(expectedResponse.Name, response.Name.Value);
                Assert.AreEqual(expectedResponse.Priority, MXResponse.Preference);
                Assert.AreEqual(expectedResponse.Exchange, MXResponse.Exchange.Value);
            }

            var expectedAdditionals = new (string Name, string Type, string Adress, uint TTL)[]{
                ("mx1.example.com", "A", "10.11.12.13", 3600),
                ("mx2.example.com", "A", "10.21.22.23", 3600),
            };

            for (int i = 0; i < expectedAdditionals.Length; i++)
            {
                var expectedAdditional = expectedAdditionals[i];
                var additionnal = header.Additionals[i];
                Assert.AreEqual(expectedAdditional.Name, additionnal.Name.Value);
                Assert.AreEqual(expectedAdditional.TTL, additionnal.TTL);
                Assert.AreEqual(expectedAdditional.Type, additionnal.RData.Name);
                Assert.AreEqual(expectedAdditional.Adress, additionnal.RData.ToString());
            }

        }

    }
}
