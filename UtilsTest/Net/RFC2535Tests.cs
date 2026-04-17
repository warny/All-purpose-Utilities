using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC2535;

namespace UtilsTest.Net
{
    /// <summary>
    /// Contains integration tests for RFC 2535 DNS records.
    /// </summary>
    [TestClass]
    public class RFC2535Tests
    {
        /// <summary>
        /// Verifies that a <see cref="KEY"/> record with <see cref="KEY.Extended"/>
        /// round-trips its conditional <see cref="KEY.Extension"/> field.
        /// </summary>
        [TestMethod]
        public void KEYExtendedFieldRoundTripTest()
        {
            var random = new Random();
            var key = new byte[64];
            random.NextBytes(key);

            var sourceHeader = new DNSHeader();
            sourceHeader.Requests.Add(new DNSRequestRecord("DNSKEY", "example.com"));
            sourceHeader.Responses.Add(
                new DNSResponseRecord(
                    "example.com",
                    3600,
                    new KEY
                    {
                        Extended = true,
                        Extension = 0xABCD,
                        Protocol = Protocol.dnssec,
                        Algorithm = Algorithm.DSA_SHA1,
                        PublicKey = key
                    }
                )
            );

            var packet = DNSPacketWriter.Default.Write(sourceHeader);
            var parsedHeader = DNSPacketReader.Default.Read(packet);

            Assert.AreEqual(sourceHeader, parsedHeader, DNSHeadersComparer.Default);
        }
    }
}
