﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC3123
{
    [DNSRecord(DNSClass.IN, 0x2A)]
    public class APL : DNSResponseDetail
    {
        /*
           The RDATA section consists of zero or more items (&lt;apitem&gt;) of the
           form

              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              |                          ADDRESSFAMILY                        |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              |             PREFIX            | N |         AFDLENGTH         |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
              /                            AFDPART                            /
              |                                                               |
              +---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

              ADDRESSFAMILY     16 bit unsigned value as assigned by IANA
                                (see IANA Considerations)
              PREFIX            8 bit unsigned binary coded prefix length.
                                Upper and lower bounds and interpretation of
                                this value are address family specific.
              N                 negation flag, indicates the presence of the
                                "!" character in the textual format.  It has
                                the value "1" if the "!" was given, "0" else.
              AFDLENGTH         length in octets of the following address
                                family dependent part (7 bit unsigned).
              AFDPART           address family dependent part.  See below.

           This document defines the AFDPARTs for address families 1 (IPv4) and
           2 (IPv6).  Future revisions may deal with additional address
           families.
        */

        [DNSField]
        public IANAddressFamily AddressFamily { get; set; }

        [DNSField]
        public byte Prefix { get; set; }

        [DNSField]
        private byte flagAndAfdLength;

        private byte AfdLength {
            get => (byte)(flagAndAfdLength & 0b0111_1111);
            set => flagAndAfdLength = (byte)((value & 0b0111_1111) | (flagAndAfdLength & 0b1000_0000));
        }

        public bool Negate
        {
            get => (flagAndAfdLength & 0b1000_0000) != 0;
            set => flagAndAfdLength = (byte)((value ? 0b1000_0000 : 0) | (flagAndAfdLength & 0b0111_1111));
        }

        [DNSField]
        public byte[] afdPart;

        [DNSField(nameof(AfdLength))]
        public byte[] AfdPart { get; set; }

    }
}
