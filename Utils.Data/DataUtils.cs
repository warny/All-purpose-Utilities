using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Utils.Data;

/// <summary>
/// Utility class for mapping data from IDataRecord or IDataReader to objects.
/// </summary>
public static class DataUtils
{
	// Cache of Type to FieldMap[] to optimize object filling.
	private static readonly Dictionary<Type, FieldMap[]> maps = new Dictionary<Type, FieldMap[]>();

	/// <summary>
	/// Converts an IDataRecord to an instance of type T.
	/// </summary>
	/// <typeparam name="T">Type to which the record should be converted. Must have a parameterless constructor.</typeparam>
	/// <param name="record">The IDataRecord to convert.</param>
	/// <returns>An object of type T populated with data from the record.</returns>
	public static T ToObject<T>(this IDataRecord record) where T : new()
	{
		T obj = new T();
		record.FillObject(obj);
		return obj;
	}

	/// <summary>
	/// Populates an object's fields or properties from an IDataRecord.
	/// </summary>
	/// <param name="record">The IDataRecord containing the data.</param>
	/// <param name="obj">The object to populate.</param>
	public static void FillObject(this IDataRecord record, object obj)
	{
		var fieldsOrProperties = GetFieldsOrProperties(obj.GetType());
		FillObject(record, obj, fieldsOrProperties);
	}

	/// <summary>
	/// Fills the fields or properties of an object using the values in the IDataRecord.
	/// </summary>
	/// <param name="record">The IDataRecord containing the data.</param>
	/// <param name="obj">The object to populate.</param>
	/// <param name="fieldsOrProperties">The FieldMap[] containing the mappings of fields or properties.</param>
	private static void FillObject(IDataRecord record, object obj, FieldMap[] fieldsOrProperties)
	{
		foreach (var fieldOrProperty in fieldsOrProperties)
		{
			// Attempt to retrieve the value from the IDataRecord
			object value = fieldOrProperty.GetValue(record);

			// Set the value to the respective field/property on the object.
			fieldOrProperty.Member.SetValue(obj, value);
		}
	}

	/// <summary>
	/// Converts the results from an IDataReader into an IEnumerable of type T.
	/// </summary>
	/// <typeparam name="T">Type to which each record should be converted. Must have a parameterless constructor.</typeparam>
	/// <param name="reader">The IDataReader containing the data.</param>
	/// <returns>An IEnumerable of T, where each item is populated with data from a record in the reader.</returns>
	public static IEnumerable<T> AsEnumerable<T>(this IDataReader reader) where T : new()
	{
		// Precompute field and property mappings once per type.
		var fieldsOrProperties = GetFieldsOrProperties(typeof(T));

		// Iterate over each record in the reader and populate an object.
		while (reader.Read())
		{
			var obj = new T();
			FillObject(reader, obj, fieldsOrProperties);
			yield return obj;
		}
	}

	/// <summary>
	/// Retrieves the cached or newly computed field/property mappings for a given type.
	/// </summary>
	/// <param name="t">The Type for which to get field/property mappings.</param>
	/// <returns>An array of FieldMap objects that map the fields and properties of the type.</returns>
	private static FieldMap[] GetFieldsOrProperties(Type t)
	{
		if (!maps.TryGetValue(t, out var fieldsOrProperties))
		{
			// Find all public and non-public fields and properties on the type
			fieldsOrProperties = t
				.FindMembers(MemberTypes.Field | MemberTypes.Property,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
					null, null)
				.Select(m => new FieldMap(new Reflection.PropertyOrFieldInfo(m)))
				.Where(m => m.FieldAttribute != null)  // Filter those with FieldAttribute only
				.ToArray();

			// Cache the computed field mappings for later use
			maps[t] = fieldsOrProperties;
		}

		return fieldsOrProperties;
	}
}
