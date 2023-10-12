using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSClass(0x09)]
public class MR : DNSResponseDetail
{
    /*
        MR RDATA format (EXPERIMENTAL)

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   NEWNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        NEWNAME         A <domain-name> which specifies a mailbox which is the
                        proper rename of the specified mailbox.

        MR records cause no additional section processing.  The main use for MR
        is as a forwarding entry for a user who has moved to a different
        mailbox.

     */

    [DNSField]
    public DNSDomainName NewName { get; set; }

    public override string ToString() => NewName;

}
