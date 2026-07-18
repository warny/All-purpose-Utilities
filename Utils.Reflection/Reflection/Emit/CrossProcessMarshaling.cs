using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Determines whether interface members can be forwarded across a process boundary through
/// JSON serialization, and rejects shapes that cannot (pointers, handles, arbitrary reference types).
/// </summary>
/// <remarks>
/// "Supported" here does not mean CLR-blittable in the P/Invoke sense (a blittable struct may still
/// contain a <see cref="string"/> field, which is not blittable but is perfectly serializable). The
/// actual criterion is: every value reachable from a method's parameters/return type must be
/// representable as JSON without any dependency on the address space of the process that produced it.
/// </remarks>
internal static class CrossProcessMarshaling
{
    private const int MaxRecursionDepth = 32;

    /// <summary>
    /// Options shared by every <see cref="JsonSerializer"/> call that marshals interface
    /// arguments/return values across the worker boundary. <see cref="IsSupportedType"/> inspects a
    /// struct's <b>fields</b> (public and non-public) rather than its properties, which matches the
    /// typical shape of P/Invoke interop structs (e.g. <c>public int X;</c> with no property wrapper);
    /// without <see cref="JsonSerializerOptions.IncludeFields"/>, <see cref="JsonSerializer"/> silently
    /// serializes such a struct as <c>{}</c>, discarding every field.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    private static readonly HashSet<Type> SupportedScalars =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string),
    ];

    /// <summary>
    /// Validates every method of <paramref name="interfaceType"/> and throws when a parameter or
    /// return type cannot cross a process boundary.
    /// </summary>
    /// <param name="interfaceType">Interface to validate.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when at least one member uses a type that cannot be marshaled across processes.
    /// </exception>
    internal static void EnsureInterfaceIsSupported(Type interfaceType)
    {
        List<string> problems = [];

        foreach (MethodInfo method in interfaceType.GetMethods())
        {
            if (method.ReturnType != typeof(void) && !IsSupportedType(method.ReturnType, 0))
            {
                problems.Add($"'{method.Name}' return type '{method.ReturnType}' cannot cross a process boundary.");
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (!IsSupportedType(parameter.ParameterType, 0))
                {
                    problems.Add($"'{method.Name}' parameter '{parameter.Name}' of type '{parameter.ParameterType}' cannot cross a process boundary.");
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new NotSupportedException(
                $"The interface '{interfaceType.FullName}' cannot be mapped through an isolated " +
                $"(out-of-process) LibraryMapper.Emit because it uses types that cannot safely cross " +
                $"a process boundary (pointers, handles, or arbitrary reference types are never " +
                $"supported):{Environment.NewLine}{string.Join(Environment.NewLine, problems)}{Environment.NewLine}" +
                $"Use LibraryMapper.EmitInProcess<T> instead if the interface definition is fully trusted " +
                $"and you accept compiling/running the generated mapping code in this process.");
        }
    }

    /// <summary>
    /// Determines whether a single type can be marshaled across a process boundary as JSON.
    /// </summary>
    /// <param name="type">Type to inspect. May be a by-ref parameter type.</param>
    /// <param name="depth">Current recursion depth, used to bound cyclic/deeply nested struct graphs.</param>
    /// <returns><see langword="true"/> when the type is safe to serialize; otherwise <see langword="false"/>.</returns>
    internal static bool IsSupportedType(Type type, int depth)
    {
        if (depth > MaxRecursionDepth)
        {
            return false;
        }

        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        if (type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr))
        {
            return false;
        }

        if (SupportedScalars.Contains(type) || type.IsEnum)
        {
            return true;
        }

        if (type.IsArray)
        {
            return type.GetArrayRank() == 1 && IsSupportedType(type.GetElementType()!, depth + 1);
        }

        if (type.IsValueType)
        {
            // JsonSerializer with IncludeFields=true serializes public fields AND public properties.
            // Non-public fields are NOT serialized, so they must not be validated here — doing so
            // would falsely reject structs whose backing fields are compiler-generated or private.
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsSupportedType(field.FieldType, depth + 1))
                {
                    return false;
                }
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && !IsSupportedType(property.PropertyType, depth + 1))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}
