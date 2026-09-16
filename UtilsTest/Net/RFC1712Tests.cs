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

            foreach ((double longitude, double latitude, double altitude) in new (double, double, double)[]
            {
                (-179.999, -89.999, -8999.9), (179.999, 89.999, 8999.9), (2.3522, 48.8566, 35.0), (-73.9857, 40.7484, 381.0)
            })
            {
                DNSHeader header1 = new DNSHeader();
                var gpos = new GPOS()
                {
                    Longitude = longitude,
                    Latitude = latitude,
                    Altitude = altitude
                };
                header1.Responses.Add(new DNSResponseRecord("example.com", 1000, gpos));

                var datagram = packetWriter.Write(header1);
                var header2 = packetReader.Read(datagram);

                Assert.AreEqual(header1, header2, DNSHeadersComparer.Default);
            }
        }
    }
}
