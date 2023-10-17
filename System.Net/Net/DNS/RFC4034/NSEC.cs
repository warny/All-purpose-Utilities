using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4034
{
    [DNSClass(0x2F)]
    public class NSEC : DNSResponseDetail
    {
        /*
                                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
            0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           /                      Next Domain Name                         /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
           /                       Type Bit Maps                           /
           +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        */

        [DNSField]
        public DNSDomainName NextDomainName { get; set; }

        [DNSField]
        public byte[] TypeBitmaps { get; set; }
    }
}
