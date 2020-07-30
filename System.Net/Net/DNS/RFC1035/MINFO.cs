using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x0E)]
    public class MINFO : DNSResponseDetail
    {
        /*
            MINFO RDATA format (EXPERIMENTAL)

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                    RMAILBX                    /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                    EMAILBX                    /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            RMAILBX         A <domain-name> which specifies a mailbox which is
                            responsible for the mailing list or mailbox.  If this
                            domain name names the root, the owner of the MINFO RR is
                            responsible for itself.  Note that many existing mailing
                            lists use a mailbox X-request for the RMAILBX field of
                            mailing list X, e.g., Msgroup-request for Msgroup.  This
                            field provides a more general mechanism.


            EMAILBX         A <domain-name> which specifies a mailbox which is to
                            receive error messages related to the mailing list or
                            mailbox specified by the owner of the MINFO RR (similar
                            to the ERRORS-TO: field which has been proposed).  If
                            this domain name names the root, errors should be
                            returned to the sender of the message.

            MINFO records cause no additional section processing.  Although these
            records can be associated with a simple mailbox, they are usually used
            with a mailing list.


        */
        [DNSField]
        public string EMailBx { get; set; }
        [DNSField]
        public string RMailBx { get; set; }

        public override string ToString() {
            return $"EMAILBX : \t{EMailBx}\n\tRMAILBX : \t{RMailBx}";
        }
    }
}
