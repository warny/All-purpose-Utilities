using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x07)]
    public class MB : DNSResponseDetail
    {
        /*
            MB RDATA format (EXPERIMENTAL)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   MADNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            MADNAME         A <domain-name> which specifies a host which has the
                            specified mailbox.

            MB records cause additional section processing which looks up an A type
            RRs corresponding to MADNAME.
        */
        [DNSField]
        public DNSDomainName MadName { get; set; }

        public override string ToString() => MadName;

    }
}
