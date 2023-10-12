using System;
using System.Runtime.Intrinsics.X86;

namespace Utils.Net.DNS.RFC2535;

[DNSClass(0x19)]
public class KEY : DNSResponseDetail
{
    /*
        3. The KEY Resource Record

           The KEY resource record (RR) is used to store a public key that is
           associated with a Domain Name System (DNS) name.  This can be the
           public key of a zone, a user, or a host or other end entity. Security
           aware DNS implementations MUST be designed to handle at least two
           simultaneously valid keys of the same type associated with the same
           name.

           The type number for the KEY RR is 25.

           A KEY RR is, like any other RR, authenticated by a SIG RR.  KEY RRs
           must be signed by a zone level key.

        3.1 KEY RDATA format

           The RDATA for a KEY RR consists of flags, a protocol octet, the
           algorithm number octet, and the public key itself.  The format is as
           follows:
                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |             flags             |    protocol   |   algorithm   |
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |                                                               /
           /                          public key                           /
           /                                                               /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-|

           The KEY RR is not intended for storage of certificates and a separate
           certificate RR has been developed for that purpose, defined in [RFC
           2538].

           The meaning of the KEY RR owner name, flags, and protocol octet are
           described in Sections 3.1.1 through 3.1.5 below.  The flags and
           algorithm must be examined before any data following the algorithm
           octet as they control the existence and format of any following data.
           The algorithm and public key fields are described in Section 3.2.
           The format of the public key is algorithm dependent.

           KEY RRs do not specify their validity period but their authenticating
           SIG RR(s) do as described in Section 4 below.

        3.1.1 Object Types, DNS Names, and Keys

           The public key in a KEY RR is for the object named in the owner name.

           A DNS name may refer to three different categories of things.  For
           example, foo.host.example could be (1) a zone, (2) a host or other
           end entity , or (3) the mapping into a DNS name of the user or
           account foo@host.example.  Thus, there are flag bits, as described
           below, in the KEY RR to indicate with which of these roles the owner
           name and public key are associated.  Note that an appropriate zone
           KEY RR MUST occur at the apex node of a secure zone and zone KEY RRs
           occur only at delegation points.

        3.1.2 The KEY RR Flag Field

           In the "flags" field:

             0   1   2   3   4   5   6   7   8   9   0   1   2   3   4   5
           +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
           |  A/C  | Z | XT| Z | Z | NAMTYP| Z | Z | Z | Z |      SIG      |
           +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

           Bit 0 and 1 are the key "type" bits whose values have the following
           meanings:
                   10: Use of the key is prohibited for authentication.
                   01: Use of the key is prohibited for confidentiality.
                   00: Use of the key for authentication and/or confidentiality
                       is permitted. Note that DNS security makes use of keys
                       for authentication only. Confidentiality use flagging is
                       provided for use of keys in other protocols.
                       Implementations not intended to support key distribution
                       for confidentiality MAY require that the confidentiality
                       use prohibited bit be on for keys they serve.
                   11: If both bits are one, the "no key" value, there is no key
                       information and the RR stops after the algorithm octet.
                       By the use of this "no key" value, a signed KEY RR can
                       authenticatably assert that, for example, a zone is not
                       secured.  See section 3.4 below.

           Bits 2 is reserved and must be zero.

           Bits 3 is reserved as a flag extension bit.  If it is a one, a second
                  16 bit flag field is added after the algorithm octet and
                  before the key data.  This bit MUST NOT be set unless one or
                  more such additional bits have been defined and are non-zero.

           Bits 4-5 are reserved and must be zero.

           Bits 6 and 7 form a field that encodes the name type. Field values
           have the following meanings:

                   00: indicates that this is a key associated with a "user" or
                       "account" at an end entity, usually a host.  The coding
                       of the owner name is that used for the responsible
                       individual mailbox in the SOA and RP RRs: The owner name
                       is the user name as the name of a node under the entity
                       name.  For example, "j_random_user" on
                       host.subdomain.example could have a public key associated
                       through a KEY RR with name
                       j_random_user.host.subdomain.example.  It could be used
                       in a security protocol where authentication of a user was
                       desired.  This key might be useful in IP or other
                       security for a user level service such a telnet, ftp,
                       rlogin, etc.
                   01: indicates that this is a zone key for the zone whose name
                       is the KEY RR owner name.  This is the public key used
                       for the primary DNS security feature of data origin
                       authentication.  Zone KEY RRs occur only at delegation
                       points.
                   10: indicates that this is a key associated with the non-zone
                       "entity" whose name is the RR owner name.  This will
                       commonly be a host but could, in some parts of the DNS
                       tree, be some other type of entity such as a telephone
                       number [RFC 1530] or numeric IP address.  This is the
                       public key used in connection with DNS request and
                       transaction authentication services.  It could also be
                       used in an IP-security protocol where authentication at
                       the host, rather than user, level was desired, such as
                       routing, NTP, etc.
                   11: reserved.

           Bits 8-11 are reserved and must be zero.

           Bits 12-15 are the "signatory" field.  If non-zero, they indicate
                      that the key can validly sign things as specified in DNS
                      dynamic update [RFC 2137].  Note that zone keys (see bits
                      6 and 7 above) always have authority to sign any RRs in
                      the zone regardless of the value of the signatory field.

        3.1.3 The Protocol Octet

           It is anticipated that keys stored in DNS will be used in conjunction
           with a variety of Internet protocols.  It is intended that the
           protocol octet and possibly some of the currently unused (must be
           zero) bits in the KEY RR flags as specified in the future will be
           used to indicate a key's validity for different protocols.

           The following values of the Protocol Octet are reserved as indicated:

                VALUE   Protocol

                  0      -reserved
                  1     TLS
                  2     email
                  3     dnssec
                  4     IPSEC
                 5-254   - available for assignment by IANA
                255     All

           In more detail:
                1 is reserved for use in connection with TLS.
                2 is reserved for use in connection with email.
                3 is used for DNS security.  The protocol field SHOULD be set to
                  this value for zone keys and other keys used in DNS security.
                  Implementations that can determine that a key is a DNS
                  security key by the fact that flags label it a zone key or the
                  signatory flag field is non-zero are NOT REQUIRED to check the
                  protocol field.
                4 is reserved to refer to the Oakley/IPSEC [RFC 2401] protocol
                  and indicates that this key is valid for use in conjunction
                  with that security standard.  This key could be used in
                  connection with secured communication on behalf of an end
                  entity or user whose name is the owner name of the KEY RR if
                  the entity or user flag bits are set.  The presence of a KEY
                  resource with this protocol value is an assertion that the
                  host speaks Oakley/IPSEC.
                255 indicates that the key can be used in connection with any
                  protocol for which KEY RR protocol octet values have been
                  defined.  The use of this value is discouraged and the use of
                  different keys for different protocols is encouraged.

        3.2 The KEY Algorithm Number Specification

           This octet is the key algorithm parallel to the same field for the
           SIG resource as described in Section 4.1.  The following values are
           assigned:

           VALUE   Algorithm

             0      - reserved, see Section 11
             1     RSA/MD5 [RFC 2537] - recommended
             2     Diffie-Hellman [RFC 2539] - optional, key only
             3     DSA [RFC 2536] - MANDATORY
             4     reserved for elliptic curve crypto
           5-251    - available, see Section 11
           252     reserved for indirect keys
           253     private - domain name (see below)
           254     private - OID (see below)
           255      - reserved, see Section 11

           Algorithm specific formats and procedures are given in separate
           documents.  The mandatory to implement for interoperability algorithm
           is number 3, DSA.  It is recommended that the RSA/MD5 algorithm,
           number 1, also be implemented.  Algorithm 2 is used to indicate
           Diffie-Hellman keys and algorithm 4 is reserved for elliptic curve.

           Algorithm number 252 indicates an indirect key format where the
           actual key material is elsewhere.  This format is to be defined in a
           separate document.

           Algorithm numbers 253 and 254 are reserved for private use and will
           never be assigned a specific algorithm.  For number 253, the public
           key area and the signature begin with a wire encoded domain name.
           Only local domain name compression is permitted.  The domain name
           indicates the private algorithm to use and the remainder of the
           public key area is whatever is required by that algorithm.  For
           number 254, the public key area for the KEY RR and the signature
           begin with an unsigned length byte followed by a BER encoded Object
           Identifier (ISO OID) of that length.  The OID indicates the private
           algorithm in use and the remainder of the area is whatever is
           required by that algorithm.  Entities should only use domain names
           and OIDs they control to designate their private algorithms.

           Values 0 and 255 are reserved but the value 0 is used in the
           algorithm field when that field is not used.  An example is in a KEY
           RR with the top two flag bits on, the "no-key" value, where no key is
           present.

        3.3 Interaction of Flags, Algorithm, and Protocol Bytes

           Various combinations of the no-key type flags, algorithm byte,
           protocol byte, and any future assigned protocol indicating flags are
           possible.  The meaning of these combinations is indicated below:

           NK = no key type (flags bits 0 and 1 on)
           AL = algorithm byte
           PR = protocols indicated by protocol byte or future assigned flags

           x represents any valid non-zero value(s).

            AL  PR   NK  Meaning
             0   0   0   Illegal, claims key but has bad algorithm field.
             0   0   1   Specifies total lack of security for owner zone.
             0   x   0   Illegal, claims key but has bad algorithm field.
             0   x   1   Specified protocols unsecured, others may be secure.
             x   0   0   Gives key but no protocols to use it.
             x   0   1   Denies key for specific algorithm.
             x   x   0   Specifies key for protocols.
             x   x   1   Algorithm not understood for protocol.

        3.4 Determination of Zone Secure/Unsecured Status

           A zone KEY RR with the "no-key" type field value (both key type flag
           bits 0 and 1 on) indicates that the zone named is unsecured while a
           zone KEY RR with a key present indicates that the zone named is
           secure.  The secured versus unsecured status of a zone may vary with
           different cryptographic algorithms.  Even for the same algorithm,
           conflicting zone KEY RRs may be present.

           Zone KEY RRs, like all RRs, are only trusted if they are
           authenticated by a SIG RR whose signer field is a signer for which
           the resolver has a public key they trust and where resolver policy
           permits that signer to sign for the KEY owner name.  Untrusted zone
           KEY RRs MUST be ignored in determining the security status of the
           zone.  However, there can be multiple sets of trusted zone KEY RRs
           for a zone with different algorithms, signers, etc.
           For any particular algorithm, zones can be (1) secure, indicating
           that any retrieved RR must be authenticated by a SIG RR or it will be
           discarded as bogus, (2) unsecured, indicating that SIG RRs are not
           expected or required for RRs retrieved from the zone, or (3)
           experimentally secure, which indicates that SIG RRs might or might
           not be present but must be checked if found.  The status of a zone is
           determined as follows:

           1. If, for a zone and algorithm, every trusted zone KEY RR for the
              zone says there is no key for that zone, it is unsecured for that
              algorithm.

           2. If, there is at least one trusted no-key zone KEY RR and one
              trusted key specifying zone KEY RR, then that zone is only
              experimentally secure for the algorithm.  Both authenticated and
              non-authenticated RRs for it should be accepted by the resolver.

           3. If every trusted zone KEY RR that the zone and algorithm has is
              key specifying, then it is secure for that algorithm and only
              authenticated RRs from it will be accepted.

           Examples:

           (1)  A resolver initially trusts only signatures by the superzone of
           zone Z within the DNS hierarchy.  Thus it will look only at the KEY
           RRs that are signed by the superzone.  If it finds only no-key KEY
           RRs, it will assume the zone is not secure.  If it finds only key
           specifying KEY RRs, it will assume the zone is secure and reject any
           unsigned responses.  If it finds both, it will assume the zone is
           experimentally secure

           (2)  A resolver trusts the superzone of zone Z (to which it got
           securely from its local zone) and a third party, cert-auth.example.
           When considering data from zone Z, it may be signed by the superzone
           of Z, by cert-auth.example, by both, or by neither.  The following
           table indicates whether zone Z will be considered secure,
           experimentally secure, or unsecured, depending on the signed zone KEY
           RRs for Z;

                              c e r t - a u t h . e x a m p l e

                KEY RRs|   None    |  NoKeys   |  Mixed   |   Keys   |
             S       --+-----------+-----------+----------+----------+
             u  None   | illegal   | unsecured | experim. | secure   |
             p       --+-----------+-----------+----------+----------+
             e  NoKeys | unsecured | unsecured | experim. | secure   |
             r       --+-----------+-----------+----------+----------+
             Z  Mixed  | experim.  | experim.  | experim. | secure   |
             o       --+-----------+-----------+----------+----------+
             n  Keys   | secure    | secure    | secure   | secure   |
             e         +-----------+-----------+----------+----------+

        3.5 KEY RRs in the Construction of Responses

           An explicit request for KEY RRs does not cause any special additional
           information processing except, of course, for the corresponding SIG
           RR from a security aware server (see Section 4.2).

           Security aware DNS servers include KEY RRs as additional information
           in responses, where a KEY is available, in the following cases:

           (1) On the retrieval of SOA or NS RRs, the KEY RRset with the same
           name (perhaps just a zone key) SHOULD be included as additional
           information if space is available. If not all additional information
           will fit, type A and AAAA glue RRs have higher priority than KEY
           RR(s).

           (2) On retrieval of type A or AAAA RRs, the KEY RRset with the same
           name (usually just a host RR and NOT the zone key (which usually
           would have a different name)) SHOULD be included if space is
           available.  On inclusion of A or AAAA RRs as additional information,
           the KEY RRset with the same name should also be included but with
           lower priority than the A or AAAA RRs.    
     */


