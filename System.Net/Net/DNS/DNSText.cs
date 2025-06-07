using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Net.DNS;

/// <summary>
/// Provides helper methods to convert DNS records to and from the textual
/// representation commonly used in zone files.
/// </summary>

public class DNSText : IDNSWriter<string>
{
    private static readonly Regex FormatRegex = new("{(?<name>[^}]+)}", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new("\\\"([^\\\"]*)\\\"|\\S+", RegexOptions.Compiled);

    /// <summary>
    /// Gets a default instance of <see cref="DNSText"/> for convenience.
    /// </summary>
    public static DNSText Default { get; } = new DNSText();

    /// <summary>
    /// Converts a <see cref="DNSResponseRecord"/> into a textual line as used in
    /// standard zone files.
    /// </summary>
    /// <param name="record">The record to convert.</param>
    /// <returns>A line representing the record.</returns>
    public static string ToText(DNSResponseRecord record)
    {
        var rdata = record.RData;
        var attr = rdata.GetType().GetCustomAttribute<DNSTextRecordAttribute>();
        string rdataText;
        if (attr == null)
        {
            var fields = rdata.GetType().GetMembers()
                .Where(m => m.GetCustomAttribute<DNSFieldAttribute>() != null)
                .ToArray();
            rdataText = string.Join(" ", fields.Select(m =>
            {
                object val = m is PropertyInfo pi ? pi.GetValue(rdata) : (m is FieldInfo fi ? fi.GetValue(rdata) : null);
                return FormatValue(val);
            }));
        }
        else
        {
            rdataText = FormatRegex.Replace(attr.Format, m =>
            {
                var name = m.Groups["name"].Value;
                var member = rdata.GetType().GetMember(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                object value = null;
                if (member is PropertyInfo pi)
                    value = pi.GetValue(rdata);
                else if (member is FieldInfo fi)
                    value = fi.GetValue(rdata);
                return value != null ? FormatValue(value) : m.Value;
            });
        }
        return $"{record.Name} {record.TTL} {record.Class} {rdata.Name} {rdataText}".TrimEnd();
    }

    /// <summary>
    /// Parses a single line of text into a <see cref="DNSResponseRecord"/>.
    /// </summary>
    /// <param name="line">The textual line.</param>
    /// <returns>The parsed record or <c>null</c> if the line is empty or a comment.</returns>
    public static DNSResponseRecord ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        int commentIndex = line.IndexOfAny(new[] { ';', '#' });
        if (commentIndex >= 0)
            line = line[..commentIndex];
        var tokens = TokenRegex.Matches(line).Select(m => m.Value).ToArray();
        if (tokens.Length < 5)
            return null;
        var name = new DNSDomainName(tokens[0]);
        if (!uint.TryParse(tokens[1], out uint ttl))
            return null;
        if (!Enum.TryParse(tokens[2], true, out DNSClass dnsClass))
            return null;
        string typeName = tokens[3];
        var rdataTokens = tokens.Skip(4).ToArray();

        var factory = DNSFactory.Default;
        ushort id = factory.GetClassId(dnsClass, typeName);
        var rdataType = factory.GetDNSType(dnsClass, id);
        var rdata = (DNSResponseDetail)Activator.CreateInstance(rdataType);
        var attr = rdataType.GetCustomAttribute<DNSTextRecordAttribute>();
        string[] fieldNames = attr != null
            ? FormatRegex.Matches(attr.Format).Select(m => m.Groups["name"].Value).ToArray()
            : rdataType.GetMembers().Where(m => m.GetCustomAttribute<DNSFieldAttribute>() != null).Select(m => m.Name).ToArray();
        for (int i = 0; i < fieldNames.Length && i < rdataTokens.Length; i++)
        {
            SetValue(rdata, fieldNames[i], rdataTokens[i]);
        }
        var record = new DNSResponseRecord(name.Value, ttl, rdata)
        {
            Class = dnsClass
        };
        return record;
    }

    private static void SetValue(object obj, string memberName, string value)
    {
        var member = obj.GetType().GetMember(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
        if (member is PropertyInfo pi)
            pi.SetValue(obj, ConvertTo(value, pi.PropertyType));
        else if (member is FieldInfo fi)
            fi.SetValue(obj, ConvertTo(value, fi.FieldType));
    }

    private static object ConvertTo(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value.Trim('"');
        if (targetType == typeof(byte)) return byte.Parse(value);
        if (targetType == typeof(ushort)) return ushort.Parse(value);
        if (targetType == typeof(uint)) return uint.Parse(value);
        if (targetType == typeof(int)) return int.Parse(value);
        if (targetType == typeof(IPAddress)) return IPAddress.Parse(value);
        if (targetType == typeof(DNSDomainName)) return new DNSDomainName(value);
        if (targetType == typeof(byte[])) return Convert.FromBase64String(value);
        if (targetType.IsEnum) return Enum.Parse(targetType, value, true);
        return Convert.ChangeType(value, targetType);
    }

    private static string FormatValue(object value)
    {
        if (value is string s)
        {
            return s.Contains(' ') ? $"\"{s}\"" : s;
        }
        if (value is byte[] bytes)
            return Convert.ToBase64String(bytes);
        return Convert.ToString(value);
    }

    /// <summary>
    /// Parses all records contained in a text file.
    /// </summary>
    /// <param name="path">Path to the zone file.</param>
    /// <returns>A list of <see cref="DNSResponseRecord"/> objects.</returns>
    public static List<DNSResponseRecord> ParseFile(string path)
    {
        var list = new List<DNSResponseRecord>();
        foreach (var line in File.ReadLines(path))
        {
            var rec = ParseLine(line);
            if (rec != null)
                list.Add(rec);
        }
        return list;
    }

    /// <inheritdoc />
    public string Write(DNSHeader header)
    {
        var sb = new StringBuilder();
        foreach (var r in header.Responses)
            sb.AppendLine(ToText(r));
        foreach (var r in header.Authorities)
            sb.AppendLine(ToText(r));
        foreach (var r in header.Additionals)
            sb.AppendLine(ToText(r));
        return sb.ToString();
    }
}
