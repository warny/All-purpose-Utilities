using System;
using System.Collections.Generic;
using System.Reflection;

namespace Utils.Net.Expressions
{
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
	}
}
