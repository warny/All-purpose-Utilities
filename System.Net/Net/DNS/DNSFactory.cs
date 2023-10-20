using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using Utils.Net.DNS.RFC1035;
using Utils.Net.DNS.RFC1183;
using Utils.Net.DNS.RFC1876;
using Utils.Net.DNS.RFC2052;
using Utils.Net.DNS.RFC2535;
using Utils.Net.DNS.RFC2915;

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
        private IReadOnlyDictionary<ushort, Type> DNSResponsesById { get; }
        private IReadOnlyDictionary<string, ushort> DNSClassIdByName { get; }
        private IReadOnlyDictionary<ushort, string> DNSClassNameById { get; }

        public DNSFactory()
        {
            DNSTypes = GetDNSClassesFromAssembly(typeof(DNSFactory).Assembly);

            var dnsResponsesById = new Dictionary<ushort, Type>();
            var dnsClassNameById = new Dictionary<ushort, string>();
            var dnsClassIdByName = new Dictionary<string, ushort>();

            foreach (var dnsType in DNSTypes)
            {
                foreach (var dnsClass in dnsType.GetCustomAttributes<DNSRecordAttribute>())
                {
                    string name = dnsClass.Name ?? dnsType.Name;
                    dnsResponsesById.Add(dnsClass.RecordId, dnsType);
                    dnsClassNameById.Add(dnsClass.RecordId, name);
                    dnsClassIdByName.Add(name, dnsClass.RecordId);
                }
            }
            this.DNSResponsesById = dnsResponsesById.ToImmutableDictionary();
            this.DNSClassIdByName = dnsClassIdByName.ToImmutableDictionary();
            this.DNSClassNameById = dnsClassNameById.ToImmutableDictionary();
        }

        public DNSFactory(params DNSFactory[] factories) : this((IEnumerable<DNSFactory>)factories) { }

        public DNSFactory(IEnumerable<DNSFactory> factories) {
            var dnsResponsesById = new Dictionary<ushort, Type>();
            var dnsClassNameById = new Dictionary<ushort, string>();
            var dnsClassIdByName = new Dictionary<string, ushort>();

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

        public DNSFactory(params Type[] types)
        {
            DNSTypes = GetDNSClasses(types);
        }

        public DNSFactory(params Assembly[] assemblies)
        {
            var dnsTypes = new List<Type>();
            foreach (Assembly assembly in assemblies)
            {
                dnsTypes.AddRange(GetDNSClassesFromAssembly(assembly));
            }
            DNSTypes = dnsTypes.ToImmutableList();
        }


        public string GetClassName(ushort classId) => DNSClassNameById[classId];
        public ushort GetClassId(string className) => DNSClassIdByName[className];
        public Type GetDNSType(ushort classId) => DNSResponsesById[classId];
        public Type GetDNSType(string className) => DNSResponsesById[DNSClassIdByName[className]];

        public static DNSFactory operator+(DNSFactory left, DNSFactory right) => new DNSFactory(left, right);
    }
}
