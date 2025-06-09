using System;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
	/// Represents an MF (Mail Forwarder) record, an older mail-routing DNS record type 
	/// now considered obsolete. Similar to MD, the MF record has been superseded by MX.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The MF record contains a <see cref="MadName"/> field, indicating a host that will 
	/// accept mail for the domain and forward it. However, per RFC 974, modern DNS practice 
	/// recommends using MX records instead. If an MF record is found, a recommended approach 
	/// is to convert it to an MX record with a preference of 10.
	/// </para>
	/// <para>
	/// This class is annotated with <c>[DNSRecord(DNSClass.IN, 0x04)]</c> for the IN class 
	/// and type code <c>4</c>, but also marked <c>[Obsolete]</c> to indicate it should not 
	/// be used in modern DNS configurations.
	/// </para>
	/// </remarks>
[DNSRecord(DNSClassId.IN, 0x04)]
[DNSTextRecord("{MadName}")]
[Obsolete("MF (Mail Forwarder) records are obsolete; use MX records instead.")]
public class MF : DNSResponseDetail
	{
		/*
            MF RDATA format (Obsolete)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   MADNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            MADNAME         A <domain-name> which specifies a host which has a mail
                            agent for the domain which will accept mail for
                            forwarding to the domain.

            MF records cause additional section processing which looks up an A type
            record corresponding to MADNAME.

            MF is obsolete.  See the definition of MX and [RFC-974] for details of
            the new scheme. The recommended policy for dealing with MF RRs found in
            a master file is to reject them, or to convert them to MX RRs with a
            preference of 10.
        */

		/// <summary>
		/// Gets or sets the <see cref="DNSDomainName"/> that indicates the host (MADNAME) 
		/// responsible for mail forwarding for the domain. Since <see cref="DNSDomainName"/> 
		/// is a struct, it cannot be null, but it can hold a default (empty) value.
		/// </summary>
		[DNSField]
		public DNSDomainName MadName { get; set; }

		/// <summary>
		/// Returns the string representation of the <see cref="MadName"/> field.
		/// Because <see cref="DNSDomainName"/> is a struct, it is never <c>null</c>.
		/// </summary>
		public override string ToString() => MadName.ToString();
	}
}
