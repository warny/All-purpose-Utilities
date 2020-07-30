using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1183;
using Utils.Net.DNS.RFC1876;
using Utils.Net.DNS.RFC1886;
using Utils.Net.DNS.RFC2052;

namespace Utils.Net.DNS
{
	public class DNSFactory
	{
		public static DNSFactory Default { get; } = new DNSFactory(true,
			typeof(Default),
			typeof(A), typeof(AAAA), typeof(CNAME),
			typeof(SOA), typeof(MX), typeof(MINFO), typeof(SRV),
			typeof(HINFO), typeof(TXT), typeof(NULL),
			typeof(NS), typeof(MB), typeof(MD), typeof(MF), typeof(MG), typeof(MR), 
			typeof(PTR), typeof(WKS),
			typeof(AFSDB), typeof(ISDN), typeof(RP), typeof(RT), typeof(X25),
			typeof(LOC)
		);

		protected readonly Dictionary<ushort, Type> DNSRequests = new Dictionary<ushort, Type>();
		protected readonly Dictionary<ushort, Type> DNSResponses = new Dictionary<ushort, Type>();
		protected readonly Dictionary<string, ushort> DNSClassesNames = new Dictionary<string, ushort>()
		{
			{ "ALL", 0x00FF },
			{ "AXFR", 0x00FC },
            { "MAILB", 0x00FD },
            { "MAILA", 0x00FE }
		};
		protected readonly Dictionary<ushort, string> DNSClassesIds = new Dictionary<ushort, string>()
		{
			{ 0x00FF, "ALL" },
			{ 0x00FC, "AXFR" },
			{ 0x00FD, "MAILB" },
			{ 0x00FE, "MAILA" }

		};

		private readonly bool @readonly;

		public DNSFactory() : this(false) { }
		public DNSFactory(params Type[] types) : this(false, types) { }
		public DNSFactory(bool @readonly, params Type[] types)
		{
			foreach (var type in types) AddType(type);
			this.@readonly = @readonly;
		}

		public void AddType(Type type)
		{
			if (@readonly) throw new ReadOnlyException();
			var dnsClass = type.GetCustomAttribute<DNSClassAttribute>(false);
			if (dnsClass == null) throw new ArgumentException(nameof(type), $"La classe {type.FullName} doit avoir un attribut {Types.dnsClassAttributeType.FullName}");
			if (Types.dnsRequestRecordType.IsAssignableFrom(type))
			{
				DNSRequests[dnsClass.Class] = type;
				return;
			}
			if (Types.dnsResponseDetailType.IsAssignableFrom(type))
			{
				DNSResponses[dnsClass.Class] = type;
				DNSClassesNames[type.Name] = dnsClass.Class;
				DNSClassesIds[dnsClass.Class] = type.Name;
				return;
			}
			throw new ArgumentException(nameof(type), $"La classe {type.FullName} doit dériver de {Types.dnsRequestRecordType.FullName} ou de {Types.dnsResponseDetailType.FullName}");
		}

		public DNSRequestRecord CreateRequestElement(ushort elementId)
		{
			var elementType = DNSRequests[elementId];
			return (DNSRequestRecord)Activator.CreateInstance(elementType);
		}

		public DNSResponseDetail CreateResponseDetail(ushort classIdentifier)
		{
			var elementType = DNSResponses[classIdentifier];
			return (DNSResponseDetail)Activator.CreateInstance(elementType);
		}

		public string GetClassName(ushort classIdentifier)
		{
			return DNSClassesIds[classIdentifier];
		}

		public ushort GetClassIdentifier(string className) => DNSClassesNames[className];
	}
}
