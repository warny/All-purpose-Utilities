using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Reflection;
using Utils.Net.Icmp;

namespace UtilsTest.Net;

[TestClass]
public class IcmpPacketTests
{
    [TestMethod]
    public void RoundtripPacketPreservesFields()
    {
        IcmpPacket packet = new()
        {
            PacketType = IcmpPacketType.IcmpV4EchoRequest,
            Identifier = 123,
            SequenceNumber = 7,
            Payload = new byte[] { 1, 2, 3, 4 }
        };

        byte[] bytes = packet.ToBytes();
        IcmpPacket read = IcmpPacket.ReadPacket(bytes);

        Assert.AreEqual(packet.PacketType, read.PacketType);
        Assert.AreEqual(packet.Identifier, read.Identifier);
        Assert.AreEqual(packet.SequenceNumber, read.SequenceNumber);
        CollectionAssert.AreEqual(packet.Payload, read.Payload);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReadPacketDetectsCorruptedChecksum()
    {
        IcmpPacket packet = new() { PacketType = IcmpPacketType.IcmpV4EchoRequest };
        packet.CreateRandomPayload(8);
        byte[] bytes = packet.ToBytes();
        bytes[0] ^= 0xFF; // Corrupt data
        IcmpPacket.ReadPacket(bytes);
    }

    [TestMethod]
    public void PrivateHelperParsesIpFromResponse()
    {
        byte[] responseV4 = new byte[20];
        responseV4[4] = 192;
        responseV4[5] = 168;
        responseV4[6] = 1;
        responseV4[7] = 10;

        MethodInfo? helper = typeof(Utils.Net.IcmpUtils).GetMethod(
            "GetIpFromResponse",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(helper);

        var result = (IPAddress?)helper.Invoke(null, new object[] { responseV4, true });
        Assert.AreEqual(IPAddress.Parse("192.168.1.10"), result);
    }
}
