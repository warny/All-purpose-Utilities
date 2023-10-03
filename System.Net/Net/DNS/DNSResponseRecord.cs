using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
    public sealed class DNSResponseRecord : DNSElement
    {
        /*
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            | 0| 1| 2| 3| 4| 5| 6| 7| 8| 9|10|11|12|13|14|15|
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                                               |
            /                                               /
            /                      NAME                     /
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                      TYPE                     |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                     CLASS                     |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                      TTL                      |
            |                                               |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                   RDLENGTH                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--|
            /                     RDATA                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        */

        public DNSResponseRecord() { }

		public DNSResponseRecord(string name, uint TTL, DNSResponseDetail details)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.TTL = TTL;
			this.RData = details ?? throw new ArgumentNullException(nameof(details));
		}

		[DNSField]
        public string Name { get; set; }
        [DNSField]
        public ushort Type { get; internal set; }
        [DNSField]
        public DNSClass Class { get; set; }
        [DNSField]
        public uint TTL { get; set; }
        [DNSField]
        public ushort RDLength { get; internal set; }

        private DNSResponseDetail rData;
        public DNSResponseDetail RData {
            get => rData;
            set
            {
                Type = value.ClassId;
                rData = value;
            }
        }

        public override string ToString() => $"{Name} ({RData.Name}) : {RData}, TTL : {TTL}";
	}
}
