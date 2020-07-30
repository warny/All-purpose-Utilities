using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS
{
	public class DNSRequestRecord	: DNSElement
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
        */

        public DNSRequestRecord() { }

		public DNSRequestRecord(string type, string name, DNSClass @class = DNSClass.ALL)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Class = @class;
			this.Type = type ?? throw new ArgumentNullException(nameof(type));
		}

		[DNSField]
        public string Name { get; set; }

        [DNSField]
        protected ushort RequestType { get; set; }
        [DNSField]
        public DNSClass Class { get; set; }

        public string Type { get; set; }

		protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
		{
            RequestType = factory.GetClassIdentifier(Type); 
			base.Write(datagram, factory);
		}

		protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
		{
			base.Read(datagram, factory);
            Type = factory.GetClassName(RequestType);
		}

        public override string ToString() => $"Request {Type} : {Name} ({Class})";
	}
}
