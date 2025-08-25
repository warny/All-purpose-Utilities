namespace Utils.Net.Arp;

/// <summary>
/// Defines ARP operation codes.
/// </summary>
public enum ArpOperation : ushort
{
    /// <summary>
    /// ARP request operation.
    /// </summary>
    Request = 1,

    /// <summary>
    /// ARP reply operation.
    /// </summary>
    Reply = 2
}

