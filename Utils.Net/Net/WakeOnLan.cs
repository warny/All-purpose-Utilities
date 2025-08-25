using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Provides helpers to send Wake-on-LAN magic packets.
/// </summary>
public static class WakeOnLan
{
    /// <summary>
    /// Builds the magic packet for the specified MAC address.
    /// </summary>
    /// <param name="macAddress">Target hardware address.</param>
    /// <returns>Magic packet bytes.</returns>
    public static byte[] CreateMagicPacket(PhysicalAddress macAddress)
    {
        byte[] packet = new byte[102];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        byte[] macBytes = macAddress.GetAddressBytes();
        for (int i = 1; i <= 16; i++)
        {
            macBytes.CopyTo(packet, i * 6);
        }
        return packet;
    }

    /// <summary>
    /// Sends a Wake-on-LAN magic packet using UDP broadcast.
    /// </summary>
    /// <param name="macAddress">Target hardware address.</param>
    /// <param name="broadcastAddress">Optional broadcast address; defaults to <see cref="IPAddress.Broadcast"/>.</param>
    /// <param name="port">Destination UDP port, usually 7 or 9.</param>
    public static async Task SendMagicPacketAsync(PhysicalAddress macAddress, IPAddress? broadcastAddress = null, int port = 9)
    {
        broadcastAddress ??= IPAddress.Broadcast;
        byte[] packet = CreateMagicPacket(macAddress);
        using UdpClient client = new();
        client.EnableBroadcast = true;
        await client.SendAsync(packet, packet.Length, new IPEndPoint(broadcastAddress, port));
    }
}

