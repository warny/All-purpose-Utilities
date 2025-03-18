﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2230
{

    [DNSRecord(DNSClass.IN, 0x24)]
    public class KX : DNSResponseDetail
    {
        /*
            The KX DNS record has the following RDATA format:

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                  PREFERENCE                   |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                   EXCHANGER                   /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            PREFERENCE      A 16 bit non-negative integer which specifies the
                            preference given to this RR among other KX records
                            at the same owner.  Lower values are preferred.

            EXCHANGER       A <domain-name> which specifies a host willing to
                            act as a mail exchange for the owner name.

            KX records MUST cause type A additional section processing for the
            host specified by EXCHANGER.  In the event that the host processing
            the DNS transaction supports IPv6, KX records MUST also cause type
            AAAA additional section processing.

            The KX RDATA field MUST NOT be compressed.
         */

        [DNSField]
        public ushort Preference { get; set; }
        
        [DNSField]
        public DNSDomainName Exchanger { get; set; }
    }
}
