using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Net.DNS;

/// <summary>
/// Builds expression trees used to evaluate DNS field conditions.
/// </summary>
internal static class DNSExpression
{
    /// <summary>
    /// Parses the provided condition and returns an executable boolean expression.
    /// </summary>
    /// <param name="element">Expression representing the DNS record instance.</param>
    /// <param name="expression">Condition text from <see cref="DNSFieldAttribute.Condition"/>.</param>
    /// <returns>Boolean expression used by DNS reader/writer pipelines.</returns>
    public static Expression BuildExpression(Expression element, string expression)
    {
        if (element is ParameterExpression parameterExpression)
        {
            return BuildConditionExpression(parameterExpression, expression);
        }

        var variable = Expression.Variable(element.Type, "defaultValue");
        return Expression.Block(
            [variable],
            Expression.Assign(variable, element),
            BuildConditionExpression(variable, expression)
        );
    }

    /// <summary>
    /// Converts a DNS condition string into an expression tree.
    /// </summary>
    /// <param name="element">Root expression used to resolve member accesses.</param>
    /// <param name="condition">Condition text to parse.</param>
    /// <returns>Parsed condition as an expression tree.</returns>
    private static Expression BuildConditionExpression(Expression element, string condition)
    {
        var trimmedCondition = condition.Trim();
        if (trimmedCondition.Length == 0)
        {
            throw new InvalidOperationException("DNS field condition cannot be empty.");
        }

        var equalOperatorIndex = trimmedCondition.IndexOf("==", StringComparison.Ordinal);
        if (equalOperatorIndex >= 0)
        {
            var leftToken = trimmedCondition[..equalOperatorIndex].Trim();
            var rightToken = trimmedCondition[(equalOperatorIndex + 2)..].Trim();
            return BuildComparisonExpression(element, leftToken, rightToken, Expression.Equal);
        }

        var notEqualOperatorIndex = trimmedCondition.IndexOf("!=", StringComparison.Ordinal);
        if (notEqualOperatorIndex >= 0)
        {
            var leftToken = trimmedCondition[..notEqualOperatorIndex].Trim();
            var rightToken = trimmedCondition[(notEqualOperatorIndex + 2)..].Trim();
            return BuildComparisonExpression(element, leftToken, rightToken, Expression.NotEqual);
        }

        var standaloneExpression = ResolveToken(element, trimmedCondition);
        if (standaloneExpression.Type == typeof(bool))
        {
            return standaloneExpression;
        }

        if (standaloneExpression.Type == typeof(bool?))
        {
            return Expression.Equal(standaloneExpression, Expression.Constant(true, typeof(bool?)));
        }

        throw new InvalidOperationException($"DNS field condition '{condition}' does not resolve to a boolean expression.");
    }

    /// <summary>
    /// Builds a binary comparison expression from two tokens.
    /// </summary>
    /// <param name="element">Root element used for token resolution.</param>
    /// <param name="leftToken">Left operand token.</param>
    /// <param name="rightToken">Right operand token.</param>
    /// <param name="comparisonFactory">Factory used to create the binary node.</param>
    /// <returns>Binary comparison expression.</returns>
    private static Expression BuildComparisonExpression(
        Expression element,
        string leftToken,
        string rightToken,
        Func<Expression, Expression, BinaryExpression> comparisonFactory)
    {
        var leftExpression = ResolveToken(element, leftToken);
        var rightExpression = ResolveToken(element, rightToken);
        var promotedRightExpression = PromoteExpression(rightExpression, leftExpression.Type);
        return comparisonFactory(leftExpression, promotedRightExpression);
    }

    /// <summary>
    /// Converts an expression to a target type when possible.
    /// </summary>
    /// <param name="expression">Source expression.</param>
    /// <param name="targetType">Target type.</param>
    /// <returns>Converted expression when needed.</returns>
    private static Expression PromoteExpression(Expression expression, Type targetType)
    {
        if (expression.Type == targetType)
        {
            return expression;
        }

        if (expression is ConstantExpression constantExpression
            && TryConvertConstant(constantExpression.Value, targetType, out var convertedValue))
        {
            return Expression.Constant(convertedValue, targetType);
        }

        return Expression.Convert(expression, targetType);
    }

