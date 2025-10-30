namespace Utils.Net.DNS.RFC4034;

/// <summary>
/// Represents a DNSKEY record as specified in RFC 4034.
/// A DNSKEY record stores a public key used to verify DNSSEC signatures and is a critical component in the
/// establishment of a chain of trust in DNSSEC.
/// </summary>
/// <remarks>
/// <para>
/// The DNSKEY record contains the following fields:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Flags</b>: A 16-bit field that defines the properties of the key. For example, the Secure Entry Point (SEP)
///       flag indicates whether the key is a key-signing key (KSK) used to sign DNSKEY records.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Protocol</b>: An 8-bit field that must be set to 3 for DNSSEC.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Algorithm</b>: An 8-bit field indicating the cryptographic algorithm used (e.g., RSA, DSA, ECDSA).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Key</b>: A variable-length field that contains the public key data used for signature verification.
///     </description>
///   </item>
/// </list>
/// <para>
/// These fields are used in conjunction with RRSIG records to validate the authenticity and integrity of DNS data.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x30)]
[DNSTextRecord("{Flags} {Protocol} {Algorithm} {Key}")]
public class DNSKEY : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the Flags field of the DNSKEY record.
    /// This 16-bit field specifies properties such as whether the key is a zone key or a key-signing key (KSK).
    /// </summary>
    [DNSField]
    public ushort Flags { get; set; }

    /// <summary>
    /// Gets or sets the Protocol field of the DNSKEY record.
    /// This 8-bit field must always be set to 3 for DNSSEC.
    /// </summary>
    [DNSField]
    public byte Protocol { get; set; }

    /// <summary>
    /// Gets or sets the Algorithm field of the DNSKEY record.
    /// This 8-bit field specifies the cryptographic algorithm used by the key (e.g., RSA, DSA, ECDSA).
    /// </summary>
    [DNSField]
    public byte Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the Key field of the DNSKEY record.
    /// This variable-length field contains the public key data used for DNSSEC signature verification.
    /// </summary>
    [DNSField]
    public byte[] Key { get; set; }
}