    [DNSField]
    public ushort Flags { get; private set; }
    
    [DNSField]
    public Protocol Protocol { get; set; }
    
    [DNSField]
    public Algorithm Algorithm { get; set; }
    
    [DNSField(Condition="Extended")]
    public ushort Extension {get; set; }
    
    [DNSField]
    public byte[] PublicKey { get; set; }

    public bool ProhibitedForAuthentication
    {
        get => (Flags & KeyFlags.ProhibitedForAuthentication) != 0;
        set => Flags = (ushort)((Flags & ~KeyFlags.ProhibitedForAuthentication) | (value ? KeyFlags.ProhibitedForAuthentication : 0));
    }

    public bool ProhibitedForConfidentiality
    {
        get => (Flags & KeyFlags.ProhibitedForConfidentiality) != 0;
        set => Flags = (ushort)((Flags & ~KeyFlags.ProhibitedForConfidentiality) | (value ? KeyFlags.ProhibitedForConfidentiality : 0));
    }

    public bool Extended
    {
        get => (Flags & KeyFlags.Extension) != 0;
        set => Flags = (ushort)((Flags & ~KeyFlags.Extension) | (value ? KeyFlags.Extension : 0));
    }

    public KeyOwner KeyOwner
    {
        get => (KeyOwner)(Flags & KeyFlagsMasks.KeyOwner);
        set => Flags = (ushort)((Flags & ~KeyFlagsMasks.KeyOwner) | (ushort)value);
    }

