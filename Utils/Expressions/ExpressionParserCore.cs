using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Objects;

namespace Utils.Expressions
{
	/// <summary>
	/// Provides core parsing functionality for building expression trees from tokenized input.
	/// </summary>
	public class ExpressionParserCore
	{
		#region fields

		/// <summary>
		/// Gets the parser options that affect how expressions are parsed.
		/// </summary>
		public IParserOptions Options { get; }

		/// <summary>
		/// Gets the builder responsible for creating various expression-building components.
		/// </summary>
		public IBuilder Builder { get; }

		/// <summary>
		/// Gets the resolver used to look up types, methods, and members at runtime.
		/// </summary>
		public IResolver Resolver { get; }

		#endregion

		#region ctor

		/// <summary>
		/// Initializes a new instance of the <see cref="ExpressionParserCore"/> class.
		/// </summary>
		/// <param name="options">Parser options to guide the parsing logic.</param>
		/// <param name="builder">A builder interface for creating expression builders.</param>
		/// <param name="resolver">A resolver interface for runtime binding of types and members.</param>
		internal ExpressionParserCore(IParserOptions options, IBuilder builder, IResolver resolver)
		{
			Options = options;
			Builder = builder;
			Resolver = resolver;
		}

		#endregion

		#region methods

		/// <summary>
		/// Attempts to parse a lambda prefix (e.g., (x, y) => ) from the current tokenizer context.
		/// If successful, the method populates <see cref="ParserContext.Parameters"/> accordingly.
		/// </summary>
		/// <param name="context">The parser context containing state and token information.</param>
		/// <returns><see langword="true"/> if a lambda prefix was read; otherwise <see langword="false"/>.</returns>
		private bool ReadLambdaPrefix(ParserContext context)
		{
			Parenthesis[] markers = [("<", ">"), ("(", ")")];
			int paramIndexPrefix = 0;

			// Read the next token to see if it is '(' or an identifier
			string val = context.Tokenizer.ReadToken();
			if (val == "(")
			{
				// Attempt to parse bracket contents (e.g., x, y in "(x, y) =>")
				string bracketContent = ParserExtensions.GetBracketString(context, new Parenthesis("(", ")", null), true);
				if (bracketContent != null)
				{
					string lambdaOperator = context.Tokenizer.ReadToken();
					if (lambdaOperator != "=>")
					{
						context.Tokenizer.ResetPosition();
						return false;
					}

					// Parse parameters
					var parameters = context.Parameters?.ToArray();
					context.Parameters.Clear();

					string[] paramsName = bracketContent
						.SplitCommaSeparatedList(',', markers)
						.Select(p => p.Trim())
						.ToArray();

					for (int i = 0; i < paramsName.Length; i++)
					{
						string[] typeName = paramsName[i]
							.SplitCommaSeparatedList(' ', true, markers)
							.ToArray();

						Type paramType;
						string paramName;

						if (typeName.Length == 1)
						{
							paramType = parameters?[i + paramIndexPrefix].Type ?? typeof(object);
							paramName = paramsName[i];
							context.Parameters.Add(Expression.Parameter(paramType, paramName));
						}
						else if (typeName.Length > 1)
						{
							paramType = GetType(context, typeName[0]);
							if (paramType == null)
							{
								throw new ParseUnfindTypeException(typeName[0], context.Tokenizer.Position.Index);
							}
							paramName = typeName[1];
							context.Parameters.Add(Expression.Parameter(paramType, paramName));
						}
					}
					return true;
				}
			}
			else if (char.IsLetter(val[0]) || val[0] == '_')
			{
				// Might be a single-parameter lambda like x => ...
				string lambdaOperator = context.Tokenizer.ReadToken();
				if (lambdaOperator == "=>")
				{
					Type paramType = context.Parameters?[0 + paramIndexPrefix].Type ?? typeof(object);
					context.Parameters.Add(Expression.Parameter(paramType, val));
					return true;
				}
			}

			context.Tokenizer.ResetPosition();
			return false;
		}

		/// <summary>
		/// Reads a complete expression from the tokenizer, optionally handling a lambda prefix.
		/// If the context has a <see cref="ParserContext.DelegateType"/>, its parameters are used if no prefix is found.
		/// </summary>
		/// <param name="context">The parsing context, including the tokenizer and parameters.</param>
		/// <returns>The resulting expression or <see langword="null"/> if none can be read.</returns>
		public Expression ReadExpression(ParserContext context)
		{
			// Attempt to parse a lambda prefix. If none is found but we have a delegate type, fill parameters from that.
			if (!ReadLambdaPrefix(context) && context.DelegateType != null)
			{
				var parameters = context.DelegateType
					.GetMethod("Invoke")
					?.GetParameters()
					.Select(p => Expression.Parameter(p.ParameterType, p.Name));

				if (parameters != null)
				{
					foreach (var parameter in parameters)
					{
						context.Parameters.Add(parameter);
					}
				}
			}

			return ReadExpression(context, 0, null, out _);
		}

