using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2671
{
    [DNSRecord("IN", 0x29)]
    public class OPT : DNSResponseDetail
    {
        /*
                        +0 (MSB)                            +1 (LSB)
             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
          0: |                          OPTION-CODE                          |
             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
          2: |                         OPTION-LENGTH                         |
             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
          4: |                                                               |
             /                          OPTION-DATA                          /
             /                                                               /
             +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

           OPTION-CODE    (Assigned by IANA.)

           OPTION-LENGTH  Size (in octets) of OPTION-DATA.

           OPTION-DATA    Varies per OPTION-CODE.
        */

        [DNSField]
        public ushort OptionCode { get; set; }

        [DNSField(FieldConstants.PREFIXED_SIZE_2B)]
        public byte[] OptionsData { get; set; }
    }
}