    public byte SignatoryField
    {
        get => (byte)(Flags & KeyFlagsMasks.SignatoryField);
        set => Flags = (ushort)((Flags & ~KeyFlagsMasks.SignatoryField) | ((ushort)value & KeyFlagsMasks.SignatoryField));
    }
}

static class KeyFlags
{
    // Bits 0 and 1 : Key usage
    public const ushort ProhibitedForAuthentication = (ushort)0b1000_0000_0000_0000;
    public const ushort ProhibitedForConfidentiality = (ushort)0b0100_0000_0000_0000;

    // Bit 3 : Exntension flag
    public const ushort Extension = (ushort)0b0001_0000_0000_0000;

    // Bits 6 and 7 : Key owner
    public const ushort UserOrAccountKey = (ushort)0b0000_0000_0000_0000;
    public const ushort ZoneKey = (ushort)0b0000_0010_0000_0000;
    public const ushort NonZoneKey = (ushort)0b0000_0100_0000_0000;
    public const ushort ReservedUseKey = (ushort)0b0000_0110_0000_0000;
}

public enum KeyOwner : ushort
{
    UserOrAccountKey = KeyFlags.UserOrAccountKey,
    ZoneKey = KeyFlags.ZoneKey,
    NonZoneKey = KeyFlags.NonZoneKey,
    ReservedUseKey = KeyFlags.ReservedUseKey
}

