using System.Reflection;

namespace Utils.Net.DNS
{
	public abstract class DNSElement
    {
		public virtual string DNSType => this.GetType().Name;

		internal protected virtual void Write(DNSDatagram datagram, DNSFactory factory)
        {
            foreach (var field in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var dnsField = field.GetCustomAttribute<DNSFieldAttribute>();
                if (dnsField is null) continue;
                if (field.PropertyType == typeof(DNSElement)) ((DNSElement)field.GetValue(this)).Write(datagram, factory);
                else if (field.PropertyType == typeof(string)) datagram.Write((string)field.GetValue(this));
                else if (field.PropertyType == typeof(byte)) datagram.Write((byte)field.GetValue(this));
                else if (field.PropertyType == typeof(ushort)) datagram.Write((ushort)field.GetValue(this));
                else if (field.PropertyType == typeof(uint)) datagram.Write((uint)field.GetValue(this));
                else if (field.PropertyType == typeof(DNSClass)) datagram.Write((ushort)field.GetValue(this));
            }
        }
        internal protected virtual void Read(DNSDatagram datagram, DNSFactory factory)
        {
            foreach (var field in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var dnsField = field.GetCustomAttribute<DNSFieldAttribute>();
                if (dnsField is null) continue;
				if (field.PropertyType == typeof(DNSElement)) ((DNSElement)field.GetValue(this)).Read(datagram, factory);
				else if (field.PropertyType == typeof(string)) field.SetValue(this, datagram.ReadString());
                else if (field.PropertyType == typeof(byte)) field.SetValue(this, datagram.ReadByte());
                else if (field.PropertyType == typeof(ushort)) field.SetValue(this, datagram.ReadUShort());
                else if (field.PropertyType == typeof(uint)) field.SetValue(this, datagram.ReadUInt());
                else if (field.PropertyType == typeof(DNSClass)) field.SetValue(this, datagram.ReadUShort());
            }
        }
    }
}