using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions.Builders;
using Utils.Expressions.Resolvers;

namespace Utils.Expressions;

/// <summary>
/// Provides static methods for parsing and compiling lambda expressions from string code.
/// </summary>
public static class ExpressionParser
{
	#region Overloads for Parse()

	/// <summary>
	/// Parses the specified lambda expression code (e.g., "m =&gt; m.ToString()")
	/// into a <see cref="LambdaExpression"/>. 
	/// </summary>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <param name="constants">
	/// A read-only dictionary of constant values available to the expression (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> representing the parsed code.</returns>
	public static LambdaExpression Parse(
		string lambdaCode,
		string[] namespaces = null,
		IReadOnlyDictionary<string, object> constants = null) 
	=> ParseCore([], lambdaCode, null, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code (e.g., "m =&gt; m.ToString()")
	/// and returns a <see cref="LambdaExpression"/> of the given default instance type.
	/// </summary>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="defaultInstance">
	/// A <see cref="Type"/> to serve as the default instance for instance members.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <param name="constants">
	/// A read-only dictionary of constant values available to the expression (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> for the parsed code.</returns>
	public static LambdaExpression Parse(
		string lambdaCode,
		Type defaultInstance,
		string[] namespaces = null,
		IReadOnlyDictionary<string, object> constants = null) 
	=> ParseCore<Delegate>(null, lambdaCode, defaultInstance, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code with named parameters
	/// and an optional default instance type.
	/// </summary>
	/// <param name="paramNames">
	/// An array of parameter names for the lambda expression.
	/// </param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="defaultInstance">
	/// A <see cref="Type"/> to serve as the default instance for instance members.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> for the parsed code.</returns>
	public static LambdaExpression Parse(
		string[] paramNames,
		string lambdaCode,
		Type defaultInstance,
		string[] namespaces = null) 
	=> ParseCore<Delegate>(paramNames, lambdaCode, defaultInstance, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code for the given delegate type.
	/// </summary>
	/// <param name="delegateType">The target delegate type.</param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> of the specified delegate type.</returns>
	public static LambdaExpression Parse(
		Type delegateType,
		string lambdaCode,
		string[] namespaces = null) 
	=> ParseCore<Delegate>(null, lambdaCode, null, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code for the given delegate type,
	/// optionally treating the first parameter type as the default instance.
	/// </summary>
	/// <param name="delegateType">The target delegate type.</param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> of the specified delegate type.</returns>
	public static LambdaExpression Parse(
		Type delegateType,
		string lambdaCode,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null) 
	=> ParseCore<Delegate>(null, lambdaCode, null, firstTypeIsDefaultInstance, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code (e.g., "m =&gt; m.ToString()")
	/// into an <see cref="Expression{TDelegate}"/> of type <typeparamref name="TDelegate"/>.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>
	/// An <see cref="Expression{TDelegate}"/> representing the parsed code.
	/// </returns>
	public static Expression<TDelegate> Parse<TDelegate>(
		string lambdaCode,
		string[] namespaces = null) 
	=> (Expression<TDelegate>)ParseCore<TDelegate>(null, lambdaCode, null, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code into an <see cref="Expression{TDelegate}"/>,
	/// optionally treating the first parameter type as the default instance.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>
	/// An <see cref="Expression{TDelegate}"/> representing the parsed code.
	/// </returns>
	public static Expression<TDelegate> Parse<TDelegate>(
		string lambdaCode,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null) 
	=> (Expression<TDelegate>)ParseCore<TDelegate>(null, lambdaCode, null, firstTypeIsDefaultInstance, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code using a set of known parameters.
	/// </summary>
	/// <param name="parameters">
	/// An array of <see cref="ParameterExpression"/> objects to use in the expression.
	/// </param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="defaultStaticType">
	/// A <see cref="Type"/> to serve as the default static type for method lookup (optional).
	/// </param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> built from the parsed code.</returns>
	private static LambdaExpression Parse(
		ParameterExpression[] parameters,
		string lambdaCode,
		Type defaultStaticType,
		bool firstTypeIsDefaultInstance,
		string[] namespaces) 
	=> ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code (e.g., "m =&gt; m.ToString()")
	/// into an <see cref="Expression{TDelegate}"/>, using a provided array of parameters.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="parameters">
	/// An array of <see cref="ParameterExpression"/> objects to use in the expression.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>
	/// An <see cref="Expression{TDelegate}"/> representing the parsed code.
	/// </returns>
	public static Expression<TDelegate> Parse<TDelegate>(
		string lambdaCode,
		ParameterExpression[] parameters,
		string[] namespaces = null) 
	=> (Expression<TDelegate>)ParseCore(parameters, lambdaCode, null, false, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code into an <see cref="Expression{TDelegate}"/>,
	/// optionally treating the first parameter as a default instance and using a static type.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="parameters">
	/// An array of <see cref="ParameterExpression"/> objects to use in the expression.
	/// </param>
	/// <param name="defaultStaticType">
	/// A <see cref="Type"/> to serve as the default static type for method lookup.
	/// </param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>
	/// An <see cref="Expression{TDelegate}"/> representing the parsed code.
	/// </returns>
	public static Expression<TDelegate> Parse<TDelegate>(
		string lambdaCode,
		ParameterExpression[] parameters,
		Type defaultStaticType,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null) 
	=> (Expression<TDelegate>)ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);

	/// <summary>
	/// Parses the specified lambda expression code into a <see cref="LambdaExpression"/>,
	/// optionally treating the first parameter as a default instance and using a static type.
	/// </summary>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="parameters">
	/// An array of <see cref="ParameterExpression"/> objects to use in the expression.
	/// </param>
	/// <param name="defaultStaticType">
	/// A <see cref="Type"/> to serve as the default static type for method lookup.
	/// </param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> representing the parsed code.</returns>
	public static LambdaExpression Parse(
		string lambdaCode,
		ParameterExpression[] parameters,
		Type defaultStaticType,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null) 
	=> ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);

	#endregion

	#region Overloads for Compile()

	/// <summary>
	/// Parses the specified lambda expression code and compiles it to a <see cref="Delegate"/>.
	/// </summary>
	/// <param name="lambdaCode">Lambda expression code (e.g., "m =&gt; m.ToString()").</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A compiled <see cref="Delegate"/> representing the parsed code.</returns>
	public static Delegate Compile(string lambdaCode, params string[] namespaces) 
		=> Parse(lambdaCode, namespaces).Compile();

	/// <summary>
	/// Parses the specified lambda expression code, using a default instance type,
	/// and compiles it to a <see cref="Delegate"/>.
	/// </summary>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="defaultInstance">Type of the default instance.</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A compiled <see cref="Delegate"/>.</returns>
	public static Delegate Compile(string lambdaCode, Type defaultInstance, params string[] namespaces) 
		=> Parse(namespaces, lambdaCode, defaultInstance).Compile();

	/// <summary>
	/// Parses the specified lambda expression code for the given delegate type
	/// and compiles it to a <see cref="Delegate"/>.
	/// </summary>
	/// <param name="delegateType">The target delegate type.</param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A compiled <see cref="Delegate"/>.</returns>
	public static Delegate Compile(Type delegateType, string lambdaCode, params string[] namespaces) 
		=> Parse(delegateType, lambdaCode, namespaces).Compile();

	/// <summary>
	/// Parses the specified lambda expression code for the given delegate type,
	/// optionally treating the first parameter type as the default instance,
	/// and compiles it to a <see cref="Delegate"/>.
	/// </summary>
	/// <param name="delegateType">The target delegate type.</param>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A compiled <see cref="Delegate"/>.</returns>
	public static Delegate Compile(
		Type delegateType,
		string lambdaCode,
		bool firstTypeIsDefaultInstance,
		params string[] namespaces) 
	=> Parse(delegateType, lambdaCode, firstTypeIsDefaultInstance, namespaces).Compile();

	/// <summary>
	/// Parses the specified lambda expression code into <typeparamref name="TDelegate"/>
	/// and compiles it.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code (e.g., "m =&gt; m.ToString()").</param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>
	/// A compiled delegate of type <typeparamref name="TDelegate"/>.
	/// </returns>
	public static TDelegate Compile<TDelegate>(
		string lambdaCode,
		params string[] namespaces) 
		=> Parse<TDelegate>(lambdaCode, namespaces).Compile();

	/// <summary>
	/// Parses the specified lambda expression code into <typeparamref name="TDelegate"/>,
	/// optionally treating the first parameter as the default instance, then compiles it.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="lambdaCode">Lambda expression code to parse.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">
	/// An array of namespace names used for type resolution (optional).
	/// </param>
	/// <returns>A compiled delegate of type <typeparamref name="TDelegate"/>.</returns>
	public static TDelegate Compile<TDelegate>(
		string lambdaCode,
		bool firstTypeIsDefaultInstance,
		params string[] namespaces) 
	=> Parse<TDelegate>(lambdaCode, firstTypeIsDefaultInstance, namespaces).Compile();

	#endregion

	#region Exec() Methods

	/// <summary>
	/// Executes the specified code with the given instance as the context ($0),
	/// and any additional objects as subsequent parameters ($1, $2, etc.).
	/// </summary>
	/// <typeparam name="T">The return type of the expression.</typeparam>
	/// <param name="instance">The primary object context ($0 in the parsed code).</param>
	/// <param name="code">Lambda expression code to execute (e.g., "m =&gt; m.ToString()").</param>
	/// <param name="defaultStaticType">Type used to resolve static methods (optional).</param>
	/// <param name="namespaces">Namespace imports for type resolution (optional).</param>
	/// <param name="objects">
	/// Additional parameters ($1, $2, etc.) to pass into the expression.
	/// </param>
	/// <returns>The evaluated result of type <typeparamref name="T"/>.</returns>
	public static T Exec<T>(
		object instance,
		string code,
		Type defaultStaticType,
		string[] namespaces,
		params object[] objects)
	{
		object[] allObjects = [instance, .. objects];
		var parameters = allObjects
			.Select((obj, i) => Expression.Parameter(obj?.GetType() ?? typeof(object), "Param" + i))
			.ToArray();

		object[] inputObjs = new object[objects.Length + 2];
		inputObjs[1] = inputObjs[0] = instance;
		Array.Copy(objects, 0, inputObjs, 2, objects.Length);

		string lambdaParams = string.Join(",", allObjects.Select((_, i) => "$" + i));
		Type[] paramTypes = inputObjs.Select(o => o.GetType()).ToArray();
		string newCode = string.Format("({0})=>{1}", lambdaParams, code);

		return (T)Parse(parameters, newCode, defaultStaticType, instance != null, namespaces)
			.Compile()
			.DynamicInvoke(inputObjs);
	}

	/// <summary>
	/// Executes the specified code with the given instance as the context ($0),
	/// and any additional objects as subsequent parameters ($1, $2, etc.).
	/// Returns an <see cref="object"/> result.
	/// </summary>
	/// <param name="instance">The primary object context ($0 in the parsed code).</param>
	/// <param name="code">Lambda expression code to execute.</param>
	/// <param name="namespaces">Namespace imports for type resolution (optional).</param>
	/// <param name="objects">
	/// Additional parameters ($1, $2, etc.) to pass into the expression.
	/// </param>
	/// <returns>The evaluated result as an <see cref="object"/>.</returns>
	public static object Exec(
		object instance,
		string code,
		string[] namespaces,
		params object[] objects) 
	=> Exec<object>(instance, code, null, namespaces, objects);

	#endregion

	#region Core Parse Logic

	/// <summary>
	/// Internal helper method that parses the code into a <see cref="LambdaExpression"/>
	/// of the given delegate type <typeparamref name="TDelegate"/>.
	/// </summary>
	/// <typeparam name="TDelegate">The delegate type to which the expression is compiled.</typeparam>
	/// <param name="paramNames">An optional array of parameter names.</param>
	/// <param name="lambdaCode">The lambda expression code to parse.</param>
	/// <param name="defaultStaticType">An optional default static type for method resolution.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">An array of namespace names used for type resolution (optional).</param>
	/// <param name="constants">
	/// A read-only dictionary of constant values available to the expression (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/> of the specified delegate type.</returns>
	private static LambdaExpression ParseCore<TDelegate>(
		string[] paramNames,
		string lambdaCode,
		Type defaultStaticType,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null,
		IReadOnlyDictionary<string, object> constants = null)
	{
		var options = new ParserOptions();
		var builder = new CStyleBuilder();
		var tokenizer = new Tokenizer(lambdaCode ?? throw new ArgumentNullException(nameof(lambdaCode)), builder);
		var parser = new ExpressionParserCore(
			options,
			builder,
			new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
                var context = new ParserContext(typeof(TDelegate), paramNames, defaultStaticType, tokenizer, firstTypeIsDefaultInstance);
                var body = parser.ReadExpression(context);
                return Expression.Lambda(typeof(TDelegate), body, context.Parameters);
	}

	/// <summary>
	/// Internal helper method that parses the code into a <see cref="LambdaExpression"/>
	/// based on the provided parameters.
	/// </summary>
	/// <param name="parameters">An array of <see cref="ParameterExpression"/> objects.</param>
	/// <param name="lambdaCode">The lambda expression code to parse.</param>
	/// <param name="defaultStaticType">An optional default static type for method resolution.</param>
	/// <param name="firstTypeIsDefaultInstance">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">An array of namespace names used for type resolution (optional).</param>
	/// <param name="constants">
	/// A read-only dictionary of constant values available to the expression (optional).
	/// </param>
	/// <returns>A <see cref="LambdaExpression"/>.</returns>
	private static LambdaExpression ParseCore(
		ParameterExpression[] parameters,
		string lambdaCode,
		Type defaultStaticType,
		bool firstTypeIsDefaultInstance,
		string[] namespaces = null,
		IReadOnlyDictionary<string, object> constants = null)
	{
		var options = new ParserOptions();
		var builder = new CStyleBuilder();
		var tokenizer = new Tokenizer(lambdaCode ?? throw new ArgumentNullException(nameof(lambdaCode)), builder);
		var parser = new ExpressionParserCore(
			options,
			builder,
			new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
                var context = new ParserContext(parameters, defaultStaticType, tokenizer, firstTypeIsDefaultInstance);
                var body = parser.ReadExpression(context);
                return Expression.Lambda(body, context.Parameters);
	}

	/// <summary>
	/// Parses the specified code into an <see cref="Expression"/> without
	/// creating a lambda expression. Useful for inline parsing of expressions
	/// that do not necessarily represent delegates.
	/// </summary>
	/// <param name="code">The expression code to parse.</param>
	/// <param name="parameters">The parameter expressions available to the code.</param>
	/// <param name="defaultStaticType">
	/// An optional default static type for method resolution.
	/// </param>
	/// <param name="defaultFirst">
	/// If <see langword="true"/>, treats the first parameter type as the default instance.
	/// </param>
	/// <param name="namespaces">An array of namespace names used for type resolution (optional).</param>
	/// <param name="constants">
	/// A read-only dictionary of constant values available to the expression (optional).
	/// </param>
	/// <returns>An <see cref="Expression"/> representing the parsed code.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="code"/> is <see langword="null"/>.
	/// </exception>
	public static Expression ParseExpression(
		string code,
		ParameterExpression[] parameters,
		Type defaultStaticType,
		bool defaultFirst,
		string[] namespaces = null,
		IReadOnlyDictionary<string, object> constants = null)
	{
		var options = new ParserOptions();
		var builder = new CStyleBuilder();
		var tokenizer = new Tokenizer(code ?? throw new ArgumentNullException(nameof(code)), builder);
		var parser = new ExpressionParserCore(
			options,
			builder,
			new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
		var context = new ParserContext(parameters, defaultStaticType, tokenizer, defaultFirst);

		foreach (var parameter in parameters)
		{
			context.Parameters.Add(parameter);
		}

		return parser.ReadExpression(context);
	}

	#endregion
}
