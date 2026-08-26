using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using Utils.Net.Arp;

namespace UtilsTest.Net;

[TestClass]
public class ArpPacketTests
{
    private static ArpPacket ValidRequest() => new()
    {
        Operation = ArpOperation.Request,
        SenderHardwareAddress = PhysicalAddress.Parse("00-11-22-33-44-55"),
        SenderProtocolAddress = IPAddress.Parse("192.168.1.1"),
        TargetHardwareAddress = PhysicalAddress.Parse("00-00-00-00-00-00"),
        TargetProtocolAddress = IPAddress.Parse("192.168.1.2")
    };

    private static ArpPacket ValidReply() => new()
    {
        Operation = ArpOperation.Reply,
        SenderHardwareAddress = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF"),
        SenderProtocolAddress = IPAddress.Parse("10.0.0.1"),
        TargetHardwareAddress = PhysicalAddress.Parse("00-11-22-33-44-55"),
        TargetProtocolAddress = IPAddress.Parse("10.0.0.2")
    };

    [TestMethod]
    public void ToBytes_ValidEthernetIpv4Request_ReturnsExpected28Bytes()
    {
        byte[] bytes = ValidRequest().ToBytes();

        Assert.AreEqual(28, bytes.Length);
        Assert.AreEqual(0x00, bytes[0]);
        Assert.AreEqual(0x01, bytes[1]); // HTYPE = 1
        Assert.AreEqual(0x08, bytes[2]);
        Assert.AreEqual(0x00, bytes[3]); // PTYPE = 0x0800
        Assert.AreEqual(6, bytes[4]);    // HLEN
        Assert.AreEqual(4, bytes[5]);    // PLEN
        Assert.AreEqual(0x00, bytes[6]);
        Assert.AreEqual(0x01, bytes[7]); // OPER = request
    }

    [TestMethod]
    public void ToBytes_ValidEthernetIpv4Reply_ReturnsExpected28Bytes()
    {
        byte[] bytes = ValidReply().ToBytes();

        Assert.AreEqual(28, bytes.Length);
        Assert.AreEqual(0x00, bytes[6]);
        Assert.AreEqual(0x02, bytes[7]); // OPER = reply
    }

