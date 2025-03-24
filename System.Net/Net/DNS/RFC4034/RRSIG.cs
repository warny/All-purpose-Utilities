using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC4034;

/// <summary>
/// Represents an RRSIG (Resource Record Signature) record as defined in RFC 4034.
/// The RRSIG record is used in DNSSEC to authenticate RRsets by providing a digital
/// signature that covers the RRset data.
/// </summary>
/// <remarks>
/// <para>
/// The RDATA for an RRSIG RR consists of the following fields:
/// </para>
/// <code>
///                             1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
///         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |        Type Covered           |  Algorithm    |     Labels    |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |                         Original TTL                          |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |                      Signature Expiration                     |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |                      Signature Inception                      |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |            Key Tag            |                               /
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+         Signer's Name         /
///        /                                                               /
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        /                                                               /
///        /                            Signature                          /
///        /                                                               /
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// </code>
/// <para>
/// Field details:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Type Covered</b>: A 16-bit field indicating the RR type that is covered by this signature.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Algorithm</b>: An 8-bit field specifying the cryptographic algorithm used.
///       See Section 3.2 of RFC 4034.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Labels</b>: An 8-bit field representing the number of labels in the original
///       SIG RR owner name (excluding the null label for root and any leading wildcard).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Original TTL</b>: A 32-bit unsigned integer representing the original TTL of
///       the RRset covered by the signature. This value is protected by the signature.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Signature Expiration</b>: A 32-bit unsigned integer representing the time (in seconds
///       since 1 January 1970, GMT) after which the signature is no longer valid.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Signature Inception</b>: A 32-bit unsigned integer representing the time (in seconds
///       since 1 January 1970, GMT) at which the signature becomes valid.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Key Tag</b>: A 16-bit value used to efficiently identify the DNSKEY record that
///       corresponds to this signature.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Signer's Name</b>: A domain name representing the signer (i.e., the owner of the DNSKEY
///       record used for verification). It may be compressed when transmitted.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Signature</b>: A variable-length field containing the cryptographic signature that
///       authenticates the covered RRset.
///     </description>
///   </item>
/// </list>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x2E)]
public class RRSIG : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the Type Covered field, a 16-bit integer that indicates the type of RRset
	/// that is being authenticated by this SIG record.
	/// </summary>
	[DNSField]
	public ushort TypeCovered { get; set; }

	/// <summary>
	/// Gets or sets the Algorithm field, an 8-bit integer representing the cryptographic algorithm
	/// used for the signature (e.g., RSA, DSA, etc.).
	/// </summary>
	[DNSField]
	public byte Algorithm { get; set; }

	/// <summary>
	/// Gets or sets the Labels field, an 8-bit unsigned integer indicating the number of labels
	/// in the original SIG RR owner name (excluding the root label and any wildcard "*").
	/// </summary>
	[DNSField]
	public byte Labels { get; set; }

	/// <summary>
	/// Gets or sets the Original TTL field, a 32-bit unsigned integer representing the original TTL
	/// of the RRset covered by this SIG record.
	/// </summary>
	[DNSField]
	public uint OriginalTTL { get; set; }

	/// <summary>
	/// Gets or sets the Signature Expiration field, a 32-bit unsigned integer representing the time
	/// (in seconds since January 1, 1970, GMT) after which this SIG record is no longer valid.
	/// </summary>
	[DNSField]
	public uint SignatureExpiration { get; set; }

	/// <summary>
	/// Gets or sets the Signature Inception field, a 32-bit unsigned integer representing the time
	/// (in seconds since January 1, 1970, GMT) at which this SIG record becomes valid.
	/// </summary>
	[DNSField]
	public uint SignatureInception { get; set; }

	/// <summary>
	/// Gets or sets the Key Tag field, a 16-bit integer used to help identify the DNSKEY record
	/// associated with this signature.
	/// </summary>
	[DNSField]
	public ushort KeyTag { get; set; }

	/// <summary>
	/// Gets or sets the Signer's Name field, which is the domain name of the signer that generated
	/// this SIG record. It identifies the DNSKEY record used to verify the signature.
	/// </summary>
	[DNSField(FieldsSizeOptions.PrefixedSize1B)]
	public string SignerName { get; set; }

	/// <summary>
	/// Gets or sets the Signature field, a variable-length byte array containing the digital signature
	/// that authenticates the RRset.
	/// </summary>
	[DNSField]
	public byte[] Signature { get; set; }
}
