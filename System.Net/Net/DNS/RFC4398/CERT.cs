using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4398
{

    [DNSRecord(DNSClass.IN, 0x25)]
    public class CERT: DNSResponseDetail
    {
        /*
             The CERT resource record (RR) has the structure given below.  Its RR
               type code is 37.

                                   1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
               0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
               +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
               |             type              |             key tag           |
               +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
               |   algorithm   |                                               /
               +---------------+            certificate or CRL                 /
               /                                                               /
               +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-|

               The type field is the certificate type as defined in Section 2.1
               below.

               The key tag field is the 16-bit value computed for the key embedded
               in the certificate, using the RRSIG Key Tag algorithm described in
               Appendix B of [12].  This field is used as an efficiency measure to



            Josefsson                   Standards Track                     [Page 3]


            RFC 4398            Storing Certificates in the DNS        February 2006


               pick which CERT RRs may be applicable to a particular key.  The key
               tag can be calculated for the key in question, and then only CERT RRs
               with the same key tag need to be examined.  Note that two different
               keys can have the same key tag.  However, the key MUST be transformed
               to the format it would have as the public key portion of a DNSKEY RR
               before the key tag is computed.  This is only possible if the key is
               applicable to an algorithm and complies to limits (such as key size)
               defined for DNS security.  If it is not, the algorithm field MUST be
               zero and the tag field is meaningless and SHOULD be zero.

               The algorithm field has the same meaning as the algorithm field in
               DNSKEY and RRSIG RRs [12], except that a zero algorithm field
               indicates that the algorithm is unknown to a secure DNS, which may
               simply be the result of the algorithm not having been standardized
               for DNSSEC [11].
        */

        [DNSField]
        public CertificateTypes Type { get; set; }

        [DNSField]
        public ushort KeyTag { get; set; }

        [DNSField]
        public Algorithm Algorithm { get; set; }

        [DNSField]
        public byte[] ObjectDatas { get; set; }

    }
}