    /// <summary>
    /// Resolves a token into an expression (instance member or static value).
    /// </summary>
    /// <param name="element">Root expression.</param>
    /// <param name="token">Token to resolve.</param>
    /// <returns>Resolved expression.</returns>
    private static Expression ResolveToken(Expression element, string token)
    {
        if (TryResolveMemberPath(element, token, out var memberExpression))
        {
            return memberExpression;
        }

        if (TryResolveStaticValue(token, out var staticValue) && staticValue is not null)
        {
            return Expression.Constant(staticValue, staticValue.GetType());
        }

        throw new InvalidOperationException($"Cannot resolve DNS condition token '{token}'.");
    }

    /// <summary>
    /// Tries to resolve a dotted member path from the DNS element expression.
    /// </summary>
    /// <param name="element">Root expression.</param>
    /// <param name="token">Dotted token text.</param>
    /// <param name="resolvedExpression">Resolved member expression when successful.</param>
    /// <returns><see langword="true"/> if resolved; otherwise <see langword="false"/>.</returns>
    private static bool TryResolveMemberPath(Expression element, string token, out Expression? resolvedExpression)
    {
        resolvedExpression = element;
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var startIndex = string.Equals(parts[0], "defaultValue", StringComparison.Ordinal) ? 1 : 0;
        for (var i = startIndex; i < parts.Length; i++)
        {
            if (!TryResolveInstanceMember(resolvedExpression, parts[i], out resolvedExpression))
            {
                resolvedExpression = null;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tries to resolve a single public instance field or property.
    /// </summary>
    /// <param name="ownerExpression">Expression owning the member.</param>
    /// <param name="memberName">Member name.</param>
    /// <param name="memberExpression">Resolved member expression when found.</param>
    /// <returns><see langword="true"/> if found; otherwise <see langword="false"/>.</returns>
    private static bool TryResolveInstanceMember(Expression ownerExpression, string memberName, out Expression? memberExpression)
    {
        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;

        var property = ownerExpression.Type.GetProperty(memberName, bindingFlags);
        if (property?.GetMethod is not null)
        {
            memberExpression = Expression.Property(ownerExpression, property);
            return true;
        }

        var field = ownerExpression.Type.GetField(memberName, bindingFlags);
        if (field is not null)
        {
            memberExpression = Expression.Field(ownerExpression, field);
            return true;
        }

        memberExpression = null;
        return false;
    }

    /// <summary>
    /// Resolves static enum/field/property tokens of the form <c>Namespace.Type.Member</c>.
    /// </summary>
    /// <param name="token">Token text.</param>
    /// <param name="value">Resolved static value.</param>
    /// <returns><see langword="true"/> when a static value is found; otherwise <see langword="false"/>.</returns>
    private static bool TryResolveStaticValue(string token, out object? value)
    {
        var separatorIndex = token.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
        {
            value = null;
            return false;
        }

        var typeName = token[..separatorIndex];
        var memberName = token[(separatorIndex + 1)..];
        var type = ResolveType(typeName);
        if (type is null)
        {
            value = null;
            return false;
        }

        const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var field = type.GetField(memberName, bindingFlags);
        if (field is not null)
        {
            value = field.GetValue(null);
            return true;
        }

        var property = type.GetProperty(memberName, bindingFlags);
        if (property?.GetMethod is not null)
        {
            value = property.GetValue(null);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Resolves a type from the currently loaded assemblies.
    /// </summary>
    /// <param name="typeName">Fully qualified type name.</param>
    /// <returns>Resolved type, or <see langword="null"/>.</returns>
    private static Type? ResolveType(string typeName)
    {
        var resolvedType = Type.GetType(typeName, throwOnError: false);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            resolvedType = assembly.GetType(typeName, throwOnError: false);
            if (resolvedType is not null)
            {
                return resolvedType;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to convert a constant value to a destination type.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <param name="targetType">Destination type.</param>
    /// <param name="convertedValue">Converted value when successful.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
    private static bool TryConvertConstant(object? value, Type targetType, out object? convertedValue)
    {
        try
        {
            var underlyingTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingTargetType.IsEnum)
            {
                if (value is string stringValue)
                {
                    convertedValue = Enum.Parse(underlyingTargetType, stringValue, ignoreCase: false);
                    return true;
                }

                convertedValue = Enum.ToObject(underlyingTargetType, value!);
                return true;
            }

            convertedValue = Convert.ChangeType(value, underlyingTargetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            convertedValue = null;
            return false;
        }
    }
}
