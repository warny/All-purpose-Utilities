using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x03)]
    public class MD : DNSResponseDetail
    {
        /*
            MD RDATA format (Obsolete)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   MADNAME                     /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            MADNAME         A <domain-name> which specifies a host which has a mail
                            agent for the domain which should be able to deliver
                            mail for the domain.

            MD records cause additional section processing which looks up an A type
            record corresponding to MADNAME.

            MD is obsolete.  See the definition of MX and [RFC-974] for details of
            the new scheme.  The recommended policy for dealing with MD RRs found in
            a master file is to reject them, or to convert them to MX RRs with a
            preference of 0.
         */
        [DNSField]
        public string MadName { get; set; }

        public override string ToString() => MadName;

    }
}
