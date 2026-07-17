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
    /// Retrieves the interfaces that are directly implemented by the type, excluding those inherited from base types
    /// or from other directly implemented interfaces.
    /// </summary>
    /// <remarks>
    /// For class types the method subtracts the interfaces already exposed by the base class.
    /// For interface types it subtracts the interfaces that are transitively inherited through any parent interface,
    /// retaining only the interfaces declared directly on <paramref name="type"/> itself.
    /// </remarks>
    /// <param name="type">The type to check for directly implemented interfaces.</param>
    /// <returns>An enumerable of directly implemented interfaces, in the order returned by reflection.</returns>
    public static IEnumerable<Type> GetDirectInterfaces(this Type type)
    {
        if (!type.IsInterface)
        {
            return type.BaseType == null
                ? []
                : type.GetInterfaces().Except(type.BaseType.GetInterfaces());
        }

        // For interface types BaseType is always null. Subtract the interfaces that are already
        // exposed transitively through any of the immediate parent interfaces, so only the direct
        // parents remain.
        Type[] allInterfaces = type.GetInterfaces();
        var transitivelyInherited = new HashSet<Type>(
            allInterfaces.SelectMany(i => i.GetInterfaces()));
        return allInterfaces.Except(transitivelyInherited);
    }

    /// <summary>
    /// Retrieves the type hierarchy starting from the given type, along with the directly implemented interfaces at each level.
    /// </summary>
    /// <param name="type">The starting type for the hierarchy traversal.</param>
    /// <returns>An enumerable of tuples containing the <c ref="Type" /> and its directly implemented interfaces.</returns>
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

        foreach (var type in assemblies.SelectMany(a => a.GetTypes(filter)))
        {
            yield return type;
        }
    }

    /// <summary>
    /// Gets types from <paramref name="assembly"/> matching <paramref name="filter"/>.
    /// </summary>
    /// <param name="assembly">Assembly to enumerate types from.</param>
    /// <param name="filter">Predicate applied to each type.</param>
    /// <param name="loadErrors">
    /// When not <see langword="null"/>, type-load failures are collected here instead of thrown,
    /// and any successfully loadable types are still returned (tolerant mode).
    /// When <see langword="null"/> (default), a <see cref="ReflectionTypeLoadException"/> from the
    /// underlying <see cref="Assembly.GetTypes"/> call propagates to the caller (strict mode).
    /// </param>
    /// <returns>Types from <paramref name="assembly"/> that satisfy <paramref name="filter"/>.</returns>
    public static IEnumerable<Type> GetTypes(this Assembly assembly, Func<Type, bool> filter,
        ICollection<Exception>? loadErrors = null)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) when (loadErrors is not null)
        {
            foreach (Exception? loaderEx in ex.LoaderExceptions)
            {
                if (loaderEx is not null) loadErrors.Add(loaderEx);
            }
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        foreach (var type in types.Where(filter))
        {
            yield return type;
        }
    }

    /// <summary>
    /// Gets types from each assembly in <paramref name="assemblies"/> matching <paramref name="filter"/>.
    /// </summary>
    /// <param name="assemblies">Assemblies to enumerate types from.</param>
    /// <param name="filter">Predicate applied to each type.</param>
    /// <param name="loadErrors">
    /// When not <see langword="null"/>, per-assembly type-load failures are collected here and
    /// the successfully loadable types from each assembly are still returned (tolerant mode).
    /// When <see langword="null"/> (default), any <see cref="ReflectionTypeLoadException"/> propagates
    /// immediately (strict mode).
    /// </param>
    /// <returns>Types from all assemblies that satisfy <paramref name="filter"/>.</returns>
    public static IEnumerable<Type> GetTypes(this IEnumerable<Assembly> assemblies, Func<Type, bool> filter,
        ICollection<Exception>? loadErrors = null)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes(filter, loadErrors))
            {
                yield return type;
            }
        }
    }

    /// <summary>
    /// Loads all assemblies located in the specified directory.
    /// </summary>
    /// <param name="path">The path that contains the assemblies to load.</param>
    /// <param name="raiseError">True to rethrow load exceptions; false to ignore invalid assemblies.</param>
    /// <returns>A sequence of assemblies loaded from the directory.</returns>
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
