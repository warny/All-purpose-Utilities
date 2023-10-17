using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1712;
using Utils.Net.DNS.RFC1876;

namespace UtilsTest.Net
{
    [TestClass]
    public class RFC1712Tests
    {
        [TestMethod]
        public void WriteReadTest()
        {
            var factory = new DNSFactory(typeof(GPOS));

            var packetWriter = new DNSPacketWriter(factory);
            var packetReader = new DNSPacketReader(factory);

            Random random = new Random();

            for (int i = 0; i < 20; i++)
            {
                DNSHeader header1 = new DNSHeader();
                var gpos = new GPOS()
                {
                    Longitude = random.NextDouble() * 360 - 180,
                    Latitude = random.NextDouble() * 180 - 90,
                    Altitude = random.NextDouble() * 18000 - 9000
                };
                header1.Responses.Add(new DNSResponseRecord("example.com", 1000, gpos));

                var datagram = packetWriter.Write(header1);
                var header2 = packetReader.Read(datagram);

                Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
            }
        }
    }
}
