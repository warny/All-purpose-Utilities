using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC5205
{
    [DNSRecord(DNSClass.IN, 0x37)]
    public class HIP : DNSResponseDetail
    {
        [DNSField]
        private byte HitLength { get; set; }

        [DNSField]
        public IPSecAlgorithm PKAlgorithm { get; set; }

        [DNSField]
        private ushort PKLength { get; set; }

        [DNSField(nameof(HitLength))]
        public byte[] HIT { get; set; }

        [DNSField(nameof(PKLength))]
        public byte[] PublicKey { get; set; }

        //[DNSField]
        public DNSDomainName[] RendezvousServers { get; set; }

    }
}
