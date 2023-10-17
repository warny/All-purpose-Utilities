using System;
using System.Collections;
using System.Collections.Generic;
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
        Expression left = CreateMember(obj, type, match.Groups["left"].Value);

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
            right = CreateMember(obj, type, match.Groups["right"].Value);
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

    private static Expression CreateMember(Expression obj, Type type, string name)
    {
        var member = type.GetMember(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(m => m is FieldInfo || m is PropertyInfo)
            ?? throw new MissingMemberException(type.FullName, name);

        if (member != null)
        {
            return member switch
            {
                FieldInfo f => Expression.Field(obj, f),
                PropertyInfo p => Expression.Property(obj, p),
                _ => throw new MissingMemberException(type.FullName, name)
            };
        }
        if (obj.Type.IsEnum)
        {
            var field = obj.Type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new MissingMemberException(type.FullName, name);
            return Expression.Constant(field.GetValue(null), obj.Type);
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


}