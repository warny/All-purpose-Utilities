using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Provides helpers to send Wake-on-LAN magic packets.
/// </summary>
/// <remarks>
/// Wake-on-LAN is unauthenticated. Do not use it as an authorization mechanism or
/// as proof that a machine is reachable; it only sends a best-effort UDP broadcast.
/// </remarks>
public static class WakeOnLan
{
    /// <summary>
    /// Builds the 102-byte magic packet for the specified MAC address.
    /// </summary>
    /// <param name="macAddress">Target hardware address. Must be a classic 48-bit (6-byte) Ethernet address.</param>
    /// <returns>Magic packet bytes (6 × 0xFF followed by the MAC address repeated 16 times).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="macAddress"/> is not a 6-byte Ethernet address.</exception>
    public static byte[] CreateMagicPacket(PhysicalAddress macAddress)
    {
        byte[] macBytes = macAddress.GetAddressBytes();
        if (macBytes.Length != 6)
        {
            throw new ArgumentException("Wake-on-LAN requires a 48-bit (6-byte) Ethernet MAC address.", nameof(macAddress));
        }
        byte[] packet = new byte[102];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 1; i <= 16; i++)
        {
            macBytes.CopyTo(packet, i * 6);
        }
        return packet;
    }

    /// <summary>
    /// Sends a Wake-on-LAN magic packet using UDP broadcast.
    /// </summary>
    /// <param name="macAddress">Target hardware address. Must be a classic 48-bit (6-byte) Ethernet address.</param>
    /// <param name="broadcastAddress">
    /// Optional broadcast address; defaults to <see cref="IPAddress.Broadcast"/>.
    /// Must be an IPv4 or IPv6 address.
    /// </param>
    /// <param name="port">Destination UDP port (1–65535), usually 7 or 9.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="macAddress"/> is not 6 bytes, <paramref name="port"/> is out of range,
    /// or <paramref name="broadcastAddress"/> is not an IPv4/IPv6 address.
    /// </exception>
    public static async Task SendMagicPacketAsync(
        PhysicalAddress macAddress,
        IPAddress? broadcastAddress = null,
        int port = 9,
        CancellationToken cancellationToken = default)
    {
        if (port < 1 || port > 65535)
        {
            throw new ArgumentException("UDP port must be between 1 and 65535.", nameof(port));
        }
        broadcastAddress ??= IPAddress.Broadcast;
        if (broadcastAddress.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException("Broadcast address must be an IPv4 or IPv6 address.", nameof(broadcastAddress));
        }
        byte[] packet = CreateMagicPacket(macAddress);
        using UdpClient client = new();
        client.EnableBroadcast = true;
        await client.SendAsync(packet, packet.Length, new IPEndPoint(broadcastAddress, port))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

