using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC4701
{
	/// <summary>
	/// Represents a DHCID (DHCP Identifier) record as defined in RFC 4701.
	/// The DHCID record is used by DHCP servers and clients to associate a DHCP client's identity
	/// with a DNS name, enabling deterministic dynamic DNS updates for a zone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The RDATA section of a DHCID RR in transmission contains RDLENGTH octets of binary data.
	/// The format of this data and its interpretation by DHCP servers and clients are described below.
	/// </para>
	/// <para>
	/// The DHCID RDATA consists of:
	/// </para>
	/// <list type="bullet">
	///   <item>
	///     <description>
	///       <b>Identifier type</b>: 2 octets in network byte order that specify the identifier type code.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Digest type</b>: 1 octet that indicates the digest type code.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       <b>Digest</b>: One or more octets representing the actual identifier, the length of which depends on the digest type.
	///     </description>
	///   </item>
	/// </list>
	/// <para>
	/// DNS software should treat the RDATA section as opaque. DHCP clients or servers use the DHCID record
	/// to link a DHCP client's identity with a DNS name so that multiple DHCP entities can deterministically
	/// perform dynamic DNS updates to the same zone.
	/// </para>
	/// </remarks>
	[DNSRecord(DNSClass.IN, 0x31)]
	public class DHCID : DNSResponseDetail
	{
		/// <summary>
		/// Gets or sets the Identifier Type field.
		/// This 2-octet value (in network byte order) specifies the format of the identifier that follows.
		/// </summary>
		[DNSField]
		public DHCIDIdentifierTypes IdentifierTypes { get; set; }

		/// <summary>
		/// Gets or sets the Digest Type Code.
		/// This 1-octet field indicates the digest algorithm used for generating the digest portion of the DHCID.
		/// </summary>
		[DNSField]
		public byte DigestTypeCode { get; set; }

		/// <summary>
		/// Gets or sets the Digest field.
		/// This is a variable-length byte array representing the actual identifier.
		/// The length of this field depends on the digest type specified.
		/// </summary>
		[DNSField]
		public byte[] Digest { get; set; }
	}
}
