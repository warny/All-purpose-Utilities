using System.Reflection;
using Utils.Files;

namespace Utils.Reflection;

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
		=> member switch
		{
			PropertyInfo property => property.PropertyType,
			FieldInfo field => field.FieldType,
			MethodInfo method => method.ReturnType,
			ConstructorInfo => typeof(void), // Constructor doesn't have a return type
			EventInfo eventInfo => eventInfo.EventHandlerType,
			TypeInfo typeInfo => typeInfo.AsType(),
			_ => throw new NotSupportedException($"Member type '{member.GetType().Name}' is not supported for retrieving type information.")
		};

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

	/// <summary>
	/// Get types given the specified predicate
	/// </summary>
	/// <param name="filter">Filter to apply to types</param>
	/// <returns><see cref="IEnumerable{Type}"/> of <see cref="Type"/> that match the given <paramref name="filter"/></returns>
	public static IEnumerable<Type> GetTypes(Func<Type, bool> filter)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

		foreach (var type in assemblies.SelectMany(a=>a.GetTypes(filter)))
		{
			yield return type;
		}
	}

	/// <summary>
	/// Get types given the specified predicate
	/// </summary>
	/// <param name="assembly">Assembly to load types from</param>
	/// <param name="filter">Filter to apply to types</param>
	/// <returns><see cref="IEnumerable{Type}"/> of <see cref="Type"/> that match the given <paramref name="filter"/></returns>
	public static IEnumerable<Type> GetTypes(this Assembly assembly, Func<Type, bool> filter)
	{
		Type[] types = assembly.GetTypes();

		foreach (var type in types.Where(filter))
		{
			yield return type;
		}
	}

	/// <summary>
	/// Get types given the specified predicate
	/// </summary>
	/// <param name="assemblies">Assemblies to load types from</param>
	/// <param name="filter">Filter to apply to types</param>
	/// <returns><see cref="IEnumerable{Type}"/> of <see cref="Type"/> that match the given <paramref name="filter"/></returns>
	public static IEnumerable<Type> GetTypes(this IEnumerable<Assembly> assemblies, Func<Type, bool> filter)
	{
		foreach (var assembly in assemblies)
		{
			foreach (var type in assembly.GetTypes(filter))
			{
				yield return type;
			}
		}
	}

	public static IEnumerable<Assembly> LoadAssemblies(string path, bool raiseError = false)
	{
		foreach (var file in PathUtils.EnumerateFiles(path))
		{
			Assembly assembly;
			try
			{
				assembly = Assembly.Load(file);
			}
			catch
			{
				if (raiseError) throw;
				continue;
			}
			yield return assembly;
		}
	}
}
