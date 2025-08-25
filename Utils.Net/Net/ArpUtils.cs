using System.Net;
using System.Net.NetworkInformation;
using Utils.Net.Arp;

namespace Utils.Net;

/// <summary>
/// Provides helpers to build ARP packets.
/// </summary>
public static class ArpUtils
{
    /// <summary>
    /// Creates an ARP request for the specified target.
    /// </summary>
    public static ArpPacket CreateRequest(IPAddress senderIp, PhysicalAddress senderMac, IPAddress targetIp)
    {
        return new ArpPacket
        {
            Operation = ArpOperation.Request,
            SenderHardwareAddress = senderMac,
            SenderProtocolAddress = senderIp,
            TargetHardwareAddress = PhysicalAddress.None,
            TargetProtocolAddress = targetIp
        };
    }
}

