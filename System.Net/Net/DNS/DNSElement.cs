using System.Reflection;

namespace Utils.Net.DNS
{
	public abstract class DNSElement
    {
		public virtual string DNSType => this.GetType().Name;
    }
}