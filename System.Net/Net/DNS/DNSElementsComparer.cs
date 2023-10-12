using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
    public class DNSElementsComparer : IEqualityComparer<DNSElement>
    {
        private Dictionary<Type, Func<DNSElement, DNSElement, bool>> comparers = new();
        private Dictionary<Type, Func<DNSElement, int>> getHashCodes = new();

        private DNSElementsComparer() { }

        public static DNSElementsComparer Default { get; } = new DNSElementsComparer();

        public bool Equals([AllowNull] DNSElement x, [AllowNull] DNSElement y)
        {
            if (x is null && y is null) return true;
            var typeX = x.GetType();
            var typeY = y.GetType();
            if (typeX != typeY) return false;
            if (!comparers.TryGetValue(typeX, out var comparer)) {
                comparer = CreateComparer(typeX);
            }
            return comparer(x, y);
        }

        public int GetHashCode([DisallowNull] DNSElement obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));
            var type = obj.GetType();
            if (!getHashCodes.TryGetValue(type, out var getHashCode))
            {
                getHashCode = CreateGetHasCode(type);
            }
            return getHashCode(obj);
        }

        private Func<DNSElement, DNSElement, bool> CreateComparer(Type type) {
            List<Expression> comparer = new List<Expression>();
            var param1 = Expression.Parameter(typeof(DNSElement), "param1");
            var param2 = Expression.Parameter(typeof(DNSElement), "param2");
            var variable1 = Expression.Variable(type, "variable1");
            var variable2 = Expression.Variable(type, "variable2");
            var comparison = Expression.Variable(typeof(bool), "comparison");
            
            comparer.Add(Expression.Assign(comparison, Expression.Constant(true)));
            comparer.Add(Expression.Assign(variable1, Expression.Convert(param1, type)));
            comparer.Add(Expression.Assign(variable2, Expression.Convert(param2, type)));

            foreach (var field in DNSPacketHelpers.GetDNSFields(type))
            {
                Expression member1 = PropertyOrField(variable1, field.Member);

                if (member1.Type.IsArray)
                {
                    Expression member2 = PropertyOrField(variable2, field.Member);
                    var elementType = member1.Type.GetElementType();
                    var lengthMethod = member1.Type.GetMethod("Length");
                    comparer.Add(Expression.Assign(comparison, Expression.Equal(Expression.Call(member1, lengthMethod), Expression.Call(member2, lengthMethod))));
                    
                    var variableI = Expression.Variable(typeof(int), "i");
                    var variableLength = Expression.Variable(typeof(int), "length");

                    var @break = Expression.Label("break");

                    var equalsMethod = member1.Type.GetMethod("Equals", [member1.Type]);
                    Expression comparerExpression;
                    if (equalsMethod.GetParameters()[0].ParameterType == typeof(object))
                    {
                        comparerExpression = Expression.Assign(comparison, Expression.AndAlso(comparison, Expression.Call(Expression.ArrayIndex(member1, variableI), equalsMethod, [Expression.Convert(Expression.ArrayIndex(member2, variableI), typeof(object))])));
                    }
                    else
                    {
                        comparerExpression = Expression.Assign(comparison, Expression.AndAlso(comparison, Expression.Call(Expression.ArrayIndex(member1, variableI), equalsMethod, [Expression.ArrayIndex(member2, variableI)])));
                    }

                    comparer.Add(
                        Expression.IfThen(
                            comparison,
                            Expression.Block(
                                Expression.Assign(variableI, Expression.Constant(0)),
                                Expression.Assign(variableLength, Expression.Call(member1, lengthMethod)),
                                Expression.Loop(
                                    Expression.Block(
                                        Expression.IfThen(Expression.GreaterThanOrEqual(variableI, variableLength), Expression.Break (@break)),
                                        Expression.Assign(
                                            comparison, 
                                            Expression.AndAlso(
                                                comparison, Expression.Call(Expression.ArrayIndex(member1, variableI), equalsMethod, Expression.ArrayIndex(member2, variableI))
                                            )
                                        ),
                                        Expression.IfThen(Expression.Not(comparison), Expression.Break(@break)),
                                        Expression.AddAssign(variableI, Expression.Constant(1))
                                    ),
                                    @break
                                )
                            )
                        )
                    );
                }
                else
                {
                    var equalsMethod = member1.Type.GetMethod("Equals", [member1.Type]);
                    if (equalsMethod.GetParameters()[0].ParameterType == typeof(object)) {
                        comparer.Add(Expression.Assign(comparison, Expression.AndAlso(comparison, Expression.Call(member1, equalsMethod, [Expression.Convert(PropertyOrField(variable2, field.Member), typeof(object))]))));
                    } else {
                        comparer.Add(Expression.Assign(comparison, Expression.AndAlso(comparison, Expression.Call(member1, equalsMethod, [PropertyOrField(variable2, field.Member)]))));
                    }

                }
            }
            comparer.Add(comparison);
            var comparerLambda = Expression.Lambda<Func<DNSElement, DNSElement, bool>>(
                Expression.Block(
                    typeof(bool), 
                    [variable1, variable2, comparison], 
                    comparer
                ),
                "Compare" + type.Name,
                [param1, param2]
            );
            var comparerFunc = comparerLambda.Compile();
            this.comparers.Add(type, comparerFunc); 
            return comparerFunc;
        }

        private Func<DNSElement, int> CreateGetHasCode(Type type)
        {
            List<Expression> getHashCode = new List<Expression>();


            var param = Expression.Parameter(typeof(DNSElement), "param");
            var variable = Expression.Variable(type, "variable");
            var hashCode = Expression.Variable(typeof(int), "hashCode");


            getHashCode.Add(Expression.Assign(hashCode, Expression.Constant(31)));
            getHashCode.Add(Expression.Assign(variable, Expression.Convert(param, type)));

            foreach (var field in DNSPacketHelpers.GetDNSFields(type))
            {
                Expression member = PropertyOrField(variable, field.Member);
                if (member.Type.IsArray)
                {
                    var variableI = Expression.Variable(typeof(int), "i");
                    var variableLength = Expression.Variable(typeof(int), "length");
                    var lengthMethod = member.Type.GetMethod("Length");
                    var @break = Expression.Label("break");

                    getHashCode.Add(
                        Expression.Block(
                            Expression.Assign(variableI, Expression.Constant(0)),
                            Expression.Assign(variableLength, Expression.Call(member, lengthMethod)),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.IfThen(Expression.GreaterThanOrEqual(variableI, variableLength), Expression.Break(@break)),
                                    Expression.Assign(hashCode, Expression.Add(Expression.Multiply(hashCode, Expression.Constant(27)), Expression.Call(Expression.ArrayIndex(member, variableI), nameof(GetHashCode), [], []))),
                                    Expression.AddAssign(variableI, Expression.Constant(1))
                                ),
                                @break
                            )
                        )
                    );
                }
                else
                {
                    getHashCode.Add(Expression.Assign(hashCode, Expression.Add(Expression.Multiply(hashCode, Expression.Constant(27)), Expression.Call(PropertyOrField(variable, field.Member), nameof(GetHashCode), [], []))));
                }
            }

            getHashCode.Add(hashCode);


            var getHashCodeLambda = Expression.Lambda<Func<DNSElement, int>>(
                Expression.Block(
                    typeof(int),
                    [variable, hashCode],
                    getHashCode
                ),
                "GetHashCode" + type.Name,
                [param]
            );

            var getHashCodeFunc = getHashCodeLambda.Compile();

            this.getHashCodes.Add(type, getHashCodeFunc);
            return getHashCodeFunc;
        }

        private Expression PropertyOrField(Expression expression, MemberInfo member)
            => member switch
            {
                PropertyInfo p => Expression.Property(expression, p),
                FieldInfo f => Expression.Field(expression, f),
                _=> throw new NotSupportedException()
            };

        private Type GetTypeOf(MemberInfo member)
            => member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                _ => throw new NotSupportedException()
            };
    }
}
