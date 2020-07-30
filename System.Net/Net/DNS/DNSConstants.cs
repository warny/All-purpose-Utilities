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

    /// <summary>
    /// Bit qui indique que le paquet est une réponse 1 ou une question 0
    /// Les valeurs sont décallés
    /// </summary>
    public enum DNSQRBit : ushort
    {
        Question = 0x0000,
        Response = 0x8000
    }

    /// <summary>
    /// Quatre bits qui indiquent l'opération demmandée
    /// Les valeurs possibles sont :
    /// Standart 0
    /// Inverse  1
    /// Status   2
    /// Les valeurs sont décallées
    /// </summary>
    public enum DNSOpCode : ushort
    {
        Standart = 0x0000,
        Inverse = 0x1000,
        Status = 0x2000
    }

    public enum DNSError : ushort
    {
        Ok = 0x0000, // No error condition
        FormatError = 0x0001, // Format error - The name server was unable to interpret the query.
        ServerFailure = 0x0002, // Server failure - The name server was unable to process this query due to a problem with the name server.
        NameError = 0x0003, // Name Error - Meaningful only for responses from an authoritative name server, this code signifies that the domain name referenced in the query does not exist.
        NotImplemented = 0x0004, // Not Implemented - The name server does not support the requested kind of query.
        Refused = 0x0005  // Refused - The name server refuses to perform the specified operation for policy reasons.  For example, a name server may not wish to provide the information to the particular requester, or a name server may not wish to perform a particular operation (e.g., zone transfer) for particular data.
    }

    public static class DNSConstants
    {
        public const ushort QR = 0x8000;
        public const ushort OpCode = 0x7800;

        public const ushort AuthoritativeAnswer = 0x0400;
        public const ushort MessageTruncated = 0x0200;
        public const ushort RecursionDesired = 0x0100;
        public const ushort RecursionPossible = 0x0008;
        public const ushort ReservedZ = 0x0070;
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

    public enum DNSRequestType : ushort
    {
         ALL = 0xFF,
         AXFR = 0xFC,
         MAILB = 0xFD,
         MAILA = 0xFE,
    }

    static class Types
    {
        public static readonly Type dnsRequestRecordType = typeof(DNSRequestRecord);
        public static readonly Type dnsResponseDetailType = typeof(DNSResponseDetail);
        public static readonly Type dnsClassAttributeType = typeof(DNSClassAttribute);
    }
}

