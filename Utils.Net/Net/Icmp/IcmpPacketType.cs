namespace Utils.Net.Icmp;

/// <summary>
/// Defines well-known ICMP packet types used by the utilities.
/// </summary>
public enum IcmpPacketType : byte
{
    /// <summary>
    /// IPv4 echo reply (type 0).
    /// </summary>
    IcmpV4EchoReply = 0,

    /// <summary>
    /// IPv4 echo request (type 8).
    /// </summary>
    IcmpV4EchoRequest = 8,

    /// <summary>
    /// IPv6 echo request (type 128).
    /// </summary>
    IcmpV6EchoRequest = 128,

    /// <summary>
    /// IPv6 echo reply (type 129).
    /// </summary>
    IcmpV6EchoReply = 129,

    /// <summary>
    /// IPv4 time exceeded (type 11).
    /// </summary>
    IcmpV4TimeExceeded = 11,

    /// <summary>
    /// IPv6 time exceeded (type 3).
    /// </summary>
    IcmpV6TimeExceeded = 3
}
