using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utils.Objects;

namespace Utils.Reflection;

public class PropertyOrFieldInfo : MemberInfo
{
	public MemberInfo Member { get; }
	public Type Type { get; }
	public override Type DeclaringType => Member.DeclaringType;
	public override Module Module => Member.Module;
	public override bool IsCollectible => Member.IsCollectible;
	public override int MetadataToken => Member.MetadataToken;
    public override Type ReflectedType => Member.ReflectedType;
    public override MemberTypes MemberType => Member.MemberType;
    public override string Name => Member.Name;


    #region Get/Set value
    private Action<object, object> Set { get; }
	private Func<object, object> Get { get; }

    public void SetValue(object o, object value) => Set(o, value);
	public object GetValue(object o) => Get(o);
	#endregion
	#region GetCustomAttributes
	public IEnumerable<Attribute> GetCustomAttributes() => Member.GetCustomAttributes();
	public override Attribute[] GetCustomAttributes(bool inherit) => Attribute.GetCustomAttributes(Member,inherit);
	public Attribute[] GetCustomAttributes(Type attributeType) => Attribute.GetCustomAttributes(Member, attributeType);
	public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit) => Attribute.GetCustomAttributes(Member, attributeType, inherit);
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

	public PropertyOrFieldInfo(MemberInfo member)
	{
		member.ArgMustNotBeNull();
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
		else throw new NotSupportedException($"{nameof(PropertyOrFieldInfo)} ne supporte pas {member.GetType().Name}");
	}

	public override string ToString() => Member.ToString();

    public override bool IsDefined(Type attributeType, bool inherit) => Member.IsDefined(attributeType, inherit);

    public static implicit operator PropertyOrFieldInfo(FieldInfo fieldInfo) => fieldInfo == null ? null : new PropertyOrFieldInfo(fieldInfo);
	public static implicit operator PropertyOrFieldInfo(PropertyInfo propertyInfo) => propertyInfo == null ? null : new PropertyOrFieldInfo(propertyInfo);

	public static implicit operator FieldInfo(PropertyOrFieldInfo propertyOrFieldInfo) => (FieldInfo)propertyOrFieldInfo.Member;
    public static implicit operator PropertyInfo(PropertyOrFieldInfo propertyOrFieldInfo) => (PropertyInfo)propertyOrFieldInfo.Member;
}
