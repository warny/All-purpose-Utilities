using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using Utils.Net.Arp;

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies that <see cref="ArpPacket.Read(byte[])"/> rejects truncated, oversized, zero-length,
/// or otherwise malformed raw ARP frames rather than reading out of bounds or misinterpreting
/// hostile network-captured bytes.
/// </summary>
[TestClass]
public class ArpPacketBoundaryTests
{
    private static ArpPacket ValidRequest() => new()
    {
        Operation = ArpOperation.Request,
        SenderHardwareAddress = PhysicalAddress.Parse("00-11-22-33-44-55"),
        SenderProtocolAddress = IPAddress.Parse("192.168.1.1"),
        TargetHardwareAddress = PhysicalAddress.Parse("00-00-00-00-00-00"),
        TargetProtocolAddress = IPAddress.Parse("192.168.1.2")
    };

    [TestMethod]
    public void Read_HeaderShorterThanEightBytes_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(new byte[7]));
    }

    [TestMethod]
    public void Read_DeclaredLengthExceedsBuffer_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        // Truncate the body while keeping a full 8-byte header.
        byte[] truncated = new byte[20];
        System.Array.Copy(bytes, truncated, 20);
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(truncated));
    }

    [TestMethod]
    public void Read_ZeroHardwareLength_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        bytes[4] = 0; // HLEN = 0
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }

    [TestMethod]
    public void Read_ZeroProtocolLength_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        bytes[5] = 0; // PLEN = 0
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }

    [TestMethod]
    public void Read_OversizedLengths_ThrowsInvalidDataException()
    {
        byte[] bytes = new byte[28];
        // Valid header prefix, then oversized HLEN/PLEN that exceed the buffer.
        bytes[1] = 1;      // HTYPE
        bytes[2] = 0x08;   // PTYPE high
        bytes[4] = 200;    // HLEN
        bytes[5] = 200;    // PLEN
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }

    [TestMethod]
    public void Read_UnsupportedHardwareType_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        bytes[1] = 6; // HTYPE = 6 (IEEE 802) instead of 1
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }

    [TestMethod]
    public void Read_UnsupportedProtocolType_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        bytes[2] = 0x86;
        bytes[3] = 0xDD; // PTYPE = 0x86DD (IPv6) instead of 0x0800
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }

    [TestMethod]
    public void Read_UnsupportedOperation_ThrowsInvalidDataException()
    {
        byte[] bytes = ValidRequest().ToBytes();
        // Set operation to an unsupported value (99 = 0x0063).
        bytes[6] = 0x00;
        bytes[7] = 99;
        Assert.ThrowsExactly<InvalidDataException>(() => ArpPacket.Read(bytes));
    }
}
