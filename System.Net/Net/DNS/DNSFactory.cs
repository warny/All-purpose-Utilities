using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Utils.Net.DNS;

/// <summary>
/// Provides a mechanism to discover and store DNS-related types, along with their associated class and record IDs.
/// This class uses <see cref="DNSRecordAttribute"/> metadata to map DNS record classes and IDs to corresponding
/// .NET types that implement <see cref="DNSResponseDetail"/>.
/// </summary>
/// <remarks>
/// A default singleton instance is available via <see cref="Default"/>. Additional instances can be built from
/// specified assemblies or types. Instances may be combined with the <c>+</c> operator to aggregate multiple
/// <see cref="DNSFactory"/> configurations into one.
/// </remarks>
public sealed class DNSFactory : IAdditionOperators	 <DNSFactory, DNSFactory, DNSFactory>
{
	/// <summary>
	/// Gets the default <see cref="DNSFactory"/> instance, scanning the current assembly for DNS types
	/// annotated with <see cref="DNSRecordAttribute"/>.
	/// </summary>
	public static DNSFactory Default { get; } = new DNSFactory();

	/// <summary>
	/// Gets a read-only list of all discovered DNS response types
	/// (those implementing <see cref="DNSResponseDetail"/> and annotated with <see cref="DNSRecordAttribute"/>).
	/// </summary>
	/// <remarks>
	/// Only set if you call a constructor that explicitly scans assemblies or a provided collection of types.
	/// </remarks>
	public IReadOnlyList<Type> DNSTypes { get; }

	/// <summary>
	/// Maps a (<see cref="DNSClass"/>, <c>ushort</c>) pair to the corresponding DNS response type.
	/// </summary>
	private IReadOnlyDictionary<UshortRecordKey, Type> DNSResponsesById { get; }

	/// <summary>
	/// Maps a (<see cref="DNSClass"/>, record name) pair to the corresponding record ID (<c>ushort</c>).
	/// </summary>
	private IReadOnlyDictionary<StringRecordKey, ushort> DNSClassIdByName { get; }

