using System;

namespace Utils.Data;

/// <summary>
/// Attribute to specify mapping information for fields in data records.
/// Can be used to define a field by its name or index, or both.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class FieldAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the name of the field in the data record.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Gets or sets the index of the field in the data record.
	/// </summary>
	public int? Index { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldAttribute"/> class without specifying a name or index.
	/// </summary>
	public FieldAttribute() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldAttribute"/> class with the specified field name.
	/// </summary>
	/// <param name="name">The name of the field in the data record.</param>
	public FieldAttribute(string name)
	{
		Name = name;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldAttribute"/> class with the specified field index.
	/// </summary>
	/// <param name="index">The index of the field in the data record.</param>
	public FieldAttribute(int index)
	{
		Index = index;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldAttribute"/> class with both a field name and index.
	/// </summary>
	/// <param name="name">The name of the field in the data record.</param>
	/// <param name="index">The index of the field in the data record.</param>
	public FieldAttribute(string name, int index)
	{
		Name = name;
		Index = index;
	}
}
