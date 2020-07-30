using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS
{
	[AttributeUsage(AttributeTargets.Class)]
	public class DNSClassAttribute : Attribute
	{
		public ushort Class { get; }
		public DNSClassAttribute(ushort @class)
		{
			this.Class = @class;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class DNSFieldAttribute : Attribute
	{
	}
}
