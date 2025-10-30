using System;

namespace Utils.Net.DNS.RFC1035;

/// <summary>
/// Represents an MB (Mailbox) DNS record, as described in
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.3">RFC 1035 §3.3.3</see>
/// (marked as experimental).
/// </summary>
/// <remarks>
/// The MB record specifies a domain name (represented by <see cref="MadName"/>) that hosts a particular
/// mailbox. A DNS resolver processing an MB record is advised to look up corresponding A or AAAA records
/// for the host identified in <see cref="MadName"/>.
/// <para>
/// MB is part of an older or less commonly used set of mail-related DNS records (along with MG and MR).
/// Modern mail routing primarily relies on MX records, but MB is included here for completeness in certain
/// experimental or legacy scenarios.
/// </para>
/// <para>
/// Type code is set to <c>0x07</c> for class <see cref="DNSClassId.IN"/>, as per
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.3">RFC 1035 §3.3.3</see>.
/// </para>
/// <para>The experimental RDATA layout is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                   MADNAME                     /
/// /                                               /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para><c>MADNAME</c> is the host that holds the specified mailbox; resolvers often fetch A/AAAA glue.</para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x07)]
[DNSTextRecord("{MadName}")]
[Obsolete("MB (Mailbox) records are obsolete; use MX records instead.")]
public class MB : DNSResponseDetail
{
    /// <summary>
    /// Gets or sets the mailbox domain name (MADNAME), the host that contains the
    /// specified mailbox.
    /// </summary>
    [DNSField]
    public DNSDomainName MadName { get; set; }

    /// <summary>
    /// Returns the mailbox domain name stored in <see cref="MadName"/>.
    /// </summary>
    public override string ToString() => MadName.ToString();
}
