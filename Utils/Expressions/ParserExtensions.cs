using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions
{
    internal static class ParserExtensions
    {
        private static AppDomain CurrentDomain { get; } = (AppDomain)typeof(string).GetTypeInfo().Assembly.GetType("System.AppDomain").GetRuntimeProperty("CurrentDomain").GetMethod.Invoke(null, new object[] { });


        internal static bool IsName(this string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;
            return char.IsLetter(symbol[0]) || symbol[0] == '_' || symbol[0] == '$';
        }

        public static Type ToNullableIf(this Type type, bool setNullable)
        {
            if (type == null) return type;
            if (!setNullable) return type;
            return typeof(Nullable<>).MakeGenericType(type);
        }

        public static Type GetTypeCore(string typeName)
        {
            var assemblies = CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        public static (Expression left, Expression right) AdjustNumberType(this ParserOptions options, Expression left, Expression right)
        {
            if (left.Type == right.Type) return (left, right);
            if (!options.NumberTypeLevel.TryGetValue(left.Type, out int leftLevel)) return (left, right);
            if (!options.NumberTypeLevel.TryGetValue(right.Type, out int rightLevel)) return (left, right);

            if (leftLevel > rightLevel)
            {
                right = options.AdjustType(right, left.Type);
            }
            else
            {
                left = options.AdjustType(left, right.Type);
            }
            return (left, right);
        }

        public static Expression AdjustNumberType(this ParserOptions options, Type type, Expression right)
        {
            if (type == right.Type) return right;
            if (!options.NumberTypeLevel.TryGetValue(type, out int leftLevel)) return right;
            if (!options.NumberTypeLevel.TryGetValue(right.Type, out int rightLevel)) return right;

            if (leftLevel > rightLevel)
            {
                right = options.AdjustType(right, type);
            }
            return right;
        }

        public static Expression AdjustType(this ParserOptions options, Expression expression, Type type)
        {
            if (expression is ConstantExpression ce
                && options.NumberTypeLevel.TryGetValue(ce.Type, out var constLevel)
                && options.NumberTypeLevel.TryGetValue(type, out var typeLevel)
                && constLevel < typeLevel)
            {
                return Expression.Constant(Convert.ChangeType(ce.Value, type), type);
            }
            return Expression.Convert(expression, type);
        }

        public static string GetBracketString(ParserContext context, Parenthesis markers, bool hasReadPre)
        {
            // Read the opening parenthesis if it hasn't been read previously
            if (!hasReadPre) context.Tokenizer.ReadSymbol(markers.Start);
            int startPosition = context.Tokenizer.Position.Index + markers.Start.Length;

            int depth = 1;
            // Read the content inside the brackets
            StringBuilder sb = new();
            string str;
            while (depth > 0) 
            {
                str = context.Tokenizer.ReadToken(true);
                // If an opening parenthesis is encountered, it indicates nested brackets. Revert and return null.
                if (str == markers.Start) depth++;
                if (str == markers.End) depth--;
            }
            int endPosition = context.Tokenizer.Position.Index - markers.End.Length + 1;
            return context.Tokenizer.Content[startPosition..endPosition];
        }

        public static int GetOperatorLevel(this ParserOptions options, string operatorSymbol, bool isBefore)
        {
            operatorSymbol += operatorSymbol switch
            {
                "++" or "--" => isBefore ? "before" : "behind",
                "+" or "-" => isBefore ? "before" : "",
                _ => ""
            };
            if (!options.OperatorPriorityLevel.TryGetValue(operatorSymbol, out var result)) result = 100;
            return result;
        }

        public static int CompareParametersAndTypes(this MethodInfo methodInfo, Expression obj, IEnumerable<Type> toAssign)
        {
            ParameterInfo[] toBeAssigned = methodInfo.GetParameters();
            return CompareParametersAndTypes(obj, toAssign, toBeAssigned);
        }

        public static int CompareParametersAndTypes(this ConstructorInfo constructorInfo, IEnumerable<Type> toAssign)
        {
            ParameterInfo[] toBeAssigned = constructorInfo.GetParameters();
            return CompareParametersAndTypes(null, toAssign, toBeAssigned);
        }

        public static int CompareParametersAndTypes(Expression obj, IEnumerable<Type> toAssign, ParameterInfo[] toBeAssigned)
        {
            int totalDistance = 0;

            var eToBeAssigned = ((IEnumerable<ParameterInfo>)toBeAssigned).GetEnumerator();

            var eToAssign = toBeAssigned.Any() && toBeAssigned.IsExtension()
                ? toAssign.Prepend(obj.Type).GetEnumerator()
                : toAssign.GetEnumerator();

            ParameterInfo toBeAssignLast;
            bool bToBeAssigned, bToAssign;
            while ((bToBeAssigned = eToBeAssigned.MoveNext()) & (bToAssign = eToAssign.MoveNext()))
            {
                toBeAssignLast = eToBeAssigned.Current;
                if (toBeAssignLast.HasParams())
                {
                    var paramType = toBeAssignLast.ParameterType.GetElementType();
                    // case of a param table at the end of a Method parameters declaration
                    // we first check if an array has been passed directly
                    if (eToAssign.Current.IsArray && eToAssign.Current.GetElementType().ToString() == paramType.ToString())
                    {
                        var arrayDistance = toBeAssignLast.ParameterType.GetTypeDistance(eToAssign.Current);
                        if (arrayDistance != -1)
                        {
                            totalDistance += arrayDistance;
                            return totalDistance;
                        }
                    }

                    // if not, we check elements by elements and assume the longest distance from the param element type

                    var distance = -1;
                    do
                    {
                        var elementDistance = paramType.GetTypeDistance(eToAssign.Current);
                        if (elementDistance == -1) return -1;
                        distance = Math.Max(distance, elementDistance);
                    } while (eToAssign.MoveNext());
                    totalDistance += distance;
                    return totalDistance;
                }
                else
                {
                    var distance = toBeAssignLast.ParameterType.GetTypeDistance(eToAssign.Current);
                    if (distance == -1) return -1;
                    totalDistance += distance;
                }
            }

            if (bToBeAssigned == bToAssign) return totalDistance;
            return -1;
        }

        public static int GetTypeDistance(this Type toBeAssigned, Type toAssign)
        {
            if (!toBeAssigned.IsAssignableFrom(toAssign)) return -1;
            int distance = 0;

            if (toBeAssigned.IsGenericEnumerable(out var toBeAssignedElement) && toAssign.IsGenericEnumerable(out var toAssignElement)) distance += GetTypeDistance(toBeAssignedElement, toAssignElement);

            for (Type type = toAssign; type != typeof(object); type = type.BaseType, distance++)
            {
                if (type == toBeAssigned) return distance;
                foreach (var @interface in type.GetInterfaces().Except(type.BaseType.GetInterfaces()))
                {
                    if (toBeAssigned == @interface) return distance + 1;
                }
            }
            if (toBeAssigned == typeof(object)) return distance;

            // if types can be assigned but not by derivation, we assume that it's because an operator has been defined
            // therefore, we assume a distance of 1
            return 1; 
        }

        private static readonly Type IEnumerableType = typeof(IEnumerable);
        private static readonly Type IGenericEnumerableType = typeof(IEnumerable<>);

        public static bool IsGenericEnumerable(this Type type) => type.IsGenericEnumerable(out _);
        public static bool IsGenericEnumerable(this Type type, out Type elementType)
        {
            elementType = null;
            if (!IEnumerableType.IsAssignableFrom(type)) return false;
            var result = type.IsOfGenericType(IGenericEnumerableType, out var elementsTypes);
            if (result) { elementType = elementsTypes[0]; }
            return result;
        }

        public static bool IsOfGenericType(this Type type, Type IGenericEnumerableType) => type.IsOfGenericType(IGenericEnumerableType, out _);
        public static bool IsOfGenericType(this Type type, Type IGenericEnumerableType, out Type[] elementType)
        {
            elementType = null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == IGenericEnumerableType)
            {
                elementType = type.GetGenericArguments();
                return true;
            }
            var enumerableInterface
                = type.GetInterfaces()
                .Where(t => t.IsGenericType)
                .FirstOrDefault(t => t.GetGenericTypeDefinition() == IGenericEnumerableType);

            if (enumerableInterface == null) return false;

            elementType = enumerableInterface.GetGenericArguments();
            return true;
        }

        public static void GetGenericArgumentComparableType(Type parameterType, Type toAssign, int baseDistance, List<(Type GenericType, Type Correspondance, int Distance)> correspondances)
        {
            if (parameterType == null) return;
            if (parameterType.IsGenericParameter)
            {
                correspondances.Add((parameterType, toAssign, baseDistance));
                return;
            }
            if (parameterType.IsArray && toAssign.IsArray)
            {
                GetGenericArgumentComparableType(parameterType.GetElementType(), toAssign.GetElementType(), baseDistance + 1, correspondances);
                return;
            }

            var comparisonType = toAssign.FindGenericBaseType(parameterType);

            if (comparisonType == null) return;

            var parametersGenericTypes = parameterType.GetGenericArguments();
            var toAssignGenericTypes = comparisonType.GetGenericArguments();

            if (parametersGenericTypes.Length != toAssignGenericTypes.Length) return; 
            for (var i = 0; i < parametersGenericTypes.Length; i++)
            {
                GetGenericArgumentComparableType(parametersGenericTypes[i], toAssignGenericTypes[i], baseDistance + 1, correspondances);
            }
        }

        public static Type FindGenericBaseType(this Type type, Type baseType)
        {
            if (type.ToString() == baseType.ToString()) return type;
            if (type == typeof(object)) return null;
            if (baseType.IsGenericTypeDefinition) return type.FindGenericBaseType(baseType.GetGenericTypeDefinition());

            if (type.IsGenericType && type.GetGenericTypeDefinition().ToString() == baseType.ToString()) return type;
            if (baseType.IsInterface)
                return type.GetInterfaces().FirstOrDefault(i=> (i.IsGenericType && i.GetGenericTypeDefinition().ToString() == baseType.ToString()) || i.ToString() == baseType.ToString());

            return type.BaseType.FindGenericBaseType(baseType);
        }

        public static MethodInfo InferGenericMethod(this MethodInfo method, Type objectType, Type[] parameterTypes)
        {
            if (!method.IsGenericMethodDefinition) return method;

            var methodParameters = method.GetParameters();

            var eParameters = ((IEnumerable<ParameterInfo>)method.GetParameters()).GetEnumerator();

            var eTypes = methodParameters.Length > 0 && methodParameters[0].GetCustomAttribute<ExtensionAttribute>() != null
                ? ((IEnumerable<Type>)parameterTypes.Prepend(objectType)).GetEnumerator()
                : ((IEnumerable<Type>)parameterTypes).GetEnumerator();

            List<(Type GenericType, Type Correspondance, int Distance)> correspondances = new();

            ParameterInfo toBeAssignedLast;
            bool bToBeAssigned, bToAssign;

            while ((bToBeAssigned = eParameters.MoveNext()) & (bToAssign = eTypes.MoveNext()))
            {
                toBeAssignedLast = eParameters.Current;

                if (toBeAssignedLast.ParameterType.IsArray && toBeAssignedLast.GetCustomAttribute<ParamArrayAttribute>() != null)
                {
                    ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 1, correspondances);
                    var paramType = toBeAssignedLast.ParameterType.GetElementType();

                    do
                    {
                        ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 0, correspondances);
                    } while (eTypes.MoveNext());

                }
                else
                {
                    ParserExtensions.GetGenericArgumentComparableType(toBeAssignedLast.ParameterType, eTypes.Current, 0, correspondances);
                }
            }

            var candidateArguments = correspondances
                .GroupBy(c => c.GenericType, c => c)
                .ToLookup(g => g.Key, g => g.OrderByDescending(c => c.Distance).Select(c => c.Correspondance).FirstOrDefault());
            var genericArguments = method.GetGenericArguments().Select(a => candidateArguments[a].FirstOrDefault()).ToArray();
            if (genericArguments.Any(ga => ga == null)) return null;

            return method.MakeGenericMethod(genericArguments);
        }

        public static Expression[] AdjustParameters(this ConstructorInfo constructorInfo, Expression[] listParams)
        {
            var methodParams = constructorInfo.GetParameters();
            return AdjustParameters(null, listParams, methodParams);
        }

        public static Expression[] AdjustParameters(this MethodInfo methodInfo, Expression obj, Expression[] listParams)
        {
            var methodParams = methodInfo.GetParameters();
            return AdjustParameters(obj, listParams, methodParams);
        }

        private static Expression[] AdjustParameters(Expression obj, Expression[] listParams, ParameterInfo[] methodParams)
        {
            Expression[] result;
            int lastResultIndex;
            if (methodParams.IsExtension())
            {
                result = new Expression[methodParams.Length + 1];
                result[0] = obj;
                listParams[0..result.Length].CopyTo(result, 1);
            }
            else
            {
                result = new Expression[methodParams.Length];
                listParams[0..result.Length].CopyTo(result, 0);
            }

            if (methodParams.HasParams())
            {
                lastResultIndex = result.Length - 1;
                var lastParameter = methodParams[lastResultIndex];
                var lastParameters = listParams[lastResultIndex..];
                var elementType = lastParameter.ParameterType.GetElementType();
                if (lastParameters.Length != 1 || lastParameter.ParameterType == elementType)
                {
                    result[lastResultIndex] = Expression.NewArrayInit(elementType, listParams[lastResultIndex..]);
                }
            }

            return result;
        }

        public static bool IsExtension(this MethodInfo method) => method.GetParameters().IsExtension();
        public static bool IsExtension(this IEnumerable<ParameterInfo> parameters) => parameters.FirstOrDefault()?.IsExtension() ?? false;
        public static bool IsExtension(this ParameterInfo parameter) => parameter.GetCustomAttribute<ExtensionAttribute>() != null;

        public static bool HasParams(this MethodInfo method) => method.GetParameters().HasParams();
        public static bool HasParams(this IEnumerable<ParameterInfo> parameters) => parameters.LastOrDefault()?.HasParams() ?? false;
        public static bool HasParams(this ParameterInfo parameter) => parameter.GetCustomAttribute<ParamArrayAttribute>() != null;

    }
}
