using System;
using System.Data;
using Utils.Reflection;

namespace Utils.Data;

/// <summary>
/// Represents a mapping between a field in a data record and a class member (property or field).
/// </summary>
public class FieldMap
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FieldMap"/> class using the specified member (property or field).
	/// The field name or index is derived from the <see cref="FieldAttribute"/> if present.
	/// </summary>
	/// <param name="member">The member information (property or field) to map.</param>
	public FieldMap(PropertyOrFieldInfo member)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		FieldAttribute = member.GetCustomAttribute<FieldAttribute>();

		// Use name from FieldAttribute, or fallback to member's name if not provided
		Name = FieldAttribute?.Name ?? Member.Member.Name;

		// Use index from FieldAttribute, or default to -1 if not provided
		Index = FieldAttribute?.Index ?? -1;

		// Set the value accessor based on whether index is provided or not
		getValue = FieldAttribute?.Index != null
			? (IDataRecord record) => record.GetValue(Index)
			: (IDataRecord record) => record[Name];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldMap"/> class using a specific field name.
	/// </summary>
	/// <param name="member">The member information (property or field) to map.</param>
	/// <param name="name">The name of the field in the data record.</param>
	public FieldMap(PropertyOrFieldInfo member, string name)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		Name = name ?? throw new ArgumentNullException(nameof(name)); // Ensure name is valid
		Index = -1; // Index is not used when mapping by name
		getValue = (IDataRecord record) => record[Name];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldMap"/> class using a specific field index.
	/// </summary>
	/// <param name="member">The member information (property or field) to map.</param>
	/// <param name="index">The index of the field in the data record.</param>
	public FieldMap(PropertyOrFieldInfo member, int index)
	{
		Member = member ?? throw new ArgumentNullException(nameof(member));
		Index = index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative");
		Name = null; // Name is not used when mapping by index
		getValue = (IDataRecord record) => record.GetValue(Index);
	}

	/// <summary>
	/// Gets the member information (property or field) being mapped.
	/// </summary>
	public PropertyOrFieldInfo Member { get; }

	/// <summary>
	/// Gets the associated <see cref="FieldAttribute"/> for this mapping, if available.
	/// </summary>
	internal FieldAttribute FieldAttribute { get; }

	/// <summary>
	/// Gets the name of the field in the data record being mapped, if available.
	/// </summary>
	internal string Name { get; }

	/// <summary>
	/// Gets the index of the field in the data record being mapped, or -1 if not using an index.
	/// </summary>
	internal int Index { get; }

	// Delegate to retrieve the field value from the data record
	private readonly Func<IDataRecord, object> getValue;

	/// <summary>
	/// Retrieves the value from the data record for the mapped field.
	/// </summary>
	/// <param name="record">The data record to retrieve the value from.</param>
	/// <returns>The value of the field from the data record.</returns>
	public object GetValue(IDataRecord record) => getValue(record);
}
