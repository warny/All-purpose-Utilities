using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4034
{
    [DNSClass(0x2E)]
    public class RRSIG : DNSResponseDetail
    {
        /*   
           The RDATA for an RRSIG RR consists of a 2 octet Type Covered field, a
           1 octet Algorithm field, a 1 octet Labels field, a 4 octet Original
           TTL field, a 4 octet Signature Expiration field, a 4 octet Signature
           Inception field, a 2 octet Key tag, the Signer's Name field, and the
           Signature field.

                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |        Type Covered           |  Algorithm    |     Labels    |
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |                         Original TTL                          |
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |                      Signature Expiration                     |
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |                      Signature Inception                      |
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           |            Key Tag            |                               /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+         Signer's Name         /
           /                                                               /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           /                                                               /
           /                            Signature                          /
           /                                                               /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        */

        [DNSField]
        public ushort TypeCovered { get; set; }

        [DNSField]
        public Algorithm Algorithm { get; set; }

        [DNSField]
        public byte Labels { get; set; }

        [DNSField]
        public uint OriginalTTL { get; set; }

        [DNSField]
        public uint SignatureExpriation { get; set; }  // https://datatracker.ietf.org/doc/html/rfc4034#section-3.2

        [DNSField]
        public uint SignatureInception { get; set; }   // https://datatracker.ietf.org/doc/html/rfc4034#section-3.2

        [DNSField]
        public ushort KeyTag { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE)]
        public string SignerName { get; set; }

        [DNSField]
        public byte[] SignatureField { get; set; }
       

    }
}