	/// <summary>
	/// Maps a (<see cref="DNSClass"/>, record ID) pair to the associated record name (<see cref="string"/>).
	/// </summary>
	private IReadOnlyDictionary<UshortRecordKey, string> DNSClassNameById { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSFactory"/> class by scanning the assembly
	/// that contains this type for DNS-related classes.
	/// </summary>
	/// <remarks>
	/// Discovers all types in the same assembly that derive from <see cref="DNSResponseDetail"/> and bear
	/// at least one <see cref="DNSRecordAttribute"/>.
	/// </remarks>
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
				Debug.Print($"{dnsClass.Class} {name} ({dnsClass.RecordId:x2})");
				dnsResponsesById.Add(new UshortRecordKey(dnsClass.Class, dnsClass.RecordId), dnsType);
				dnsClassNameById.Add(new UshortRecordKey(dnsClass.Class, dnsClass.RecordId), name);
				dnsClassIdByName.Add(new StringRecordKey(dnsClass.Class, dnsClass.Name ?? dnsType.Name), dnsClass.RecordId);
			}
		}

		DNSResponsesById = dnsResponsesById.ToImmutableDictionary();
		DNSClassIdByName = dnsClassIdByName.ToImmutableDictionary();
		DNSClassNameById = dnsClassNameById.ToImmutableDictionary();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSFactory"/> class by aggregating one or more
	/// <see cref="DNSFactory"/> instances into a single combined factory.
	/// </summary>
	/// <param name="factories">A collection of <see cref="DNSFactory"/> instances to combine.</param>
	/// <remarks>
	/// The aggregated factory merges the mappings from all specified <paramref name="factories"/>.
	/// If duplicates occur for the same (<see cref="DNSClass"/>, ID) or (<see cref="DNSClass"/>, name) pair,
	/// the last entry encountered overwrites previous ones.
	/// </remarks>
	public DNSFactory(params IEnumerable<DNSFactory> factories)
	{
		var dnsResponsesById = new Dictionary<UshortRecordKey, Type>();
		var dnsClassNameById = new Dictionary<UshortRecordKey, string>();
		var dnsClassIdByName = new Dictionary<StringRecordKey, ushort>();

		foreach (var factory in factories)
		{
			foreach (var responseById in factory.DNSResponsesById)
			{
				dnsResponsesById[responseById.Key] = responseById.Value;
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

		DNSResponsesById = dnsResponsesById.ToImmutableDictionary();
		DNSClassIdByName = dnsClassIdByName.ToImmutableDictionary();
		DNSClassNameById = dnsClassNameById.ToImmutableDictionary();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSFactory"/> class by scanning a set of types
	/// for DNS-related classes (those derived from <see cref="DNSResponseDetail"/> and tagged with
	/// <see cref="DNSRecordAttribute"/>).
	/// </summary>
	/// <param name="types">A collection of <see cref="Type"/> objects to scan.</param>
	/// <remarks>
	/// This constructor only sets the <see cref="DNSTypes"/> property. It does not populate internal mappings
	/// unless you combine it with another factory. For a fully initialized factory, see <see cref="DNSFactory()"/>
	/// or <see cref="DNSFactory(IEnumerable{Assembly})"/>.
	/// </remarks>
	public DNSFactory(params IEnumerable<Type> types)
	{
		DNSTypes = GetDNSClasses(types);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSFactory"/> class by scanning the given assemblies
	/// for DNS-related classes.
	/// </summary>
	/// <param name="assemblies">A collection of <see cref="Assembly"/> instances to scan.</param>
	/// <remarks>
	/// This constructor only sets the <see cref="DNSTypes"/> property. It does not populate internal mappings
	/// unless you combine it with another factory. For a fully initialized factory, see <see cref="DNSFactory()"/>
	/// or use <see cref="operator+"/> to merge with another factory.
	/// </remarks>
	public DNSFactory(params IEnumerable<Assembly> assemblies)
	{
		var dnsTypes = new List<Type>();
		foreach (Assembly assembly in assemblies)
		{
			dnsTypes.AddRange(GetDNSClassesFromAssembly(assembly));
		}
		DNSTypes = dnsTypes.ToImmutableList();
	}

	/// <summary>
	/// Retrieves the recorded name for a specific (<see cref="DNSClass"/>, <c>ushort</c>) combination.
	/// </summary>
	/// <param name="class">The <see cref="DNSClass"/>.</param>
	/// <param name="classId">The record identifier.</param>
	/// <returns>The stored record name.</returns>
	public string GetClassName(DNSClass @class, ushort classId)
		=> DNSClassNameById[new UshortRecordKey(@class, classId)];

	/// <summary>
	/// Retrieves the recorded ID for a specific (<see cref="DNSClass"/>, record name) combination.
	/// </summary>
	/// <param name="class">The <see cref="DNSClass"/>.</param>
	/// <param name="className">The record name.</param>
	/// <returns>The stored record ID.</returns>
	public ushort GetClassId(DNSClass @class, string className)
		=> DNSClassIdByName[new StringRecordKey(@class, className)];

	/// <summary>
	/// Retrieves the DNS response <see cref="Type"/> for a specific (<see cref="DNSClass"/>, <c>ushort</c>) combination.
	/// </summary>
	/// <param name="class">The <see cref="DNSClass"/>.</param>
	/// <param name="classId">The record identifier.</param>
	/// <returns>A <see cref="Type"/> that implements <see cref="DNSResponseDetail"/>.</returns>
	public Type GetDNSType(DNSClass @class, ushort classId)
		=> DNSResponsesById[new UshortRecordKey(@class, classId)];

	/// <summary>
	/// Retrieves the DNS response <see cref="Type"/> for a specific (<see cref="DNSClass"/>, record name) combination.
	/// </summary>
	/// <param name="class">The <see cref="DNSClass"/>.</param>
	/// <param name="className">The record name.</param>
	/// <returns>A <see cref="Type"/> that implements <see cref="DNSResponseDetail"/>.</returns>
	public Type GetDNSType(DNSClass @class, string className)
		=> DNSResponsesById[new UshortRecordKey(@class, GetClassId(@class, className))];

	/// <summary>
	/// Combines two <see cref="DNSFactory"/> instances into a new factory that merges their mappings.
	/// </summary>
	/// <param name="left">The first <see cref="DNSFactory"/>.</param>
	/// <param name="right">The second <see cref="DNSFactory"/>.</param>
	/// <returns>A new <see cref="DNSFactory"/> containing the merged mappings of <paramref name="left"/> and <paramref name="right"/>.</returns>
	public static DNSFactory operator +(DNSFactory left, DNSFactory right)
		=> new DNSFactory(left, right);

	/// <summary>
	/// Retrieves all DNS response classes from an <see cref="Assembly"/> that derive from <see cref="DNSResponseDetail"/>
	/// and bear the <see cref="DNSRecordAttribute"/>.
	/// </summary>
	/// <param name="assembly">The <see cref="Assembly"/> to scan.</param>
	/// <returns>A read-only list of matching types.</returns>
	private static IReadOnlyList<Type> GetDNSClassesFromAssembly(Assembly assembly)
		=> GetDNSClasses(assembly.GetTypes());

	/// <summary>
	/// Retrieves all DNS response classes from a provided collection of <see cref="Type"/> objects
	/// that derive from <see cref="DNSResponseDetail"/> and bear the <see cref="DNSRecordAttribute"/>.
	/// </summary>
	/// <param name="types">A collection of <see cref="Type"/> objects to inspect.</param>
	/// <returns>A read-only list of matching types.</returns>
	private static IReadOnlyList<Type> GetDNSClasses(IEnumerable<Type> types)
		=> types
			.Where(t => typeof(DNSResponseDetail).IsAssignableFrom(t))
			.Where(t => t.GetCustomAttributes<DNSRecordAttribute>().Any())
			.ToImmutableList();

	/// <summary>
	/// Serves as a unique dictionary key for records identified by a (<see cref="DNSClass"/>, <c>ushort</c>) tuple.
	/// </summary>
	private sealed class UshortRecordKey(DNSClass @class, ushort recordId) : IEquatable<UshortRecordKey>
	{
		public DNSClass Class { get; } = @class;
		public ushort RecordId { get; } = recordId;

		public bool Equals(UshortRecordKey other)
			=> Class == other.Class && RecordId == other.RecordId;

		public override bool Equals(object obj)
			=> obj is UshortRecordKey other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Class, RecordId);
	}

	/// <summary>
	/// Serves as a unique dictionary key for records identified by a (<see cref="DNSClass"/>, <c>string</c>) tuple.
	/// </summary>
	private sealed class StringRecordKey(DNSClass @class, string recordName) : IEquatable<StringRecordKey>
	{
		public DNSClass Class { get; } = @class;
		public string RecordName { get; } = recordName;

		public bool Equals(StringRecordKey other)
			=> Class == other.Class && RecordName == other.RecordName;

		public override bool Equals(object obj)
			=> obj is StringRecordKey other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Class, RecordName);
	}
}
