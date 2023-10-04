﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class DNSClassAttribute(ushort classId, string name = null) : Attribute
	{
		public ushort ClassId => classId;
		public string Name => name;
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class DNSFieldAttribute(int length = 0) : Attribute { 
		public int Length => length;
	}
}
