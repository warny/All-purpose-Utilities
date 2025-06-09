using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC4034
{
	/// <summary>
	/// Represents a DS (Delegation Signer) record as specified in RFC 4034.
	/// DS records are used to securely delegate authority from a parent zone to a child zone
	/// by providing a cryptographic digest of a DNSKEY record from the child zone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The RDATA for a DS RR consists of:
	/// </para>
	/// <code>
	///                           1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
	///       0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
	///      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///      |           Key Tag             |  Algorithm    |  Digest Type  |
	///      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///      /                                                               /
	///      /                            Digest                             /
	///      /                                                               /
	///      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	/// </code>
	/// <para>
	/// Fields:
	/// </para>
	/// <list type="bullet">
	///   <item>
	///     <description>
	///       <b>Key Tag</b>: A 16-bit integer that acts as an identifier for the DNSKEY
	///       record in the child zone. It allows resolvers to efficiently select the correct key.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Algorithm</b>: An 8-bit field that specifies the cryptographic algorithm used for the DNSKEY.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Digest Type</b>: An 8-bit field that indicates the digest algorithm used to generate the digest from the DNSKEY.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Digest</b>: A variable-length byte array that contains the digest (hash) of the DNSKEY.
	///       This digest is used to authenticate the DNSKEY record.
	///     </description>
	///   </item>
	/// </list>
	/// <para>
	/// The DS record is a critical element in DNSSEC, providing a secure link between a parent zone and its child.
	/// </para>
	/// </remarks>
        [DNSRecord(DNSClass.IN, 0x2B)]
        [DNSTextRecord("{KeyTag} {Algorithm} {DigestType} {Digest}")]
        public class DS : DNSResponseDetail
	{
		/// <summary>
		/// Gets or sets the Key Tag, a 16-bit integer used to identify the DNSKEY record that corresponds to this DS record.
		/// </summary>
		[DNSField]
		public ushort KeyTag { get; set; }

		/// <summary>
		/// Gets or sets the Algorithm field, an 8-bit value that specifies the cryptographic algorithm
		/// used for the DNSKEY (e.g., RSA/MD5, DSA, etc.).
		/// </summary>
		[DNSField]
		public Algorithm Algorithm { get; set; }

		/// <summary>
		/// Gets or sets the Digest Type field, an 8-bit value that indicates the digest algorithm used
		/// to generate the digest from the DNSKEY.
		/// </summary>
		[DNSField]
		public DigestTypes DigestType { get; set; }

		/// <summary>
		/// Gets or sets the Digest field, a variable-length byte array containing the cryptographic digest of the DNSKEY.
		/// </summary>
		[DNSField]
		public byte[] Digest { get; set; }
	}
}
