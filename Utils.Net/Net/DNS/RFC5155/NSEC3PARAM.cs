using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC5155
{
    [DNSRecord(DNSClassId.IN, 0x33)]
    [DNSTextRecord("{HashAlgorithm} {Flag} {Iterations} {Salt}")]
    /// <summary>
    /// Represents the NSEC3PARAM resource record describing NSEC3 hashing parameters for a DNS zone.
    /// </summary>
    public class NSEC3PARAM : DNSResponseDetail
    {
        /*
            The RDATA of the NSEC3PARAM RR is as shown below:

                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |   Hash Alg.   |     Flags     |          Iterations           |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |  Salt Length  |                     Salt                      /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

            Hash Algorithm is a single octet.

            Flags field is a single octet.

            Iterations is represented as a 16-bit unsigned integer, with the most
            significant bit first.

            Salt Length is represented as an unsigned octet.  Salt Length
            represents the length of the following Salt field in octets.  If the
            value is zero, the Salt field is omitted.

            Salt, if present, is encoded as a sequence of binary octets.  The
            length of this field is determined by the preceding Salt Length
            field.
        */
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
