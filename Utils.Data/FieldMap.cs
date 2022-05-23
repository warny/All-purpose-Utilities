using System;
using System.Data;
using Utils.Reflection;

namespace Utils.Data;

public class FieldMap {
	public FieldMap(FieldOrPropertyInfo member)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		FieldAttribute = member.GetCustomAttribute<FieldAttribute>();
		Name = FieldAttribute.Name ?? Member.Member.Name;
		Index = FieldAttribute.Index ?? -1;
		getValue = FieldAttribute.Index != null
				? (IDataRecord r) => r.GetValue(Index)
				: (IDataRecord r) => r[Name];
	}

	public FieldMap(FieldOrPropertyInfo member, string name)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		Name = name;
		Index = -1;
		getValue = (IDataRecord r) => r[Name];
	}

	public FieldMap(FieldOrPropertyInfo member, int index)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		Name = null;
		Index = FieldAttribute.Index ?? -1;
		getValue = (IDataRecord r) => r.GetValue(Index);
	}

	public FieldOrPropertyInfo Member { get; }
	internal FieldAttribute FieldAttribute { get; }
	internal string Name { get; }
	internal int Index { get; }
	private Func<IDataRecord, object> getValue { get; }

	public object GetValue(IDataRecord obj) => getValue(obj);
}
