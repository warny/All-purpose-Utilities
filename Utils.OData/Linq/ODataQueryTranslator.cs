using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace Utils.OData.Linq;

/// <summary>
/// Provides translation from LINQ expression trees into OData query fragments.
/// </summary>
internal static class ODataQueryTranslator
{
    /// <summary>
    /// Translates the specified expression tree into an <see cref="ODataQueryCompilation"/> instance.
    /// </summary>
    /// <param name="expression">The expression tree to translate.</param>
    /// <param name="defaultEntitySetName">The fallback entity set name when no root is explicitly provided.</param>
    /// <returns>The compiled query representation.</returns>
    public static ODataQueryCompilation Translate(Expression expression, string defaultEntitySetName)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        if (string.IsNullOrWhiteSpace(defaultEntitySetName))
        {
            throw new ArgumentException("The entity set name must be provided.", nameof(defaultEntitySetName));
        }

        var visitor = new TranslatorVisitor(defaultEntitySetName);
        visitor.Visit(expression);
        return visitor.CreateCompilation();
    }

    /// <summary>
    /// Visitor that builds the compilation result while traversing the expression tree.
    /// </summary>
    private sealed class TranslatorVisitor : ExpressionVisitor
    {
        private readonly List<string> _filters = new();
        private readonly List<string> _expansions = new();
        private string? _entitySetName;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslatorVisitor"/> class.
        /// </summary>
        /// <param name="defaultEntitySetName">Entity set name used when the root cannot be inferred.</param>
        public TranslatorVisitor(string defaultEntitySetName)
        {
            DefaultEntitySetName = defaultEntitySetName;
        }

        /// <summary>
        /// Gets the default entity set name.
        /// </summary>
        private string DefaultEntitySetName { get; }

        /// <summary>
        /// Creates the final <see cref="ODataQueryCompilation"/> instance once the expression tree has been visited.
        /// </summary>
        /// <returns>The compilation result.</returns>
        public ODataQueryCompilation CreateCompilation()
        {
            string entitySet = _entitySetName ?? DefaultEntitySetName;
            return new ODataQueryCompilation(entitySet, _filters, _expansions);
        }

        /// <inheritdoc />
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is IODataQueryableRoot root)
            {
                _entitySetName = root.EntitySetName;
            }

            return base.VisitConstant(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(ODataQueryableExtensions) && node.Method.Name == nameof(ODataQueryableExtensions.Expand))
            {
                Visit(node.Arguments[0]);
                RegisterExpansions(node.Arguments[1]);
                return node;
            }

            if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == nameof(Queryable.Where))
            {
                Visit(node.Arguments[0]);
                var lambda = StripQuotes(node.Arguments[1]) as LambdaExpression;
                if (lambda is null)
                {
                    throw new NotSupportedException("Unable to extract the predicate from the Where call.");
                }

                string filter = TranslatePredicate(lambda.Body);
                _filters.Add(filter);
                return node;
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Registers the navigation properties expanded by the current method call.
        /// </summary>
        /// <param name="argument">Expression describing the navigation properties.</param>
        private void RegisterExpansions(Expression argument)
        {
            object? value = EvaluateExpression(argument);
            if (value is string property)
            {
                AddExpansion(property);
                return;
            }

            if (value is IEnumerable<string> properties)
            {
                foreach (string item in properties)
                {
                    AddExpansion(item);
                }

                return;
            }

            throw new NotSupportedException("Expand expressions must evaluate to navigation property names.");
        }

        /// <summary>
        /// Adds a navigation property to the list of expansions when it is valid.
        /// </summary>
        /// <param name="property">The navigation property name to add.</param>
        private void AddExpansion(string property)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new NotSupportedException("Expand expressions must provide non-empty navigation property names.");
            }

            if (_expansions.Any(existing => string.Equals(existing, property, StringComparison.Ordinal)))
            {
                return;
            }

            _expansions.Add(property);
        }

        /// <summary>
        /// Removes quote expressions that wrap lambda expressions.
        /// </summary>
        /// <param name="expression">Expression to inspect.</param>
        /// <returns>The unwrapped expression.</returns>
        private static Expression StripQuotes(Expression expression)
        {
            var current = expression;
            while (current.NodeType == ExpressionType.Quote)
            {
                current = ((UnaryExpression)current).Operand;
            }

            return current;
        }

        /// <summary>
        /// Translates a predicate expression into an OData $filter fragment.
        /// </summary>
        /// <param name="expression">Expression describing the predicate.</param>
        /// <returns>The filter fragment.</returns>
        private static string TranslatePredicate(Expression expression)
        {
            if (expression is BinaryExpression binary)
            {
                if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.OrElse)
                {
                    string left = TranslatePredicate(binary.Left);
                    string right = TranslatePredicate(binary.Right);
                    string logical = binary.NodeType == ExpressionType.AndAlso ? "and" : "or";
                    return string.Create(CultureInfo.InvariantCulture, $"({left}) {logical} ({right})");
                }

                return TranslateBinary(binary);
            }

            throw new NotSupportedException($"Unsupported predicate expression of type '{expression.NodeType}'.");
        }

        /// <summary>
        /// Translates a binary expression into a filter fragment.
        /// </summary>
        /// <remarks>
        /// Item 11: when the member (property access) is on the right-hand side and the value is
        /// on the left — for example <c>5 &lt; entity.Quantity</c> — the operands are swapped and
        /// the comparison operator is reversed so the OData fragment always reads
        /// <c>Quantity gt 5</c>.
        /// </remarks>
        /// <param name="binary">Binary expression to translate.</param>
        /// <returns>The resulting filter fragment.</returns>
        private static string TranslateBinary(BinaryExpression binary)
        {
            bool leftIsMember = IsMemberOrColumnAccess(binary.Left);
            bool rightIsMember = IsMemberOrColumnAccess(binary.Right);

            // Normalise: ensure the member expression is always on the left.
            Expression memberSide;
            Expression valueSide;
            bool swapped;

            if (leftIsMember && !rightIsMember)
            {
                memberSide = binary.Left;
                valueSide = binary.Right;
                swapped = false;
            }
            else if (rightIsMember && !leftIsMember)
            {
                memberSide = binary.Right;
                valueSide = binary.Left;
                swapped = true;
            }
            else if (leftIsMember)
            {
                // Both sides are members: emit left op right without swapping.
                memberSide = binary.Left;
                valueSide = binary.Right;
                swapped = false;
            }
            else
            {
                throw new NotSupportedException(
                    "At least one operand of a comparison expression must be a member or column access.");
            }

            string operatorToken = (binary.NodeType, swapped) switch
            {
                (ExpressionType.Equal, _) => "eq",
                (ExpressionType.NotEqual, _) => "ne",
                (ExpressionType.GreaterThan, false) => "gt",
                (ExpressionType.GreaterThan, true) => "lt",   // 5 > x  →  x lt 5
                (ExpressionType.GreaterThanOrEqual, false) => "ge",
                (ExpressionType.GreaterThanOrEqual, true) => "le", // 5 >= x →  x le 5
                (ExpressionType.LessThan, false) => "lt",
                (ExpressionType.LessThan, true) => "gt",      // 5 < x  →  x gt 5
                (ExpressionType.LessThanOrEqual, false) => "le",
                (ExpressionType.LessThanOrEqual, true) => "ge", // 5 <= x →  x ge 5
                _ => throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported.")
            };

            string member = TranslateMember(memberSide);
            string value = rightIsMember && leftIsMember
                ? TranslateMember(valueSide)
                : TranslateValue(valueSide);
            return string.Create(CultureInfo.InvariantCulture, $"{member} {operatorToken} {value}");
        }

        /// <summary>
        /// Returns <see langword="true"/> when the expression is an OData column access:
        /// either a member chain that roots at the lambda parameter (entity property), or an
        /// untyped row column accessor.
        /// </summary>
        /// <remarks>
        /// A <see cref="MemberExpression"/> whose root is a <see cref="ConstantExpression"/> (the
        /// compiler-generated closure class) is a captured local variable and must be evaluated,
        /// not translated as an OData path.  We only consider a member chain an OData access when
        /// it traces back to a <see cref="ParameterExpression"/>.
        /// </remarks>
        private static bool IsMemberOrColumnAccess(Expression expression)
        {
            Expression inner = RemoveConvert(expression);

            if (inner is MemberExpression)
                return IsMemberDependentOnParameter(inner);

            if (inner is MethodCallExpression mc && IsColumnAccessor(mc))
                return true;

            if (inner is IndexExpression)
                return true;

            return false;
        }

        /// <summary>
        /// Walks a member-access chain upward and returns <see langword="true"/> when the root
        /// of the chain is a <see cref="ParameterExpression"/> (the lambda entity parameter),
        /// meaning this is an OData property path rather than a captured closure field.
        /// </summary>
        private static bool IsMemberDependentOnParameter(Expression expression)
        {
            Expression current = RemoveConvert(expression);
            while (current is MemberExpression member)
            {
                if (member.Expression is null)
                    return false;
                current = RemoveConvert(member.Expression);
            }
            return current is ParameterExpression;
        }

        /// <summary>
        /// Extracts the member name referenced by an expression.
        /// </summary>
        /// <param name="expression">Expression describing the member access.</param>
        /// <returns>The member name.</returns>
        private static string TranslateMember(Expression expression)
        {
            if (expression is null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            Expression current = RemoveConvert(expression);

            if (current is MemberExpression memberExpression)
            {
                return BuildMemberPath(memberExpression);
            }

            if (current is MethodCallExpression methodCall && IsColumnAccessor(methodCall))
            {
                return ExtractColumnName(methodCall.Arguments);
            }

            if (current is IndexExpression indexExpression)
            {
                return ExtractColumnName(indexExpression.Arguments);
            }

            throw new NotSupportedException("Only direct member access or untyped column access expressions are currently supported.");
        }

        /// <summary>
        /// Removes conversion expressions that wrap a given expression.
        /// </summary>
        /// <param name="expression">Expression to simplify.</param>
        /// <returns>The simplified expression.</returns>
        private static Expression RemoveConvert(Expression expression)
        {
            Expression current = expression;
            while (current.NodeType == ExpressionType.Convert || current.NodeType == ExpressionType.ConvertChecked || current.NodeType == ExpressionType.TypeAs)
            {
                current = ((UnaryExpression)current).Operand;
            }

            return current;
        }

        /// <summary>
        /// Builds the navigation path represented by a member expression chain.
        /// </summary>
        /// <param name="memberExpression">The member expression describing the access chain.</param>
        /// <returns>The formatted navigation path.</returns>
        private static string BuildMemberPath(MemberExpression memberExpression)
        {
            if (memberExpression is null)
            {
                throw new ArgumentNullException(nameof(memberExpression));
            }

            var segments = new Stack<string>();
            Expression? current = memberExpression;

            while (current is MemberExpression member)
            {
                segments.Push(member.Member.Name);

                Expression? parent = member.Expression;
                if (parent is null)
                {
                    break;
                }

                parent = RemoveConvert(parent);

                if (parent is ParameterExpression or ConstantExpression)
                {
                    break;
                }

                if (parent is MemberExpression parentMember)
                {
                    current = parentMember;
                    continue;
                }

                if (parent is MethodCallExpression methodCall && IsColumnAccessor(methodCall))
                {
                    segments.Push(ExtractColumnName(methodCall.Arguments));
                    break;
                }

                if (parent is IndexExpression indexExpression)
                {
                    segments.Push(ExtractColumnName(indexExpression.Arguments));
                    break;
                }

                throw new NotSupportedException("Unsupported member access chain encountered while building a navigation path.");
            }

            return string.Join('/', segments);
        }

        /// <summary>
        /// Determines whether the provided method call represents an untyped column access.
        /// </summary>
        /// <param name="methodCall">Method call expression to inspect.</param>
        /// <returns><see langword="true"/> when the call accesses a column.</returns>
        private static bool IsColumnAccessor(MethodCallExpression methodCall)
        {
            if (methodCall is null)
            {
                throw new ArgumentNullException(nameof(methodCall));
            }

            if (methodCall.Method.IsSpecialName && methodCall.Method.Name == "get_Item" && methodCall.Arguments.Count == 1)
            {
                return true;
            }

            if (methodCall.Method.Name == nameof(ODataUntypedRow.GetValue) && methodCall.Arguments.Count == 1)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the column name from the provided indexer or accessor call arguments.
        /// </summary>
        /// <param name="arguments">Arguments supplied to the accessor.</param>
        /// <returns>The resolved column name.</returns>
        private static string ExtractColumnName(IReadOnlyList<Expression> arguments)
        {
            if (arguments.Count != 1)
            {
                throw new NotSupportedException("Column accessors must receive exactly one argument representing the column name.");
            }

            object? argumentValue = EvaluateExpression(arguments[0]);
            if (argumentValue is string columnName)
            {
                return columnName;
            }

            throw new NotSupportedException("Column accessors must be invoked with a constant column name.");
        }

        /// <summary>
        /// Converts an expression representing a value into its OData literal representation.
        /// </summary>
        /// <param name="expression">Expression that evaluates to a value.</param>
        /// <returns>The formatted literal.</returns>
        private static string TranslateValue(Expression expression)
        {
            object? value = EvaluateExpression(expression);
            return FormatLiteral(value);
        }

        /// <summary>
        /// Evaluates an expression into a CLR value using a safe, restricted subset.
        /// </summary>
        /// <remarks>
        /// Item 10: only constants, compiler-generated closure <em>field</em> reads, numeric/enum
        /// type conversions, <c>TypeAs</c>, and new-array initialisers are supported.
        /// <list type="bullet">
        ///   <item><description>
        ///     <b>Property getters are explicitly rejected</b>: they are arbitrary user code that
        ///     can modify state, perform I/O, or throw. Assign the value to a local variable before
        ///     building the LINQ query.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Failed conversions are not silenced</b>: enum, nullable, and numeric conversions
        ///     that cannot succeed propagate their exception rather than returning the wrong type.
        ///   </description></item>
        ///   <item><description>
        ///     Method calls and all other expression forms are rejected with
        ///     <see cref="NotSupportedException"/>.
        ///   </description></item>
        /// </list>
        /// </remarks>
        /// <param name="expression">Expression to evaluate.</param>
        /// <returns>The resulting value.</returns>
        private static object? EvaluateExpression(Expression expression)
        {
            if (expression is ConstantExpression constant)
                return constant.Value;

            // TypeAs: return null if the value is not assignable to the target type; never throws.
            if (expression is UnaryExpression typeAs && typeAs.NodeType == ExpressionType.TypeAs)
            {
                object? inner = EvaluateExpression(typeAs.Operand);
                return (inner is not null && typeAs.Type.IsInstanceOfType(inner)) ? inner : null;
            }

            // Numeric / enum / nullable type conversions.
            if (expression is UnaryExpression convert
                && (convert.NodeType == ExpressionType.Convert || convert.NodeType == ExpressionType.ConvertChecked))
            {
                object? inner = EvaluateExpression(convert.Operand);
                if (inner is null) return null;

                Type targetType = convert.Type;
                // Nullable<T>: convert to the underlying type; boxing handles re-wrapping.
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    targetType = targetType.GetGenericArguments()[0];
                // Enum: use Enum.ToObject to produce the correct named enum instance.
                if (targetType.IsEnum)
                    return Enum.ToObject(targetType, inner);
                // All other numeric and reference conversions; OverflowException propagates for checked nodes.
                return System.Convert.ChangeType(inner, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }

            // Compiler-generated closure field reads (item 10).
            // ONLY FieldInfo is allowed: compiler-generated closures store captured variables as fields.
            // PropertyInfo is REJECTED because property getters are arbitrary user code.
            if (expression is MemberExpression member && member.Expression is not null)
            {
                object? target = EvaluateExpression(member.Expression);
                if (member.Member is System.Reflection.FieldInfo field)
                    return field.GetValue(target);
                if (member.Member is System.Reflection.PropertyInfo)
                    throw new NotSupportedException(
                        $"Property getter '{member.Member.DeclaringType?.Name}.{member.Member.Name}' " +
                        "cannot be executed during query compilation because property getters are arbitrary " +
                        "user code. Assign the value to a local variable before building the LINQ query.");
                throw new NotSupportedException(
                    $"Member '{member.Member.Name}' of kind '{member.Member.MemberType}' cannot be safely evaluated.");
            }

            // Array initialisation (e.g., new[] { "A", "B" } in Expand calls).
            if (expression is NewArrayExpression newArray && newArray.NodeType == ExpressionType.NewArrayInit)
            {
                object?[] items = newArray.Expressions.Select(EvaluateExpression).ToArray();
                Type elementType = newArray.Type.GetElementType()!;
                System.Array array = System.Array.CreateInstance(elementType, items.Length);
                for (int i = 0; i < items.Length; i++)
                    array.SetValue(items[i], i);
                return array;
            }

            throw new NotSupportedException(
                $"Expression of kind '{expression.NodeType}' cannot be safely evaluated during query compilation. " +
                "Only constants, closure field reads, type conversions, TypeAs, and new-array initialisers are supported. " +
                "Pre-evaluate method calls or complex expressions before building the LINQ query.");
        }

        /// <summary>
        /// Formats a CLR value into its OData literal representation.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>The formatted literal string.</returns>
        private static string FormatLiteral(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            return value switch
            {
                string text => $"'{EscapeString(text)}'",
                bool boolean => boolean ? "true" : "false",
                DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o", CultureInfo.InvariantCulture),
                Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
                // Item 12: OData enum literals use the member name, not an integer.
                // Full metadata-aware qualified format (Namespace.EnumType'Member') requires
                // EDM type information that is not available at LINQ compile time; using the
                // member name alone is accepted by most services and is at minimum correct for
                // services that expose enums as strings.  Callers needing the fully qualified
                // form should pre-convert the enum value to a string before building the query.
                Enum enumValue => $"'{EscapeString(enumValue.ToString())}'",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// Escapes quotes in string literals.
        /// </summary>
        /// <param name="value">The string literal to escape.</param>
        /// <returns>The escaped string.</returns>
        private static string EscapeString(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }
    }
}
