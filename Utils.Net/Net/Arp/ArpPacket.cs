using System;
using System.Net;
using System.Net.NetworkInformation;

namespace Utils.Net.Arp;

/// <summary>
/// Represents an Address Resolution Protocol packet.
/// </summary>
public class ArpPacket
{
    /// <summary>
    /// Gets or sets the hardware type (Ethernet = 1).
    /// </summary>
    public ushort HardwareType { get; set; } = 1;

    /// <summary>
    /// Gets or sets the protocol type (IPv4 = 0x0800).
    /// </summary>
    public ushort ProtocolType { get; set; } = 0x0800;

    /// <summary>
    /// Gets or sets the hardware address length in bytes.
    /// </summary>
    public byte HardwareAddressLength { get; set; } = 6;

    /// <summary>
    /// Gets or sets the protocol address length in bytes.
    /// </summary>
    public byte ProtocolAddressLength { get; set; } = 4;

    /// <summary>
    /// Gets or sets the ARP operation.
    /// </summary>
    public ArpOperation Operation { get; set; }
    /// <summary>
    /// Gets or sets the sender hardware address.
    /// </summary>
    public PhysicalAddress SenderHardwareAddress { get; set; } = PhysicalAddress.None;

    /// <summary>
    /// Gets or sets the sender protocol address.
    /// </summary>
    public IPAddress SenderProtocolAddress { get; set; } = IPAddress.None;

    /// <summary>
    /// Gets or sets the target hardware address.
    /// </summary>
    public PhysicalAddress TargetHardwareAddress { get; set; } = PhysicalAddress.None;

    /// <summary>
    /// Gets or sets the target protocol address.
    /// </summary>
    public IPAddress TargetProtocolAddress { get; set; } = IPAddress.None;

    /// <summary>
    /// Serializes the ARP packet into bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] buffer = new byte[28];
        buffer[0] = (byte)(HardwareType >> 8);
        buffer[1] = (byte)HardwareType;
        buffer[2] = (byte)(ProtocolType >> 8);
        buffer[3] = (byte)ProtocolType;
        buffer[4] = HardwareAddressLength;
        buffer[5] = ProtocolAddressLength;
        buffer[6] = (byte)((ushort)Operation >> 8);
        buffer[7] = (byte)Operation;

        byte[] senderMac = SenderHardwareAddress.GetAddressBytes();
        byte[] senderIp = SenderProtocolAddress.GetAddressBytes();
        byte[] targetMac = TargetHardwareAddress.GetAddressBytes();
        byte[] targetIp = TargetProtocolAddress.GetAddressBytes();

        senderMac.CopyTo(buffer, 8);
        senderIp.CopyTo(buffer, 14);
        targetMac.CopyTo(buffer, 18);
        targetIp.CopyTo(buffer, 24);

        return buffer;
    }

    /// <summary>
    /// Reads an ARP packet from raw bytes.
    /// </summary>
    public static ArpPacket Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 28)
            throw new ArgumentException("ARP packet too short", nameof(data));

        ArpPacket packet = new()
        {
            HardwareType = (ushort)((data[0] << 8) | data[1]),
            ProtocolType = (ushort)((data[2] << 8) | data[3]),
            HardwareAddressLength = data[4],
            ProtocolAddressLength = data[5],
            Operation = (ArpOperation)((data[6] << 8) | data[7])
        };

        byte[] senderMac = data.Slice(8, packet.HardwareAddressLength).ToArray();
        byte[] senderIp = data.Slice(8 + packet.HardwareAddressLength, packet.ProtocolAddressLength).ToArray();
        byte[] targetMac = data.Slice(8 + packet.HardwareAddressLength + packet.ProtocolAddressLength, packet.HardwareAddressLength).ToArray();
        byte[] targetIp = data.Slice(8 + 2 * packet.HardwareAddressLength + packet.ProtocolAddressLength, packet.ProtocolAddressLength).ToArray();

        packet.SenderHardwareAddress = new PhysicalAddress(senderMac);
        packet.SenderProtocolAddress = new IPAddress(senderIp);
        packet.TargetHardwareAddress = new PhysicalAddress(targetMac);
        packet.TargetProtocolAddress = new IPAddress(targetIp);

        return packet;
    }
}

