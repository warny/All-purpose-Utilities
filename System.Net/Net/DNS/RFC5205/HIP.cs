using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC5205;

/// <summary>
/// Represents a HIP (Host Identity Protocol) record as defined in RFC 5205.
/// HIP records provide a way to associate a host identity with a public key and,
/// optionally, a list of rendezvous servers. This mechanism is part of the Host Identity Protocol,
/// which separates the roles of IP addresses as locators from host identities.
/// </summary>
/// <remarks>
/// <para>
/// The HIP RR RDATA is structured as follows:
/// </para>
/// <code>
///   +-------------------------------+
///   |         HIT Length            |  (1 octet)
///   +-------------------------------+
///   |         Public Key Algorithm  |  (1 octet)
///   +-------------------------------+
///   |         Public Key Length     |  (2 octets)
///   +-------------------------------+
///   |             HIT               |  (variable, length defined by HIT Length)
///   +-------------------------------+
///   |         Public Key            |  (variable, length defined by Public Key Length)
///   +-------------------------------+
///   |  [Optional Rendezvous Servers]|
///   +-------------------------------+
/// </code>
/// <para>
/// In this implementation:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <see cref="HitLength"/> is a private one-octet field that holds the length of the HIT.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="PKAlgorithm"/> represents the public key algorithm used for the HIP record.
///       Its type is <see cref="IPSecAlgorithm"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="PKLength"/> is a private two-octet field that holds the length of the public key.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="HIT"/> is a byte array containing the Host Identity Tag. Its length is determined by <see cref="HitLength"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="PublicKey"/> is a byte array containing the public key data. Its length is determined by <see cref="PKLength"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>RendezvousServers</c> is an optional array of <see cref="DNSDomainName"/> that can be used to specify rendezvous servers.
///       This field is currently not annotated for serialization.
///     </description>
///   </item>
/// </list>
/// <para>
/// Note: The structure and handling of HIP records are defined in RFC 5205.
/// This implementation relies on reflection-based serialization using the <c>[DNSField]</c> attribute.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x37)]
public class HIP : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the HIT Length (Host Identity Tag Length), a one-octet field that indicates the length of the HIT.
	/// </summary>
	[DNSField]
	private byte HitLength { get; set; }

	/// <summary>
	/// Gets or sets the public key algorithm used in this HIP record.
	/// The algorithm is represented by an <see cref="IPSecAlgorithm"/> value.
	/// </summary>
	[DNSField]
	public IPSecAlgorithm PKAlgorithm { get; set; }

	/// <summary>
	/// Gets or sets the Public Key Length, a two-octet field that indicates the length of the public key data.
	/// </summary>
	[DNSField]
	private ushort PKLength { get; set; }

	/// <summary>
	/// Gets or sets the Host Identity Tag (HIT) as a byte array.
	/// The length of this array is defined by <see cref="HitLength"/>.
	/// </summary>
	[DNSField(nameof(HitLength))]
	public byte[] HIT { get; set; }

	/// <summary>
	/// Gets or sets the public key data as a byte array.
	/// The length of this array is defined by <see cref="PKLength"/>.
	/// </summary>
	[DNSField(nameof(PKLength))]
	public byte[] PublicKey { get; set; }

	/// <summary>
	/// Gets or sets the optional array of rendezvous servers.
	/// These servers may be used to help locate the host in a HIP-enabled network.
	/// This field is not annotated with <c>[DNSField]</c> in this implementation.
	/// </summary>
	public DNSDomainName[] RendezvousServers { get; set; }
}