		/// <summary>
		/// Reads an expression from the tokenizer, handling a particular priority level,
		/// optional parenthesis wrappers, and updating a flag if the wrapper is closed.
		/// </summary>
		/// <param name="context">The parsing context, including the tokenizer and parameters.</param>
		/// <param name="priorityLevel">The current parsing priority.</param>
		/// <param name="markers">Optional parenthesis definitions for grouping.</param>
		/// <param name="isClosedWrap">Becomes <see langword="true"/> if the read operation encounters a closing marker.</param>
		/// <returns>An <see cref="Expression"/> corresponding to parsed tokens, or <see langword="null"/> if none found.</returns>
		internal Expression ReadExpression(ParserContext context, int priorityLevel, Parenthesis markers, out bool isClosedWrap)
		{
			Expression currentExpression = null;
			isClosedWrap = false;

			// Read the first token
			string val = context.Tokenizer.PeekToken();
			if (markers?.Test(val, out isClosedWrap) ?? false) return null;
			context.Tokenizer.ReadToken();

			if (val == null) return null; // No token to read
			char firstChar = val[0];

			/****************** First read: handle unary or object-like expressions ******************/
			if (char.IsDigit(firstChar))
			{
				// Numeric value
				currentExpression = Builder.NumberBuilder.Build(this, context, val, priorityLevel, markers, ref isClosedWrap);
			}
			else
			{
				// Non-numeric value
				if (!Builder.StartExpressionBuilders.TryGetValue(val, out var expressionBuilder))
				{
					expressionBuilder = Builder.FallbackUnaryBuilder;
				}
				currentExpression = expressionBuilder.Build(this, context, val, priorityLevel, markers, ref isClosedWrap);
			}

			/****************** Subsequent reads: handle binary or ternary operators ******************/
			int nextLevel;
			while (!isClosedWrap && (nextLevel = TryGetNextPriorityLevel(context)) > priorityLevel)
			{
				string nextVal = context.Tokenizer.PeekToken();
				if (markers?.Test(nextVal, out isClosedWrap) ?? false) return currentExpression;

				context.Tokenizer.ReadToken();

				if (!Builder.FollowUpExpressionBuilder.TryGetValue(nextVal, out var expressionBuilder))
				{
					expressionBuilder = Builder.FallbackBinaryOrTernaryBuilder;
				}

				currentExpression = expressionBuilder.Build(
					this,
					context,
					currentExpression,
					val,
					nextVal,
					priorityLevel,
					ref nextLevel,
					markers,
					ref isClosedWrap
				);
			}

			return currentExpression;
		}

		/// <summary>
		/// Reads generic type parameters from the tokenizer, for expressions like Foo&lt;int,string&gt;.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="startSymbol">The symbol marking the start of generics (defaults to "&lt;").</param>
		/// <param name="endSymbol">The symbol marking the end of generics (defaults to "&gt;").</param>
		/// <returns>An array of types read from the tokenizer, or <see langword="null"/> if not applicable.</returns>
		public Type[] ReadGenericParams(ParserContext context, string startSymbol = "<", string endSymbol = ">")
		{
			if (context.Tokenizer.PeekToken() != startSymbol) return null;

			// Consume '<'
			context.Tokenizer.ReadSymbol("<");
			List<Type> result = new();

			while (true)
			{
				// If the next token is '>', break out
				if (context.Tokenizer.PeekToken() == endSymbol)
				{
					context.Tokenizer.ReadToken();
					break;
				}

				// Read the next type
				var type = ReadType(context, null);
				if (type == null) return null;
				result.Add(type);
			}

			return [.. result];
		}

		/// <summary>
		/// Reads a sequence of expressions until a closing marker is reached or no more content is available.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="markers">The pair of symbols defining the start and end of the expression group.</param>
		/// <param name="readStartSymbol">If <see langword="true"/>, expects the start symbol to be present before reading.</param>
		/// <param name="ignoreSeparatorAfterBlock">Whether to ignore the separator after a block expression.</param>
		/// <returns>An array of <see cref="Expression"/> objects read from the tokenizer.</returns>
		public Expression[] ReadExpressions(
			ParserContext context,
			Parenthesis markers,
			bool readStartSymbol = true,
			bool ignoreSeparatorAfterBlock = false)
		{
			if (readStartSymbol)
			{
				if (context.Tokenizer.PeekToken() != markers.Start) return null;
				context.Tokenizer.ReadSymbol(markers.Start);
			}

			List<Expression> result = [];
			bool newIsClosedWrap = false;

			while (!newIsClosedWrap)
			{
				Expression expression = ReadExpression(context, 0, markers, out newIsClosedWrap);
				if (expression == null && newIsClosedWrap)
				{
					context.Tokenizer.ReadSymbol(markers.End);
					break;
				}

				result.Add(expression);
				var nextToken = context.Tokenizer.ReadToken();

				if (!markers.Test(nextToken, expression is BlockExpression, out var isEnd))
				{
					throw new ParseUnknownException(nextToken, context.Tokenizer.Position.Index);
				}

				if (isEnd) break;
			}

			return [.. result];
		}

