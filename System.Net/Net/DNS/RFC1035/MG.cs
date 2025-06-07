using System;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an MG (Mail Group) record, classified as experimental in RFC 1035.
/// </summary>
/// <remarks>
/// <para>
/// MG records store a <see cref="DNSDomainName"/> (<see cref="MGName"/>) for a mailbox that is
/// a member of a mail group. Unlike some other mail-related records, MG does not trigger
/// additional section processing in DNS resolution.
/// </para>
/// <para>
/// The record type is <c>0x08</c> for <see cref="DNSClass.IN"/>. Although it may still be
/// encountered, MG is considered part of an older, less common suite of experimental mail
/// group DNS records. Modern mail routing largely relies on MX, but MG is included here for
/// completeness.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x08)]
[DNSTextRecord("{MGName}")]
[Obsolete("MG (Mail Group) records are obsolete; use MX records instead.")]
public class MG : DNSResponseDetail
{
	/*
            MG RDATA format (EXPERIMENTAL)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   MGMNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            MGMNAME         A <domain-name> which specifies a mailbox that is
                            a member of the mail group specified by the owner name.

            MG records cause no additional section processing.
        */

	/// <summary>
	/// Gets or sets the mailbox domain name (MGMNAME) associated with this mail group record.
	/// Because <see cref="DNSDomainName"/> is a struct, it cannot be null.
	/// </summary>
	[DNSField]
	public DNSDomainName MGName { get; set; }

	/// <summary>
	/// Returns the string representation of <see cref="MGName"/>.
	/// </summary>
	public override string ToString() => MGName.ToString();
}
