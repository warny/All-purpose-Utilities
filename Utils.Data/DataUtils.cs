using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Utils.Data;

public static class DataUtils
{
	private readonly static Dictionary<Type, FieldMap[]> maps = new Dictionary<Type, FieldMap[]>();


	public static T ToObject<T>(this IDataRecord record) where T : new()
	{
		T obj = new T();
		record.FillObject(obj);
		return obj;
	}

	public static void FillObject(this IDataRecord record, object obj)
	{
		var fieldsOrProperties = GetFieldsOrProperties(obj.GetType());
		FillObject(record, obj, fieldsOrProperties);
	}

	private static void FillObject(IDataRecord record, object obj, FieldMap[] fieldsOrProperties)
	{
		foreach (var fieldOrProperty in fieldsOrProperties)
		{
			object value = fieldOrProperty.GetValue(record);
			fieldOrProperty.Member.SetValue(obj, value);
		}
	}

	public static IEnumerable<T> AsEnumerable<T>(this IDataReader reader)
		where T : new()
	{
		var fieldsOrProperties = GetFieldsOrProperties(typeof(T));
		while (reader.Read())
		{
			var obj = new T();
			FillObject (reader, obj, fieldsOrProperties);
			yield return obj;
		}
	}


	private static FieldMap[] GetFieldsOrProperties(Type t)
	{
		if (!maps.TryGetValue(t, out var fieldsOrProperties)) {
			fieldsOrProperties = t
				.FindMembers(
					MemberTypes.Field | MemberTypes.Property,
					BindingFlags.Public | BindingFlags.NonPublic,
					null, null)
				.Select(m => new FieldMap(new Reflection.PropertyOrFieldInfo(m)))
				.Where(m => m.FieldAttribute != null)
				.ToArray();
			maps.Add(t, fieldsOrProperties);
		}

		return fieldsOrProperties;
	}
}
