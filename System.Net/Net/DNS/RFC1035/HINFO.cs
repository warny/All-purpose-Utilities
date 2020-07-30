using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x0D)]
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

        public string Info { get; set; }

		protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
		{
			Info = Encoding.ASCII.GetString(datagram.ReadBytes(this.Length));
		}

		protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
		{
            datagram.Write(Encoding.ASCII.GetBytes(Info));
        }

        public override string ToString() => Info;
	}
}
