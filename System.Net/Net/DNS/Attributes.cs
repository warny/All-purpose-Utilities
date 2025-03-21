using System;
using System.Linq.Expressions;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Specifies a DNS record attribute that can be applied at the class level to identify
	/// the DNS <see cref="DNSClass"/>, record ID, and an optional descriptive name.
	/// </summary>
	/// <remarks>
	/// Multiple <see cref="DNSRecordAttribute"/> instances can be applied to the same class
	/// if a single DNS record maps to multiple class/record ID pairs.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class DNSRecordAttribute(DNSClass @class, ushort recordId, string name = null) : Attribute
	{
		/// <summary>
		/// Gets the DNS class of the record. This indicates the protocol group or
		/// namespace in which the record is used.
		/// </summary>
		public DNSClass Class => @class;

		/// <summary>
		/// Gets the numerical DNS record type identifier.
		/// </summary>
		public ushort RecordId => recordId;

		/// <summary>
		/// Gets an optional descriptive name for the DNS record type.
		/// </summary>
		public string Name => name;
	}

	/// <summary>
	/// Applied to properties or fields within a DNS record class to indicate how the
	/// corresponding data should be serialized, deserialized, or otherwise interpreted.
	/// </summary>
	/// <remarks>
	/// The <see cref="Length"/> property can be used to specify the size or length of the
	/// field. If <see cref="Length"/> is an integer, it represents a fixed size. If it is a
	/// string, the named property or field is used to determine this size dynamically.
	/// If <see cref="Length"/> is a <see cref="FieldsSizeOptions"/>, the size is inferred
	/// from a prefix specification. The <see cref="Condition"/> property allows specifying
	/// an expression or condition that determines whether this field should be read or
	/// written.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class DNSFieldAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DNSFieldAttribute"/> class without a length constraint.
		/// </summary>
		public DNSFieldAttribute()
		{
			Length = null;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSFieldAttribute"/> class using a fixed length.
		/// </summary>
		/// <param name="length">The fixed size of this field or property.</param>
		public DNSFieldAttribute(int length)
		{
			Length = length;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSFieldAttribute"/> class using a length expression.
		/// </summary>
		/// <param name="length">
		/// A string that names another field or property which provides the dynamic size for this field.
		/// </param>
		public DNSFieldAttribute(string length)
		{
			Length = length;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSFieldAttribute"/> class using one of the prefixed-size options.
		/// </summary>
		/// <param name="options">The <see cref="FieldsSizeOptions"/> enum value indicating how this field is sized.</param>
		public DNSFieldAttribute(FieldsSizeOptions options)
		{
			Length = options;
		}

		/// <summary>
		/// Gets an object that describes the size of this field. It may be an <see cref="int"/>,
		/// a <see cref="string"/>, or one of the <see cref="FieldsSizeOptions"/> values.
		/// </summary>
		public object Length { get; }

		/// <summary>
		/// Gets or sets an optional condition string that determines if or when this field should be processed.
		/// </summary>
		public string Condition { get; init; }
	}

	/// <summary>
	/// Enumerates the supported prefixed-size mechanisms for array or field lengths.
	/// </summary>
	public enum FieldsSizeOptions
	{
		/// <summary>
		/// The field's size is prefixed by a one-byte (8-bit) length indicator.
		/// </summary>
		PrefixedSize1B,

		/// <summary>
		/// The field's size is prefixed by a two-byte (16-bit) length indicator.
		/// </summary>
		PrefixedSize2B,

		/// <summary>
		/// The field's size is prefixed in bits (1B indicating length in bits),
		/// rather than bytes.
		/// </summary>
		PrefixedSizeBits1B
	}
}
