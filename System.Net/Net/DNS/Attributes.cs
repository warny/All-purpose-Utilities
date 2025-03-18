using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace Utils.Net.DNS
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class DNSRecordAttribute(DNSClass @class, ushort recordId, string name = null) : Attribute
	{
		public DNSClass Class => @class;
		public ushort RecordId => recordId;
		public string Name => name;
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class DNSFieldAttribute : Attribute {
		public DNSFieldAttribute()
		{
			Length = null;
		}

		public DNSFieldAttribute(int length)
		{
			Length = length;
		}

		public DNSFieldAttribute(string length)
		{ 
			Length = length;
		}

		public DNSFieldAttribute(FieldsSizeOptions options)
		{
			Length = options;
		}

		public object Length { get; }
		public string Condition { get; init; }
	}


	public enum FieldsSizeOptions
	{
		PrefixedSize1B,
        PrefixedSize2B,
		PrefixedSizeBits1B  
    }
}
