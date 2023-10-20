using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSRecord("IN", 0x08)]
public class MG : DNSResponseDetail
{
    /*
        MG RDATA format (EXPERIMENTAL)

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   MGMNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        MGMNAME         A <domain-name> which specifies a mailbox which is a
                        member of the mail group specified by the domain name.

        MG records cause no additional section processing.
     */
    [DNSField]
    public DNSDomainName MGName { get; set; }

    public override string ToString() => MGName;

}
