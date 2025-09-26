using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS;



/// <summary>
/// Represents the DNS header section of a DNS packet, including standard bits from
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.1">RFC 1035 §4.1.1</see> and the
/// AD/CD extensions introduced in <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see>.
/// The header provides information about flags, counts for queries/answers/authorities/additionals,
/// and other DNS-level metadata.
/// </summary>
/// <remarks>
/// A DNS header contains:
/// <list type="bullet">
/// <item><description><see cref="ID"/>: A 16-bit identifier assigned by the program that generates any kind of DNS query.</description></item>
/// <item><description><see cref="Flags"/>: A collection of DNS flags and codes packed into a 16-bit field (e.g., QR, OpCode, AA, etc.).</description></item>
/// <item><description>Counts of queries, answers, authority records, and additional records (see <see cref="QDCount"/>, <see cref="ANCount"/>, <see cref="NSCount"/>, <see cref="ARCount"/>).</description></item>
/// </list>
/// <para>The wire format defined in
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.1">RFC 1035 §4.1.1</see>
/// (extended by <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see>) is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                      ID                       |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |QR|   Opcode  |AA|TC|RD|RA| Z|AD|CD|   RCODE   |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                    QDCOUNT                    |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                    ANCOUNT                    |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                    NSCOUNT                    |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                    ARCOUNT                    |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// <para>
/// <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see> introduces the AD (Authenticated Data)
/// and CD (Checking Disabled) bits. Clients should only trust AD when the upstream server is trusted,
/// and the CD flag allows requesting unsigned data for local validation.
/// </para>
/// </remarks>
public class DNSHeader : DNSElement
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DNSHeader"/> class with a randomly generated
	/// <see cref="ID"/> and default QR bit set to <see cref="DNSQRBit.Question"/>.
	/// </summary>
	public DNSHeader()
	{
		ID = (ushort)new Random().Next();
		QrBit = DNSQRBit.Question;
	}

	/// <summary>
	/// Gets or sets the 16-bit DNS packet identifier.
	/// </summary>
	[DNSField]
	public ushort ID { get; set; }

	/// <summary>
	/// Gets or sets the raw 16-bit flags field for DNS. This includes bits for
	/// QR, OpCode, AA, TC, RD, RA, Z, AD, CD, and RCODE.
	/// </summary>
	[DNSField]
	internal ushort Flags { get; set; }

	/// <summary>
	/// Gets or sets the number of questions in the Question section of the DNS packet.
	/// </summary>
	[DNSField]
	internal ushort QDCount { get; set; }

	/// <summary>
	/// Gets or sets the number of answer records in the Answer section of the DNS packet.
	/// </summary>
	[DNSField]
	internal ushort ANCount { get; set; }

	/// <summary>
	/// Gets or sets the number of name server (authority) records in the Authority section of the DNS packet.
	/// </summary>
	[DNSField]
	internal ushort NSCount { get; set; }

	/// <summary>
	/// Gets or sets the number of additional records in the Additional section of the DNS packet.
	/// </summary>
	[DNSField]
	internal ushort ARCount { get; set; }

	/// <summary>
	/// Gets a list of DNS question records associated with this header.
	/// </summary>
	public IList<DNSRequestRecord> Requests { get; } = new List<DNSRequestRecord>();

	/// <summary>
	/// Gets a list of DNS answer records associated with this header.
	/// </summary>
	public IList<DNSResponseRecord> Responses { get; } = new List<DNSResponseRecord>();

	/// <summary>
	/// Gets a list of DNS authority records associated with this header.
	/// </summary>
	public IList<DNSResponseRecord> Authorities { get; } = new List<DNSResponseRecord>();

	/// <summary>
	/// Gets a list of DNS additional records associated with this header.
	/// </summary>
	public IList<DNSResponseRecord> Additionals { get; } = new List<DNSResponseRecord>();

	/// <summary>
	/// Gets or sets the <see cref="DNSQRBit"/> (query or response) portion of the DNS flags.
	/// </summary>
	public DNSQRBit QrBit
	{
		get => (DNSQRBit)(Flags & DNSConstants.QR);
		set => Flags = (ushort)((Flags & ~DNSConstants.QR) | (ushort)value);
	}

	/// <summary>
	/// Gets or sets the <see cref="DNSOpCode"/> portion of the DNS flags, indicating the
	/// requested operation (e.g., Standard query, Inverse, Status).
	/// </summary>
	public DNSOpCode OpCode
	{
		get => (DNSOpCode)(Flags & DNSConstants.OpCode);
		set => Flags = (ushort)((Flags & ~DNSConstants.OpCode) | (ushort)value);
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Authoritative Answer (AA) bit is set.
	/// </summary>
	/// <remarks>
	/// This bit is set in responses when the responding server is an authority for the domain in question.
	/// </remarks>
	public bool AuthoritativeAnswer
	{
		get => (Flags & DNSConstants.AuthoritativeAnswer) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.AuthoritativeAnswer) | (value ? DNSConstants.AuthoritativeAnswer : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Message Truncated (TC) bit is set.
	/// </summary>
	/// <remarks>
	/// If true, the total size of the DNS packet exceeded the allowable size, and the message was truncated.
	/// </remarks>
	public bool MessageTruncated
	{
		get => (Flags & DNSConstants.MessageTruncated) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.MessageTruncated) | (value ? DNSConstants.MessageTruncated : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Recursion Desired (RD) bit is set.
	/// </summary>
	/// <remarks>
	/// In a query, setting this bit requests that the DNS server perform recursive resolution on behalf of the client.
	/// </remarks>
	public bool RecursionDesired
	{
		get => (Flags & DNSConstants.RecursionDesired) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.RecursionDesired) | (value ? DNSConstants.RecursionDesired : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Recursion Available (RA) bit is set.
	/// </summary>
	/// <remarks>
	/// In a response, if this bit is set, the DNS server supports recursive queries.
	/// </remarks>
	public bool RecursionPossible
	{
		get => (Flags & DNSConstants.RecursionPossible) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.RecursionPossible) | (value ? DNSConstants.RecursionPossible : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Authentic Data (AD) bit is set.
	/// </summary>
	/// <remarks>
        /// This bit (introduced in <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see>) indicates that all data
        /// included in the answer and authority sections of the response has been authenticated by the server according to the
        /// server's policies.
	/// </remarks>
	public bool AuthenticDatas
	{
		get => (Flags & DNSConstants.AuthenticDatas) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.AuthenticDatas) | (value ? DNSConstants.AuthenticDatas : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the Checking Disabled (CD) bit is set.
	/// </summary>
	/// <remarks>
        /// This bit (introduced in <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see>) is set in a query to
        /// indicate that Pending (non-authenticated) data is acceptable to the resolver sending the query.
	/// </remarks>
	public bool CheckingDisabled
	{
		get => (Flags & DNSConstants.CheckingDisabled) != 0;
		set => Flags = (ushort)((Flags & ~DNSConstants.CheckingDisabled) | (value ? DNSConstants.CheckingDisabled : 0));
	}

	/// <summary>
	/// Gets or sets the portion of the header reserved for future use (Z bits).
	/// </summary>
	/// <remarks>
        /// Historically, these bits must be zero, but with <see href="https://www.rfc-editor.org/rfc/rfc2535">RFC 2535</see>,
        /// some of them were repurposed for AD/CD.
	/// </remarks>
	public byte ReservedFlags
	{
		get => (byte)(Flags & DNSConstants.ReservedZ);
		set => Flags = (ushort)((Flags | ~DNSConstants.ReservedZ) & (value & DNSConstants.ReservedZ));
	}

	/// <summary>
	/// Gets or sets the error code (RCODE) in the DNS header.
	/// </summary>
	/// <remarks>
	/// This indicates the status of the response (e.g., <see cref="DNSError.FormatError"/>, <see cref="DNSError.ServerFailure"/>, etc.).
	/// </remarks>
	public DNSError ErrorCode
	{
		get => (DNSError)(Flags & DNSConstants.Error);
		set => Flags = (ushort)((Flags | ~DNSConstants.Error) & ((ushort)value & DNSConstants.Error));
	}

	/// <summary>
	/// Merges (appends) the requests, responses, authority, and additional records from another
	/// <see cref="DNSHeader"/> into the current one, provided the IDs do not match.
	/// </summary>
	/// <param name="header">Another <see cref="DNSHeader"/> to merge from.</param>
	/// <exception cref="Exception">Thrown if the <paramref name="header"/> has the same <see cref="ID"/> as the current header.</exception>
	public void Append(DNSHeader header)
	{
		if (this.ID == header.ID)
		{
			throw new Exception("Mismatch headers");
		}

		foreach (var request in header.Requests)
		{
			if (!Requests.Contains(request, DNSElementsComparer.Default))
			{
				Requests.Add((DNSRequestRecord)request.Clone());
			}
		}

		foreach (var response in header.Responses)
		{
			if (!Responses.Contains(response))
			{
				Responses.Add((DNSResponseRecord)response.Clone());
			}
		}

		foreach (var authority in header.Authorities)
		{
			if (!Authorities.Contains(authority))
			{
				Authorities.Add((DNSResponseRecord)authority.Clone());
			}
		}

		foreach (var additional in header.Additionals)
		{
			if (!Additionals.Contains(additional))
			{
				Additionals.Add((DNSResponseRecord)additional.Clone());
			}
		}
	}

	/// <summary>
	/// Returns a string describing the current header fields and associated request/response sets.
	/// </summary>
	/// <returns>A human-readable <see cref="string"/> representation of this DNS header.</returns>
	public override string ToString() =>
		$""""
		{QrBit} ID = {ID}, Operation Code = {OpCode} 
			Recursition possible = {RecursionPossible}, Recursion desired = {RecursionDesired}
			Authentic Datas = {AuthenticDatas}, Checking Disables {CheckingDisabled}
		Requests :
			{string.Join(Environment.NewLine + "\t", Requests.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
		Responses : 
			{string.Join(Environment.NewLine + "\t", Responses.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
		Authorities : 
			{string.Join(Environment.NewLine + "\t", Authorities.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
		Additionals : 
			{string.Join(Environment.NewLine + "\t", Additionals.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
		"""";
}
