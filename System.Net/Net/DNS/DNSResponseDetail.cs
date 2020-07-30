using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS
{
	public abstract class DNSResponseDetail : DNSElement
	{
		public virtual string Name => this.GetType().Name;

		internal ushort Length { get; set; }
	}
}
