using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC5155
{
	/// <summary>
	/// Represents an NSEC3 (Next Secure 3) record as defined in RFC 5155.
	/// NSEC3 records are used in DNSSEC to provide authenticated denial of existence
	/// while mitigating zone enumeration. They replace the earlier NSEC records by hashing
	/// the owner names.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The RDATA of the NSEC3 RR is formatted as follows:
	/// </para>
	/// <code>
	///                             1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
	///         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
	///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///        |   Hash Alg.   |     Flags     |          Iterations           |
	///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///        |  Salt Length  |                     Salt                      /
	///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///        |  Hash Length  |             Next Hashed Owner Name            /
	///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///        /                         Type Bit Maps                         /
	///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	/// </code>
	/// <para>
	/// The fields are interpreted as follows:
	/// </para>
	/// <list type="bullet">
	///   <item>
	///     <description>
	///       <b>Hash Algorithm</b>: A single octet that specifies the hash algorithm used to hash the owner name.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Flags</b>: A single octet where the least significant bit (bit 0) is the Opt-Out flag.
	///       (Bit layout: bits 1-7 are reserved; bit 0 is the opt-out flag.)
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Iterations</b>: A 16-bit unsigned integer (most significant byte first) indicating the number of additional hash iterations.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Salt Length</b>: A single unsigned octet indicating the length of the following Salt field in octets.
	///       If this value is zero, the Salt field is omitted.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Salt</b>: A sequence of binary octets whose length is specified by the Salt Length field.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Hash Length</b>: A single unsigned octet indicating the length of the Next Hashed Owner Name field.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Next Hashed Owner Name</b>: The binary hash value (unencoded) of the next owner name,
	///       whose length is specified by the preceding Hash Length field.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Type Bit Maps</b>: A variable-length bit map indicating which RR types exist for the original owner name.
	///     </description>
	///   </item>
	/// </list>
	/// <para>
	/// This record type is used to securely indicate non-existence of names while preventing zone enumeration.
	/// </para>
	/// </remarks>
        [DNSRecord(DNSClass.IN, 0x32)]
        [DNSTextRecord("{HashAlgorithm} {Flag} {Iterations} {Salt} {NextHashOwnerName} {TypeBitMaps}")]
        public class NSEC3 : DNSResponseDetail
	{
		/// <summary>
		/// Gets or sets the hash algorithm used in the NSEC3 record.
		/// This is a single octet value.
		/// </summary>
		[DNSField]
		public byte HashAlgorithm { get; set; }

		/// <summary>
		/// Gets or sets the flags field. This is a single octet where the least significant bit (LSB)
		/// is the Opt-Out flag.
		/// </summary>
		[DNSField]
		public byte Flag { get; set; }

		/// <summary>
		/// Gets or sets the number of iterations to perform on the hash.
		/// This is a 16-bit unsigned integer with the most significant byte first.
		/// </summary>
		[DNSField]
		public ushort Iterations { get; set; }

		/// <summary>
		/// Gets or sets the salt. The salt is a sequence of binary octets whose length is
		/// determined by the preceding Salt Length field. A one-byte length prefix is used.
		/// </summary>
		[DNSField(FieldsSizeOptions.PrefixedSize1B)]
		public byte[] Salt { get; set; }

		/// <summary>
		/// Gets or sets the Next Hashed Owner Name field. This field contains the unencoded binary hash
		/// value of the next owner name, and its length is determined by a one-byte Hash Length prefix.
		/// </summary>
		[DNSField(FieldsSizeOptions.PrefixedSize1B)]
		public byte[] NextHashOwnerName { get; set; }

		/// <summary>
		/// Gets or sets the Type Bit Maps field, a variable-length bit map that indicates which RR types
		/// exist for the original owner name.
		/// </summary>
		[DNSField]
		public byte[] TypeBitMaps { get; set; }
	}
}
