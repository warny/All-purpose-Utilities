using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSRecord("IN", 0x05)]
public sealed class CNAME : DNSResponseDetail
{
    /*
        CNAME RDATA format

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                     CNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        CNAME           A <domain-name> which specifies the canonical or primary
                        name for the owner.  The owner name is an alias.

        CNAME RRs cause no additional section processing, but name servers may
        choose to restart the query at the canonical name in certain cases.  See
        the description of name server logic in [RFC-1034] for details.
    */
    [DNSField]
    public DNSDomainName CName { get; set; }

    public override string ToString() => Name;
	}
