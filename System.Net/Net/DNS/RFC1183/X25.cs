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

        [DNSField(-1)]
        public string PSDN { get; set; }

        public override string ToString()
        {
            return PSDN;
        }

    }
}
