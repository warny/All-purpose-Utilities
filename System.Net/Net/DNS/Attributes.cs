using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace Utils.Net.DNS
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class DNSRecordAttribute(string @class, ushort recordId, string name = null) : Attribute
	{
		public string Class => @class;
		public ushort RecordId => recordId;
		public string Name => name;
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class DNSFieldAttribute(int length = 0 ) : Attribute { 
		public int Length => length;
		public string Condition { get; init; }
	}


	public static class FieldConstants
	{
		public const int PREFIXED_SIZE = -1;
	}
}
