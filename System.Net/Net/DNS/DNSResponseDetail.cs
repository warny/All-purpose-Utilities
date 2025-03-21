using System;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Represents the base class for DNS response details (RData). Concrete types that inherit from this class
	/// are annotated with <see cref="DNSRecordAttribute"/> for identifying DNS record types and include
	/// fields marked by <see cref="DNSFieldAttribute"/> for serialization.
	/// </summary>
	/// <remarks>
	/// Typical DNS record data (for example, A, AAAA, MX, NS, etc.) should derive from this class.
	/// Reflection is used by the DNS serialization/deserialization engine to read and write the fields
	/// that carry record-specific data.
	/// </remarks>
	public abstract class DNSResponseDetail : DNSElement, ICloneable
	{
		/// <summary>
		/// Gets the numeric DNS record ID (e.g., 0x01 for A, 0x1C for AAAA) via reflection on
		/// the <see cref="DNSRecordAttribute"/>. 
		/// </summary>
		/// <remarks>
		/// This ID identifies the record type on the wire. Each derived class must be decorated
		/// with at least one <see cref="DNSRecordAttribute"/> to provide this value.
		/// </remarks>
		internal virtual ushort ClassId
			=> GetType().GetCustomAttribute<DNSRecordAttribute>()?.RecordId
			   ?? throw new InvalidOperationException($"No DNSRecordAttribute found on {GetType().FullName}.");

		/// <summary>
		/// Gets the human-readable DNS record name from the <see cref="DNSRecordAttribute"/>
		/// or defaults to the class name if none is specified.
		/// </summary>
		public virtual string Name
			=> GetType().Name;

		/// <summary>
		/// Gets or sets the length (in bytes) of the data portion of the response (RData).
		/// This is updated automatically during serialization.
		/// </summary>
		internal ushort Length { get; set; }

		/// <summary>
		/// Returns a string representation of this DNS record by listing its annotated fields 
		/// and properties, including their current values.
		/// </summary>
		/// <returns>
		/// A multi-line string listing the record's field names and values.
		/// </returns>
		public override string ToString()
		{
			var result = new StringBuilder();
			foreach (var dnsField in DNSPacketHelpers.GetDNSFields(GetType()))
			{
				if (dnsField.Member is FieldInfo f)
				{
					result.AppendLine($"{f.Name}: {f.GetValue(this)}");
				}
				else if (dnsField.Member is PropertyInfo p)
				{
					result.AppendLine($"{p.Name}: {p.GetValue(this)}");
				}
			}
			return result.ToString();
		}

		/// <summary>
		/// Creates a shallow copy of this DNS response detail record by duplicating the values
		/// of all fields/properties annotated with <see cref="DNSFieldAttribute"/>.
		/// </summary>
		/// <returns>A new instance of the same concrete type with copied field values.</returns>
		public object Clone()
		{
			// Instantiate a new object of the derived type.
			var result = Activator.CreateInstance(GetType());
			foreach (var dnsField in DNSPacketHelpers.GetDNSFields(GetType()))
			{
				if (dnsField.Member is FieldInfo f)
				{
					f.SetValue(result, f.GetValue(this));
				}
				else if (dnsField.Member is PropertyInfo p)
				{
					p.SetValue(result, p.GetValue(this));
				}
			}
			return result;
		}
	}
}
