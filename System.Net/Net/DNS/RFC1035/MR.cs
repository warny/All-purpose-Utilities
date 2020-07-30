using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
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
        string NewName { get; set; }

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
        {
            NewName = Encoding.ASCII.GetString(datagram.ReadBytes(this.Length));
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
        {
            datagram.Write(Encoding.ASCII.GetBytes(NewName));
        }

        public override string ToString() => NewName;

    }
}
