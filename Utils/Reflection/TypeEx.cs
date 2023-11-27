using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
            .Select (m=> new PropertyOrFieldInfo(m))
            .FirstOrDefault();

    public static Type GetUnderlyingType(this Type type)
    {
        if (type.IsEnum) return type.GetEnumUnderlyingType();
        if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition()) return type.GetGenericArguments()[0];
        return type;
    }

}
