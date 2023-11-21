using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions
{
    public interface IResolver
    {
        Type ResolveType(string name);
        Type ResolveType(string name, Type[] genericParameters);
        ConstructorInfo[] GetConstructors(Type type);
        MethodInfo[] GetInstanceMethods(Type type, string name);
        MethodInfo[] GetStaticMethods(Type type, string name);
        (ConstructorInfo Method, Expression[] Parameters)? SelectConstructor(IEnumerable<ConstructorInfo> constructors, Expression[] arguments);
        (MethodInfo Method, Expression[] Parameters)? SelectMethod(IEnumerable<MethodInfo> methods, Expression obj, Type[] genericParameters, Expression[] arguments);
        MemberInfo GetStaticPropertyOrField(Type type, string name);
        MemberInfo GetInstancePropertyOrField(Type type, string name);
        bool TryGetConstant(string name, out ConstantExpression constantExpression);
    }

    public interface IDistanceValue<T> 
    {
        int Distance { get; }
        T Value { get; }
    }


    public interface ITypeFinder
    {
        Type FindType(string name, Type[] genericArguments);
        MethodInfo[] FindExtensionMethods(Type extendedType, string name);
    }
}
