using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utils.Objects;

namespace Utils.Reflection;

/// <summary>
/// Provides extension methods for various <see cref="Type"/> reflection operations.
/// </summary>
public static class TypeEx
{
    /// <summary>
    /// Maps each numeric type to the set of numeric types it can implicitly receive, matching
    /// the C# implicit numeric conversion table (§10.2.3 of the C# spec).
    /// </summary>
    private static readonly Dictionary<Type, HashSet<Type>> ImplicitNumericConversions = new()
    {
        [typeof(short)]   = [typeof(sbyte), typeof(byte)],
        [typeof(ushort)]  = [typeof(byte)],
        [typeof(int)]     = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(char)],
        [typeof(uint)]    = [typeof(byte), typeof(ushort), typeof(char)],
        [typeof(long)]    = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(char)],
        [typeof(ulong)]   = [typeof(byte), typeof(ushort), typeof(uint), typeof(char)],
        [typeof(float)]   = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char)],
        [typeof(double)]  = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(char)],
        [typeof(decimal)] = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char)],
    };

    /// <summary>
    /// Checks if <paramref name="toAssign"/> can be implicitly assigned to a variable of type
    /// <paramref name="toBeAssigned"/>.
    /// </summary>
    /// <remarks>
    /// For numeric types, only the implicit numeric conversions defined by the C# specification are
    /// accepted. <c>uint → int</c> or <c>long → int</c> are therefore rejected even though they share
    /// the same storage size.  Use <see cref="Type.IsAssignableFrom"/> when CLR assignability — rather
    /// than C# implicit conversion — is the intended check.
    /// </remarks>
    /// <param name="toBeAssigned">Target type of the assignment.</param>
    /// <param name="toAssign">Source type of the assignment.</param>
    /// <returns>
    /// <see langword="true"/> if a C# implicit conversion exists from <paramref name="toAssign"/> to
    /// <paramref name="toBeAssigned"/>, or if the CLR considers <paramref name="toAssign"/> directly
    /// assignable to <paramref name="toBeAssigned"/>.
    /// </returns>
    public static bool IsAssignableFromEx(this Type toBeAssigned, Type toAssign)
    {
        if (toBeAssigned == toAssign) return true;

        if (ImplicitNumericConversions.TryGetValue(toBeAssigned, out HashSet<Type>? sources))
        {
            return sources.Contains(toAssign);
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
    /// <returns>
    /// A <see cref="PropertyOrFieldInfo"/> representing the found member, or <see langword="null"/> if
    /// no matching member exists.
    /// </returns>
    /// <exception cref="AmbiguousMatchException">
    /// Thrown when the lookup resolves to more than one member (for example, a property and a field
    /// with the same name in different scopes, or a hidden member in a derived class).
    /// </exception>
    public static PropertyOrFieldInfo? GetPropertyOrField(this Type type, string name)
    {
        var candidates = type.GetMember(name)
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .ToList();

        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new AmbiguousMatchException(
                $"'{type.FullName}.{name}' resolves to {candidates.Count} members. " +
                "Provide BindingFlags to narrow the search to the intended member.")
        };
    }

    /// <summary>
    /// Gets a property or field by name from the specified type, using the given binding flags.
    /// </summary>
    /// <param name="type">The type from which to retrieve the property or field.</param>
    /// <param name="name">The name of the property or field.</param>
    /// <param name="bindingFlags">A combination of <see cref="BindingFlags"/> to control the search.</param>
    /// <returns>
    /// A <see cref="PropertyOrFieldInfo"/> representing the found member, or <see langword="null"/> if
    /// no matching member exists.
    /// </returns>
    /// <exception cref="AmbiguousMatchException">
    /// Thrown when the lookup resolves to more than one member under the given <paramref name="bindingFlags"/>.
    /// </exception>
    public static PropertyOrFieldInfo? GetPropertyOrField(this Type type, string name, BindingFlags bindingFlags)
    {
        var candidates = type.GetMember(name, bindingFlags)
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .ToList();

        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new AmbiguousMatchException(
                $"'{type.FullName}.{name}' resolves to {candidates.Count} members under the given BindingFlags. " +
                "Refine the BindingFlags to narrow the search to the intended member.")
        };
    }

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
    /// Determines whether the specified <paramref name="type"/> is derived from, implements, 
    /// or is the same as the <paramref name="baseType"/>. This includes handling for generic 
    /// type definitions (e.g. <code>List&lt;&gt;</code>).
    /// </summary>
    /// <param name="type">The concrete type to check.</param>
    /// <param name="baseType">The base or interface type to check against. May be generic.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="type"/> is or inherits from <paramref name="baseType"/>, 
    /// or implements <paramref name="baseType"/> (if it's an interface), including handling for 
    /// generic type definitions; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if either <paramref name="type"/> or <paramref name="baseType"/> is <c>null</c>.
    /// </exception>
    public static bool IsDefinedBy(this Type type, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(baseType);

        // Quick check: if they're the exact same Type instance, return true
        if (type == baseType) return true;

        // Handle interface logic first
        if (baseType.IsInterface)
        {
            // If the base is a generic interface definition (e.g., IEnumerable<>)
            if (baseType.IsGenericTypeDefinition)
            {
                // Example: if type is List<int>, then
                //   type.GetGenericTypeDefinition() == typeof(List<>)
                //   type.GetInterfaces() might contain IEnumerable<int>, etc.
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == baseType)
                {
                    return true;
                }

                // Check if any of the type’s interfaces is a generic instantiation
                // of the given base interface
                return type.GetInterfaces().Any(i =>
                    i.IsGenericType
                    && i.GetGenericTypeDefinition() == baseType);
            }
            else
            {
                // Non-generic interface: simply check if 'type' implements it
                return type.GetInterfaces().Any(i => i == baseType);
            }
        }

        // If we get here, baseType is a class (not an interface).
        // We'll walk up the inheritance chain of 'type' until we reach null.
        for (var current = type; current != null; current = current.BaseType)
        {
            // If we find the baseType up the chain, it matches
            if (current == baseType)
                return true;

            // If baseType is a generic type definition, see if 'current' is 
            // an instantiation of that base type. 
            // Example: current might be List<int>, baseType might be List<>
            if (current.IsGenericType
                && baseType.IsGenericTypeDefinition
                && current.GetGenericTypeDefinition() == baseType)
            {
                return true;
            }
        }

        // No match found
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
    /// Gets all public static methods named <paramref name="methodName"/> from the closed generic type
    /// obtained by applying <paramref name="genericTypeArguments"/> to <paramref name="type"/>.
    /// </summary>
    /// <param name="type">An open generic type definition (e.g. <c>typeof(List&lt;&gt;)</c>).</param>
    /// <param name="genericTypeArguments">Type arguments to close the generic definition.</param>
    /// <param name="methodName">Name of the static methods to retrieve.</param>
    /// <returns>An array of matching <see cref="MethodInfo"/> instances; empty when none match.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="type"/> is not an open generic type definition, or when the number
    /// of <paramref name="genericTypeArguments"/> does not match the type's arity.
    /// </exception>
    public static MethodInfo[] GetStaticMethods(this Type type, Type[] genericTypeArguments, string methodName)
    {
        ValidateGenericTypeDefinition(type, genericTypeArguments, nameof(type), nameof(genericTypeArguments));
        var t = type.MakeGenericType(genericTypeArguments);
        return t.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(m => m.Name == methodName).ToArray();
    }

    /// <summary>
    /// Gets the public static method named <paramref name="methodName"/> with the exact
    /// <paramref name="argumentTypes"/> from the closed generic type obtained by applying
    /// <paramref name="genericTypeArguments"/> to <paramref name="type"/>.
    /// </summary>
    /// <param name="type">An open generic type definition (e.g. <c>typeof(List&lt;&gt;)</c>).</param>
    /// <param name="genericTypeArguments">Type arguments to close the generic definition.</param>
    /// <param name="methodName">Name of the static method to retrieve.</param>
    /// <param name="argumentTypes">Exact parameter types of the method to retrieve.</param>
    /// <returns>
    /// The matching <see cref="MethodInfo"/>, or <see langword="null"/> when no method with the given
    /// name and parameter types exists.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="type"/> is not an open generic type definition, or when the number
    /// of <paramref name="genericTypeArguments"/> does not match the type's arity.
    /// </exception>
    public static MethodInfo? GetStaticMethod(this Type type, Type[] genericTypeArguments, string methodName, Type[] argumentTypes)
    {
        ValidateGenericTypeDefinition(type, genericTypeArguments, nameof(type), nameof(genericTypeArguments));
        var t = type.MakeGenericType(genericTypeArguments);
        return t.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null, argumentTypes, null);
    }

    /// <summary>
    /// Verifies that <paramref name="type"/> is an open generic type definition and that
    /// <paramref name="genericTypeArguments"/> provides the correct number of type arguments.
    /// </summary>
    private static void ValidateGenericTypeDefinition(Type type, Type[] genericTypeArguments,
        string typeParamName, string argsParamName)
    {
        if (!type.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"'{type.FullName}' is not an open generic type definition. " +
                "Pass an unbound generic type such as typeof(List<>) instead of a closed type like typeof(List<int>).",
                typeParamName);

        int expected = type.GetGenericArguments().Length;
        int actual = genericTypeArguments?.Length ?? 0;
        if (actual != expected)
            throw new ArgumentException(
                $"'{type.FullName}' requires {expected} generic type argument(s), but {actual} were supplied.",
                argsParamName);
    }
}
