using System;
using System.Net;

namespace Utils.Net.DNS.RFC4025;

/// <summary>
/// Represents an IPSECKEY (IPsec Key) record, as defined in RFC 4025.
/// This record can store IPsec-related data such as a precedence value,
/// gateway type (IPv4, IPv6, domain name, or none), an IPsec algorithm,
/// and a public key.
/// </summary>
/// <remarks>
/// <para>
/// The IPSECKEY RDATA is structured as follows:
/// <list type="bullet">
/// <item><description><c>Precedence</c> (1 byte): A priority or precedence level.</description></item>
/// <item><description><c>GatewayType</c> (1 byte): Indicates whether the gateway is
/// an IPv4 address, IPv6 address, wire-encoded domain, or none (<see cref="GatewayType"/>).</description></item>
/// <item><description><c>SecAlgorithm</c> (1 byte): Defines the IPsec public key algorithm (<see cref="IPSecAlgorithm"/>).</description></item>
/// <item><description><c>GatewayAddress</c>: If <c>GatewayType</c> is IPv4 or IPv6, stores the corresponding IP address.</description></item>
/// <item><description><c>GatewayDomainName</c>: If <c>GatewayType</c> is a wire-encoded domain, stores the <see cref="DNSDomainName"/>.</description></item>
/// <item><description><c>PublicKey</c>: The raw public key bytes (optional usage, not directly annotated with <see cref="DNSFieldAttribute"/>).</description></item>
/// </list>
/// </para>
/// <para>
/// Depending on the <see cref="GatewayType"/>, different fields will be serialized:
/// <list type="bullet">
/// <item><description>IPv4 gateway: a 4-byte address, subject to <c>Condition = "GatewayType==...IPV4GatewayAddress"</c>.</description></item>
/// <item><description>IPv6 gateway: a 16-byte address, subject to <c>Condition = "GatewayType==...IPV6GatewayAddress"</c>.</description></item>
/// <item><description>Wire-encoded domain: stored in <see cref="GatewayDomainName"/>, subject to <c>Condition = "GatewayType==...WireEncodedDomain"</c>.</description></item>
/// <item><description>No gateway: indicates no gateway address or domain is written.</description></item>
/// </list>
/// </para>
/// <para>
/// The record type is <c>0x2D</c> (decimal 45) for the IN class. See RFC 4025 for details on
/// how resolvers process and interpret IPSECKEY records.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x2D)]
[DNSTextRecord("{Precedence} {gatewayType} {SecAlgorithm} {gatewayAddressIPv4} {gatewayAddressIPv6} {gatewayDomainName} {PublicKey}")]
public class IPSECKEY : DNSResponseDetail
{
	/*
            The RDATA for an IPSECKEY RR consists of:
              - precedence (1 byte)
              - gateway type (1 byte)
              - algorithm type (1 byte)
              - an optional gateway address (IPv4, IPv6, or domain)
              - a public key blob

            0                   1                   2                   3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |  precedence   | gateway type  |  algorithm  |     gateway     |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-------------+                 +
            ~                            gateway                            ~
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                                                               /
            /                          public key                           /
            /                                                               /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        */

	/// <summary>
	/// Gets or sets the precedence value (1 byte). Lower values indicate higher priority.
	/// </summary>
	[DNSField]
	public byte Precedence { get; set; }

	/// <summary>
	/// Internal storage for the <see cref="GatewayType"/>. This is set when either
	/// <see cref="GatewayAddress"/> or <see cref="GatewayDomainName"/> is changed.
	/// </summary>
	[DNSField]
	private GatewayType gatewayType;

	/// <summary>
	/// Gets the current gateway type, indicating whether the gateway is 
	/// an IPv4 address, IPv6 address, wire-encoded domain, or none.
	/// </summary>
	public GatewayType GatewayType => gatewayType;

	/// <summary>
	/// Gets or sets the IPsec public key algorithm.
	/// </summary>
	[DNSField]
	public IPSecAlgorithm SecAlgorithm { get; set; }

