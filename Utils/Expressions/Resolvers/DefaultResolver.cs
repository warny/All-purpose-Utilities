using System.Linq.Expressions;
using System.Reflection;
using Utils.String;

namespace Utils.Expressions.Resolvers;

/// <summary>
/// Provides a default implementation of <see cref="IResolver"/>, enabling
/// type resolution, method/constructor selection, and constant retrieval.
/// </summary>
public class DefaultResolver : IResolver
{
	/// <summary>
	/// Gets the <see cref="ITypeFinder"/> used to locate types and extension methods.
	/// </summary>
	protected ITypeFinder TypeFinder { get; }

	/// <summary>
	/// Defines the opening and closing symbols for generic arguments, typically "&lt;" and "&gt;".
	/// </summary>
	public Parenthesis GenericMarkers { get; } = new("<", ">");

	/// <summary>
	/// Defines the opening and closing symbols for arrays, typically "[" and "]".
	/// </summary>
	public Parenthesis ArrayMarkers { get; } = new("[", "]");

	/// <summary>
	/// Gets the character used to mark nullable types (e.g., "int?").
	/// </summary>
	public char NullableMarkChar { get; } = '?';

	/// <summary>
	/// Gets a read-only dictionary of constants that can be retrieved by name.
	/// </summary>
	public IReadOnlyDictionary<string, object> Constants { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultResolver"/> class with
	/// the specified <see cref="ITypeFinder"/> and an optional dictionary of constants.
	/// </summary>
	/// <param name="typeFinder">
	/// An object implementing <see cref="ITypeFinder"/> to locate types in assemblies.
	/// </param>
	/// <param name="constants">
	/// A read-only dictionary of constant names and values. If <see langword="null"/>,
	/// an empty dictionary is used.
	/// </param>
	public DefaultResolver(ITypeFinder typeFinder, IReadOnlyDictionary<string, object> constants)
	{
		constants ??= new Dictionary<string, object>();
		TypeFinder = typeFinder;
		Constants = constants;
	}

	/// <inheritdoc/>
	public Type ResolveType(string name) => ResolveType(name, null);

	/// <inheritdoc/>
	public Type ResolveType(string name, Type[] genericParameters)
	{
		name = name.Trim();
		if (string.IsNullOrEmpty(name)) return null;

		// Handle nullable types (e.g., "int?")
		if (name.EndsWith(NullableMarkChar))
		{
			var baseName = name[..^1];
			var result = ResolveType(baseName);
			return typeof(Nullable<>).MakeGenericType(result);
		}

		// Handle array syntax (e.g., "int[]" or "int[,,]")
		if (name.EndsWith(ArrayMarkers.End))
		{
			var arrayStartIndex = name.LastIndexOf(ArrayMarkers.Start);
			var elementTypeName = name[..arrayStartIndex];
			var result = ResolveType(elementTypeName);
			var arrayMarker = name[arrayStartIndex..];
			var rank = arrayMarker.Count(c => c == ',') + 1;

			return rank == 1
				? result.MakeArrayType()
				: result.MakeArrayType(rank);
		}

		// Handle generics (e.g., "List<T>")
		if (name.EndsWith(GenericMarkers.End))
		{
			if (genericParameters != null)
				throw new Exception("Unexpected generic type definition.");

			var genericStartIndex = name.LastIndexOf(GenericMarkers.Start);
			var genericMarker = name[genericStartIndex..];
			var trimmed = genericMarker.Trim(' ', '<', '>');
			var splitted = trimmed.SplitCommaSeparatedList(',', [GenericMarkers, ArrayMarkers]);
			genericParameters = splitted
				.Select(ResolveType)
				.ToArray();
		}

		// Try to locate the type
		var type = TypeFinder.FindType(name, genericParameters);
		return type;
	}

	/// <summary>
	/// Retrieves all public constructors for the specified <paramref name="type"/>.
	/// </summary>
	/// <param name="type">A <see cref="Type"/> whose constructors are requested.</param>
	/// <returns>An array of <see cref="ConstructorInfo"/> objects.</returns>
	public ConstructorInfo[] GetConstructors(Type type)
	{
		return type.GetTypeInfo().GetConstructors();
	}

	/// <summary>
	/// Retrieves all instance methods matching the given <paramref name="name"/> on the specified <paramref name="type"/>,
	/// including extension methods discovered via <see cref="ITypeFinder"/>.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> to examine.</param>
	/// <param name="name">The name of the instance (or extension) methods to retrieve.</param>
	/// <returns>An array of <see cref="MethodInfo"/> that match the given criteria.</returns>
	public MethodInfo[] GetInstanceMethods(Type type, string name)
	{
		return type
			.GetRuntimeMethods()
			.Where(m => !m.IsStatic && m.Name == name)
			.Union(TypeFinder.FindExtensionMethods(type, name))
			.ToArray();
	}

	/// <summary>
	/// Retrieves all static methods matching the given <paramref name="name"/> on the specified <paramref name="type"/>.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> to examine.</param>
	/// <param name="name">The name of the static methods to retrieve.</param>
	/// <returns>An array of <see cref="MethodInfo"/> that match the given criteria.</returns>
	public MethodInfo[] GetStaticMethods(Type type, string name)
	{
		return type
			.GetRuntimeMethods()
			.Where(m => m.IsStatic && m.Name == name)
			.ToArray();
	}

	/// <summary>
	/// Selects the most suitable constructor from the provided <paramref name="constructors"/>
	/// based on the specified <paramref name="arguments"/>. This method uses parameter matching
	/// and assigns a "distance" score to find the best match.
	/// </summary>
	/// <param name="constructors">A sequence of <see cref="ConstructorInfo"/> candidates.</param>
	/// <param name="arguments">The argument expressions used in the constructor call.</param>
	/// <returns>
	/// A tuple containing the <see cref="ConstructorInfo"/> and the adjusted <see cref="Expression"/> array,
	/// or <see langword="null"/> if no suitable constructor is found.
	/// </returns>
	public (ConstructorInfo Method, Expression[] Parameters)? SelectConstructor(
		IEnumerable<ConstructorInfo> constructors,
		Expression[] arguments)
	{
		var argumentTypes = arguments.Select(a => a.Type).ToArray();

		return constructors
			.Select(c => new DistanceValue<ConstructorInfo>(c.CompareParametersAndTypes(argumentTypes), c))
			.Where(dv => dv.Distance >= 0)
			.OrderBy(dv => dv.Distance)
			.Select(dv => (dv.Value, dv.Value.AdjustParameters(arguments)))
			.Cast<(ConstructorInfo Method, Expression[] Parameters)?>()
			.FirstOrDefault();
	}

	/// <summary>
	/// Selects the most suitable method from the provided <paramref name="methods"/>,
	/// optionally applying <paramref name="genericParameters"/> if it's a generic method definition.
	/// Determines a "distance" score to pick the best match, then returns the adjusted expressions.
	/// </summary>
	/// <param name="methods">A sequence of <see cref="MethodInfo"/> candidates.</param>
	/// <param name="obj">
	/// An <see cref="Expression"/> representing the target object (for instance methods),
	/// or <see langword="null"/> for static calls.
	/// </param>
	/// <param name="genericParameters">Optional array of types to use if the method is generic.</param>
	/// <param name="arguments">The argument expressions for the method call.</param>
	/// <returns>
	/// A tuple containing the <see cref="MethodInfo"/> and the adjusted <see cref="Expression"/> array,
	/// or <see langword="null"/> if no suitable method is found.
	/// </returns>
	public (MethodInfo Method, Expression[] Parameters)? SelectMethod(
		IEnumerable<MethodInfo> methods,
		Expression obj,
		Type[] genericParameters,
		Expression[] arguments)
	{
		var argumentTypes = arguments.Select(a => a.Type).ToArray();

		return methods
			// Apply generic parameters if needed
			.Select(m => (genericParameters is null || !m.IsGenericMethodDefinition)
						 ? m.InferGenericMethod(obj?.Type, argumentTypes)
						 : m.MakeGenericMethod(genericParameters))
			.Where(m => m is not null)
			// Evaluate distance
			.Select(m => new DistanceValue<MethodInfo>(m.CompareParametersAndTypes(obj, argumentTypes), m))
			.Where(dv => dv.Distance >= 0)
			.OrderBy(dv => dv.Distance)
			.Select(dv => (dv.Value, dv.Value.AdjustParameters(obj, arguments)))
			.Cast<(MethodInfo Method, Expression[] Parameters)?>()
			.FirstOrDefault();
	}

	/// <summary>
	/// Retrieves a static property or field on <paramref name="type"/> with the given <paramref name="name"/>.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> to inspect for the property or field.</param>
	/// <param name="name">The name of the desired member.</param>
	/// <returns>
	/// A <see cref="MemberInfo"/> representing a public static property or field,
	/// or <see langword="null"/> if none is found.
	/// </returns>
	public MemberInfo GetStaticPropertyOrField(Type type, string name)
	{
		return (MemberInfo)type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)
			   ?? (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.Static);
	}

