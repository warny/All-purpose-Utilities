using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text.Json.Serialization;
using System.Threading;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Reflection;

public static class TypeEx
{
    public static bool IsAssignableFromEx(this Type toBeAssigned, Type toAssign)
    {
        if (toBeAssigned.In(Types.Number) && toAssign.In(Types.Number))
        {
            if (toBeAssigned.In(Types.FloatingPointNumber)) return true;
            return Marshal.SizeOf(toBeAssigned) >= Marshal.SizeOf(toAssign);
        }
        return toBeAssigned.IsAssignableFrom(toAssign);
    }

    public static PropertyOrFieldInfo[] GetPropertiesOrFields(this Type type)
        => type.GetMembers()
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .ToArray();

    public static PropertyOrFieldInfo[] GetPropertiesOrFields(this Type type, BindingFlags bindingFlags)
        => type.GetMembers(bindingFlags)
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .ToArray();

    public static PropertyOrFieldInfo GetFieldOrProperty(this Type type, string name)
        => type.GetMember(name)
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .FirstOrDefault();

    public static PropertyOrFieldInfo GetFieldOrProperty(this Type type, string name, BindingFlags bindingFlags)
        => type.GetMember(name, bindingFlags)
            .Where(m => m is PropertyInfo || m is FieldInfo)
            .Select(m => new PropertyOrFieldInfo(m))
            .FirstOrDefault();

    public static Type GetUnderlyingType(this Type type)
    {
        if (type.IsEnum) return type.GetEnumUnderlyingType();
        if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition()) return type.GetGenericArguments()[0];
        return type;
    }

    /// <summary>
    /// Determine is <paramref name="type"/> is based on <paramref name="baseType"/>. 
    /// </summary>
    /// <param name="type">Type to chech</param>
    /// <param name="baseType">Base type from which <paramref name="type" /> is defined or derived</param>
    /// <returns></returns>
    public static bool IsDefinedBy(this Type type, Type baseType)
    {
        if (type == baseType) return true;
        if (baseType.IsInterface)
        {
            if (baseType.IsGenericTypeDefinition) return type.GetInterfaces().Where(i => i.IsGenericType).Any(i => i.GetGenericTypeDefinition() == baseType);
            return baseType.GetInterfaces().Any(i => i == baseType);
        }
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t == baseType) return true;
            if (t.IsGenericType && baseType.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == baseType) return true;
        }
        return false;
    }

    /// <summary>
    /// Return generic parameters for the specified <paramref name="baseType"/> that <paramref name="type"/> is based on
    /// </summary>
    /// <param name="type"></param>
    /// <param name="baseType"></param>
    /// <returns></returns>
    public static Type[] GetGenericTypeDefinitionFor(this Type type, Type baseType)
    {
        if (!baseType.IsGenericTypeDefinition) return null;
        if (baseType.IsInterface)
        {
            var @interface = type.GetInterfaces().Where(i => i.IsGenericType).FirstOrDefault(i => i.GetGenericTypeDefinition() == baseType);
            return @interface?.GetGenericArguments();
        }

        for (var t = type; t is not null; t = t.BaseType)
        {
            if (!t.IsGenericType) continue;
            if (t.GetGenericTypeDefinition() == baseType) return t.GetGenericArguments();
        }
        return null;
    }
}
