using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1183;

[DNSRecord(DNSClass.IN, 0x14)]
public class ISDN : DNSResponseDetail
{
    //La repr�sentation du ISDN est fausse, mais je n'ai pas trouv� les specs d�taill�es de l'enregistrement

    public string PhoneNumber { get; set; }

		public override string ToString() => PhoneNumber;

}
