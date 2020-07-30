using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS.RFC1183
{
    [DNSClass(20)]
    public class X25 : DNSResponseDetail
    {
        //La représentation du ISDN est fausse, mais je n'ai pas trouvé les specs détaillées de l'enregistrement

        public string PSDN { get; set; }

        public override string ToString()
        {
            return PSDN;
        }

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
        {
            PSDN = Encoding.ASCII.GetString(datagram.ReadBytes(this.Length));
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
        {
            datagram.Write(Encoding.ASCII.GetBytes(PSDN));
        }
    }
}
