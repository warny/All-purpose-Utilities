using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.NetworkInformation;
using Utils.Net;

namespace UtilsTest.Net;

[TestClass]
public class WakeOnLanTests
{
    [TestMethod]
    public void MagicPacketHasExpectedFormat()
    {
        PhysicalAddress mac = PhysicalAddress.Parse("01-23-45-67-89-AB");
        byte[] packet = WakeOnLan.CreateMagicPacket(mac);

        Assert.AreEqual(102, packet.Length);
        for (int i = 0; i < 6; i++) Assert.AreEqual(0xFF, packet[i]);
        byte[] macBytes = mac.GetAddressBytes();
        for (int i = 6; i < packet.Length; i += 6)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.AreEqual(macBytes[j], packet[i + j]);
            }
        }
    }
}

