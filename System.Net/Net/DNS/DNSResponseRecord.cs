using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation.Peers;

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
        protected ushort Type { get; set; }
        [DNSField]
        public DNSClass Class { get; set; }
        [DNSField]
        public uint TTL { get; set; }
        [DNSField]
        private ushort RDLength { get; set; }
        public DNSResponseDetail RData { get; set; }

        protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
        {
            base.Read(datagram, factory);
            RData = factory.CreateResponseDetail(Type);
            RData.Length = RDLength;
            RData.Read(datagram, factory);
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
        {
            Type = factory.GetClassIdentifier(RData.Name);
            base.Write(datagram, factory);
            var lengthPosition = datagram.Length - 2;
            RData.Write(datagram, factory);
            //ecriture de la taille des données (RDLength)
            RDLength = (ushort)(datagram.Length - lengthPosition - 2);
            datagram.Write(lengthPosition, RDLength);
        }

        public override string ToString() => $"{Name} ({RData.Name}) : {RData}, TTL : {TTL}";
	}
}
