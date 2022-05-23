using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utils.Objects;

namespace Utils.Reflection;

public class FieldOrPropertyInfo
{
	public MemberInfo Member { get; }
	public Type Type { get; }
	#region Get/Set value
	private Action<object, object> Set { get; }
	private Func<object, object> Get { get; }

	public void SetValue(object o, object value) => Set(o, value);
	public object GetValue(object o) => Get(o);
	#endregion
	#region GetCustomAttributes
	public IEnumerable<Attribute> GetCustomAttributes() => Member.GetCustomAttributes();
	public Attribute[] GetCustomAttributes(bool inherit) => Attribute.GetCustomAttributes(Member,inherit);
	public Attribute[] GetCustomAttributes(Type attributeType) => Attribute.GetCustomAttributes(Member, attributeType);
	public Attribute[] GetCustomAttributes(Type attributeType, bool inherit) => Attribute.GetCustomAttributes(Member, attributeType, inherit);
	public IEnumerable<T> GetCustomAttributes<T>() where T : Attribute
		=> GetCustomAttributes(typeof(T)).Cast<T>();
	public IEnumerable<T> GetCustomAttributes<T>(bool inherit) where T : Attribute
		=>  GetCustomAttributes(typeof(T), inherit).Cast<T>();
	#endregion
	#region GetCustomAttribute
	public Attribute GetCustomAttribute(Type type) 
		=> Attribute.GetCustomAttribute(Member, type);
	public Attribute GetCustomAttribute(Type type, bool inherit) 
		=> Attribute.GetCustomAttribute(Member, type, inherit);
	public T GetCustomAttribute<T>() where T : Attribute
		=> (T)GetCustomAttribute(typeof(T));
	public T GetCustomAttribute<T>(bool inherit) where T : Attribute
		=> (T)GetCustomAttribute(typeof(T), inherit);
	#endregion

	public FieldOrPropertyInfo(MemberInfo Member)
	{
		Member.ArgMustNotBeNull();
		this.Member = Member;
		if (Member is PropertyInfo pi)
		{
			this.Type = pi.PropertyType;
			this.Set = pi.SetValue;
			this.Get = pi.GetValue;
		}
		else if (Member is FieldInfo fi)
		{
			this.Type = fi.FieldType;
			this.Set = fi.SetValue;
			this.Get = fi.GetValue;
		}
		else throw new NotSupportedException($"{nameof(FieldOrPropertyInfo)} ne supporte pas {Member.GetType().Name}");
	}

	public override string ToString() => Member.ToString();

	public static implicit operator FieldOrPropertyInfo(MemberInfo memberInfo) => memberInfo == null ? null : new FieldOrPropertyInfo(memberInfo);
	public static implicit operator FieldOrPropertyInfo(FieldInfo fieldInfo) => fieldInfo == null ? null : new FieldOrPropertyInfo(fieldInfo);
	public static implicit operator FieldOrPropertyInfo(PropertyInfo propertyInfo) => propertyInfo == null ? null : new FieldOrPropertyInfo(propertyInfo);

}
