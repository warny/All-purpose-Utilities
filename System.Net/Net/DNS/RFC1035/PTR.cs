using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSClass(0x0C)]
public class PTR : DNSResponseDetail
{
    /*
        PTR RDATA format

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   PTRDNAME                    /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        PTRDNAME        A <domain-name> which points to some location in the
                        domain name space.

        PTR records cause no additional section processing.  These RRs are used
        in special domains to point to some other location in the domain space.
        These records are simple data, and don't imply any special processing
        similar to that performed by CNAME, which identifies aliases.  See the
        description of the IN-ADDR.ARPA domain for an example.
    */
    [DNSField]
    public DNSDomainName PTRName { get; set; }

    public override string ToString() => PTRName;
}
