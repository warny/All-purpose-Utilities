using System;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Enumerates algorithm identifiers used in DNS security (DNSSEC) and related cryptographic functions.
	/// Defined primarily for keys and signatures in DNSSEC records.
	/// </summary>
	public enum Algorithm : byte
	{
		/// <summary>
		/// Reserved (not a valid or supported algorithm).
		/// </summary>
		Reserved0 = 0,

		/// <summary>
		/// RSA with MD5 hashing (deprecated).
		/// </summary>
		RSA_MD5 = 1,

		/// <summary>
		/// Diffie-Hellman key agreement algorithm.
		/// </summary>
		DiffieHellman = 2,

		/// <summary>
		/// DSA (Digital Signature Algorithm) with SHA-1 hashing.
		/// </summary>
		DSA_SHA1 = 3,

		/// <summary>
		/// Elliptic Curve (not widely used in older DNSSEC deployments).
		/// </summary>
		EllipticCurve = 4,

		/// <summary>
		/// RSA with SHA-1 hashing.
		/// </summary>
		RSA_SHA1 = 5,

		/// <summary>
		/// Indicates an indirect key reference (not a direct algorithm).
		/// </summary>
		IndirectKey = 252,

		/// <summary>
		/// A private algorithm domain name, meaning the algorithm is indicated by a domain name.
		/// </summary>
		Private_DomainName = 253,

		/// <summary>
		/// A private algorithm indicated by an OID (Object Identifier).
		/// </summary>
		Private_OID = 254,

		/// <summary>
		/// Reserved (not a valid or supported algorithm).
		/// </summary>
		Reserved255 = 255
	}

	/// <summary>
	/// Enumerates the protocols associated with a DNSKEY (or KEY) resource record,
	/// specifying how the key is intended to be used (e.g., for TLS, email, DNSSEC).
	/// </summary>
	public enum Protocol : byte
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved = 0,

		/// <summary>
		/// Key for use in connection with TLS.
		/// </summary>
		TLS = 1,

		/// <summary>
		/// Key for use in connection with email (S/MIME, etc.).
		/// </summary>
		email = 2,

		/// <summary>
		/// Key used for DNSSEC. This is the typical value for DNS zone-signing keys.
		/// </summary>
		dnssec = 3,

		/// <summary>
		/// Key used for IPSEC and the Oakley key management protocol (RFC 2401).
		/// </summary>
		IPSEC = 4,

		/// <summary>
		/// Key is valid for all defined DNS protocol usage.
		/// Use of this value is discouraged in favor of more specific protocols.
		/// </summary>
		All = 255
	}

	/// <summary>
	/// Enumerates digest/hash algorithms for DNSSEC-related records, such as DS (Delegation Signer).
	/// </summary>
	public enum DigestTypes : byte
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved = 0,

		/// <summary>
		/// SHA-1 hashing algorithm.
		/// </summary>
		SHA1 = 1,
	}

	/// <summary>
	/// Enumerates certificate types that might appear within CERT DNS records.
	/// </summary>
	public enum CertificateTypes : ushort
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved = 0,

		/// <summary>
		/// X.509 certificate, as per PKIX.
		/// </summary>
		PKIX = 1,

		/// <summary>
		/// SPKI (Simple Public Key Infrastructure) certificate.
		/// </summary>
		SPKI = 2,

		/// <summary>
		/// OpenPGP packet.
		/// </summary>
		PGP = 3,

		/// <summary>
		/// The URL of an X.509 data object (IPKIX).
		/// </summary>
		IPKIX = 4,

		/// <summary>
		/// The URL of an SPKI certificate (ISPKI).
		/// </summary>
		ISPKI = 5,

		/// <summary>
		/// The fingerprint and URL of an OpenPGP packet (IPGP).
		/// </summary>
		IPGP = 6,

		/// <summary>
		/// Attribute Certificate (ACPKIX).
		/// </summary>
		ACPKIX = 7,

		/// <summary>
		/// The URL of an Attribute Certificate (IACPKIX).
		/// </summary>
		IACPKIX = 8,

		/// <summary>
		/// URI private usage.
		/// </summary>
		URI = 253,

		/// <summary>
		/// OID (Object Identifier) private usage.
		/// </summary>
		OID = 254,

		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		ReservedFF = 255,

		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		ReservedFFFF = 65535
	}

	/// <summary>
	/// Enumerates algorithm types for SSHFP records (SSH Fingerprint records in DNS).
	/// </summary>
	public enum SSHAlgorithm : byte
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved = 0,

		/// <summary>
		/// RSA public key algorithm.
		/// </summary>
		RSA = 1,

		/// <summary>
		/// DSA (Digital Signature Algorithm) public key.
		/// </summary>
		DSS = 2
	}

	/// <summary>
	/// Enumerates fingerprint/hash algorithms for SSHFP records.
	/// </summary>
	public enum SSHFingerprintType : byte
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved = 0,

		/// <summary>
		/// SHA-1 hashing for SSH fingerprinting.
		/// </summary>
		SHA1 = 1
	}

	/// <summary>
	/// Enumerates gateway types used in IPSECKEY records to indicate the nature
	/// of the gateway (e.g., an IPv4 address, IPv6 address, or a domain name).
	/// </summary>
	public enum GatewayType : byte
	{
		/// <summary>
		/// No gateway is present.
		/// </summary>
		NoGateway = 0,

		/// <summary>
		/// Gateway is specified by an IPv4 address.
		/// </summary>
		IPV4GatewayAddress = 1,

		/// <summary>
		/// Gateway is specified by an IPv6 address.
		/// </summary>
		IPV6GatewayAddress = 2,

		/// <summary>
		/// Gateway is specified by a domain name (wire-encoded).
		/// </summary>
		WireEncodedDomain = 3,
	}

	/// <summary>
	/// Enumerates cryptographic algorithms for IPSECKEY records.
	/// </summary>
	public enum IPSecAlgorithm : byte
	{
		/// <summary>
		/// No key present.
		/// </summary>
		NoKey = 0,

		/// <summary>
		/// DSA key.
		/// </summary>
		DSAKey = 1,

		/// <summary>
		/// RSA key.
		/// </summary>
		RSAKey = 2
	}

	/// <summary>
	/// Enumerates IANA address family types used in DNS-based IPSECKEY or other resource records
	/// that store numeric address family identifiers.
	/// </summary>
	public enum IANAddressFamily : ushort
	{
		/// <summary>
		/// Reserved (not valid).
		/// </summary>
		Reserved0 = 0,

		/// <summary>
		/// Internet Protocol (IP).
		/// </summary>
		IP = 4,

		/// <summary>
		/// ST Datagram Mode.
		/// </summary>
		STDM = 5,

		/// <summary>
		/// SIP (Session Initiation Protocol).
		/// </summary>
		SIP = 6,

		/// <summary>
		/// TP/IX transport protocol.
		/// </summary>
		IP_IX = 7,

		/// <summary>
		/// PIP protocol.
		/// </summary>
		PIP = 8,

		/// <summary>
		/// TUBA (TCP and UDP with Bigger Addresses).
		/// </summary>
		TUBA = 9,

		/// <summary>
		/// Reserved (value 15).
		/// </summary>
		Reserved15 = 15,

		/// <summary>
		/// Novell IPX protocol.
		/// </summary>
		IPX = 16
	}

	/// <summary>
	/// Enumerates identifier types for DHCID records (DHCP Identifier),
	/// distinguishing how a client or device is identified in DHCP.
	/// </summary>
	public enum DHCIDIdentifierTypes : ushort
	{
		/// <summary>
		/// The 1-octet 'htype' followed by 'hlen' octets of 'chaddr'
		/// from a DHCPv4 client's DHCPREQUEST message.
		/// </summary>
		DHCPv4ClientDHCPRequest = 0,

		/// <summary>
		/// The data octets (i.e., Type and Client-Identifier fields)
		/// from a DHCPv4 client's Client Identifier option.
		/// </summary>
		DHCPv4ClientIdentifierOption = 1,

		/// <summary>
		/// The client's DUID (i.e., the data octets of a DHCPv6 client's Client Identifier option
		/// or the DUID field from a DHCPv4 client's Client Identifier option).
		/// </summary>
		DHCPv4ClientDUIDField = 2
	}
}
