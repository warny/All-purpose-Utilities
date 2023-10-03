using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
	public abstract class DNSResponseDetail : DNSElement
	{
		internal ushort ClassId => this.GetType().GetCustomAttribute<DNSClassAttribute>().ClassId;

        public virtual string Name => this.GetType().Name;

		internal ushort Length { get; set; }
	}
}
