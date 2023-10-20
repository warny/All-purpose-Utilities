using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSRecord("IN", 0x04)]
[Obsolete]
public class MF : DNSResponseDetail
{
    /*
        MF RDATA format (Obsolete)

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   MADNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        MADNAME         A <domain-name> which specifies a host which has a mail
                        agent for the domain which will accept mail for
                        forwarding to the domain.

        MF records cause additional section processing which looks up an A type
        record corresponding to MADNAME.

        MF is obsolete.  See the definition of MX and [RFC-974] for details ofw
        the new scheme.  The recommended policy for dealing with MD RRs found in
        a master file is to reject them, or to convert them to MX RRs with a
        preference of 10.

     */
    [DNSField]
    public DNSDomainName MadName { get; set; }

    public override string ToString() => MadName;

}
