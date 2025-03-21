using System;
using System.Linq;
using System.Net;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents a WKS (Well Known Services) record, as specified by RFC 1035 Section 3.4.
/// A WKS record details which well-known services are available for a given IPv4 address
/// and IP protocol (e.g., TCP or UDP).
/// </summary>
/// <remarks>
/// <para>
/// The WKS record includes:
/// <list type="bullet">
///   <item>
///     <description>A 32-bit IPv4 address (<see cref="IPAddress"/>)</description>
///   </item>
///   <item>
///     <description>An 8-bit protocol number (<see cref="Protocol"/>). Examples: 6 (TCP), 17 (UDP).</description>
///   </item>
///   <item>
///     <description>A variable-length bit map (<see cref="Bitmap"/>), with one bit per port.
///     For instance, if <c>Protocol=6 (TCP)</c>, the 26th bit corresponds to TCP port 25 (SMTP).
///     If that bit is set, the service on port 25 is advertised as available.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// For each bit in <see cref="Bitmap"/>, the index corresponds to a port number. If the bit is <c>1</c>,
/// that port is available for the specified <see cref="Protocol"/>; otherwise <c>0</c> means unavailable.
/// This approach is considered obsolete and rarely used in modern DNS contexts. Instead, SRV records
/// or other service discovery methods are typically employed.
/// </para>
/// <para>
/// Example usage (pseudocode):
/// <code>
/// var wksRecord = new WKS
/// {
///     IPAddress = IPAddress.Parse("192.0.2.1"),
///     Protocol = 6, // TCP
///     Bitmap = new byte[] { 0x00, 0x01, ... } // Indicating ports 8, 9, etc. might be active
/// };
/// </code>
/// </para>
/// <para>
/// WKS RRs do not trigger additional section processing. They are primarily for describing
/// availability of TCP/UDP services on an IPv4 address, although they are considered
/// effectively deprecated in modern DNS practice.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x0B)]
public class WKS : DNSResponseDetail
{
	/*
        WKS RDATA format (RFC 1035, Section 3.4):

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ADDRESS                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |       PROTOCOL        |                       |
            +--+--+--+--+--+--+--+--+                       |
            |                                               |
            /                   <BIT MAP>                   /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        ADDRESS   = 32-bit IPv4 address
        PROTOCOL  = 8-bit IP protocol number (e.g., 6 for TCP, 17 for UDP)
        <BIT MAP> = bits for ports 0..(n*8 - 1). The nth byte corresponds to ports [n*8..n*8+7].
    */

	/// <summary>
	/// Stores the IPv4 address in bytes for serialization. This field is private so that
	/// library mechanisms handle reading/writing it directly. The corresponding property
	/// is <see cref="IPAddress"/>.
	/// </summary>
	[DNSField]
	private byte[] IpAddressBytes
	{
		get => ipAddress?.GetAddressBytes();
		set => ipAddress = value != null ? new IPAddress(value) : null;
	}

	private IPAddress ipAddress;

	/// <summary>
	/// Gets or sets the <see cref="System.Net.IPAddress"/> for this WKS record.
	/// Must be IPv4 (AddressFamily.InterNetwork), as WKS does not support IPv6.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown if an attempt is made to set an IPv6 address.</exception>
	public IPAddress IPAddress
	{
		get => ipAddress;
		set {
			if (value != null && value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
			{
				throw new NotSupportedException("WKS records only support IPv4 addresses.");
			}
			ipAddress = value;
		}
	}

	/// <summary>
	/// Gets or sets the 8-bit IP protocol number (e.g., 6 = TCP, 17 = UDP).
	/// </summary>
	[DNSField]
	public byte Protocol { get; set; }

	/// <summary>
	/// Gets or sets the variable-length bit map indicating which ports
	/// are available for this <see cref="Protocol"/>.
	/// The nth byte covers ports [n*8..n*8+7].
	/// </summary>
	[DNSField]
	public byte[] Bitmap { get; set; }

	/// <summary>
	/// Returns a string showing the IPv4 address, the protocol number,
	/// and the bit map in hex bytes.
	/// </summary>
	/// <returns>A textual representation for debugging/logging.</returns>
	public override string ToString()
	{
		// Example: 192.0.2.1:6 (00 01 02 ...)
		var hexBitmap = Bitmap != null
			? string.Join(" ", Bitmap.Select(d => d.ToString("X2")))
			: "(no bitmap)";

		return $"{IPAddress}:{Protocol}\t[{hexBitmap}]";
	}
}
