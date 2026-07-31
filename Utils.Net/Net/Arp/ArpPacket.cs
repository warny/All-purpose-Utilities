using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Utils.Net.Arp;

/// <summary>
/// Represents an Address Resolution Protocol (ARP) packet, specialised for the
/// Ethernet / IPv4 binding defined in <see href="https://www.rfc-editor.org/rfc/rfc826">RFC 826</see>.
/// </summary>
/// <remarks>
/// <para>
/// This type only supports the classic Ethernet over IPv4 encoding: a hardware type of
/// <c>1</c> (Ethernet), a protocol type of <c>0x0800</c> (IPv4), a 6-byte hardware address
/// length and a 4-byte protocol address length. These invariants are enforced both when
/// serialising (<see cref="ToBytes"/>) and when parsing (<see cref="Read"/>).
/// </para>
/// <para>
/// The hardware/protocol type and length fields are exposed as read-only constants
/// (<see cref="HardwareType"/>, <see cref="ProtocolType"/>, <see cref="HardwareAddressLength"/>,
/// <see cref="ProtocolAddressLength"/>) because varying them would produce a packet that this
/// implementation cannot serialise or parse.
/// </para>
/// <para>
/// An <see cref="ArpPacket"/> is not immediately serialisable after construction. Before
/// calling <see cref="ToBytes"/>, the caller must set <see cref="Operation"/>,
/// <see cref="SenderHardwareAddress"/>, <see cref="SenderProtocolAddress"/>,
/// <see cref="TargetHardwareAddress"/> and <see cref="TargetProtocolAddress"/> to valid
/// Ethernet/IPv4 values. <see cref="ToBytes"/> performs final validation and throws
/// <see cref="InvalidOperationException"/> when any constraint is violated.
/// Only <see cref="ArpOperation.Request"/> (1) and <see cref="ArpOperation.Reply"/> (2) are
/// supported; any other <see cref="Operation"/> value will also cause <see cref="ToBytes"/>
/// to throw.
/// </para>
/// </remarks>
public class ArpPacket
{
    /// <summary>Ethernet hardware type value (RFC 826).</summary>
    public const ushort EthernetHardwareType = 1;

    /// <summary>IPv4 protocol type value (EtherType 0x0800).</summary>
    public const ushort Ipv4ProtocolType = 0x0800;

    /// <summary>Ethernet hardware address length, in bytes.</summary>
    public const byte EthernetHardwareAddressLength = 6;

    /// <summary>IPv4 protocol address length, in bytes.</summary>
    public const byte Ipv4ProtocolAddressLength = 4;

    /// <summary>Fixed on-the-wire length of an Ethernet/IPv4 ARP packet, in bytes.</summary>
    public const int EthernetIpv4PacketLength = 28;

    /// <summary>
    /// Gets the hardware type. Always <see cref="EthernetHardwareType"/> (Ethernet = 1).
    /// </summary>
    public ushort HardwareType => EthernetHardwareType;

    /// <summary>
    /// Gets the protocol type. Always <see cref="Ipv4ProtocolType"/> (IPv4 = 0x0800).
    /// </summary>
    public ushort ProtocolType => Ipv4ProtocolType;

    /// <summary>
    /// Gets the hardware address length in bytes. Always <see cref="EthernetHardwareAddressLength"/> (6).
    /// </summary>
    public byte HardwareAddressLength => EthernetHardwareAddressLength;

    /// <summary>
    /// Gets the protocol address length in bytes. Always <see cref="Ipv4ProtocolAddressLength"/> (4).
    /// </summary>
    public byte ProtocolAddressLength => Ipv4ProtocolAddressLength;

    /// <summary>
    /// Gets or sets the ARP operation.
    /// </summary>
    public ArpOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets the sender hardware (MAC) address. Must be exactly 6 bytes when serialising.
    /// </summary>
    public PhysicalAddress SenderHardwareAddress { get; set; } = PhysicalAddress.None;

    /// <summary>
    /// Gets or sets the sender protocol (IPv4) address. Must be an IPv4 address when serialising.
    /// </summary>
    public IPAddress SenderProtocolAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// Gets or sets the target hardware (MAC) address. Must be exactly 6 bytes when serialising.
    /// </summary>
    public PhysicalAddress TargetHardwareAddress { get; set; } = PhysicalAddress.None;

