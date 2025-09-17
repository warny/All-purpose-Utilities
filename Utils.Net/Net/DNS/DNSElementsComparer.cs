using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Provides an <see cref="IEqualityComparer{T}"/> implementation for <see cref="DNSElement"/> objects.
	/// </summary>
	/// <remarks>
	/// This comparer dynamically generates comparison and hashing functions for each unique
	/// derived <see cref="DNSElement"/> type. It leverages expression trees to analyze fields and
	/// properties, ensuring that two DNS elements of the same derived type are compared field by field
	/// (including arrays), or via an <see cref="IEquatable{T}"/> interface implementation if available.
	/// 
	/// When generating hash codes, each relevant field or array element is considered. This mechanism allows
	/// for a high degree of flexibility and extensibility when new DNS types or fields are introduced.
	/// </remarks>
	public class DNSElementsComparer : IEqualityComparer<DNSElement>
	{
		/// <summary>
		/// Stores the per-type comparison functions once they are compiled.
		/// </summary>
		private readonly Dictionary<Type, Func<DNSElement, DNSElement, bool>> comparers = new();

		/// <summary>
		/// Stores the per-type hash code generation functions once they are compiled.
		/// </summary>
		private readonly Dictionary<Type, Func<DNSElement, int>> getHashCodes = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSElementsComparer"/> class.
		/// This constructor is private to enforce singleton usage via the <see cref="Default"/> property.
		/// </summary>
		private DNSElementsComparer()
		{
		}

		/// <summary>
		/// Gets the default, shared instance of the <see cref="DNSElementsComparer"/>.
		/// </summary>
		/// <remarks>
		/// The default instance is suitable for most scenarios where a single global comparer
		/// is required for <see cref="DNSElement"/> objects.
		/// </remarks>
		public static DNSElementsComparer Default { get; } = new DNSElementsComparer();

		/// <inheritdoc />
		public bool Equals([AllowNull] DNSElement x, [AllowNull] DNSElement y)
		{
			// If both references are null, they're considered equal.
			if (x is null && y is null)
				return true;

			// If only one is null, they're not equal.
			if (x is null || y is null)
				return false;

			var typeX = x.GetType();
			var typeY = y.GetType();

			// If types differ, they're not equal.
			if (typeX != typeY)
				return false;

			// Attempt to retrieve or create a suitable comparer.
			if (!comparers.TryGetValue(typeX, out var comparer))
			{
				comparer = CreateComparer(typeX);
			}

			return comparer(x, y);
		}

		/// <inheritdoc />
		public int GetHashCode([DisallowNull] DNSElement obj)
		{
			ArgumentNullException.ThrowIfNull(obj);

			var type = obj.GetType();

			// Attempt to retrieve or create a suitable hash code generator.
			if (!getHashCodes.TryGetValue(type, out var getHashCode))
			{
				getHashCode = CreateGetHasCode(type);
			}

			return getHashCode(obj);
		}

		/// <summary>
		/// Creates and compiles an expression tree to compare two <see cref="DNSElement"/> objects
		/// of the specified <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The concrete derived type of <see cref="DNSElement"/> for which to generate the comparer.</param>
		/// <returns>
		/// A function that accepts two <see cref="DNSElement"/> instances of the specified type and
		/// returns <see langword="true"/> if they are equal; otherwise, <see langword="false"/>.
		/// </returns>
		private Func<DNSElement, DNSElement, bool> CreateComparer(Type type)
		{
			var comparerExpressions = new List<Expression>();

			// Parameters for the lambda expression, each representing one of the two objects to compare.
			var param1 = Expression.Parameter(typeof(DNSElement), "param1");
			var param2 = Expression.Parameter(typeof(DNSElement), "param2");

			// Typed local variables, holding the converted DNSElement references.
			var variable1 = Expression.Variable(type, "variable1");
			var variable2 = Expression.Variable(type, "variable2");

			// Boolean local variable that holds the incremental comparison result.
			var comparison = Expression.Variable(typeof(bool), "comparison");

			// Convert the input parameters to the specific derived type.
			comparerExpressions.Add(Expression.Assign(variable1, Expression.Convert(param1, type)));
			comparerExpressions.Add(Expression.Assign(variable2, Expression.Convert(param2, type)));

			// If the derived type implements IEquatable<T>, use its Equals method directly.
			var comparableType = typeof(IEquatable<>).MakeGenericType(type);
			if (comparableType.IsAssignableFrom(type))
			{
				var equalsMethod = comparableType.GetMethod("Equals");
				comparerExpressions.Add(
					Expression.Assign(
						comparison,
						Expression.Call(variable1, equalsMethod, [variable2])
					)
				);
			}
			else
			{
				// Otherwise, compare all relevant fields and properties, including array handling.
				comparerExpressions.Add(Expression.Assign(comparison, Expression.Constant(true)));

				foreach (var field in DNSPacketHelpers.GetDNSFields(type))
				{
					var member1 = PropertyOrField(variable1, field.Member);

					// If the member is an array, do a step-by-step comparison of each element.
					if (member1.Type.IsArray)
					{
						CreateArrayComparer(comparerExpressions, variable2, comparison, field, member1);
					}
					else
					{
						var member2 = PropertyOrField(variable2, field.Member);
						comparerExpressions.Add(
							Expression.Assign(
								comparison,
								Expression.AndAlso(
									comparison,
									CreateEqualityComparer(member1, member2)
								)
							)
						);
					}
				}
			}

			// Add the final comparison result to the expression block.
			comparerExpressions.Add(comparison);

			// Build and compile the expression into a delegate.
			var comparerLambda = Expression.Lambda<Func<DNSElement, DNSElement, bool>>(
				Expression.Block(
					typeof(bool),
					[variable1, variable2, comparison],
					comparerExpressions
				),
				"Compare" + type.Name,
				[param1, param2]
			);

			var comparerFunc = comparerLambda.Compile();
			comparers.Add(type, comparerFunc);

			return comparerFunc;
		}

		/// <summary>
		/// Creates and compiles an expression tree to compare each element in an array property or field.
		/// </summary>
		/// <param name="comparer">A collection of expression nodes to which array comparison instructions are appended.</param>
		/// <param name="variable2">The second typed object to compare.</param>
		/// <param name="comparison">A <see cref="bool"/> variable storing the ongoing result of the comparison.</param>
		/// <param name="field">A tuple containing the custom attribute data and the <see cref="MemberInfo"/> reference.</param>
		/// <param name="member1">An expression representing the first array.</param>
		private void CreateArrayComparer(
			List<Expression> comparer,
			ParameterExpression variable2,
			ParameterExpression comparison,
			(DNSFieldAttribute Attribute, MemberInfo Member) field,
			Expression member1)
		{
			var member2 = PropertyOrField(variable2, field.Member);

			// First compare the array lengths. If they differ, comparison is already false.
			var lengthProperty = member1.Type.GetProperty("Length");
			comparer.Add(
				Expression.Assign(
					comparison,
					Expression.Equal(
						Expression.Property(member1, lengthProperty),
						Expression.Property(member2, lengthProperty)
					)
				)
			);

			// We'll loop through each index in the arrays and compare elements if the lengths match.
			var variableI = Expression.Variable(typeof(int), "i");
			var variableLength = Expression.Variable(typeof(int), "length");
			var breakLabel = Expression.Label("break");

			// Prepare the element-by-element comparison expression.
			var comparerExpression = CreateEqualityComparer(
				Expression.ArrayIndex(member1, variableI),
				Expression.ArrayIndex(member2, variableI)
			);

			comparer.Add(
				Expression.IfThen(
					comparison,
					Expression.Block(
						[ variableI, variableLength ],
						Expression.Assign(variableI, Expression.Constant(0)),
						Expression.Assign(variableLength, Expression.Property(member1, lengthProperty)),
						Expression.Loop(
							Expression.Block(
								Expression.IfThen(
									Expression.GreaterThanOrEqual(variableI, variableLength),
									Expression.Break(breakLabel)
								),
								Expression.Assign(
									comparison,
									Expression.AndAlso(
										comparison,
										comparerExpression
									)
								),
								Expression.IfThen(
									Expression.Not(comparison),
									Expression.Break(breakLabel)
								),
								Expression.AddAssign(variableI, Expression.Constant(1))
							),
							breakLabel
						)
					)
				)
			);
		}

		/// <summary>
		/// Creates an expression to compare two members (fields or properties).
		/// </summary>
		/// <param name="member1">Expression referencing the first member.</param>
		/// <param name="member2">Expression referencing the second member.</param>
		/// <returns>An expression that yields <see langword="true"/> if the members are equal; otherwise, <see langword="false"/>.</returns>
		private Expression CreateEqualityComparer(Expression member1, Expression member2)
		{
			// The core value-based equality expression (e.g., 'm1.Equals(m2)').
			var valueEquality = CreateValueEqualityComparer(member1, member2);

			// For reference types, account for possible null values on either side.
			if (member1.Type.IsClass && member2.Type.IsClass)
			{
				return Expression.OrElse(
					Expression.AndAlso(
						Expression.Equal(member1, Expression.Constant(null, member1.Type)),
						Expression.Equal(member2, Expression.Constant(null, member2.Type))
					),
					Expression.AndAlso(
						Expression.NotEqual(member1, Expression.Constant(null, member1.Type)),
						valueEquality
					)
				);
			}

			// For non-reference types, just return the core equality check.
			return valueEquality;
		}

		/// <summary>
		/// Creates an expression for a direct value-based equality check.
		/// </summary>
		/// <param name="member1">The first member expression.</param>
		/// <param name="member2">The second member expression.</param>
		/// <returns>An expression representing a valid equality check between the two members.</returns>
		private Expression CreateValueEqualityComparer(Expression member1, Expression member2)
		{
			// Try to call a strongly-typed 'Equals' method first.
			var equalsMethod = member1.Type.GetMethod("Equals", new[] { member2.Type });
			if (equalsMethod != null && equalsMethod.GetParameters()[0].ParameterType != typeof(object))
			{
				return Expression.Call(member1, equalsMethod, member2);
			}

			// If there's an 'op_Equality' operator, use that.
			var opEqualsMethod = member1.Type.GetMethod("op_Equality", new[] { member1.Type, member2.Type });
			if (opEqualsMethod != null)
			{
				return Expression.Call(null, opEqualsMethod, member1, member2);
			}

			// Otherwise, fall back to the object-based 'Equals(object)' method.
			return Expression.Call(member1, equalsMethod, Expression.Convert(member2, typeof(object)));
		}

		/// <summary>
		/// Creates and compiles an expression tree to generate a hash code for a <see cref="DNSElement"/>
		/// of the specified <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The concrete derived type of <see cref="DNSElement"/> for which to generate a hashing function.</param>
		/// <returns>A function that calculates the hash code of a <see cref="DNSElement"/> instance.</returns>
		private Func<DNSElement, int> CreateGetHasCode(Type type)
		{
			var getHashCodeExpressions = new List<Expression>();

			// Parameters and local variables.
			var param = Expression.Parameter(typeof(DNSElement), "param");
			var variable = Expression.Variable(type, "variable");
			var hashCode = Expression.Variable(typeof(int), "hashCode");

			// Initialize a non-zero, "prime-like" base value to reduce collisions.
			getHashCodeExpressions.Add(Expression.Assign(hashCode, Expression.Constant(31)));

			// Convert the input parameter to the derived type.
			getHashCodeExpressions.Add(Expression.Assign(variable, Expression.Convert(param, type)));

			// Combine hash values from relevant fields and properties.
			foreach (var field in DNSPacketHelpers.GetDNSFields(type))
			{
				var member = PropertyOrField(variable, field.Member);

				// Handle array fields by hashing each element in sequence.
				if (member.Type.IsArray)
				{
					var variableI = Expression.Variable(typeof(int), "i");
					var variableLength = Expression.Variable(typeof(int), "length");
					var lengthMethod = member.Type.GetMethod("Length");
					var breakLabel = Expression.Label("break");

					getHashCodeExpressions.Add(
						Expression.Block(
							Expression.Assign(variableI, Expression.Constant(0)),
							Expression.Assign(variableLength, Expression.Call(member, lengthMethod)),
							Expression.Loop(
								Expression.Block(
									Expression.IfThen(
										Expression.GreaterThanOrEqual(variableI, variableLength),
										Expression.Break(breakLabel)
									),
									Expression.Assign(
										hashCode,
										Expression.Add(
											Expression.Multiply(hashCode, Expression.Constant(27)),
											// Call GetHashCode on the array element.
											Expression.Call(
												Expression.ArrayIndex(member, variableI),
												nameof(GetHashCode),
												Type.EmptyTypes
											)
										)
									),
									Expression.AddAssign(variableI, Expression.Constant(1))
								),
								breakLabel
							)
						)
					);
				}
				else
				{
					// Directly combine the hash code from each field/property.
					getHashCodeExpressions.Add(
						Expression.Assign(
							hashCode,
							Expression.Add(
								Expression.Multiply(hashCode, Expression.Constant(27)),
								Expression.Call(
									PropertyOrField(variable, field.Member),
									nameof(GetHashCode),
									Type.EmptyTypes
								)
							)
						)
					);
				}
			}

			// Return the computed hash code.
			getHashCodeExpressions.Add(hashCode);

			// Build and compile the expression into a delegate.
			var getHashCodeLambda = Expression.Lambda<Func<DNSElement, int>>(
				Expression.Block(
					typeof(int),
					[variable, hashCode],
					getHashCodeExpressions
				),
				"GetHashCode" + type.Name,
				[param]
			);

			var getHashCodeFunc = getHashCodeLambda.Compile();
			getHashCodes.Add(type, getHashCodeFunc);

			return getHashCodeFunc;
		}

		/// <summary>
		/// Returns an expression that accesses the specified member (field or property) on the given parent expression.
		/// </summary>
		/// <param name="expression">The parent expression from which to access the member.</param>
		/// <param name="member">The <see cref="MemberInfo"/> representing the member to access.</param>
		/// <returns>An expression referencing the member on the parent.</returns>
		/// <exception cref="NotSupportedException">Thrown when the member is not a property or field.</exception>
		private Expression PropertyOrField(Expression expression, MemberInfo member)
			=> member switch
			{
				PropertyInfo p => Expression.Property(expression, p),
				FieldInfo f => Expression.Field(expression, f),
				_ => throw new NotSupportedException()
			};
	}
}
