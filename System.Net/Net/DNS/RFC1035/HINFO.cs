using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035;

[DNSRecord(DNSClass.IN, 0x0D)]
public class HINFO : DNSResponseDetail
	{
    /*
        HINFO RDATA format

            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                      CPU                      /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            /                       OS                      /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

        where:

        CPU             A <character-string> which specifies the CPU type.

        OS              A <character-string> which specifies the operating
                        system type.

        Standard values for CPU and OS can be found in [RFC-1010].

        HINFO records are used to acquire general information about a host.  The
        main use is for protocols such as FTP that can use special procedures
        when talking between machines or operating systems of the same type.
    */

    [DNSField]
    public string Info { get; set; }

    public override string ToString() => Info;
	}
