using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4034
{
    [DNSRecord(DNSClass.IN, 0x2B)]
    public class DS : DNSResponseDetail
    {
        /*
            The RDATA for a DS RR consists of a 2 octet Key Tag field, a 1 octet
            Algorithm field, a 1 octet Digest Type field, and a Digest field.

                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |           Key Tag             |  Algorithm    |  Digest Type  |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            /                                                               /
            /                            Digest                             /
            /                                                               /
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+ 
        */

        [DNSField]
        public ushort KeyTag { get; set; }

        [DNSField]
        public Algorithm Algorithm { get; set; }

        [DNSField]
        public DigestTypes DigestType { get; set; }

        [DNSField]
        public byte[] Digest { get; set; }
    }
}