static class KeyFlagsMasks
{
    /// <summary>
    /// Bits 0 and 1 : Key usage
    /// </summary>
    public const ushort KeyUsage = (ushort)0b1100_0000_0000_0000;

    /// <summary>
    /// Bit 3 : Extension flag
    /// </summary>
    public const ushort Extension = (ushort)0b0001_0000_0000_0000;
    /// <summary>
    /// Bits 6 and 7 : Key owner
    /// </summary>
    public const ushort KeyOwner = (ushort)0b0000_0110_0000_0000;
    /// <summary>
    /// Bits 12-15 : SignatoryField
    ///  If non-zero, they indicate
    ///  that the key can validly sign things as specified in DNS
    ///  dynamic update[RFC 2137].  Note that zone keys(see bits
    ///  6 and 7 above) always have authority to sign any RRs in
    ///  the zone regardless of the value of the signatory field.
    /// </summary>
    public const ushort SignatoryField = (ushort)0b0000_0000_0000_1111;
}

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

/// <summary>
/// Algorithm Number Specification
/// </summary>
public enum Algorithm : byte
{
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved1 = 0,
    RSA_MD5 = 1,
    DiffieHellman = 2,
    DSA = 3,
    EllipticCurve = 4,
    IndirectKey = 252,
    Private_DomainName = 253,
    Private_OID = 254,
    /// <summary>
    /// Reserved
    /// </summary>
    Reserved2 = 255
}
