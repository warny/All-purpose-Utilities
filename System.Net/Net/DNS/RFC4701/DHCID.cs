using System;
using System.Collections.Generic;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC4701
{
    [DNSRecord(DNSClass.IN, 0x31)]
    public class DHCID : DNSResponseDetail
    {
        /*
            The RDATA section of a DHCID RR in transmission contains RDLENGTH
            octets of binary data.  The format of this data and its
            interpretation by DHCP servers and clients are described below.

            DNS software should consider the RDATA section to be opaque.  DHCP
            clients or servers use the DHCID RR to associate a DHCP client's
            identity with a DNS name, so that multiple DHCP clients and servers
            may deterministically perform dynamic DNS updates to the same zone.
            From the updater's perspective, the DHCID resource record RDATA
            consists of a 2-octet identifier type, in network byte order,
            followed by a 1-octet digest type, followed by one or more octets
            representing the actual identifier:

                    < 2 octets >    Identifier type code
                    < 1 octet >     Digest type code
                    < n octets >    Digest (length depends on digest type)
        */
        [DNSField]
        public DHCIDIdentifierTypes IdentifierTypes { get; set; }

        [DNSField]
        public byte DigestTypeCode { get; set; }

        [DNSField]
        public byte[] Digest { get; set; }
    }
}
