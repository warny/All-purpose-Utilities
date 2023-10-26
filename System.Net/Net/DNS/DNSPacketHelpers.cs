﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Net.DNS;

namespace Utils.Net.DNS;

internal static class DNSPacketHelpers
{
    public static IEnumerable<(DNSFieldAttribute Attribute, MemberInfo Member)> GetDNSFields(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (!typeof(DNSElement).IsAssignableFrom(type)) throw new ArgumentException($"{type.FullName} is not a DNSElement", nameof(type));

        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo))
        {
            var attribute = member.GetCustomAttribute<DNSFieldAttribute>();
            if (attribute is null) continue;

            yield return (attribute, member);
        }
    }
}