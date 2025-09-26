using System;

namespace Utils.Net.DNS
{
	

	/// <summary>
        /// Represents a DNS query (request) record within a DNS packet, following the layout defined in
        /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.2">RFC 1035 §4.1.2</see> and indicating
        /// which record type (e.g., A, AAAA, MX) is being queried for a particular domain name.
	/// </summary>
        /// <remarks>
        /// This class stores fields such as:
        /// <list type="bullet">
        /// <item><description><see cref="Name"/>: The domain name being queried.</description></item>
        /// <item><description><see cref="RequestType"/>: A 16-bit identifier for the query type (e.g., 0x01 for A records).</description></item>
        /// <item><description><see cref="Class"/>: The DNS class, typically <see cref="DNSClassId.IN"/> or <see cref="DNSClassId.ALL"/>.</description></item>
        /// </list>
        /// It also provides an explicit <see cref="Type"/> property representing the record type in string format.
        /// The <see cref="Clone"/> method returns a shallow copy of this record’s essential fields.
        /// <para>The query section wire layout from
        /// <see href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.2">RFC 1035 §4.1.2</see> is:</para>
        /// <code>
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// /                      NAME                     /
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// |                      TYPE                     |
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// |                     CLASS                     |
        /// +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        /// </code>
        /// </remarks>
        public class DNSRequestRecord : DNSElement, ICloneable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DNSRequestRecord"/> class.
		/// </summary>
		public DNSRequestRecord()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSRequestRecord"/> class with the specified
		/// <paramref name="type"/>, <paramref name="name"/>, and optionally, a DNS <paramref name="class"/>.
		/// </summary>
		/// <param name="type">The string identifier for the requested record type (e.g., "A", "AAAA").</param>
		/// <param name="name">The domain name being queried.</param>
		/// <param name="class">The DNS class for the query (default is <see cref="DNSClassId.ALL"/>).</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="type"/> or <paramref name="name"/> is <c>null</c>.
		/// </exception>
		public DNSRequestRecord(string type, string name, DNSClassId @class = DNSClassId.ALL)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Class = @class;
			Type = type ?? throw new ArgumentNullException(nameof(type));
		}

		/// <summary>
		/// Gets or sets the domain name being queried.
		/// </summary>
		[DNSField]
		public DNSDomainName Name { get; set; }

		/// <summary>
		/// Gets or sets the numeric DNS request type, as required by the DNS wire format.
		/// </summary>
		/// <remarks>
		/// This field is an internal raw numeric ID. It corresponds to the <see cref="Type"/> property at runtime.
		/// </remarks>
		[DNSField]
		internal ushort RequestType { get; set; }

		/// <summary>
		/// Gets or sets the DNS class of the request (e.g., IN or ALL).
		/// </summary>
		[DNSField]
		public DNSClassId Class { get; set; }

		/// <summary>
		/// Gets or sets the string identifier for the requested DNS record type (e.g., "A", "AAAA", "MX").
		/// </summary>
		/// <remarks>
		/// This property is not directly part of the DNS wire format; rather, it determines
		/// <see cref="RequestType"/> internally when the record is serialized.
		/// </remarks>
		public string Type { get; set; }

		/// <inheritdoc />
		/// <summary>
		/// Returns a string containing the record type, domain name, and DNS class.
		/// </summary>
		public override string ToString() => $"Request {Type} : {Name} ({Class})";

		/// <inheritdoc />
		/// <summary>
		/// Creates a shallow copy of the current <see cref="DNSRequestRecord"/> object,
		/// preserving fields such as <see cref="Name"/>, <see cref="Type"/>, and <see cref="Class"/>.
		/// </summary>
		/// <returns>A new, independent object with the same field values.</returns>
		public object Clone() => new
		{
			Name,
			Class,
			Type
		};
	}
}
