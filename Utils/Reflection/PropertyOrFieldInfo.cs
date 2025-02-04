using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Reflection
{
	/// <summary>
	/// Represents a reflection wrapper around either a <see cref="PropertyInfo"/> or a <see cref="FieldInfo"/>,
	/// providing unified access to both for getting and setting values, as well as attribute retrieval.
	/// </summary>
	public class PropertyOrFieldInfo : MemberInfo
	{
		private readonly Func<object, object> _getter;
		private readonly Action<object, object> _setter;

		/// <summary>
		/// Gets the underlying <see cref="MemberInfo"/> representing either a property or a field.
		/// </summary>
		public MemberInfo Member { get; }

		/// <summary>
		/// Gets the type of the property or field.
		/// </summary>
		public Type Type { get; }

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="PropertyOrFieldInfo"/> class, 
		/// wrapping either a <see cref="PropertyInfo"/> or a <see cref="FieldInfo"/>.
		/// </summary>
		/// <param name="member">The property or field to wrap.</param>
		/// <exception cref="NotSupportedException">
		/// Thrown when <paramref name="member"/> is neither <see cref="PropertyInfo"/> nor <see cref="FieldInfo"/>.
		/// </exception>
		public PropertyOrFieldInfo(MemberInfo member)
		{
			member.Arg().MustNotBeNull(); // Ensures the argument is not null

			Member = member;

			switch (member)
			{
				case PropertyInfo pi:
					Type = pi.PropertyType;
					_getter = CreateGetter(pi);
					_setter = CreateSetter(pi);
					break;

				case FieldInfo fi:
					Type = fi.FieldType;
					_getter = CreateGetter(fi);
					_setter = CreateSetter(fi);
					break;

				default:
					throw new NotSupportedException($"{nameof(PropertyOrFieldInfo)} does not support {member.GetType().Name}.");
			}
		}

		#endregion

		#region Public API - Get/Set Value

		/// <summary>
		/// Gets the value of the wrapped property or field for the specified instance.
		/// </summary>
		/// <param name="instance">The object whose member value will be returned.</param>
		/// <returns>The value of the property or field.</returns>
		public object GetValue(object instance) => _getter(instance);

		/// <summary>
		/// Sets the value of the wrapped property or field for the specified instance.
		/// </summary>
		/// <param name="instance">The object whose member value will be modified.</param>
		/// <param name="value">The new value to assign to the property or field.</param>
		/// <exception cref="InvalidOperationException">
		/// Thrown when trying to set the value of a read-only property or field.
		/// </exception>
		public void SetValue(object instance, object value) => _setter(instance, value);

		#endregion

		#region Convenience Properties

		/// <summary>
		/// Gets a value indicating whether this member is a property (<see cref="PropertyInfo"/>).
		/// </summary>
		public bool IsProperty => Member is PropertyInfo;

		/// <summary>
		/// Gets a value indicating whether this member is a field (<see cref="FieldInfo"/>).
		/// </summary>
		public bool IsField => Member is FieldInfo;

		/// <summary>
		/// Gets a value indicating whether this member is static.
		/// </summary>
		public bool IsStatic
		{
			get {
				return Member switch
				{
					PropertyInfo pi => pi.GetAccessors(true).FirstOrDefault()?.IsStatic ?? false,
					FieldInfo fi => fi.IsStatic,
					_ => false
				};
			}
		}

		/// <summary>
		/// Gets a value indicating whether this member is read-only 
		/// (i.e., has no setter or is an init-only/const field).
		/// </summary>
		public bool IsReadOnly
		{
			get {
				return Member switch
				{
					PropertyInfo pi => !pi.CanWrite,
					FieldInfo fi => fi.IsInitOnly || fi.IsLiteral,
					_ => throw new NotSupportedException()
				};
			}
		}

		/// <summary>
		/// Gets a value indicating whether this member can be read (always true for fields, or <see cref="PropertyInfo.CanRead"/> for properties).
		/// </summary>
		public bool CanRead
		{
			get {
				return Member switch
				{
					PropertyInfo pi => pi.CanRead,
					FieldInfo => true,
					_ => false
				};
			}
		}

		/// <summary>
		/// Gets a value indicating whether this member can be written to (false for read-only fields or write-restricted properties).
		/// </summary>
		public bool CanWrite
		{
			get {
				return Member switch
				{
					PropertyInfo pi => pi.CanWrite,
					FieldInfo fi => !fi.IsInitOnly && !fi.IsLiteral,
					_ => false
				};
			}
		}

		#endregion

		#region Attribute Access

		/// <inheritdoc />
		public override bool IsDefined(Type attributeType, bool inherit)
			=> Member.IsDefined(attributeType, inherit);

		/// <summary>
		/// Retrieves all custom attributes on this member.
		/// </summary>
		/// <returns>An enumeration of attributes.</returns>
		public IEnumerable<Attribute> GetCustomAttributes()
			=> Member.GetCustomAttributes();

		/// <summary>
		/// Retrieves all custom attributes of the specified type.
		/// </summary>
		/// <param name="attributeType">The attribute type to find.</param>
		/// <returns>An array of <see cref="Attribute"/> instances.</returns>
		public Attribute[] GetCustomAttributes(Type attributeType)
			=> Attribute.GetCustomAttributes(Member, attributeType);

		/// <summary>
		/// Retrieves the custom attribute of the specified type on this member.
		/// Returns <c>null</c> if not found.
		/// </summary>
		/// <param name="type">The attribute type to find.</param>
		/// <returns>A matching <see cref="Attribute"/> or <c>null</c>.</returns>
		public Attribute GetCustomAttribute(Type type)
			=> Attribute.GetCustomAttribute(Member, type);

		/// <summary>
		/// Retrieves all custom attributes of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The attribute type to find.</typeparam>
		/// <returns>An enumeration of attributes of type <typeparamref name="T"/>.</returns>
		public IEnumerable<T> GetCustomAttributes<T>() where T : Attribute
			=> GetCustomAttributes(typeof(T)).Cast<T>();

		/// <summary>
		/// Retrieves the first custom attribute of type <typeparamref name="T"/> or <c>null</c> if not found.
		/// </summary>
		/// <typeparam name="T">The attribute type to find.</typeparam>
		/// <returns>A matching attribute of type <typeparamref name="T"/> or <c>null</c>.</returns>
		public T GetCustomAttribute<T>() where T : Attribute
			=> (T)GetCustomAttribute(typeof(T));

		/// <inheritdoc />
		public override Attribute[] GetCustomAttributes(bool inherit)
			=> Attribute.GetCustomAttributes(Member, inherit);

		/// <inheritdoc />
		public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit)
			=> Attribute.GetCustomAttributes(Member, attributeType, inherit);

		/// <summary>
		/// Retrieves the first custom attribute of type <typeparamref name="T"/> or <c>null</c> if not found.
		/// </summary>
		/// <typeparam name="T">The attribute type to find.</typeparam>
		/// <param name="inherit">Whether to search the inheritance chain.</param>
		/// <returns>A matching attribute of type <typeparamref name="T"/> or <c>null</c>.</returns>
		public T GetCustomAttribute<T>(bool inherit) where T : Attribute
			=> (T)Attribute.GetCustomAttribute(Member, typeof(T), inherit);

		#endregion

		#region MemberInfo Overrides

		/// <inheritdoc />
		public override Type DeclaringType => Member.DeclaringType!;

		/// <inheritdoc />
		public override Module Module => Member.Module;

		/// <inheritdoc />
		public override bool IsCollectible => Member.IsCollectible;

		/// <inheritdoc />
		public override int MetadataToken => Member.MetadataToken;

		/// <inheritdoc />
		public override Type ReflectedType => Member.ReflectedType!;

		/// <inheritdoc />
		public override MemberTypes MemberType => Member.MemberType;

		/// <inheritdoc />
		public override string Name => Member.Name;

		/// <inheritdoc />
		public override IList<CustomAttributeData> GetCustomAttributesData()
			=> Member.GetCustomAttributesData();

		/// <inheritdoc />
		public override string ToString() => Member.ToString() ?? base.ToString()!;

		#endregion

		#region Conversion Operators

		/// <summary>
		/// Implicit conversion from <see cref="FieldInfo"/> to <see cref="PropertyOrFieldInfo"/>.
		/// </summary>
		/// <param name="fieldInfo">The field to wrap.</param>
		/// <returns>A new <see cref="PropertyOrFieldInfo"/> instance wrapping the field.</returns>
		public static implicit operator PropertyOrFieldInfo(FieldInfo fieldInfo)
			=> fieldInfo == null ? null : new PropertyOrFieldInfo(fieldInfo);

		/// <summary>
		/// Implicit conversion from <see cref="PropertyInfo"/> to <see cref="PropertyOrFieldInfo"/>.
		/// </summary>
		/// <param name="propertyInfo">The property to wrap.</param>
		/// <returns>A new <see cref="PropertyOrFieldInfo"/> instance wrapping the property.</returns>
		public static implicit operator PropertyOrFieldInfo(PropertyInfo propertyInfo)
			=> propertyInfo == null ? null : new PropertyOrFieldInfo(propertyInfo);

		/// <summary>
		/// Implicit conversion from <see cref="PropertyOrFieldInfo"/> to <see cref="FieldInfo"/>.
		/// </summary>
		/// <param name="propertyOrFieldInfo">The wrapper to unwrap.</param>
		/// <returns>The underlying <see cref="FieldInfo"/>, or <c>null</c> if the wrapper holds a property.</returns>
		public static implicit operator FieldInfo(PropertyOrFieldInfo propertyOrFieldInfo)
			=> propertyOrFieldInfo?.Member as FieldInfo;

		/// <summary>
		/// Implicit conversion from <see cref="PropertyOrFieldInfo"/> to <see cref="PropertyInfo"/>.
		/// </summary>
		/// <param name="propertyOrFieldInfo">The wrapper to unwrap.</param>
		/// <returns>The underlying <see cref="PropertyInfo"/>, or <c>null</c> if the wrapper holds a field.</returns>
		public static implicit operator PropertyInfo(PropertyOrFieldInfo propertyOrFieldInfo)
			=> propertyOrFieldInfo?.Member as PropertyInfo;

		#endregion

		#region Private Helpers: Expression-based Getters/Setters

		/// <summary>
		/// Creates a typed getter function via expression trees for faster access.
		/// </summary>
		private static Func<object, object> CreateGetter(PropertyInfo propertyInfo)
		{
			if (!propertyInfo.CanRead)
			{
				return _ => throw new InvalidOperationException($"Property '{propertyInfo.Name}' is not readable.");
			}

			var instanceParam = Expression.Parameter(typeof(object), "instance");
			var castedInstance = Expression.Convert(instanceParam, propertyInfo.DeclaringType!);
			var propertyAccess = Expression.Property(castedInstance, propertyInfo);
			var convertToObject = Expression.Convert(propertyAccess, typeof(object));
			return Expression.Lambda<Func<object, object>>(convertToObject, instanceParam).Compile();
		}

		/// <summary>
		/// Creates a typed getter function for the given field via expression trees for faster access.
		/// </summary>
		private static Func<object, object> CreateGetter(FieldInfo fieldInfo)
		{
			var instanceParam = Expression.Parameter(typeof(object), "instance");
			var castedInstance = Expression.Convert(instanceParam, fieldInfo.DeclaringType!);
			var fieldAccess = Expression.Field(castedInstance, fieldInfo);
			var convertToObject = Expression.Convert(fieldAccess, typeof(object));
			return Expression.Lambda<Func<object, object>>(convertToObject, instanceParam).Compile();
		}

		/// <summary>
		/// Creates a typed setter function via expression trees for faster access.
		/// If the property is read-only, the returned delegate will throw an <see cref="InvalidOperationException"/>.
		/// </summary>
		private static Action<object, object> CreateSetter(PropertyInfo propertyInfo)
		{
			if (!propertyInfo.CanWrite)
			{
				return (_, _)
					=> throw new InvalidOperationException($"Property '{propertyInfo.Name}' is read-only and cannot be set.");
			}

			var instanceParam = Expression.Parameter(typeof(object), "instance");
			var valueParam = Expression.Parameter(typeof(object), "value");

			var castedInstance = Expression.Convert(instanceParam, propertyInfo.DeclaringType!);
			var castedValue = Expression.Convert(valueParam, propertyInfo.PropertyType);

			var propertyAccess = Expression.Property(castedInstance, propertyInfo);
			var assign = Expression.Assign(propertyAccess, castedValue);
			var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);

			return lambda.Compile();
		}

		/// <summary>
		/// Creates a typed setter function for the given field via expression trees for faster access.
		/// If the field is init-only or const, the returned delegate will throw an <see cref="InvalidOperationException"/>.
		/// </summary>
		private static Action<object, object> CreateSetter(FieldInfo fieldInfo)
		{
			if (fieldInfo.IsInitOnly || fieldInfo.IsLiteral)
			{
				return (_, _)
					=> throw new InvalidOperationException($"Field '{fieldInfo.Name}' is read-only and cannot be set.");
			}

			var instanceParam = Expression.Parameter(typeof(object), "instance");
			var valueParam = Expression.Parameter(typeof(object), "value");

			var castedInstance = Expression.Convert(instanceParam, fieldInfo.DeclaringType!);
			var castedValue = Expression.Convert(valueParam, fieldInfo.FieldType);

			var fieldAccess = Expression.Field(castedInstance, fieldInfo);
			var assign = Expression.Assign(fieldAccess, castedValue);
			var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);

			return lambda.Compile();
		}

		#endregion
	}
}
