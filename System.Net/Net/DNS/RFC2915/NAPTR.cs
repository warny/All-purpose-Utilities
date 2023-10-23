using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2915
{
    [DNSRecord("IN", 0x23)]
    public class NAPTR : DNSResponseDetail
    {
        // DNSField attribute is used to associate a name with each field
        [DNSField]
        public ushort Order { get; set; }

        [DNSField]
        public ushort Preference { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_1B)]
        public string Flags { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_1B)]
        public string Service { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_1B)]
        public string Regexp { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_1B)]
        public string Replacement { get; set; }


    }
}
