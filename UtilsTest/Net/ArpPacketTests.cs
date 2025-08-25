using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.NetworkInformation;
using Utils.Net.Arp;

namespace UtilsTest.Net;

[TestClass]
public class ArpPacketTests
{
    [TestMethod]
    public void RoundtripPacketPreservesFields()
    {
        ArpPacket packet = new()
        {
            Operation = ArpOperation.Request,
            SenderHardwareAddress = PhysicalAddress.Parse("00-11-22-33-44-55"),
            SenderProtocolAddress = IPAddress.Parse("192.168.1.1"),
            TargetHardwareAddress = PhysicalAddress.Parse("00-00-00-00-00-00"),
            TargetProtocolAddress = IPAddress.Parse("192.168.1.2")
        };

        byte[] bytes = packet.ToBytes();
        ArpPacket read = ArpPacket.Read(bytes);

        Assert.AreEqual(packet.HardwareType, read.HardwareType);
        Assert.AreEqual(packet.ProtocolType, read.ProtocolType);
        Assert.AreEqual(packet.Operation, read.Operation);
        CollectionAssert.AreEqual(packet.SenderHardwareAddress.GetAddressBytes(), read.SenderHardwareAddress.GetAddressBytes());
        Assert.AreEqual(packet.SenderProtocolAddress, read.SenderProtocolAddress);
        CollectionAssert.AreEqual(packet.TargetHardwareAddress.GetAddressBytes(), read.TargetHardwareAddress.GetAddressBytes());
        Assert.AreEqual(packet.TargetProtocolAddress, read.TargetProtocolAddress);
    }
}

