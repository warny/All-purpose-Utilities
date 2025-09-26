using System;

namespace Utils.Net.DNS;



/// <summary>
/// Represents a DNS response record, containing all necessary fields to describe
/// a specific DNS answer in a response packet, including the domain name, the record type,
/// TTL, and the <see cref="RData"/>, as defined by
/// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.3">RFC 1035 §4.1.3</see>.
/// </summary>
/// <remarks>
/// A DNS response record includes:
/// <list type="bullet">
/// <item><description>The queried <see cref="Name"/> (DNSDomainName).</description></item>
/// <item><description>The numeric <see cref="Class"/> of the response (e.g., 1 for A, 28 for AAAA), stored in <see cref="DNSResponseDetail.ClassId"/>.</description></item>
/// <item><description>The DNS class (<see cref="ClassId"/>), typically <see cref="DNSClassId.IN"/>.</description></item>
/// <item><description>The <see cref="TTL"/>, or time-to-live, indicating how long the record may be cached.</description></item>
/// <item><description>An <see cref="RDLength"/> (2 bytes) indicating the size of the resource data.</description></item>
/// <item><description>The <see cref="RData"/>, which is a <see cref="DNSResponseDetail"/> object representing the record-specific data (e.g., IP addresses, mail server, etc.).</description></item>
/// </list>
/// <para>The wire layout in <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.3">RFC 1035 §4.1.3</see> is:</para>
/// <code>
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// /                      NAME                     /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                      TYPE                     |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                     CLASS                     |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                      TTL                      |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// |                   RDLENGTH                    |
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--|
/// /                     RDATA                     /
/// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
/// </code>
/// </remarks>
public sealed class DNSResponseRecord : DNSElement, ICloneable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DNSResponseRecord"/> class.
	/// </summary>
	public DNSResponseRecord()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSResponseRecord"/> class with the specified domain name,
	/// TTL, and record details.
	/// </summary>
	/// <param name="name">The domain name to which the record pertains.</param>
	/// <param name="TTL">The time-to-live for this record.</param>
	/// <param name="details">A concrete <see cref="DNSResponseDetail"/> subclass representing the record data.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> or <paramref name="details"/> is <c>null</c>.
	/// </exception>
	public DNSResponseRecord(string name, uint TTL, DNSResponseDetail details)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		this.TTL = TTL;
		RData = details ?? throw new ArgumentNullException(nameof(details));
	}

	/// <summary>
	/// Gets or sets the DNS domain name associated with this response record.
	/// </summary>
	[DNSField]
	public DNSDomainName Name { get; set; }

	/// <summary>
	/// Gets or sets the numeric record type for this DNS response.
	/// Typically set by <see cref="RData"/>'s <see cref="DNSResponseDetail.ClassId"/>.
	/// </summary>
	[DNSField]
	public ushort Class { get; internal set; }

	/// <summary>
	/// Gets or sets the DNS class for this response (commonly <see cref="DNSClassId.IN"/>).
	/// </summary>
	[DNSField]
	public DNSClassId ClassId { get; internal set; }

	/// <summary>
	/// Gets or sets the time-to-live (TTL) for this record, indicating how long it may be cached.
	/// </summary>
	[DNSField]
	public uint TTL { get; set; }

	/// <summary>
	/// Gets or sets the length of the RData section, in bytes. This is determined during serialization.
	/// </summary>
	[DNSField]
	public ushort RDLength { get; internal set; }

	private DNSResponseDetail rData;

	/// <summary>
	/// Gets or sets the <see cref="DNSResponseDetail"/> describing the record data (RDATA).
	/// Setting this value updates <see cref="Class"/> to reflect the RData's <see cref="DNSResponseDetail.ClassId"/>.
	/// </summary>
	public DNSResponseDetail RData
	{
		get => rData;
		set {
			// Ensure the Type property is in sync with the RData's declared ClassId.
			Class = value.ClassId;
			ClassId = value.Class;
			rData = value;
		}
	}

	/// <summary>
	/// Returns a multi-line string describing this response record, including its domain name, class, TTL,
	/// and the specific RData fields.
	/// </summary>
	/// <returns>A human-readable summary of this DNS response record.</returns>
	public override string ToString() =>
		$"""
            {RData.Name} {Name} {ClassId}, TTL : {TTL}
            	{RData.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")}
            """;

	/// <inheritdoc />
	/// <summary>
	/// Creates a shallow copy of this <see cref="DNSResponseRecord"/>. 
	/// </summary>
	/// <remarks>
	/// The returned object is an anonymous type containing copies of the basic fields. 
	/// The <see cref="RData"/> is also cloned, calling <see cref="DNSResponseDetail.Clone"/> 
	/// to duplicate its content fields.
	/// </remarks>
	/// <returns>A new object preserving the relevant data fields.</returns>
	public object Clone() => new
	{
		Name,
		TTL,
		ClassId,
		RDLength,
		RData = (DNSResponseDetail)rData?.Clone()
	};
}
