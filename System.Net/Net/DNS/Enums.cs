using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Utils.Net.DNS;


/// <summary>
/// Algorithm Number Specification
/// </summary>
public enum Algorithm : byte
{
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved0 = 0,
    RSA_MD5 = 1,
    DiffieHellman = 2,
    DSA_SHA1 = 3,
    EllipticCurve = 4,
    RSA_SHA1 = 5,
    IndirectKey = 252,
    Private_DomainName = 253,
    Private_OID = 254,
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved255 = 255
}

/// <summary>
/// Protocol which the key is to be used with
/// </summary>
public enum Protocol : byte
{
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved = 0,
    /// <summary>
    /// for use in connection with TLS.
    /// </summary>
    TLS = 1,
    /// <summary>
    /// for use in connection with email.
    /// </summary>
    email = 2,
    /// <summary>
    /// DNS security.  The protocol field SHOULD be set to
    ///  this value for zone keys and other keys used in DNS security.
    ///  Implementations that can determine that a key is a DNS
    ///  security key by the fact that flags label it a zone key or the
    ///  signatory flag field is non-zero are NOT REQUIRED to check the
    ///  protocol field.
    /// </summary>
    dnssec = 3,
    /// <summary>
    /// Oakley/IPSEC <see cref="https://datatracker.ietf.org/doc/html/rfc2401">[RFC 2401]</see> protocol
    ///  and indicates that this key is valid for use in conjunction
    ///  with that security standard.  This key could be used in
    ///  connection with secured communication on behalf of an end
    ///  entity or user whose name is the owner name of the KEY RR if
    ///  the entity or user flag bits are set.  The presence of a KEY
    ///  resource with this protocol value is an assertion that the
    ///  host speaks Oakley/IPSEC.
    /// </summary>
    IPSEC = 4,
    /// <summary>
    /// indicates that the key can be used in connection with any
    ///  protocol for which KEY RR protocol octet values have been
    ///  defined.  The use of this value is discouraged and the use of
    ///  different keys for different protocols is encouraged.
    /// </summary>
    All = 255
}

public enum DigestTypes : byte
{
    Reserved = 0,
    SHA1 = 1,
}

public enum CertificateTypes : ushort
{
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved = 0,
    /// <summary>
    /// X.509 as per PKIX
    /// </summary>
    PKIX = 1,
    /// <summary>
    /// SPKI certificate
    /// </summary>
    SPKI = 2,
    /// <summary>
    /// OpenPGP packet
    /// </summary>
    PGP = 3,
    /// <summary>
    /// The URL of an X.509 data object
    /// </summary>
    IPKIX = 4,
    /// <summary>
    /// The URL of an SPKI certificate
    /// </summary>
    ISPKI = 5,
    /// <summary>
    /// The fingerprint and URL of an OpenPGP packet
    /// </summary>
    IPGP = 6,
    /// <summary>
    /// Attribute Certificate
    /// </summary>
    ACPKIX = 7,
    /// <summary>
    /// The URL of an Attribute Certificate
    /// </summary>
    IACPKIX = 8,
    /// <summary>
    /// URI private
    /// </summary>
    URI = 253,
    /// <summary>
    /// OID private
    /// </summary>
    OID = 254,
    /// <summary>
    /// Reserved
    /// </summary>
    ReservedFF = 255,
    /// <summary>
    /// Reserved
    /// </summary>
    ReservedFFFF = 65535
}

public enum SSHAlgorithm : byte
{
    Reserved = 0,
    RSA = 1,
    DSS = 2
}

public enum SSHFingerprintType : byte
{
    Reserved = 0,
    SHA1 = 1
}

public enum GatewayType : byte
{
    NoGateway = 0,
    IPV4GatewayAddress = 1,
    IPV6GatewayAddress = 2,
    WireEncodedDomain = 3,
}

public enum IPSecAlgorithm : byte
{
    NoKey = 0,
    DSAKey = 1,
    RSAKey = 2
}

public enum IANAddressFamily : ushort
{
    Reserved0 = 0,
    /// <summary>
    /// Internet Protocol (IP)
    /// </summary>
    IP = 4,
    /// <summary>
    /// ST Datagram Mode
    /// </summary>
    STDM = 5,
    /// <summary>
    /// SIP
    /// </summary>
    SIP = 6,
    /// <summary>
    /// TP/IX
    /// </summary>
    IP_IX = 7,
    /// <summary>
    /// PIP
    /// </summary>
    PIP = 8,
    /// <summary>
    /// TUBA
    /// </summary>
    TUBA = 9,
    Reserved15 = 15,
    /// <summary>
    /// Novell IPX
    /// </summary>
    IPX = 16
}

public enum DHCIDIdentifierTypes : ushort
{
    /// <summary>
    /// The 1-octet 'htype' followed by 'hlen' octets
    /// of 'chaddr' from a DHCPv4 client's DHCPREQUEST
    /// </summary>
    DHCPv4ClientDHCPRequest = 0,
    /// <summary>
    /// The data octets (i.e., the Type and
    /// Client-Identifier fields) from a DHCPv4
    /// client's Client Identifier option.
    /// </summary>
    DHCPv4ClientIdentifierOption = 1,
    /// <summary>
    /// The client's DUID (i.e., the data octets of a
    /// DHCPv6 client's Client Identifier option
    /// or the DUID field from a DHCPv4 client's
    /// Client Identifier option).
    /// </summary>
    DHCPv4ClientDUIDField = 2
}
