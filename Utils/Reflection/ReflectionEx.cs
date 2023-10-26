using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Utils.Net.Expressions
{
    public static class ReflectionEx
    {
        public static Type GetTypeOf(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                MethodInfo m => m.ReturnType,
                _ => throw new NotSupportedException($"{member.GetType()} is not supported for create a member call")
            };
        }

        public static Type GetUnderlyingType(Type type)
        {
            if (type.IsEnum) return type.GetEnumUnderlyingType();
            if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition()) return type.GetGenericArguments()[0];
            return type;
        }
    }
}