	/// <summary>
	/// Retrieves an instance property or field on <paramref name="type"/> with the given <paramref name="name"/>.
	/// </summary>
	/// <param name="type">The <see cref="Type"/> to inspect for the property or field.</param>
	/// <param name="name">The name of the desired member.</param>
	/// <returns>
	/// A <see cref="MemberInfo"/> representing a public instance property or field,
	/// or <see langword="null"/> if none is found.
	/// </returns>
	public MemberInfo GetInstancePropertyOrField(Type type, string name)
	{
		return (MemberInfo)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
			   ?? (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
	}

	/// <summary>
	/// Attempts to retrieve a named constant from the internal <see cref="Constants"/> dictionary.
	/// </summary>
	/// <param name="name">The name of the constant.</param>
	/// <param name="constantExpression">
	/// When this method returns, contains a <see cref="ConstantExpression"/> of the constant value if found.
	/// </param>
	/// <returns><see langword="true"/> if the constant was found; otherwise <see langword="false"/>.</returns>
	public bool TryGetConstant(string name, out ConstantExpression constantExpression)
	{
		if (Constants.TryGetValue(name, out var value))
		{
			constantExpression = Expression.Constant(value);
			return true;
		}
		constantExpression = null;
		return false;
	}

	/// <summary>
	/// A local struct or class used to pair a distance measure with a particular
	/// <typeparamref name="T"/> item. This is useful for method/constructor selection logic.
	/// </summary>
	private class DistanceValue<T>(int distance, T value) : IDistanceValue<T>
	{
		/// <summary>
		/// Gets the numeric distance for the ranked item.
		/// </summary>
		public int Distance => distance;

		/// <summary>
		/// Gets the associated item (e.g., a <see cref="MethodInfo"/> or <see cref="ConstructorInfo"/>).
		/// </summary>
		public T Value => value;
	}
}
