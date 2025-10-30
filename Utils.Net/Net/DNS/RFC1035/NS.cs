using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC1035
{
    /// <summary>
    /// Represents an NS (Name Server) record as specified in
    /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.11">RFC 1035 §3.3.11</see>.
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
    /// <para>The wire-format RDATA layout defined in
    /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.11">RFC 1035 §3.3.11</see> is:</para>
    /// <code>
    /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /// /                   NSDNAME                     /
    /// /                                               /
    /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /// </code>
    /// <para>
    /// The <c>NSDNAME</c> value is a <c>&lt;domain-name&gt;</c> that identifies a host which
    /// should be authoritative for the owner name. Additional section processing
    /// is usually performed to provide glue A or AAAA records for that host.
    /// </para>
    /// </remarks>
    [DNSRecord(DNSClassId.IN, 0x02)]
    [DNSTextRecord("{DNSName}")]
    public class NS : DNSResponseDetail
    {
        /// <summary>
        /// Gets or sets the domain name of the authoritative name server
        /// (e.g., <c>ns1.example.com</c>).
        /// </summary>
        [DNSField]
        public DNSDomainName DNSName { get; set; }
    }
}
