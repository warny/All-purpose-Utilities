using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC4025;
using Utils.Net.DNS.RFC4034;

namespace UtilsTest.Net
{
    [TestClass]
    public class RFC4025Tests
    {
        [TestMethod]
        public void IPSECKEYIPV4AdresseTest()
        {
            Random random = new Random();

            var key = new byte[128];
            random.NextBytes(key);

            DNSHeader header1 = new DNSHeader();
            header1.Requests.Add(new DNSRequestRecord("DNSKEY", "example.com"));
            header1.Responses.Add(
                new DNSResponseRecord(
                    "example.com", 3600, new IPSECKEY
                    {
                        GatewayAddress = new System.Net.IPAddress([10, 11, 12, 13]),
                        Precedence = 0,
                        SecAlgorithm = IPSecAlgorithm.DSAKey,
                        PublicKey = key,
                    }
                )
            );

            var packet = DNSPacketWriter.Default.Write(header1);
            var header2 = DNSPacketReader.Default.Read(packet);

            Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
        }

        [TestMethod]
        public void IPSECKEYIPV6AdresseTest()
        {
            Random random = new Random();

            var key = new byte[128];
            random.NextBytes(key);

            DNSHeader header1 = new DNSHeader();
            header1.Requests.Add(new DNSRequestRecord("DNSKEY", "example.com"));
            header1.Responses.Add(
                new DNSResponseRecord(
                    "example.com", 3600, new IPSECKEY
                    {
                        GatewayAddress = new System.Net.IPAddress([10, 11, 12, 13, 14, 15, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26]),
                        Precedence = 0,
                        SecAlgorithm = IPSecAlgorithm.DSAKey,
                        PublicKey = key,
                    }
                )
            );

            var packet = DNSPacketWriter.Default.Write(header1);
            var header2 = DNSPacketReader.Default.Read(packet);

            Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
        }

        [TestMethod]
        public void IPSECKEYDomainTest()
        {
            Random random = new Random();

            var key = new byte[128];
            random.NextBytes(key);

            DNSHeader header1 = new DNSHeader();
            header1.Requests.Add(new DNSRequestRecord("DNSKEY", "example.com"));
            header1.Responses.Add(
                new DNSResponseRecord(
                    "example.com", 3600, new IPSECKEY
                    {
                        GatewayDomainName = "ipseckey.example.com",
                        Precedence = 0,
                        SecAlgorithm = IPSecAlgorithm.DSAKey,
                        PublicKey = key,
                    }
                )
            );

            var packet = DNSPacketWriter.Default.Write(header1);
            var header2 = DNSPacketReader.Default.Read(packet);

            Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
        }

    }
}
