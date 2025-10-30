using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC5155
{
    /// <summary>
    /// Represents the NSEC3PARAM resource record describing NSEC3 hashing parameters for a DNS zone,
    /// as defined in <see href="https://www.rfc-editor.org/rfc/rfc5155#section-4">RFC 5155 §4</see>.
    /// </summary>
    /// <remarks>
    /// <para>The wire format defined in <see href="https://www.rfc-editor.org/rfc/rfc5155#section-4.2">RFC 5155 §4.2</see> is:</para>
    /// <code>
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |   Hash Alg.   |     Flags     |          Iterations           |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Salt Length  |                     Salt                      /
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// </code>
    /// <para>
    /// <b>Hash Algorithm</b> and <b>Flags</b> are single-octet fields, <b>Iterations</b> is a 16-bit count, and the optional <b>Salt</b>
    /// field is prefixed by a length octet. If the salt length is zero the salt bytes are omitted.
    /// </para>
    /// </remarks>
    [DNSRecord(DNSClassId.IN, 0x33)]
    [DNSTextRecord("{HashAlgorithm} {Flag} {Iterations} {Salt}")]
    public class NSEC3PARAM : DNSResponseDetail
    {

        /// <summary>
        /// Gets or sets the hash algorithm identifier used when computing NSEC3 digests.
        /// </summary>
        [DNSField]
        public byte HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets additional processing flags such as opt-out behavior.
        /// </summary>
        [DNSField]
        public byte Flag { get; set; }

        /// <summary>
        /// Gets or sets the number of additional hash iterations applied to the base digest.
        /// </summary>
        [DNSField]
        public ushort Iterations { get; set; }

        /// <summary>
        /// Gets or sets the optional binary salt that is appended to owner names before hashing.
        /// </summary>
        [DNSField(FieldsSizeOptions.PrefixedSize1B)]
        public byte[] Salt { get; set; }
    }
}
