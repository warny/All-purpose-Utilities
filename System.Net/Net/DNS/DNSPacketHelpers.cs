using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Net.DNS;

namespace Utils.Net.DNS;

internal static class DNSPacketHelpers
{
    static Regex simpleConditionRegex = new(@"\s*(?<left>\w+)\s*(?<comparison>(?<operator>==|!=|\<\>|\<=|\>=|=|\<|\>)\s*((?<constant>true|false)|(?<number>(0b|0x)?\d+(\.\d+)?)|\""(?<string>(\""\""|[^\""])*)\""|(?<right>\w+))\s*)?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public static IEnumerable<(DNSFieldAttribute Attribute, MemberInfo Member)> GetDNSFields(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (!typeof(DNSElement).IsAssignableFrom(type)) throw new ArgumentException($"{type.FullName} is not a DNSElement", nameof(type));

        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo))
        {
            var attribute = member.GetCustomAttribute<DNSFieldAttribute>();
            if (attribute is null) continue;

            yield return (attribute, member);
        }
    }


    public static Expression ConditionBuilder(Expression obj, string condition)
    {
        var type = obj.Type;
        int lastPosition = 0;
        int position = 0;
        char booleanOperand = '\0';
        BinaryExpressionBuilder builder = new BinaryExpressionBuilder();
        BinaryExpressionBuilder lastRigthTree = null;
        while ((position = condition.IndexOfAny(['&', '|'], lastPosition)) > 0)
        {
            Expression result = ReadOperation(obj, condition, type, lastPosition, position);
            AddToTree(result, booleanOperand, ref builder, ref lastRigthTree);

            booleanOperand = condition[position];
            lastPosition = position + 1;
        }
        Expression result2 = ReadOperation(obj, condition, type, lastPosition, condition.Length);
        AddToTree(result2, booleanOperand, ref builder, ref lastRigthTree);

        var expression = builder.Build();
        return expression;
    }

    private static Expression ReadOperation(Expression obj, string condition, Type type, int lastPosition, int position)
    {
        var length = position - lastPosition;
        Match match = simpleConditionRegex.Match(condition, lastPosition, length);
        if (!match.Success) throw new NotSupportedException($"{condition.Substring(lastPosition, position - 1)} is not a valid condition");
        if (match.Length != length) throw new NotSupportedException($"{condition.Substring(lastPosition, position - 1)} is not a valid condition");

        //traite la valeur de gauche
        Expression left = CreateMember(obj, type, null, match.Groups["left"].Value);

        if (match.Groups["comparison"].Success)
        {
            if (CreateComparison(obj, type, match, left, out var result))
            {
                return result;
            }
            throw new NotSupportedException($"{condition.Substring(lastPosition, position - 1)} is not a valid condition");
        }
        
        if (left.Type == typeof(bool))
        {
            return left;
        }
        throw new NotSupportedException($"{left} is not a boolean in {match.Value.Trim()} at {lastPosition}");

    }

    private static void AddToTree(Expression result, char booleanOperand, ref BinaryExpressionBuilder builder, ref BinaryExpressionBuilder lastRigthTree)
    {
        if (booleanOperand == '\0')
        {
            builder.Left = result;
            return;
        }
        
        if (builder.Right is null)
        {
            builder.Right = new BinaryExpressionBuilder { Left = result };
            builder.Operand = booleanOperand;
            lastRigthTree = builder;
            return;
        }
        
        if (booleanOperand == '&')
        {
            builder.Right = new BinaryExpressionBuilder
            {
                Left = builder.Right.Build(),
                Right = new BinaryExpressionBuilder { Left = result },
                Operand = booleanOperand
            };
            lastRigthTree = builder.Right;
            return;
        }
        
        if (booleanOperand == '|')
        {
            lastRigthTree.Right = new BinaryExpressionBuilder
            {
                Left = builder.Right.Build(),
                Right = new BinaryExpressionBuilder { Left = result },
                Operand = booleanOperand
            };
            lastRigthTree = lastRigthTree.Right;
            return;
        }
    }

    private static bool CreateComparison(Expression obj, Type type, Match match, Expression left, out Expression result)
    {
        Expression right;
        if (match.Groups["constant"].Success)
        {
            right = CreateConstant(match.Groups["constant"].Value);
        }
        else if (match.Groups["number"].Success)
        {
            right = CreateNumber(match.Groups["number"].Value, left.Type);
        }
        else if (match.Groups["string"].Success)
        {
            right = CreateStringOrConstructor(match.Groups["string"].Value.Replace("\"\"", "\""), left.Type);
        }
        else if (match.Groups["right"].Success)
        {
            right = CreateMember(obj, type, left.Type, match.Groups["right"].Value);
        }
        else
        {
            result = null;
            return false;
        }
        result = CreateComparison(match.Groups["operator"].Value, left, right);
        return true;
    }

    private static Expression CreateStringOrConstructor(string str, Type type)
    {
        ConstantExpression strConst = Expression.Constant(str);
        if (type == typeof(string)) return strConst;

        var constructor = type.GetConstructor([typeof(string)]);
        if (constructor != null) return Expression.New(constructor, strConst);

        var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.ReturnType == type).FirstOrDefault(m=>m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        if (method != null) return Expression.Call(null, method, strConst);

        throw new NotSupportedException($"{type.FullName} can't be converted from string");
    }

    private static Expression CreateNumber(string str, Type type)
    {
        var result = type.InvokeMember("Parse", BindingFlags.Static | BindingFlags.Public, null, null, [str]);
        return Expression.Constant(result, type);
    }

    private static Expression CreateMember(Expression obj, Type type, Type leftType, string name)
    {
        var member = type.GetMember(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(m => m is FieldInfo || m is PropertyInfo);

        if (member != null)
        {
            return member switch
            {
                FieldInfo f => Expression.Field(obj, f),
                PropertyInfo p => Expression.Property(obj, p),
                _ => throw new MissingMemberException(type.FullName, name)
            };
        }
        if (leftType is not null && leftType.IsEnum)
        {
            var value = Enum.Parse(leftType, name, true);
            return Expression.Constant(value, leftType);
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static Expression CreateConstant(string constant)
    {
        return Expression.Constant(
            constant.ToLower() switch
            {
                "true" => Expression.Constant(true, typeof(Boolean)),
                "false" => Expression.Constant(false, typeof(Boolean)),
                _ => throw new NotSupportedException($"{constant} is not supported")
            }
        );
    }

    private static Expression CreateComparison(string operand, Expression left, Expression right) {
        return operand switch
        {
            "=" or "==" => Expression.Equal(left, right),
            "<>" or "!=" => Expression.NotEqual(left, right),
            "<" => Expression.LessThan(left, right),
            ">" => Expression.GreaterThan(left, right),
            "<=" => Expression.LessThanOrEqual(left, right),
            ">=" => Expression.GreaterThanOrEqual(left, right),
            _ => throw new NotSupportedException($" {operand} comparison operand is not supported")
        };
    }
                         
    private class BinaryExpressionBuilder
    {
        public Expression Left { get; set; }
        public char Operand { get; set; }
        public BinaryExpressionBuilder Right { get; set; }

        public Expression Build()
        {
            return Operand switch
            {
                '&' => Expression.AndAlso(Left, Right.Build()),
                '|' => Expression.OrElse(Left, Right.Build()),
                '\0' => Left,
                _ => throw new NotSupportedException($" {Operand} comparison operand is not supported")
            };
        }
    }


    public static Expression CreateExpressionCall(Expression expression, string name, params Expression[] arguments)
    {
        Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
        var method = expression.Type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, argumentTypes, null);
        return Expression.Call(expression, method, arguments);
    }

    /// <summary>
    /// Try to get a conversion method from an expression or the target type
    /// This function searches 
    /// </summary>
    /// <param name="source">Source expression to get a convert from</param>
    /// <param name="outType">Target Type to get a convert to</param>
    /// <param name="builder">Resulting expression</param>
    /// <returns></returns>
    public static bool TryGetConverter(Expression source, Type outType, out Expression builder)
    {
        var methodsInstance = source.Type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 0)
            .ToArray();

        foreach (var method in new MethodInfo[] {
                methodsInstance.FirstOrDefault(m => m.Name.StartsWith("As")),
                methodsInstance.FirstOrDefault(m => m.Name.StartsWith("To")),
                methodsInstance.FirstOrDefault(),
            }.Where(m => m is not null))
        {
                builder = Expression.Call(source, method);
                return true;
        }

        var methodsStatic = source.Type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .ToArray();

        var methodsTarget = outType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .ToArray();

        foreach (var method in new MethodInfo[] {
                methodsStatic.FirstOrDefault(m => m.Name.StartsWith("As")),
                methodsStatic.FirstOrDefault(m => m.Name.StartsWith("To")),
                methodsStatic.FirstOrDefault(),
                methodsTarget.FirstOrDefault(m => m.Name.StartsWith("From")),
                methodsTarget.FirstOrDefault(m => m.Name.StartsWith("Parse")),
                methodsTarget.FirstOrDefault(),
            }.Where(m => m is not null))
        {
            builder = Expression.Call(method, source);
            return true;
        }

        var constructorTarget = outType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
            .Where(c => c is not null)
            .ToArray();

        foreach (var constructor in constructorTarget)
        {
            builder = Expression.New(constructor, source);
            return true;
        }

        builder = null;
        return false;
    }


    public static Type GetUnderlyingType(Type type)
    {
        if (type.IsEnum) return type.GetEnumUnderlyingType();

        if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition())
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }


}