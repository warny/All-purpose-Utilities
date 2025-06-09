using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035
{
	/// <summary>
	/// Represents an NS (Name Server) record as specified in RFC 1035 Section 3.3.11.
	/// An NS record indicates the authoritative name server for a given domain.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The NS record stores the domain name (<see cref="DNSName"/>) of a host that
	/// should be authoritative for the owner name of this record. For example,
	/// if this record is under <c>example.com</c>, <c>DNSName</c> might be
	/// <c>ns1.example.net</c>, meaning <c>ns1.example.net</c> is an authoritative
	/// name server for <c>example.com</c>.
	/// </para>
	/// <para>
	/// Usage typically involves additional section processing within DNS responses
	/// to provide "glue records" (A or AAAA RRs) so resolvers can quickly find
	/// the IP addresses of these name servers.
	/// </para>
	/// </remarks>
        [DNSRecord(DNSClassId.IN, 0x02)]
        [DNSTextRecord("{DNSName}")]
        public class NS : DNSResponseDetail
	{
		/*
            NS RDATA format (RFC 1035 Section 3.3.11):

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   NSDNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            NSDNAME         A <domain-name> which specifies a host which should be
                            authoritative for the specified class and domain.

            NS records cause both the usual additional section processing to locate
            a type A or AAAA record, and, when used in a referral, a special search
            of the zone in which they reside for glue information.

            The NS RR states that the named host should be expected to have a zone
            starting at the owner name of the specified class.
        */

		/// <summary>
		/// Gets or sets the domain name of the authoritative name server
		/// (e.g., <c>ns1.example.com</c>).
		/// </summary>
		[DNSField]
		public DNSDomainName DNSName { get; set; }
	}
}
