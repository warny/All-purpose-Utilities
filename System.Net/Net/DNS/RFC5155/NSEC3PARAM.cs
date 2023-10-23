using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC5155
{
    [DNSRecord("IN", 0x33)]
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
        [DNSField]
        public byte HashAlgorithm { get; set; }

        [DNSField]
        public byte Flag { get; set; }

        [DNSField]
        public ushort Iterations { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_1B)]
        public byte[] Salt { get; set; }
    }
}
