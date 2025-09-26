using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an IP address record (A, AAAA, or NSAP) in DNS, as described by
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.4.1">RFC 1035 §3.4.1</see> (A records),
/// <see href="https://www.rfc-editor.org/rfc/rfc1886">RFC 1886</see> (superseded but historically defining AAAA records),
/// and the OSI NSAP address type. This class stores the IP address
/// and reflects the correct numeric record ID based on the address family.
/// </summary>
/// <remarks>
/// <para>
/// Three different DNS record types are annotated:
/// <list type="bullet">
/// <item><description><c>A</c> (0x01) for IPv4</description></item>
/// <item><description><c>AAAA</c> (0x1C) for IPv6</description></item>
/// <item><description><c>NSAP</c> (0x17) for OSI-based addressing</description></item>
/// </list>
/// </para>
/// <para>
/// The numeric record ID is determined by the <see cref="System.Net.Sockets.AddressFamily"/> of the
/// stored <see cref="System.Net.IPAddress"/> object. If the address family is unsupported, a
/// <see cref="NotSupportedException"/> is thrown.
/// </para>
/// <para>
/// Internally, this class overrides <see cref="DNSResponseDetail.ClassId"/> and <see cref="DNSResponseDetail.Name"/>
/// to ensure the wire-format ID matches the address type.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x01, "A")]
[DNSRecord(DNSClassId.IN, 0x1C, "AAAA")]
[DNSRecord(DNSClassId.IN, 0x17, "NSAP")]
[DNSTextRecord("{IPAddress}")]
public sealed class Address : DNSResponseDetail
{

	/// <inheritdoc />
	/// <summary>
	/// Gets the DNS record class <see cref="DNSClassId.IN"/>.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown if the IP address family is not InterNetwork, InterNetworkV6, or Osi.</exception>
	internal override DNSClassId Class => DNSClassId.IN;

	/// <inheritdoc />
	/// <summary>
	/// Gets the DNS record ID based on the <see cref="System.Net.Sockets.AddressFamily"/> of <see cref="IPAddress"/>.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown if the IP address family is not InterNetwork, InterNetworkV6, or Osi.</exception>
	internal override ushort ClassId => ipAddress.AddressFamily switch
	{
		AddressFamily.InterNetwork => 0x01,  // A
		AddressFamily.InterNetworkV6 => 0x1C, // AAAA
		AddressFamily.Osi => 0x17,           // NSAP
		_ => throw new NotSupportedException("A and AAAA records only support IPv4, IPv6, or NSAP addresses.")
	};

	/// <inheritdoc />
	/// <summary>
	/// Gets the record name ("A", "AAAA", or "NSAP") based on the <see cref="System.Net.Sockets.AddressFamily"/>.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown if the IP address family is not InterNetwork, InterNetworkV6, or Osi.</exception>
	public override string Name => ipAddress.AddressFamily switch
	{
		AddressFamily.InterNetwork => "A",
		AddressFamily.InterNetworkV6 => "AAAA",
		AddressFamily.Osi => "NSAP",
		_ => throw new NotSupportedException("A and AAAA records only support IPv4, IPv6, or NSAP addresses.")
	};

	/// <summary>
	/// The underlying IP address stored in this DNS record.
	/// </summary>
	[DNSField]
	private IPAddress ipAddress = null;

	/// <summary>
	/// Gets or sets the <see cref="System.Net.IPAddress"/> associated with this record. If the address family
	/// is not IPv4, IPv6, or Osi, a <see cref="NotSupportedException"/> is thrown.
	/// </summary>
	/// <exception cref="NotSupportedException">
	/// Thrown when an address family outside of InterNetwork, InterNetworkV6, or Osi is assigned.
	/// </exception>
	public IPAddress IPAddress
	{
		get => ipAddress;
		set {
			var validFamilies = new[]
			{
				AddressFamily.InterNetwork,
				AddressFamily.InterNetworkV6,
				AddressFamily.Osi
			};

			if (!validFamilies.Contains(value.AddressFamily))
			{
				throw new NotSupportedException(
					$"{value.AddressFamily} is not supported by A/AAAA/NSAP DNS records."
				);
			}
			ipAddress = value;
		}
	}

	/// <summary>
	/// Returns the string representation of the underlying <see cref="IPAddress"/>.
	/// </summary>
	/// <returns>The IP address in dotted-decimal or colon-hex notation.</returns>
	public override string ToString() => IPAddress.ToString();
}
