using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utils.Objects;

namespace Utils.Reflection;

/// <summary>
/// Represents a wrapper for either a property or a field in a class, allowing unified access to both.
/// </summary>
public class PropertyOrFieldInfo : MemberInfo
{
	/// <summary>
	/// The underlying property or field that this instance represents.
	/// </summary>
	public MemberInfo Member { get; }

	/// <summary>
	/// Gets the type of the property or field.
	/// </summary>
	public Type Type { get; }

	public override Type DeclaringType => Member.DeclaringType;
	public override Module Module => Member.Module;
	public override bool IsCollectible => Member.IsCollectible;
	public override int MetadataToken => Member.MetadataToken;
	public override Type ReflectedType => Member.ReflectedType;
	public override MemberTypes MemberType => Member.MemberType;
	public override string Name => Member.Name;

	#region Get/Set Value

	// Delegates for accessing the value of the property or field.
	private Action<object, object> Set { get; }
	private Func<object, object> Get { get; }

	/// <summary>
	/// Sets the value of the property or field.
	/// </summary>
	/// <param name="instance">The object that owns the property or field.</param>
	/// <param name="value">The value to set.</param>
	public void SetValue(object instance, object value) => Set(instance, value);

	/// <summary>
	/// Gets the value of the property or field.
	/// </summary>
	/// <param name="instance">The object that owns the property or field.</param>
	/// <returns>The value of the property or field.</returns>
	public object GetValue(object instance) => Get(instance);

	#endregion

	#region Custom Attributes Access

	/// <summary>
	/// Retrieves all custom attributes on the member.
	/// </summary>
	/// <returns>An enumeration of attributes.</returns>
	public IEnumerable<Attribute> GetCustomAttributes() => Member.GetCustomAttributes();

	public override Attribute[] GetCustomAttributes(bool inherit) => Attribute.GetCustomAttributes(Member, inherit);

	/// <summary>
	/// Retrieves all custom attributes of the specified type.
	/// </summary>
	/// <param name="attributeType">The type of attribute to retrieve.</param>
	/// <returns>An array of attributes.</returns>
	public Attribute[] GetCustomAttributes(Type attributeType) => Attribute.GetCustomAttributes(Member, attributeType);

	public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit) => Attribute.GetCustomAttributes(Member, attributeType, inherit);

	/// <summary>
	/// Retrieves custom attributes of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of the custom attribute.</typeparam>
	/// <returns>An enumeration of attributes.</returns>
	public IEnumerable<T> GetCustomAttributes<T>() where T : Attribute => GetCustomAttributes(typeof(T)).Cast<T>();

	public IEnumerable<T> GetCustomAttributes<T>(bool inherit) where T : Attribute => GetCustomAttributes(typeof(T), inherit).Cast<T>();

	#endregion

	#region Single Custom Attribute Access

	/// <summary>
	/// Retrieves the custom attribute of the specified type on the member.
	/// </summary>
	/// <param name="type">The type of the attribute to retrieve.</param>
	/// <returns>The attribute if found; otherwise, null.</returns>
	public Attribute GetCustomAttribute(Type type) => Attribute.GetCustomAttribute(Member, type);

	public Attribute GetCustomAttribute(Type type, bool inherit) => Attribute.GetCustomAttribute(Member, type, inherit);

	/// <summary>
	/// Retrieves the custom attribute of the specified type on the member.
	/// </summary>
	/// <typeparam name="T">The type of the custom attribute.</typeparam>
	/// <returns>The attribute if found; otherwise, null.</returns>
	public T GetCustomAttribute<T>() where T : Attribute => (T)GetCustomAttribute(typeof(T));

	public T GetCustomAttribute<T>(bool inherit) where T : Attribute => (T)GetCustomAttribute(typeof(T), inherit);

	#endregion

	#region Constructor

	/// <summary>
	/// Initializes a new instance of the <see cref="PropertyOrFieldInfo"/> class, wrapping either a property or a field.
	/// </summary>
	/// <param name="member">The property or field to wrap.</param>
	public PropertyOrFieldInfo(MemberInfo member)
	{
		member.ArgMustNotBeNull(); // Ensures the argument is not null
		this.Member = member;

		if (member is PropertyInfo pi)
		{
			this.Type = pi.PropertyType;
			this.Set = pi.SetValue;
			this.Get = pi.GetValue;
		}
		else if (member is FieldInfo fi)
		{
			this.Type = fi.FieldType;
			this.Set = fi.SetValue;
			this.Get = fi.GetValue;
		}
		else
		{
			throw new NotSupportedException($"{nameof(PropertyOrFieldInfo)} does not support {member.GetType().Name}");
		}
	}

	#endregion

	#region Miscellaneous

	public override string ToString() => Member.ToString();

	public override bool IsDefined(Type attributeType, bool inherit) => Member.IsDefined(attributeType, inherit);

	#endregion

	#region Type Conversion Operators

	/// <summary>
	/// Implicit conversion from <see cref="FieldInfo"/> to <see cref="PropertyOrFieldInfo"/>.
	/// </summary>
	/// <param name="fieldInfo">The field to wrap.</param>
	public static implicit operator PropertyOrFieldInfo(FieldInfo fieldInfo) => fieldInfo == null ? null : new PropertyOrFieldInfo(fieldInfo);

	/// <summary>
	/// Implicit conversion from <see cref="PropertyInfo"/> to <see cref="PropertyOrFieldInfo"/>.
	/// </summary>
	/// <param name="propertyInfo">The property to wrap.</param>
	public static implicit operator PropertyOrFieldInfo(PropertyInfo propertyInfo) => propertyInfo == null ? null : new PropertyOrFieldInfo(propertyInfo);

	/// <summary>
	/// Implicit conversion from <see cref="PropertyOrFieldInfo"/> to <see cref="FieldInfo"/>.
	/// </summary>
	/// <param name="propertyOrFieldInfo">The wrapped field to retrieve.</param>
	public static implicit operator FieldInfo(PropertyOrFieldInfo propertyOrFieldInfo) => propertyOrFieldInfo.Member as FieldInfo;

	/// <summary>
	/// Implicit conversion from <see cref="PropertyOrFieldInfo"/> to <see cref="PropertyInfo"/>.
	/// </summary>
	/// <param name="propertyOrFieldInfo">The wrapped property to retrieve.</param>
	public static implicit operator PropertyInfo(PropertyOrFieldInfo propertyOrFieldInfo) => propertyOrFieldInfo.Member as PropertyInfo;

	#endregion
}
