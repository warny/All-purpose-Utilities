using System.Globalization;
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
            return new ODataQueryCompilation(entitySet, _filters);
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
        /// <param name="binary">Binary expression to translate.</param>
        /// <returns>The resulting filter fragment.</returns>
        private static string TranslateBinary(BinaryExpression binary)
        {
            string operatorToken = binary.NodeType switch
            {
                ExpressionType.Equal => "eq",
                ExpressionType.NotEqual => "ne",
                ExpressionType.GreaterThan => "gt",
                ExpressionType.GreaterThanOrEqual => "ge",
                ExpressionType.LessThan => "lt",
                ExpressionType.LessThanOrEqual => "le",
                _ => throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported.")
            };

            string left = TranslateMember(binary.Left);
            string right = TranslateValue(binary.Right);
            return string.Create(CultureInfo.InvariantCulture, $"{left} {operatorToken} {right}");
        }

        /// <summary>
        /// Extracts the member name referenced by an expression.
        /// </summary>
        /// <param name="expression">Expression describing the member access.</param>
        /// <returns>The member name.</returns>
        private static string TranslateMember(Expression expression)
        {
            if (expression is UnaryExpression unary && expression.NodeType == ExpressionType.Convert)
            {
                return TranslateMember(unary.Operand);
            }

            if (expression is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            throw new NotSupportedException("Only direct member access expressions are currently supported.");
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
        /// Evaluates an expression into a CLR value.
        /// </summary>
        /// <param name="expression">Expression to evaluate.</param>
        /// <returns>The resulting value.</returns>
        private static object? EvaluateExpression(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)));
            return lambda.Compile().Invoke();
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
                Enum enumValue => Convert.ToInt64(enumValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
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
