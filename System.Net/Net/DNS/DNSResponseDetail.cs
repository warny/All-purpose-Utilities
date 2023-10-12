using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
	public abstract class DNSResponseDetail : DNSElement
	{
		internal virtual ushort ClassId => this.GetType().GetCustomAttribute<DNSClassAttribute>().ClassId;

        public virtual string Name => this.GetType().Name;

		internal ushort Length { get; set; }

        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var property in GetType().GetProperties())
            {
                var dnsFieldAttr = (DNSFieldAttribute)Attribute.GetCustomAttribute(property, typeof(DNSFieldAttribute));
                if (dnsFieldAttr is null) continue;
                result.AppendLine($"{property.Name}: {property.GetValue(this)}");
            }
            return result.ToString();
        }

    }
}
