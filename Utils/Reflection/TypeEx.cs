using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Utils.Objects;

namespace Utils.Reflection;

/// <summary>
/// Provides extension methods for various <see cref="Type"/> reflection operations.
/// </summary>
public static class TypeEx
{
	/// <summary>
	/// Checks if <paramref name="toBeAssigned"/> can be assigned from <paramref name="toAssign"/>.
	/// For numeric types, this also checks size compatibility when both types are numbers.
	/// </summary>
	/// <param name="toBeAssigned">The type to be assigned to.</param>
	/// <param name="toAssign">The type to be assigned.</param>
	/// <returns>True if <paramref name="toAssign"/> can be assigned to <paramref name="toBeAssigned"/>; otherwise, false.</returns>
	public static bool IsAssignableFromEx(this Type toBeAssigned, Type toAssign)
	{
		if (toBeAssigned.In(Types.Number) && toAssign.In(Types.Number))
		{
			// If it's a floating point number, allow assignment.
			if (toBeAssigned.In(Types.FloatingPointNumber)) return true;

			// For other numbers, compare their sizes using Marshal.
			return Marshal.SizeOf(toBeAssigned) >= Marshal.SizeOf(toAssign);
		}

		return toBeAssigned.IsAssignableFrom(toAssign);
	}

	/// <summary>
	/// Gets all properties and fields of the specified type.
	/// </summary>
	/// <param name="type">The type whose properties and fields to retrieve.</param>
	/// <returns>An array of <see cref="PropertyOrFieldInfo"/> representing all properties and fields of the type.</returns>
	public static PropertyOrFieldInfo[] GetPropertiesOrFields(this Type type)
		=> type.GetMembers()
			.Where(m => m is PropertyInfo || m is FieldInfo)
			.Select(m => new PropertyOrFieldInfo(m))
			.ToArray();

	/// <summary>
	/// Gets all properties and fields of the specified type using the specified binding flags.
	/// </summary>
	/// <param name="type">The type whose properties and fields to retrieve.</param>
	/// <param name="bindingFlags">A combination of <see cref="BindingFlags"/> to control the search.</param>
	/// <returns>An array of <see cref="PropertyOrFieldInfo"/> representing all properties and fields of the type.</returns>
	public static PropertyOrFieldInfo[] GetPropertiesOrFields(this Type type, BindingFlags bindingFlags)
		=> type.GetMembers(bindingFlags)
			.Where(m => m is PropertyInfo || m is FieldInfo)
			.Select(m => new PropertyOrFieldInfo(m))
			.ToArray();

	/// <summary>
	/// Gets a property or field by name from the specified type.
	/// </summary>
	/// <param name="type">The type from which to retrieve the property or field.</param>
	/// <param name="name">The name of the property or field.</param>
	/// <returns>A <see cref="PropertyOrFieldInfo"/> representing the found member, or null if not found.</returns>
	public static PropertyOrFieldInfo GetPropertyOrField(this Type type, string name)
		=> type.GetMember(name)
			.Where(m => m is PropertyInfo || m is FieldInfo)
			.Select(m => new PropertyOrFieldInfo(m))
			.FirstOrDefault();

	/// <summary>
	/// Gets a property or field by name from the specified type, using the given binding flags.
	/// </summary>
	/// <param name="type">The type from which to retrieve the property or field.</param>
	/// <param name="name">The name of the property or field.</param>
	/// <param name="bindingFlags">A combination of <see cref="BindingFlags"/> to control the search.</param>
	/// <returns>A <see cref="PropertyOrFieldInfo"/> representing the found member, or null if not found.</returns>
	public static PropertyOrFieldInfo GetPropertyOrField(this Type type, string name, BindingFlags bindingFlags)
		=> type.GetMember(name, bindingFlags)
			.Where(m => m is PropertyInfo || m is FieldInfo)
			.Select(m => new PropertyOrFieldInfo(m))
			.FirstOrDefault();

