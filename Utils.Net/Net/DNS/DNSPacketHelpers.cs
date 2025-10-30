using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Utils.Net.DNS;

/// <summary>
/// Provides reflection-based helper methods for discovering fields and properties within
/// <see cref="DNSElement"/>-derived classes that are annotated with <see cref="DNSFieldAttribute"/>.
/// </summary>
internal static class DNSPacketHelpers
{
    /// <summary>
    /// Retrieves all public or non-public instance fields and properties from the specified <paramref name="type"/>
    /// that have a <see cref="DNSFieldAttribute"/> applied, along with the corresponding attribute data.
    /// </summary>
    /// <param name="type">A <see cref="Type"/> that must derive from <see cref="DNSElement"/>.</param>
    /// <returns>
    /// A sequence of tuples, where each tuple contains the <see cref="DNSFieldAttribute"/> and a
    /// <see cref="MemberInfo"/> object representing the annotated field or property.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the specified <paramref name="type"/> does not derive from <see cref="DNSElement"/>.
    /// </exception>
    public static IEnumerable<(DNSFieldAttribute Attribute, MemberInfo Member)> GetDNSFields(Type type)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));

        if (!typeof(DNSElement).IsAssignableFrom(type))
            throw new ArgumentException($"{type.FullName} is not a DNSElement", nameof(type));

        // Scan all instance fields and properties (public or non-public).
        // Yield only those that carry the DNSFieldAttribute.
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                   .Where(m => m is PropertyInfo || m is FieldInfo))
        {
            var attribute = member.GetCustomAttribute<DNSFieldAttribute>();
            if (attribute is null)
                continue;

            yield return (attribute, member);
        }
    }
}
