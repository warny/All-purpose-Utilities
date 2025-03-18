using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Utils.Net.DNS
{
    public sealed class DNSFactory
    {
        public static DNSFactory Default { get; } = new DNSFactory();

        private static IReadOnlyList<Type> GetDNSClassesFromAssembly(Assembly assembly)
            => GetDNSClasses(assembly.GetTypes());

        private static IReadOnlyList<Type> GetDNSClasses(IEnumerable<Type> types)
            => types
                .Where(t => typeof(DNSResponseDetail).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttributes<DNSRecordAttribute>().Any())
                .ToImmutableList();

        public IReadOnlyList<Type> DNSTypes { get; }
        private IReadOnlyDictionary<UshortRecordKey, Type> DNSResponsesById { get; }
        private IReadOnlyDictionary<StringRecordKey, ushort> DNSClassIdByName { get; }
        private IReadOnlyDictionary<UshortRecordKey, string> DNSClassNameById { get; }

        public DNSFactory()
        {
            DNSTypes = GetDNSClassesFromAssembly(typeof(DNSFactory).Assembly);

            var dnsResponsesById = new Dictionary<UshortRecordKey, Type>();
            var dnsClassNameById = new Dictionary<UshortRecordKey, string>();
            var dnsClassIdByName = new Dictionary<StringRecordKey, ushort>();

            foreach (var dnsType in DNSTypes)
            {
                foreach (var dnsClass in dnsType.GetCustomAttributes<DNSRecordAttribute>())
                {
                    string name = dnsClass.Name ?? dnsType.Name;
                    dnsResponsesById.Add(new(dnsClass.Class, dnsClass.RecordId), dnsType);
                    dnsClassNameById.Add(new(dnsClass.Class, dnsClass.RecordId), name);
                    dnsClassIdByName.Add(new(dnsClass.Class, dnsClass.Name ?? dnsType.Name), dnsClass.RecordId);
                }
            }
            this.DNSResponsesById = dnsResponsesById.ToImmutableDictionary();
            this.DNSClassIdByName = dnsClassIdByName.ToImmutableDictionary();
            this.DNSClassNameById = dnsClassNameById.ToImmutableDictionary();
        }

        public DNSFactory(params IEnumerable<DNSFactory> factories) {
            var dnsResponsesById = new Dictionary<UshortRecordKey, Type>();
            var dnsClassNameById = new Dictionary<UshortRecordKey, string>();
            var dnsClassIdByName = new Dictionary<StringRecordKey, ushort>();

            foreach (var factory in factories)
            {
                foreach (var ResponseById in factory.DNSResponsesById)
                {
                    dnsResponsesById[ResponseById.Key] = ResponseById.Value;
                }
                foreach (var classNameById in factory.DNSClassNameById)
                {
                    dnsClassNameById[classNameById.Key] = classNameById.Value;
                }
                foreach (var classIdByName in factory.DNSClassIdByName)
                {
                    dnsClassIdByName[classIdByName.Key] = classIdByName.Value;
                }
            }
            this.DNSResponsesById = dnsResponsesById.ToImmutableDictionary();
            this.DNSClassIdByName = dnsClassIdByName.ToImmutableDictionary();
            this.DNSClassNameById = dnsClassNameById.ToImmutableDictionary();
        }

        public DNSFactory(params IEnumerable<Type> types)
        {
            DNSTypes = GetDNSClasses(types);
        }

        public DNSFactory(params IEnumerable<Assembly> assemblies)
        {
            var dnsTypes = new List<Type>();
            foreach (Assembly assembly in assemblies)
            {
                dnsTypes.AddRange(GetDNSClassesFromAssembly(assembly));
            }
            DNSTypes = dnsTypes.ToImmutableList();
        }


        public string GetClassName(DNSClass @class, ushort classId) => DNSClassNameById[new (@class, classId)];
        public ushort GetClassId(DNSClass @class, string className) => DNSClassIdByName[new (@class, className)];
        public Type GetDNSType(DNSClass @class, ushort classId) => DNSResponsesById[new(@class, classId)];
        public Type GetDNSType(DNSClass @class, string className) => DNSResponsesById[new (@class, GetClassId(@class, className))];

        public static DNSFactory operator+(DNSFactory left, DNSFactory right) => new(left, right);


		private sealed class UshortRecordKey(DNSClass @class, ushort recordId) : IEquatable<UshortRecordKey>
		{
			public DNSClass Class { get; } = @class;
			public ushort RecordId { get; } = recordId;

			public bool Equals(UshortRecordKey other) 
				=> this.Class == other.Class && this.RecordId == other.RecordId;

			public override bool Equals(object obj)
				=> obj switch
				{
					UshortRecordKey other => Equals(other),
					_ => false,
				};

			public override int GetHashCode() => HashCode.Combine(Class, RecordId);
		}

		private sealed class StringRecordKey(DNSClass @class, string recordName) : IEquatable<StringRecordKey>
		{
			public DNSClass Class { get; } = @class;
			public string RecordName { get; } = recordName;

			public bool Equals(StringRecordKey other)
				=> this.Class == other.Class && this.RecordName == other.RecordName;

			public override bool Equals(object obj)
				=> obj switch
				{
					UshortRecordKey other => Equals(other),
					_ => false,
				};

			public override int GetHashCode() => HashCode.Combine(Class, RecordName);
		}
	}
}
