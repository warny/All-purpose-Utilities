using System;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
        /// Represents an MD (Mail Destination) record, which is an obsolete DNS record type
        /// as per <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.4">RFC 1035 §3.3.4</see>.
	/// </summary>
        /// <remarks>
        /// <para>
        /// MD records were originally used to specify a host (see <see cref="MadName"/>)
        /// that acted as a mail agent for the domain. Resolvers would perform additional
        /// section processing to locate an A record for that host.
        /// </para>
        /// <para>
        /// However, the MD record type is marked as <c>obsolete</c> because mail routing
        /// has long been replaced by MX records, as detailed in
        /// <see href="https://www.rfc-editor.org/rfc/rfc974">RFC 974</see>. The recommended
        /// policy upon encountering an MD record is to treat it as an MX record with a
        /// preference of <c>0</c>.
        /// </para>
        /// <para>
        /// This class is annotated with <c>[Obsolete]</c> and <c>[DNSRecord(DNSClass.IN, 0x03)]</c>
        /// for completeness but should not be used in modern DNS scenarios.
        /// </para>
        /// <para>The obsolete RDATA format is:</para>
        /// <code>
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                   MADNAME                     /
        /// /                                               /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// </code>
        /// <para><c>MADNAME</c> is the mail agent host; resolvers historically fetched its address.</para>
        /// </remarks>
[DNSRecord(DNSClassId.IN, 0x03)]
[DNSTextRecord("{MadName}")]
[Obsolete("MD (Mail Destination) records are obsolete; use MX records instead.")]
public class MD : DNSResponseDetail
        {
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
