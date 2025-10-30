using System;

namespace Utils.Net.DNS.RFC1035
{
    /// <summary>
    /// Represents an MF (Mail Forwarder) record, an older mail-routing DNS record type
    /// now considered obsolete per <see href="https://www.rfc-editor.org/rfc/rfc1035#section-3.3.5">RFC 1035 §3.3.5</see>.
    /// Similar to MD, the MF record has been superseded by MX.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MF record contains a <see cref="MadName"/> field, indicating a host that will
    /// accept mail for the domain and forward it. However, per
    /// <see href="https://www.rfc-editor.org/rfc/rfc974">RFC 974</see>, modern DNS practice
    /// recommends using MX records instead. If an MF record is found, a recommended approach
    /// is to convert it to an MX record with a preference of 10.
    /// </para>
    /// <para>
    /// This class is annotated with <c>[DNSRecord(DNSClass.IN, 0x04)]</c> for the IN class
    /// and type code <c>4</c>, but also marked <c>[Obsolete]</c> to indicate it should not
    /// be used in modern DNS configurations.
    /// </para>
    /// <para>The RDATA layout (now obsolete) is:</para>
    /// <code>
    /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /// /                   MADNAME                     /
    /// /                                               /
    /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /// </code>
    /// <para>
    /// <c>MADNAME</c> is a <c>&lt;domain-name&gt;</c> pointing to a host that accepts mail for
    /// the owner and forwards it. Older resolvers performed additional section processing to
    /// fetch address records for that host.
    /// </para>
    /// </remarks>
    [DNSRecord(DNSClassId.IN, 0x04)]
    [DNSTextRecord("{MadName}")]
    [Obsolete("MF (Mail Forwarder) records are obsolete; use MX records instead.")]
    public class MF : DNSResponseDetail
    {
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