	/// <summary>
	/// Gets the underlying type for the given type. 
	/// If the type is an enum, it returns the enum's underlying type. 
	/// If the type is nullable, it returns the underlying non-nullable type.
	/// </summary>
	/// <param name="type">The type whose underlying type to retrieve.</param>
	/// <returns>The underlying type, or the original type if it is not an enum or nullable.</returns>
	public static Type GetUnderlyingType(this Type type)
	{
		if (type.IsEnum) return type.GetEnumUnderlyingType();
		if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition())
			return type.GetGenericArguments()[0];

		return type;
	}

	/// <summary>
	/// Checks if a given type is defined by a specific base type, including support for generic types.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <param name="baseType">The base type to check against.</param>
	/// <returns>True if <paramref name="type"/> is derived from or implements <paramref name="baseType"/>; otherwise, false.</returns>
	public static bool IsDefinedBy(this Type type, Type baseType)
	{
		if (type == baseType) return true;

		if (baseType.IsInterface)
		{
			if (baseType.IsGenericTypeDefinition)
			{
				return (type.IsGenericType && type.GetGenericTypeDefinition() == baseType)
					   || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == baseType);
			}

			return baseType.GetInterfaces().Any(i => i == baseType);
		}

		for (var t = type.BaseType; t is not null; t = t.BaseType)
		{
			if (t == baseType) return true;
			if (t.IsGenericType && baseType.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == baseType) return true;
		}

		return false;
	}

	/// <summary>
	/// Returns the generic type arguments for the specified <paramref name="baseType"/> that the current type is derived from.
	/// </summary>
	/// <param name="type">The type being checked.</param>
	/// <param name="baseType">The generic base type definition.</param>
	/// <returns>An array of generic type arguments if found; otherwise, null.</returns>
	public static Type[] GetGenericTypeDefinitionFor(this Type type, Type baseType)
	{
		if (!baseType.IsGenericTypeDefinition) return null;

		if (baseType.IsInterface)
		{
			var @interface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == baseType);
			return @interface?.GetGenericArguments();
		}

		for (var t = type; t is not null; t = t.BaseType)
		{
			if (t.IsGenericType && t.GetGenericTypeDefinition() == baseType) return t.GetGenericArguments();
		}

		return null;
	}

	/// <summary>
	/// Gets all static methods from a generic type, based on the specified generic type arguments and method name.
	/// </summary>
	/// <param name="type">The generic type.</param>
	/// <param name="genericTypeArguments">The generic type arguments to apply.</param>
	/// <param name="methodName">The name of the method to retrieve.</param>
	/// <returns>An array of <see cref="MethodInfo"/> representing the static methods found.</returns>
	/// <exception cref="ArgumentException">Thrown if the type is not generic.</exception>
	public static MethodInfo[] GetStaticMethods(this Type type, Type[] genericTypeArguments, string methodName)
	{
		if (!type.IsGenericType)
			throw new ArgumentException($"{type.FullName} is not a generic type", nameof(type));

		var t = type.MakeGenericType(genericTypeArguments);
		return t.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.Where(m => m.Name == methodName).ToArray();
	}

	/// <summary>
	/// Gets a specific static method from a generic type, based on the specified method name and argument types.
	/// </summary>
	/// <param name="type">The generic type.</param>
	/// <param name="genericTypeArguments">The generic type arguments to apply.</param>
	/// <param name="methodName">The name of the method to retrieve.</param>
	/// <param name="argumentTypes">The argument types for the method.</param>
	/// <returns>The <see cref="MethodInfo"/> representing the method, or null if not found.</returns>
	/// <exception cref="ArgumentException">Thrown if the type is not generic.</exception>
	public static MethodInfo GetStaticMethod(this Type type, Type[] genericTypeArguments, string methodName, Type[] argumentTypes)
	{
		if (!type.IsGenericType)
			throw new ArgumentException($"{type.FullName} is not a generic type", nameof(type));

		var t = type.MakeGenericType(genericTypeArguments);
		return t.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null, argumentTypes, null);
	}
}
