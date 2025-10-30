using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Provides functionality to resolve types, constructors, methods, and members at runtime,
/// as well as to manage constants in expressions.
/// </summary>
public interface IResolver
{
    /// <summary>
    /// Attempts to resolve a type by its name.
    /// </summary>
    /// <param name="name">The name of the type to resolve, potentially including a namespace.</param>
    /// <returns>
    /// A <see cref="Type"/> object if resolution is successful; otherwise <see langword="null"/>.
    /// </returns>
    Type ResolveType(string name);

    /// <summary>
    /// Attempts to resolve a generic type by its name, using the provided generic parameters.
    /// </summary>
    /// <param name="name">The name of the type to resolve.</param>
    /// <param name="genericParameters">An array of types to use as generic arguments.</param>
    /// <returns>
    /// A constructed <see cref="Type"/> object if resolution is successful; otherwise <see langword="null"/>.
    /// </returns>
    Type ResolveType(string name, Type[] genericParameters);

    /// <summary>
    /// Retrieves all public constructors for the specified type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> whose constructors are retrieved.</param>
    /// <returns>An array of <see cref="ConstructorInfo"/> objects.</returns>
    ConstructorInfo[] GetConstructors(Type type);

    /// <summary>
    /// Retrieves a list of instance methods on the specified type that match the given method name.
    /// </summary>
    /// <param name="type">The target <see cref="Type"/>.</param>
    /// <param name="name">The name of the desired instance methods.</param>
    /// <returns>An array of <see cref="MethodInfo"/> objects representing the matching methods.</returns>
    MethodInfo[] GetInstanceMethods(Type type, string name);

    /// <summary>
    /// Retrieves a list of static methods on the specified type that match the given method name.
    /// </summary>
    /// <param name="type">The target <see cref="Type"/>.</param>
    /// <param name="name">The name of the desired static methods.</param>
    /// <returns>An array of <see cref="MethodInfo"/> objects representing the matching methods.</returns>
    MethodInfo[] GetStaticMethods(Type type, string name);

    /// <summary>
    /// Attempts to select the best matching constructor from a collection, given an array of argument expressions.
    /// </summary>
    /// <param name="constructors">An enumerable of candidate <see cref="ConstructorInfo"/> objects.</param>
    /// <param name="arguments">The expressions representing constructor arguments.</param>
    /// <returns>
    /// A tuple of (ConstructorInfo, Expression[]) if a suitable match is found; otherwise <see langword="null"/>.
    /// </returns>
    (ConstructorInfo Method, Expression[] Parameters)? SelectConstructor(
        IEnumerable<ConstructorInfo> constructors,
        Expression[] arguments);

    /// <summary>
    /// Attempts to select the best matching method from a collection, given a target (object expression),
    /// optional generic parameters, and an array of argument expressions.
    /// </summary>
    /// <param name="methods">An enumerable of candidate <see cref="MethodInfo"/> objects.</param>
    /// <param name="obj">An expression for the method's target object, or <see langword="null"/> for static methods.</param>
    /// <param name="genericParameters">An optional array of generic arguments for a generic method.</param>
    /// <param name="arguments">Expressions representing the method arguments.</param>
    /// <returns>
    /// A tuple of (MethodInfo, Expression[]) if a suitable match is found; otherwise <see langword="null"/>.
    /// </returns>
    (MethodInfo Method, Expression[] Parameters)? SelectMethod(
        IEnumerable<MethodInfo> methods,
        Expression obj,
        Type[] genericParameters,
        Expression[] arguments);

    /// <summary>
    /// Retrieves a static property or field on the specified type, by name.
    /// </summary>
    /// <param name="type">The target <see cref="Type"/>.</param>
    /// <param name="name">The property or field name.</param>
    /// <returns>
    /// A <see cref="MemberInfo"/> representing the static property or field, or <see langword="null"/>
    /// if it cannot be found.
    /// </returns>
    MemberInfo GetStaticPropertyOrField(Type type, string name);

    /// <summary>
    /// Retrieves an instance property or field on the specified type, by name.
    /// </summary>
    /// <param name="type">The target <see cref="Type"/>.</param>
    /// <param name="name">The property or field name.</param>
    /// <returns>
    /// A <see cref="MemberInfo"/> representing the instance property or field, or <see langword="null"/>
    /// if it cannot be found.
    /// </returns>
    MemberInfo GetInstancePropertyOrField(Type type, string name);

    /// <summary>
    /// Attempts to retrieve a constant expression associated with the specified name.
    /// </summary>
    /// <param name="name">The name that might correspond to a constant value.</param>
    /// <param name="constantExpression">
    /// When this method returns, contains a <see cref="ConstantExpression"/> if one was found;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the constant was found; otherwise <see langword="false"/>.</returns>
    bool TryGetConstant(string name, out ConstantExpression constantExpression);
}

/// <summary>
/// Represents a pairing of a distance measure and a value, typically used
/// for scoring or ranking operations in method or constructor selection.
/// </summary>
/// <typeparam name="T">The type of the associated value.</typeparam>
public interface IDistanceValue<T>
{
    /// <summary>
    /// Gets the numeric distance for this entry, typically used for ranking.
    /// </summary>
    int Distance { get; }

    /// <summary>
    /// Gets the value associated with this distance.
    /// </summary>
    T Value { get; }
}

/// <summary>
/// Provides methods for finding types and extension methods at runtime.
/// </summary>
public interface ITypeFinder
{
    /// <summary>
    /// Attempts to locate a type by name, optionally applying generic arguments if needed.
    /// </summary>
    /// <param name="name">The name of the type to find (can include namespace).</param>
    /// <param name="genericArguments">Generic type arguments to apply if the type is generic.</param>
    /// <returns>A <see cref="Type"/> object if found; otherwise <see langword="null"/>.</returns>
    Type FindType(string name, Type[] genericArguments);

    /// <summary>
    /// Searches for extension methods applicable to the specified extended type.
    /// </summary>
    /// <param name="extendedType">The <see cref="Type"/> being extended by the extension methods.</param>
    /// <param name="name">The name of the method(s) to find.</param>
    /// <returns>An array of <see cref="MethodInfo"/> objects representing the found extension methods.</returns>
    MethodInfo[] FindExtensionMethods(Type extendedType, string name);
}
