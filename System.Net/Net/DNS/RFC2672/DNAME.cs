using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC2672;

/// <summary>
/// Represents a DNAME (Delegation Name) record as defined in RFC 2672.
/// A DNAME record provides redirection for an entire subtree of the DNS namespace.
/// </summary>
/// <remarks>
/// <para>
/// The DNAME record is similar in purpose to a CNAME record, but whereas a CNAME
/// redirects a single alias to a canonical name, a DNAME record redirects an entire
/// subtree of the domain namespace. When a DNAME record is present for a name,
/// all queries for names in the subtree are mapped to a new domain name.
/// </para>
/// <para>
/// The RDATA of a DNAME record consists of a single <see cref="DNSDomainName"/>
/// which specifies the target domain for the redirection. For instance, if a DNAME record
/// is present for "foo.example.com" and its target is "bar.example.net", then a query for
/// "www.foo.example.com" would be redirected to "www.bar.example.net".
/// </para>
/// <para>
/// See RFC 2672 for complete details on the DNAME record format and its processing.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x27)]
public class DNAME : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the target domain name that replaces the original subtree.
	/// All queries falling under the DNAME record's owner name are redirected based
	/// on this target.
	/// </summary>
	[DNSField]
	public DNSDomainName Target { get; set; }
}
