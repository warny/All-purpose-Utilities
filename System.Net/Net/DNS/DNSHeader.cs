using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS
{
	public class DNSHeader : DNSElement
	{
        /*
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            | 0| 1| 2| 3| 4| 5| 6| 7| 8| 9|10|11|12|13|14|15|
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                      ID                       |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |QR|   Opcode  |AA|TC|RD|RA|   Z    |   RCODE   |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    QDCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ANCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    NSCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ARCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        */

        public DNSHeader() {
            ID = (ushort)new Random().Next();
            QrBit = DNSQRBit.Question;
        }
        public DNSHeader(Stream datas, DNSFactory factory = null) : this(new DNSDatagram(datas), factory) { }
        public DNSHeader(byte[] datas, DNSFactory factory = null) : this(new DNSDatagram(datas), factory) { }
        internal DNSHeader(DNSDatagram datagram, DNSFactory factory = null) {
            factory = factory ?? DNSFactory.Default;
            this.Read(datagram, factory);
        }


        [DNSField]
        public ushort ID { get; set; }
        [DNSField]
        protected ushort Flags { get; set; }
        [DNSField]
        protected ushort QDCount { get; set; }
        [DNSField]
        protected ushort ANCount { get; set; }
        [DNSField]
        protected ushort NSCount { get; set; }
        [DNSField]
        protected ushort ARCount { get; set; }

        public IList<DNSRequestRecord> Requests { get; } = new List<DNSRequestRecord>();
        public IList<DNSResponseRecord> Responses { get; } = new List<DNSResponseRecord>();
        public IList<DNSResponseRecord> Authorities { get; } = new List<DNSResponseRecord>();
        public IList<DNSResponseRecord> Additionnals { get; } = new List<DNSResponseRecord>();

        public DNSQRBit QrBit
        {
            get => (DNSQRBit)(Flags & DNSConstants.QR);
            set => Flags = (ushort)((Flags & ~DNSConstants.QR) | (ushort)value);
        }
        public DNSOpCode OpCode
        {
            get => (DNSOpCode)(Flags & DNSConstants.OpCode);
            set => Flags = (ushort)((Flags & ~DNSConstants.OpCode) | (ushort)value);
        }

        public bool AuthoritativeAnswer
        {
            get => (Flags & DNSConstants.AuthoritativeAnswer) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.AuthoritativeAnswer) | (value ? DNSConstants.AuthoritativeAnswer : 0));
        }

        public bool MessageTruncated
        {
            get => (Flags & DNSConstants.MessageTruncated) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.MessageTruncated) | (value ? DNSConstants.MessageTruncated : 0));
        }

        public bool RecursionDesired 
        {
            get => (Flags & DNSConstants.RecursionDesired) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.RecursionDesired) | (value ? DNSConstants.RecursionDesired : 0));
        }
        public bool RecursionPossible
        {
            get => (Flags & DNSConstants.RecursionPossible) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.RecursionPossible) | (value ? DNSConstants.RecursionPossible : 0));
        }

        public byte ReservedFlags
        {
            get => (byte)(Flags & DNSConstants.ReservedZ);
            set => Flags = (ushort)((Flags | ~DNSConstants.ReservedZ) & (value & DNSConstants.ReservedZ));
        }

        public DNSError ErrorCode
        {
            get => (DNSError)((ushort)Flags & DNSConstants.Error);
            set => Flags = (ushort)((Flags | ~DNSConstants.Error) & ((ushort)value & DNSConstants.Error));
        }

		protected internal override void Read(DNSDatagram datagram, DNSFactory factory)
		{
			base.Read(datagram, factory);
            for (int i = 0; i < QDCount; i++)
            {
                DNSRequestRecord request = new DNSRequestRecord();
                request.Read(datagram, factory);
                Requests.Add(request);
            }
            for (int i = 0; i < ANCount; i++)
            {
                DNSResponseRecord responseRecord = new DNSResponseRecord();
                responseRecord.Read(datagram, factory);
                Responses.Add(responseRecord);
            }
            for (int i = 0; i < NSCount; i++)
            {
                DNSResponseRecord responseRecord = new DNSResponseRecord();
                responseRecord.Read(datagram, factory);
                Authorities.Add(responseRecord);
            }
            for (int i = 0; i < ARCount; i++)
            {
                DNSResponseRecord responseRecord = new DNSResponseRecord();
                responseRecord.Read(datagram, factory);
                Additionnals.Add(responseRecord);
            }
        }

        protected internal override void Write(DNSDatagram datagram, DNSFactory factory)
		{
            QDCount = (ushort)Requests.Count;
            ANCount = (ushort)Responses.Count;
            NSCount = (ushort)Authorities.Count;
            ARCount = (ushort)Additionnals.Count;
            base.Write(datagram, factory);
            foreach (var element in Requests.Union<DNSElement>(Responses).Union(Authorities).Union(Additionnals))
            {
                element.Write(datagram, factory);
            }
		}

        public Byte[] ToByteArray(DNSFactory factory = null)
        {
            factory ??= DNSFactory.Default;
            DNSDatagram datagram = new DNSDatagram();
            this.Write(datagram, factory);
            return datagram.ToBytes();
        }
	}
}
