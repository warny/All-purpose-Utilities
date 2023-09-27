using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Utils.Net.DNS.RFC1035
{
    [DNSClass(0x01)]
	public sealed class A: DNSResponseDetail
	{
        /*
            A RDATA format

                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                    ADDRESS                    |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            where:

            ADDRESS         A 32 bit Internet address.

            Hosts that have multiple Internet addresses will have multiple A
            records.
            A records cause no additional section processing.  The RDATA section of
            an A line in a master file is an Internet address expressed as four
            decimal numbers separated by dots without any imbedded spaces (e.g.,
            "10.2.0.52" or "192.0.5.6").
         */

        public IPAddress IPAddress
        {
            get => ipAddress;
            set {
                if (value.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new NotSupportedException("A records only support IPV4 addresses");
                ipAddress = value;
            }
        }
        private System.Net.IPAddress ipAddress = null;

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
		{
			IPAddress = new IPAddress(datagram.ReadBytes(4));
		}

		protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
		{
            datagram.Write(IPAddress.GetAddressBytes());
		}

		public override string ToString() => IPAddress.ToString();
	}
}
