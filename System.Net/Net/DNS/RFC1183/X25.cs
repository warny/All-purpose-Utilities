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

        [DNSField(-1)]
        public string PSDN { get; set; }

        public override string ToString()
        {
            return PSDN;
        }

    }
}
