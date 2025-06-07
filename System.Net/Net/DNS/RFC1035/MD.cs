using System;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
	/// Represents an MD (Mail Destination) record, which is an obsolete DNS record type
	/// as per RFC 1035 Section 3.3.4.
	/// </summary>
	/// <remarks>
	/// <para>
	/// MD records were originally used to specify a host (see <see cref="MadName"/>)
	/// that acted as a mail agent for the domain. Resolvers would perform additional
	/// section processing to locate an A record for that host. 
	/// </para>
	/// <para>
	/// However, the MD record type is marked as <c>obsolete</c> because mail routing
	/// has long been replaced by MX records, as detailed in RFC 974. The recommended
	/// policy upon encountering an MD record is to treat it as an MX record with a 
	/// preference of <c>0</c>.
	/// </para>
	/// <para>
	/// This class is annotated with <c>[Obsolete]</c> and <c>[DNSRecord(DNSClass.IN, 0x03)]</c>
	/// for completeness but should not be used in modern DNS scenarios.
	/// </para>
	/// </remarks>
[DNSRecord(DNSClass.IN, 0x03)]
[DNSTextRecord("{MadName}")]
[Obsolete("MD (Mail Destination) records are obsolete; use MX records instead.")]
public class MD : DNSResponseDetail
	{
		/*
            MD RDATA format (Obsolete)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   MADNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            MADNAME         A <domain-name> which specifies a host which has a mail
                            agent for the domain which should be able to deliver
                            mail for the domain.

            MD records cause additional section processing which looks up an A type
            record corresponding to MADNAME.

            MD is obsolete.  See the definition of MX and [RFC-974] for details of
            the new scheme.  The recommended policy for dealing with MD RRs found in
            a master file is to reject them, or to convert them to MX RRs with a
            preference of 0.
        */

		/// <summary>
		/// Gets or sets the mailbox agent domain name (MADNAME) for the (now obsolete) MD record.
		/// </summary>
		[DNSField]
		public DNSDomainName MadName { get; set; }

		/// <summary>
		/// Returns the domain name associated with this obsolete mail destination record.
		/// </summary>
		public override string ToString() => MadName.ToString();
	}
}
