using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
	public abstract class DNSResponseDetail : DNSElement, ICloneable
	{
		internal virtual ushort ClassId => this.GetType().GetCustomAttribute<DNSRecordAttribute>().RecordId;

        public virtual string Name => this.GetType().Name;

		internal ushort Length { get; set; }

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

        public object Clone()
        {
            var result = Activator.CreateInstance(this.GetType());
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
