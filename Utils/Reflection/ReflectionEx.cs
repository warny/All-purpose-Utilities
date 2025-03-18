﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Utils.Net.Expressions;

/// <summary>
/// Provides extension methods to simplify reflection-based operations.
/// </summary>
public static class ReflectionEx
{
	/// <summary>
	/// Retrieves the type associated with the specified <see cref="MemberInfo"/>. 
	/// This could be a property type, field type, return type for methods, and more.
	/// </summary>
	/// <param name="member">The <see cref="MemberInfo"/> from which to retrieve the type.</param>
	/// <returns>The type associated with the member.</returns>
	/// <exception cref="NotSupportedException">Thrown if the <paramref name="member"/> type is unsupported.</exception>
	public static Type GetTypeOf(this MemberInfo member)
	{
		return member switch
		{
			PropertyInfo property => property.PropertyType,
			FieldInfo field => field.FieldType,
			MethodInfo method => method.ReturnType,
			ConstructorInfo => typeof(void), // Constructor doesn't have a return type
			EventInfo eventInfo => eventInfo.EventHandlerType,
			TypeInfo typeInfo => typeInfo.AsType(),
			_ => throw new NotSupportedException($"Member type '{member.GetType().Name}' is not supported for retrieving type information.")
		};
	}

	/// <summary>
	/// Retrieves the interfaces that are directly implemented by the type, excluding those inherited from base types.
	/// </summary>
	/// <param name="type">The type to check for directly implemented interfaces.</param>
	/// <returns>An enumerable of directly implemented interfaces.</returns>
	public static IEnumerable<Type> GetDirectInterfaces(this Type type)
		=> type.BaseType == null
		? []
		: type.GetInterfaces().Except(type.BaseType.GetInterfaces());

	/// <summary>
	/// Retrieves the type hierarchy starting from the given type, along with the directly implemented interfaces at each level.
	/// </summary>
	/// <param name="type">The starting type for the hierarchy traversal.</param>
	/// <param name="returnObject">If true, includes the object type in the hierarchy.</param>
	/// <returns>An enumerable of tuples containing the type and its directly implemented interfaces.</returns>
	public static IEnumerable<(Type Type, Type[] Interfaces)> GetTypeHierarchy(this Type type)
	{
		for (var t = type; t != null; t = t.BaseType)
		{
			yield return (t, t.GetDirectInterfaces().ToArray());
		}
	}
}