		/// <summary>
		/// Attempts to determine the next operator's priority level based on the upcoming token.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <returns>An integer indicating the next operator's priority; 0 if none.</returns>
		private int TryGetNextPriorityLevel(ParserContext context)
		{
			string nextString = context.Tokenizer.PeekToken();
			if (string.IsNullOrEmpty(nextString)
				|| nextString == ";"
				|| nextString == "}"
				|| nextString == ","
				|| nextString == ":")
			{
				return 0;
			}

			return Options.GetOperatorLevel(nextString, false);
		}

		/// <summary>
		/// Reads a type (e.g., MyNamespace.SomeType&lt;T&gt;) from the tokenizer, handling generic arguments if present.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="val">An optional initial token for the type name; if null, a token is read from the tokenizer.</param>
		/// <returns>A <see cref="Type"/> instance or <see langword="null"/> if it cannot be resolved.</returns>
		public Type ReadType(ParserContext context, string val)
		{
			// If no token is specified, read the next token
			string strVal = string.IsNullOrEmpty(val) ? context.Tokenizer.ReadToken() : val;
			Type type = null;

			while (type == null)
			{
				List<Type> listGenericType = [];

				// Check for generic parameters (e.g., <T, U>)
				if (context.Tokenizer.PeekToken() == "<")
				{
					context.Tokenizer.ReadToken();
					while (true)
					{
						listGenericType.Add(ReadType(context, null));
						if (context.Tokenizer.PeekToken() == ",")
						{
							context.Tokenizer.ReadToken();
						}
						else
						{
							break;
						}
					}
					context.Tokenizer.ReadSymbol(">");
				}

				// Attempt to resolve the type via the resolver
				type = Resolver.ResolveType(strVal, [.. listGenericType]);

				if (type == null)
				{
					// If not resolved, check for a dot indicating nested or namespaced type continuation
					bool result = context.Tokenizer.ReadSymbol(".", false);
					if (!result)
					{
						throw new ParseUnfindTypeException(strVal, context.Tokenizer.Position.Index);
					}

					strVal += "." + context.Tokenizer.ReadToken();
				}
			}

			return type;
		}

		/// <summary>
		/// Attempts to resolve a named type using the configured resolver.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="typeName">The textual name of the type (e.g., "System.String").</param>
		/// <returns>A <see cref="Type"/> if found; otherwise <see langword="null"/>.</returns>
		public Type GetType(ParserContext context, string typeName)
		{
			return Resolver.ResolveType(typeName);
		}

		/// <summary>
		/// Retrieves an expression representing either a method call or a property/field access
		/// on the given instance expression, based on the provided member name.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="currentExpression">An <see cref="Expression"/> representing the instance (object) on which the member is accessed.</param>
		/// <param name="name">The method or property/field name.</param>
		/// <returns>An <see cref="Expression"/> for the method call or member access, or <see langword="null"/> if not found.</returns>
		public Expression GetExpression(ParserContext context, Expression currentExpression, string name)
		{
			var methods = Resolver.GetInstanceMethods(currentExpression.Type, name);
			if (methods.Any())
			{
				var genericParams = ReadGenericParams(context)?.ToArray();
				var listArguments = ReadExpressions(context, new Parenthesis("(", ")", ",")).ToArray();
				var methodAndParameters = Resolver.SelectMethod(methods, currentExpression, genericParams, listArguments);

				if (methodAndParameters is not null)
				{
					return Expression.Call(currentExpression, methodAndParameters?.Method, methodAndParameters?.Parameters);
				}
			}

			// If not a method, attempt to retrieve a property or field
			var propertyOrField = Resolver.GetInstancePropertyOrField(currentExpression.Type, name);
			return propertyOrField switch
			{
				PropertyInfo pi => Expression.Property(currentExpression, pi),
				FieldInfo fi => Expression.Field(currentExpression, fi),
				_ => null
			};
		}

		/// <summary>
		/// Retrieves an expression representing a static method call or a static property/field access
		/// on the given type, based on the provided member name.
		/// </summary>
		/// <param name="context">The parsing context.</param>
		/// <param name="type">The <see cref="Type"/> on which to locate the member.</param>
		/// <param name="name">The method or property/field name.</param>
		/// <returns>An <see cref="Expression"/> for the method call or member access, or <see langword="null"/> if not found.</returns>
		public Expression GetExpression(ParserContext context, Type type, string name)
		{
			var methods = Resolver.GetStaticMethods(type, name);
			if (methods.Any())
			{
				var genericParams = ReadGenericParams(context)?.ToArray();
				var listArguments = ReadExpressions(context, new Parenthesis("(", ")", ",")).ToArray();
				var methodAndParameters = Resolver.SelectMethod(methods, null, genericParams, listArguments);

				if (methodAndParameters?.Method is not null)
				{
					return Expression.Call(null, methodAndParameters?.Method, methodAndParameters?.Parameters);
				}
			}

			// If not a method, attempt to retrieve a static property or field
			var propertyOrField = Resolver.GetStaticPropertyOrField(type, name);
			return propertyOrField switch
			{
				PropertyInfo pi => Expression.Property(null, pi),
				FieldInfo fi => Expression.Field(null, fi),
				_ => null
			};
		}

		#endregion
	}
}
