using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Reflection
{
    public static class ExpressionEx
    {
        /// <summary>
        /// Create an expression call on an object given the specified arguments
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="name"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static MethodCallExpression CreateExpressionCall(Expression expression, string name, params Expression[] arguments)
            => CreateExpressionCall(expression, name, BindingFlags.Public | BindingFlags.Instance, arguments);

        /// <summary>
        /// Create an expression call on an object given the specified arguments
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="name"></param>
        /// <param name="bindingFlags"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static MethodCallExpression CreateExpressionCall(Expression expression, string name, BindingFlags bindingFlags, params Expression[] arguments)
        {
            bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

            Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
            var method = expression.Type.GetMethod(name, bindingFlags | BindingFlags.Instance, null, argumentTypes, null) ?? throw new MissingMethodException(name);
            return Expression.Call(expression, method, arguments);
        }

        /// <summary>
        /// Create a static expression call given the specified arguments
        /// </summary>
        /// <param name="name"></param>
        /// <param name="bindingFlags"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static MethodCallExpression CreateExpressionCall(Type type, string name, BindingFlags bindingFlags, params Expression[] arguments)
        {
            bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

            Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
            var method = type.GetMethod(name, bindingFlags | BindingFlags.Static, null, argumentTypes, null) ?? throw new MissingMethodException(name);
            return Expression.Call(null, method, arguments);
        }


        /// <summary>
        /// Try to get a conversion method from an expression or the target type
        /// This function searches 
        /// </summary>
        /// <param name="source">Source expression to get a convert from</param>
        /// <param name="outType">Target Type to get a convert to</param>
        /// <param name="builder">Resulting expression</param>
        /// <returns></returns>
        public static bool TryGetConverterMethod(Expression source, Type outType, out Expression builder)
            => TryGetConverterMethod(source, outType, BindingFlags.Public, out builder);

        /// <summary>
        /// Try to get a conversion method from an expression or the target type
        /// This function searches 
        /// </summary>
        /// <param name="source">Source expression to get a convert from</param>
        /// <param name="outType">Target Type to get a convert to</param>
        /// <param name="builder">Resulting expression</param>
        /// <returns></returns>
        public static bool TryGetConverterMethod(Expression source, Type outType, BindingFlags bindingFlags, out Expression builder)
        {
            bindingFlags &= BindingFlags.Public | BindingFlags.NonPublic;

            var methodsInstance = source.Type
                .GetMethods(bindingFlags | BindingFlags.Instance)
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
                .GetMethods(bindingFlags | BindingFlags.Static)
                .Where(m => m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type)
                .ToArray();

            var methodsTarget = outType
                .GetMethods(bindingFlags | BindingFlags.Static)
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
                .GetConstructors(bindingFlags | BindingFlags.Instance)
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
    }
}
