using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSRecord("IN", 0x0F)]
public class MX : DNSResponseDetail
	{
    /*
        MX RDATA format

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                  PREFERENCE                   |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   EXCHANGE                    /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        PREFERENCE      A 16 bit integer which specifies the preference given to
                        this RR among others at the same owner.  Lower values
                        are preferred.

        EXCHANGE        A <domain-name> which specifies a host willing to act as
                        a mail exchange for the owner name.

        MX records cause type A additional section processing for the host
        specified by EXCHANGE.  The use of MX RRs is explained in detail in
        [RFC-974].
    */

    [DNSField]
    public ushort Preference { get; set; }

    [DNSField]
    public DNSDomainName Exchange { get; set; }

    public override string ToString() => $"{Exchange} ({Preference})";
	}
