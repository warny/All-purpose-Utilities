using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1183;

[DNSRecord(DNSClass.IN, 0x14)]
public class ISDN : DNSResponseDetail
{
    //La représentation du ISDN est fausse, mais je n'ai pas trouvé les specs détaillées de l'enregistrement

    public string PhoneNumber { get; set; }

		public override string ToString() => PhoneNumber;

}
