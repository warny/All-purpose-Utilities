using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x10)]
    public class TXT : DNSResponseDetail
    {
        /*
            TXT RDATA format

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   TXT-DATA                    /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            TXT-DATA        One or more <character-string>s.

            TXT RRs are used to hold descriptive text.  The semantics of the text
            depends on the domain where it is found.
        */
        [DNSField(-1)]
        public string Text { get; set; }

        public override string ToString() => Text;

    }
}
