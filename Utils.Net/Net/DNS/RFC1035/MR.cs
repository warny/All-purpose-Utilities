using System;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an MR (Mail Rename) record, marked as experimental and obsolete as per
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.8">RFC 1035 §3.3.8</see>.
/// </summary>
/// <remarks>
/// <para>
/// An MR record (type code 0x09) specifies a new mailbox domain name (<see cref="NewName"/>)
/// for redirecting mail from an older mailbox name. It was once used for forwarding
/// or renaming a mailbox, but modern DNS practice suggests using MX records for
/// mail routing.
/// </para>
/// <para>
/// This class is annotated with <see cref="ObsoleteAttribute"/> and
/// <c>[DNSRecord(DNSClass.IN, 0x09)]</c>, indicating that it should not be used in production
/// scenarios due to obsolescence.
/// </para>
/// <para>The experimental RDATA layout is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   NEWNAME                     /
/// /                                               /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// <c>NEWNAME</c> is a <c>&lt;domain-name&gt;</c> representing the replacement mailbox host.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x09)]
[DNSTextRecord("{NewName}")]
[Obsolete("MR (mail redirection) records are obsolete; use MX records instead.")]
public class MR : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the new mailbox domain name. This is the name to which
    /// mail was originally meant to be redirected.
    /// </summary>
    [DNSField]
    public DNSDomainName NewName { get; set; }

    /// <summary>
    /// Returns the <see cref="NewName"/> string representation.
    /// Since <see cref="DNSDomainName"/> is a struct, it can't be null
    /// but can be empty if uninitialized.
    /// </summary>
    public override string ToString() => NewName.ToString();
}
