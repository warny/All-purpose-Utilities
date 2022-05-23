using System;

namespace Utils.Data;

public class FieldAttribute : Attribute
{

	public FieldAttribute()
	{
		Name = null;
		Index = null;
	}

	public FieldAttribute(string name)
	{
		Name = name;
		Index = null;
	}

	public FieldAttribute(int index)
	{
		Name = null;
		Index = index;
	}

	public FieldAttribute(string name, int index)
	{
		Name = name;
		Index = index;
	}

	public string Name { get; set; }
	public int? Index { get; set; }
}