    /// <summary>
    /// Gets or sets the target protocol (IPv4) address. Must be an IPv4 address when serialising.
    /// </summary>
    public IPAddress TargetProtocolAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// Serialises the ARP packet into its 28-byte Ethernet/IPv4 wire representation.
    /// </summary>
    /// <returns>A 28-byte array containing the serialised packet.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the packet cannot be represented as a valid Ethernet/IPv4 ARP packet: the
    /// hardware or protocol addresses have the wrong length or family (IPv6, <see cref="PhysicalAddress.None"/>,
    /// a MAC that is not exactly 6 bytes, or a protocol address that is not IPv4).
    /// </exception>
    public byte[] ToBytes()
    {
        ushort operationValue = (ushort)Operation;
        if (operationValue != 1 && operationValue != 2)
            throw new InvalidOperationException($"Unsupported ARP operation value {operationValue}; only Request (1) and Reply (2) are supported.");

        byte[] senderMac = GetMacBytes(SenderHardwareAddress, nameof(SenderHardwareAddress));
        byte[] targetMac = GetMacBytes(TargetHardwareAddress, nameof(TargetHardwareAddress));
        byte[] senderIp = GetIpv4Bytes(SenderProtocolAddress, nameof(SenderProtocolAddress));
        byte[] targetIp = GetIpv4Bytes(TargetProtocolAddress, nameof(TargetProtocolAddress));

        byte[] buffer = new byte[EthernetIpv4PacketLength];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), HardwareType);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), ProtocolType);
        buffer[4] = HardwareAddressLength;
        buffer[5] = ProtocolAddressLength;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(6, 2), (ushort)Operation);

        senderMac.CopyTo(buffer, 8);
        senderIp.CopyTo(buffer, 14);
        targetMac.CopyTo(buffer, 18);
        targetIp.CopyTo(buffer, 24);

        return buffer;
    }

    private static byte[] GetMacBytes(PhysicalAddress address, string propertyName)
    {
        if (address is null)
            throw new InvalidOperationException($"{propertyName} must not be null.");

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != EthernetHardwareAddressLength)
            throw new InvalidOperationException(
                $"{propertyName} must be a 6-byte Ethernet MAC address; got {bytes.Length} byte(s).");

        return bytes;
    }

    private static byte[] GetIpv4Bytes(IPAddress address, string propertyName)
    {
        if (address is null)
            throw new InvalidOperationException($"{propertyName} must not be null.");

        if (address.AddressFamily != AddressFamily.InterNetwork)
            throw new InvalidOperationException(
                $"{propertyName} must be an IPv4 address; got {address.AddressFamily}.");

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != Ipv4ProtocolAddressLength)
            throw new InvalidOperationException(
                $"{propertyName} must be a 4-byte IPv4 address; got {bytes.Length} byte(s).");

        return bytes;
    }

    /// <summary>
    /// Reads an Ethernet/IPv4 ARP packet from raw bytes.
    /// </summary>
    /// <param name="data">The raw packet bytes. May contain trailing padding after the packet.</param>
    /// <returns>The parsed <see cref="ArpPacket"/>.</returns>
    /// <remarks>
    /// <para>
    /// Parsing proceeds in two steps. First the 8-byte fixed header (HTYPE, PTYPE, HLEN, PLEN,
    /// OPER) is read and the declared body length is computed as
    /// <c>8 + 2*HLEN + 2*PLEN</c>. The buffer must be at least that long. Only then are the
    /// Ethernet/IPv4 invariants checked (HTYPE == 1, PTYPE == 0x0800, HLEN == 6, PLEN == 4).
    /// </para>
    /// <para>
    /// Trailing bytes beyond the declared packet length are ignored. This is intentional:
    /// Ethernet frames pad payloads shorter than 46 bytes, so a 28-byte ARP packet routinely
    /// arrives with 18 padding bytes appended. The padding is not part of the ARP packet and is
    /// discarded.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when the header is shorter than 8 bytes, when the declared body length exceeds the
    /// buffer, or when the hardware/protocol type or length fields are not the supported
    /// Ethernet/IPv4 combination.
    /// </exception>
    public static ArpPacket Read(ReadOnlySpan<byte> data)
    {
        // Step 1: require the 8-byte fixed header before reading any length-dependent field.
        if (data.Length < 8)
            throw new InvalidDataException(
                $"ARP header requires at least 8 bytes; got {data.Length}.");

        ushort hardwareType = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(0, 2));
        ushort protocolType = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2, 2));
        byte hardwareLength = data[4];
        byte protocolLength = data[5];
        ushort operation = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(6, 2));

        // Step 1b: validate the operation field.
        if (operation != 1 && operation != 2)
            throw new InvalidDataException($"Unsupported ARP operation value {operation}; only Request (1) and Reply (2) are supported.");

        // Step 2: validate that the declared body fits within the buffer, using checked
        // arithmetic so an oversized HLEN/PLEN cannot overflow the length computation.
        int expectedLength = checked(8 + 2 * hardwareLength + 2 * protocolLength);
        if (expectedLength > data.Length)
            throw new InvalidDataException(
                $"ARP packet declares a body of {expectedLength} bytes but only {data.Length} are available.");

        // Step 3: enforce the supported Ethernet/IPv4 combination.
        if (hardwareType != EthernetHardwareType)
            throw new InvalidDataException(
                $"Unsupported ARP hardware type {hardwareType}; only Ethernet ({EthernetHardwareType}) is supported.");
        if (protocolType != Ipv4ProtocolType)
            throw new InvalidDataException(
                $"Unsupported ARP protocol type 0x{protocolType:X4}; only IPv4 (0x{Ipv4ProtocolType:X4}) is supported.");
        if (hardwareLength != EthernetHardwareAddressLength)
            throw new InvalidDataException(
                $"Unsupported ARP hardware address length {hardwareLength}; expected {EthernetHardwareAddressLength}.");
        if (protocolLength != Ipv4ProtocolAddressLength)
            throw new InvalidDataException(
                $"Unsupported ARP protocol address length {protocolLength}; expected {Ipv4ProtocolAddressLength}.");

        byte[] senderMac = data.Slice(8, EthernetHardwareAddressLength).ToArray();
        byte[] senderIp = data.Slice(14, Ipv4ProtocolAddressLength).ToArray();
        byte[] targetMac = data.Slice(18, EthernetHardwareAddressLength).ToArray();
        byte[] targetIp = data.Slice(24, Ipv4ProtocolAddressLength).ToArray();

        return new ArpPacket
        {
            Operation = (ArpOperation)operation,
            SenderHardwareAddress = new PhysicalAddress(senderMac),
            SenderProtocolAddress = new IPAddress(senderIp),
            TargetHardwareAddress = new PhysicalAddress(targetMac),
            TargetProtocolAddress = new IPAddress(targetIp),
        };
    }
}
