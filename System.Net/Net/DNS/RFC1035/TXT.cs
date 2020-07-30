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
        public string Text { get; set; }

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
        {
            Text = Encoding.ASCII.GetString(datagram.ReadBytes(this.Length));
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
        {
            datagram.Write(Encoding.ASCII.GetBytes(Text));
        }

        public override string ToString() => Text;

    }
}
