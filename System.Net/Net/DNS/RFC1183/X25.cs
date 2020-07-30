using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS.RFC1183
{
    [DNSClass(20)]
    public class X25 : DNSResponseDetail
    {
        //La repr�sentation du ISDN est fausse, mais je n'ai pas trouv� les specs d�taill�es de l'enregistrement

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
