using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Net.Icmp;

public class IcmpException : Exception
{
	public IcmpException(string message) : base(message) { }

	public IcmpException(string message, Exception innerException) : base(message, innerException) { }
}
