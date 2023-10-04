using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1183
{
    [DNSClass(0x12)]
    public class AFSDB : DNSResponseDetail
    {
        /*
            AFSDB RDATA format

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                  PREFERENCE                   |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                    SERVER                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            PREFERENCE      A 16 bit integer which specifies the preference given to
                            this RR among others at the same owner.  Lower values
                            are preferred.

            SERVER          A <domain-name> which specifies a host willing to act as
                            a afs server for the owner name.

        */
        [DNSField]
        public ushort Preference { get; set; }
        [DNSField] 
        public DNSDomainName AFSServer { get; set; }

        public override string ToString() {
            return $"{AFSServer} ({Preference})";
        }
    }
}
