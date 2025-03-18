using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC5155
{
    [DNSRecord(DNSClass.IN, 0x32)]
    public class NSEC3 : DNSResponseDetail
    {
        /*
            The RDATA of the NSEC3 RR is as shown below:

                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |   Hash Alg.   |     Flags     |          Iterations           |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |  Salt Length  |                     Salt                      /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |  Hash Length  |             Next Hashed Owner Name            /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            /                         Type Bit Maps                         /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

            Hash Algorithm is a single octet.

            Flags field is a single octet, the Opt-Out flag is the least
            significant bit, as shown below:

            0 1 2 3 4 5 6 7
            +-+-+-+-+-+-+-+-+
            |             |O|
            +-+-+-+-+-+-+-+-+

            Iterations is represented as a 16-bit unsigned integer, with the most
            significant bit first.

            Salt Length is represented as an unsigned octet.  Salt Length
            represents the length of the Salt field in octets.  If the value is
            zero, the following Salt field is omitted.

            Salt, if present, is encoded as a sequence of binary octets.  The
            length of this field is determined by the preceding Salt Length
            field.

            Hash Length is represented as an unsigned octet.  Hash Length
            represents the length of the Next Hashed Owner Name field in octets.

            The next hashed owner name is not base32 encoded, unlike the owner
            name of the NSEC3 RR.  It is the unmodified binary hash value.  It
            does not include the name of the containing zone.  The length of this
            field is determined by the preceding Hash Length field.
        */

        [DNSField]
        public byte HashAlgorithm { get; set; }

        [DNSField]
        public byte Flag { get; set; }

        [DNSField]
        public ushort Iterations { get; set; }

        [DNSField(FieldsSizeOptions.PrefixedSize1B)]
        public byte[] Salt { get; set; }

        [DNSField(FieldsSizeOptions.PrefixedSize1B)]
        public byte[] NextHashOwnerName { get; set; }

        [DNSField]
        public byte[] TypeBitMaps { get; set; }
    }
}
