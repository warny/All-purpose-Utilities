using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Utils.Net.Structures;

namespace Utils.Net.DNS.RFC1712
{
    [DNSRecord("IN", 0x1B)]
    public class GPOS : DNSResponseDetail
    {
        [DNSField(FieldConstants.PREFIXED_SIZE)]
        public StringEncodedDouble Longitude { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE)]
        public StringEncodedDouble Latitude { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE)]
        public StringEncodedDouble Altitude { get; set; }
    }
}