    [TestMethod]
    public void ToBytes_Ipv6Sender_Throws()
    {
        ArpPacket packet = ValidRequest();
        packet.SenderProtocolAddress = IPAddress.Parse("2001:db8::1");
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_Ipv6Target_Throws()
    {
        ArpPacket packet = ValidRequest();
        packet.TargetProtocolAddress = IPAddress.Parse("2001:db8::2");
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_InvalidSenderMacLength_Throws()
    {
        ArpPacket packet = ValidRequest();
        packet.SenderHardwareAddress = new PhysicalAddress(new byte[] { 1, 2, 3 });
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_InvalidTargetMacLength_Throws()
    {
        ArpPacket packet = ValidRequest();
        packet.TargetHardwareAddress = new PhysicalAddress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_InconsistentHardwareLength_Throws()
    {
        // PhysicalAddress.None serialises to zero bytes, which is not a 6-byte MAC.
        ArpPacket packet = ValidRequest();
        packet.SenderHardwareAddress = PhysicalAddress.None;
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_InconsistentProtocolLength_Throws()
    {
        // IPAddress.IPv6None is not InterNetwork and is not 4 bytes.
        ArpPacket packet = ValidRequest();
        packet.TargetProtocolAddress = IPAddress.IPv6None;
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_UnsupportedHardwareType_Throws()
    {
        // The invariant is enforced structurally: a non-6-byte MAC represents a non-Ethernet
        // hardware type and is rejected.
        ArpPacket packet = ValidRequest();
        packet.SenderHardwareAddress = new PhysicalAddress(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void ToBytes_UnsupportedProtocolType_Throws()
    {
        // A non-IPv4 protocol address represents a non-IPv4 protocol type and is rejected.
        ArpPacket packet = ValidRequest();
        packet.SenderProtocolAddress = IPAddress.IPv6Loopback;
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void Read_ValidEthernetIpv4Packet_ParsesAllFields()
    {
        ArpPacket original = ValidReply();
        byte[] bytes = original.ToBytes();

        ArpPacket parsed = ArpPacket.Read(bytes);

        Assert.AreEqual(ArpOperation.Reply, parsed.Operation);
        Assert.AreEqual(1, parsed.HardwareType);
        Assert.AreEqual(0x0800, parsed.ProtocolType);
        Assert.AreEqual(6, parsed.HardwareAddressLength);
        Assert.AreEqual(4, parsed.ProtocolAddressLength);
        CollectionAssert.AreEqual(original.SenderHardwareAddress.GetAddressBytes(), parsed.SenderHardwareAddress.GetAddressBytes());
        Assert.AreEqual(original.SenderProtocolAddress, parsed.SenderProtocolAddress);
        CollectionAssert.AreEqual(original.TargetHardwareAddress.GetAddressBytes(), parsed.TargetHardwareAddress.GetAddressBytes());
        Assert.AreEqual(original.TargetProtocolAddress, parsed.TargetProtocolAddress);
    }

    [TestMethod]
    public void Read_TrailingPadding_IsHandledAccordingToDocumentedPolicy()
    {
        // Documented policy: trailing Ethernet padding after the 28-byte packet is ignored.
        byte[] bytes = ValidRequest().ToBytes();
        byte[] padded = new byte[60]; // minimum Ethernet frame payload
        System.Array.Copy(bytes, padded, bytes.Length);

        ArpPacket parsed = ArpPacket.Read(padded);
        Assert.AreEqual(ArpOperation.Request, parsed.Operation);
        Assert.AreEqual(IPAddress.Parse("192.168.1.1"), parsed.SenderProtocolAddress);
    }

    [TestMethod]
    public void RoundTrip_ValidPacket_PreservesFields()
    {
        ArpPacket packet = ValidRequest();
        ArpPacket read = ArpPacket.Read(packet.ToBytes());

        Assert.AreEqual(packet.HardwareType, read.HardwareType);
        Assert.AreEqual(packet.ProtocolType, read.ProtocolType);
        Assert.AreEqual(packet.Operation, read.Operation);
        CollectionAssert.AreEqual(packet.SenderHardwareAddress.GetAddressBytes(), read.SenderHardwareAddress.GetAddressBytes());
        Assert.AreEqual(packet.SenderProtocolAddress, read.SenderProtocolAddress);
        CollectionAssert.AreEqual(packet.TargetHardwareAddress.GetAddressBytes(), read.TargetHardwareAddress.GetAddressBytes());
        Assert.AreEqual(packet.TargetProtocolAddress, read.TargetProtocolAddress);
    }

    [TestMethod]
    public void ToBytes_UnsupportedOperation_ThrowsInvalidOperationException()
    {
        ArpPacket packet = ValidRequest();
        packet.Operation = (ArpOperation)99;
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void NewPacket_WithDefaultAddresses_CannotBeSerialized()
    {
        // A freshly constructed packet has PhysicalAddress.None (0-byte MAC) and IPAddress.Any;
        // ToBytes must reject these even when Operation is set.
        var packet = new ArpPacket { Operation = ArpOperation.Request };
        Assert.ThrowsExactly<InvalidOperationException>(() => packet.ToBytes());
    }

    [TestMethod]
    public void HardwareAndProtocolProperties_AreReadOnlyInvariants()
    {
        // Verify that the invariant properties always return the expected constant values.
        var packet = new ArpPacket { Operation = ArpOperation.Request };
        Assert.AreEqual((ushort)1, packet.HardwareType);
        Assert.AreEqual((ushort)0x0800, packet.ProtocolType);
        Assert.AreEqual((byte)6, packet.HardwareAddressLength);
        Assert.AreEqual((byte)4, packet.ProtocolAddressLength);
    }
}