	/// <summary>
	/// Backing field for IPv4 gateway address, serialized when 
	/// <see cref="GatewayType"/> is <see cref="GatewayType.IPV4GatewayAddress"/>.
	/// </summary>
	[DNSField(4, Condition = "GatewayType==Utils.Net.DNS.GatewayType.IPV4GatewayAddress")]
	private IPAddress gatewayAddressIPv4 = null;

	/// <summary>
	/// Backing field for IPv6 gateway address, serialized when
	/// <see cref="GatewayType"/> is <see cref="GatewayType.IPV6GatewayAddress"/>.
	/// </summary>
	[DNSField(16, Condition = "GatewayType==Utils.Net.DNS.GatewayType.IPV6GatewayAddress")]
	private IPAddress gatewayAddressIPv6 = null;

	/// <summary>
	/// Gets or sets the IP gateway address if the gateway is of type IPv4 or IPv6.
	/// Assigning an address updates <see cref="GatewayType"/> accordingly and clears any domain name value.
	/// Setting it to <c>null</c> resets to <see cref="GatewayType.NoGateway"/>.
	/// </summary>
	/// <exception cref="NotSupportedException">Thrown if the address is not IPv4 or IPv6.</exception>
	public IPAddress GatewayAddress
	{
		get => gatewayAddressIPv4 ?? gatewayAddressIPv6;
		set {
			if (value is null)
			{
				gatewayType = GatewayType.NoGateway;
			}
			else
			{
				switch (value.AddressFamily)
				{
					case System.Net.Sockets.AddressFamily.InterNetwork:
						gatewayType = GatewayType.IPV4GatewayAddress;
						break;
					case System.Net.Sockets.AddressFamily.InterNetworkV6:
						gatewayType = GatewayType.IPV6GatewayAddress;
						break;
					default:
						throw new NotSupportedException(
							"IPSECKEY only supports IPv4 and IPv6 addresses or wire-encoded domain."
						);
				}
			}

			// Update backing fields
			gatewayAddressIPv4 = (value?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				? value
				: null;

			gatewayAddressIPv6 = (value?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
				? value
				: null;

			// Clear any domain name
			gatewayDomainName = null;
		}
	}

	/// <summary>
	/// Backing field for domain name gateway, serialized when
	/// <see cref="GatewayType"/> is <see cref="GatewayType.WireEncodedDomain"/>.
	/// <para>
	/// This is a nullable struct because <see cref="DNSDomainName"/> is itself a struct
	/// that cannot be null. The <c>? operator indicates that the entire struct can be absent.
	/// </para>
	/// </summary>
	[DNSField(Condition = "GatewayType==Utils.Net.DNS.GatewayType.WireEncodedDomain")]
	private DNSDomainName? gatewayDomainName = null;

	/// <summary>
	/// Gets or sets a nullable domain name for the gateway if the gateway type
	/// is set to <see cref="GatewayType.WireEncodedDomain"/>.
	/// Setting this property updates <see cref="GatewayType"/> accordingly and clears any IP addresses.
	/// </summary>
	public DNSDomainName? GatewayDomainName
	{
		get => gatewayDomainName;
		set {
			if (value is null)
			{
				gatewayType = GatewayType.NoGateway;
			}
			else
			{
				gatewayType = GatewayType.WireEncodedDomain;
			}

			gatewayAddressIPv4 = null;
			gatewayAddressIPv6 = null;
			gatewayDomainName = value;
		}
	}

	/// <summary>
	/// Gets or sets the raw public key data for this IPSECKEY record. If you want to serialize
	/// this as part of the DNS record, you can add a <see cref="DNSFieldAttribute"/> here
	/// and specify length options or a prefixed size approach.
	/// </summary>
	/// <remarks>
	/// In many implementations, the public key is mandatory, but your usage may vary.
	/// </remarks>
	public byte[] PublicKey { get; set; }
}
