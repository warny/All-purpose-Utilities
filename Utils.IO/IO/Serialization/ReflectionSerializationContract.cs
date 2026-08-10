using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Utils.IO.Serialization;

/// <summary>Defines whether a contract is being built for reading or writing.</summary>
internal enum SerializationDirection { Read, Write }

/// <summary>Holds a validated attributed member and its stable wire order.</summary>
internal sealed record SerializableMemberContract(MemberInfo Member, Type ValueType, int Order);

/// <summary>Holds the reflection-independent decisions needed by delegate generation.</summary>
internal sealed record ReflectionSerializationContract(Type Type, IReadOnlyList<SerializableMemberContract> Members)
{
    private IReadOnlyList<SerializableMemberContract> _members = Members.ToImmutableArray();

    /// <summary>Gets the serializable members as an immutable, stably ordered snapshot.</summary>
    internal IReadOnlyList<SerializableMemberContract> Members
    {
        get => _members;
        init => _members = value.ToImmutableArray();
    }
}

/// <summary>Discovers and validates reflection serialization contracts before expressions are created.</summary>
internal static class ReflectionContractBuilder
{
    /// <summary>Builds a complete contract and aggregates all locally discoverable defects.</summary>
    internal static ReflectionSerializationContract Build(Type type, SerializationDirection direction)
    {
        List<SerializationContractDiagnostic> diagnostics = [];
        if (type.IsInterface) diagnostics.Add(new("UIORT001", "Interfaces cannot be constructed or serialized as concrete contracts."));
        if (type.IsAbstract) diagnostics.Add(new("UIORT001", "Abstract types cannot be constructed as serialization contracts."));
        if (type.ContainsGenericParameters) diagnostics.Add(new("UIORT001", "Open generic types are not supported."));
        if (direction == SerializationDirection.Read && !type.IsValueType && type.GetConstructor(Type.EmptyTypes) is null)
            diagnostics.Add(new("UIORT002", "A public parameterless constructor is required for deserialization."));

        var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => (Member: member, Attribute: member.GetCustomAttribute<FieldAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => CreateMember(item.Member, item.Attribute!.Order, direction, diagnostics))
            .Where(member => member is not null)
            .Cast<SerializableMemberContract>()
            .ToArray();

        foreach (var group in members.GroupBy(member => member.Order).Where(group => group.Count() > 1))
        {
            string names = string.Join(", ", group.Select(member => member.Member.Name).OrderBy(name => name, StringComparer.Ordinal));
            diagnostics.Add(new("UIORT004", $"Members {names} all use field order {group.Key}."));
        }

        if (diagnostics.Count > 0) throw new SerializationContractException(type, diagnostics);
        return new(type, members.OrderBy(member => member.Order).ThenBy(member => member.Member.Name, StringComparer.Ordinal).ToImmutableArray());
    }

    /// <summary>Validates one attributed member and returns its executable description.</summary>
    private static SerializableMemberContract? CreateMember(MemberInfo member, int order, SerializationDirection direction, List<SerializationContractDiagnostic> diagnostics)
    {
        if (member is FieldInfo field)
        {
            if (field.IsStatic) diagnostics.Add(new("UIORT003", $"Field {field.Name} is static."));
            if (!field.IsPublic) diagnostics.Add(new("UIORT003", $"Field {field.Name} is not public."));
            if (direction == SerializationDirection.Read && field.IsInitOnly) diagnostics.Add(new("UIORT003", $"Field {field.Name} is readonly and cannot be assigned during deserialization."));
            return new(field, field.FieldType, order);
        }
        if (member is PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0) diagnostics.Add(new("UIORT003", $"Property {property.Name} is an indexer."));
            MethodInfo? accessor = direction == SerializationDirection.Read ? property.SetMethod : property.GetMethod;
            if (accessor is null || !accessor.IsPublic) diagnostics.Add(new("UIORT003", $"Property {property.Name} has no accessible {(direction == SerializationDirection.Read ? "setter" : "getter")}."));
            if (direction == SerializationDirection.Read && property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) == true)
                diagnostics.Add(new("UIORT010", $"Property {property.Name} is init-only and cannot be assigned during deserialization."));
            if (accessor?.IsStatic == true) diagnostics.Add(new("UIORT003", $"Property {property.Name} is static."));
            return new(property, property.PropertyType, order);
        }
        diagnostics.Add(new("UIORT003", $"Member {member.Name} is not a field or property."));
        return null;
    }
}
