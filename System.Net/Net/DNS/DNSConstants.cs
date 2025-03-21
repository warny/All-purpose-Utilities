using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS
{
	/*
        +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        | 0| 1| 2| 3| 4| 5| 6| 7| 8| 9|10|11|12|13|14|15|
        +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        |                      ID                       |
        +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
		|QR|   Opcode  |AA|TC|RD|RA| Z|AD|CD|   RCODE   |
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

	/// <summary>
	/// Is the packet is a response or a question (1 bit, aligned)
	/// </summary>
	public enum DNSQRBit : ushort
	{
		Question = 0x0000,
		Response = 0x8000
	}

	/// <summary>
	/// Requested operation (4bits, aligned)
	/// </summary>
	public enum DNSOpCode : ushort
	{
		Standart = 0x0000,
		Inverse = 0x1000,
		Status = 0x2000
	}

	/// <summary>
	/// Four bits that indicates the returned error value (4 bits, aligned)
	/// </summary>
	public enum DNSError : ushort
	{
		/// <summary>
		/// No error condition
		/// </summary>
		Ok = 0x0000,
		/// <summary>
		/// Format error - The name server was unable to interpret the query.
		/// </summary>
		FormatError = 0x0001,
		/// <summary>
		/// Server failure - The name server was unable to process this query due to a problem with the name server.
		/// </summary>
		ServerFailure = 0x0002,
		/// <summary>
		/// Name Error - Meaningful only for responses from an authoritative name server, this code signifies that the domain name referenced in the query does not exist.
		/// </summary>
		NameError = 0x0003,
		/// <summary>
		/// Not Implemented - The name server does not support the requested kind of query.
		/// </summary>
		NotImplemented = 0x0004,
		/// <summary>
		/// Refused - The name server refuses to perform the specified operation for policy reasons.  For example, a name server may not wish to provide the information to the particular requester, or a name server may not wish to perform a particular operation (e.g., zone transfer) for particular data.
		/// </summary>
		Refused = 0x0005
	}

	/// <summary>
	/// DNS Standart constants masks
	/// </summary>
	/// <summary>
	/// Constants representing DNS datagram flags and fields masks.
	/// </summary>
	public static class DNSConstants
	{
		/// <summary>
		/// QR (Query/Response) bit. Set to 1 for Response, 0 for Query.
		/// <seealso cref="DNSQRBit"/>
		/// </summary>
		public const ushort QR = 0x8000;

		/// <summary>
		/// OpCode (Operation Code) field. Indicates the type of DNS query.
		/// <seealso cref="DNSOpCode"/>
		/// </summary>
		public const ushort OpCode = 0x7800;

		/// <summary>
		/// Authoritative Answer bit. Set to 1 if the responding server is an authority for the queried domain.
		/// </summary>
		public const ushort AuthoritativeAnswer = 0x0400;

		/// <summary>
		/// Message Truncated bit. Set to 1 if the DNS message was truncated during transmission.
		/// </summary>
		public const ushort MessageTruncated = 0x0200;

		/// <summary>
		/// Recursion Desired bit. Set to 1 if the client requests a recursive DNS query.
		/// </summary>
		public const ushort RecursionDesired = 0x0100;

		/// <summary>
		/// Recursion Possible bit. Set to 1 if the server supports recursive queries.
		/// </summary>
		public const ushort RecursionPossible = 0x0008;

		/// <summary>
		/// Reserved Zero (Z) bits. Reserved for future use. Should be set to 0.
		/// </summary>
		public const ushort ReservedZ = 0x0040;

		/// <summary>
		/// indicates in a response that all the data included in the answer and authority
		/// portion of the response has been authenticated by the server
		/// according to the policies of that server.
		/// </summary>
		public const ushort AuthenticDatas = 0x20;

		/// <summary>
		/// indicates in a query that Pending(non-authenticated) 
		/// data is acceptable to the resolver sending the query.
		/// </summary>
		public const ushort CheckingDisabled = 0x10;

		/// <summary>
		/// Error field. Indicates the type of DNS error encountered in the response.
		/// <seealso cref="DNSError"/>
		/// </summary>
		public const ushort Error = 0x000F;
	}
	public enum DNSClass : ushort
	{
		/// <summary>
		/// the Internet
		/// </summary>
		IN = 0x0001,
		/// <summary>
		/// the CSNET class (Obsolete - used only for examples in some obsolete RFCs) 
		/// </summary>
		[Obsolete]
		CS = 0x0002,
		/// <summary>
		/// the CHAOS class
		/// </summary>
		CH = 0x0003,
		/// <summary>
		/// Hesiod [Dyer 87]
		/// </summary>
		HS = 0x04,
		/// <summary>
		/// any class
		/// </summary>
		ALL = 0x00FF
	}

	public static class DNSRequestType
	{
		public const ushort ALL = 0xFF;
		public const ushort AXFR = 0xFC;
		[Obsolete]
		public const ushort MAILB = 0xFD;
		[Obsolete]
		public const ushort MAILA = 0xFE;
	}

	static class Types
	{
		public static readonly Type dnsRequestRecordType = typeof(DNSRequestRecord);
		public static readonly Type dnsResponseDetailType = typeof(DNSResponseDetail);
		public static readonly Type dnsClassAttributeType = typeof(DNSRecordAttribute);
	}
}

