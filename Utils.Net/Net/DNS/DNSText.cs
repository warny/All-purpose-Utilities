using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Net.DNS;

/// <summary>
/// Provides helper methods to convert DNS records to and from the textual
/// representation commonly used in zone files.
/// </summary>

public partial class DNSText : IDNSWriter<string>, IDNSReader<string>, IDNSReader<TextReader>
{
    private static readonly Regex FormatRegex = CreateFormatRegex();
    private static readonly Regex TokenRegex = CreateTokenRegex();

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
        string rdataText = "";
        if (attr is null)
        {
            var fields = rdata.GetType().GetMembers()
                .Where(m => m.GetCustomAttribute<DNSFieldAttribute>() != null)
                .Select(m =>
                {
                    var member = rdata.GetType().GetMember(m.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                    return member switch
                    {
                        PropertyInfo pi => FormatValue(pi.GetValue(rdata)),
                        FieldInfo fi => FormatValue(fi.GetValue(rdata)),
                        _ => null
                    };
                });

            rdataText = string.Join(' ', fields);
        }
        else
        {
            var fields = rdata.GetType().GetMembers()
                .Where(m => m is PropertyInfo || m is FieldInfo)
                .ToDictionary(m => m.Name);
            rdataText = FormatRegex.Replace(attr.Format, m =>
                fields[m.Groups["name"].Value] switch
                {
                    PropertyInfo pi => FormatValue(pi.GetValue(rdata)),
                    FieldInfo fi => FormatValue(fi.GetValue(rdata)),
                    _ => null
                }
            );
        }


        return $"{record.Name} {record.TTL} {record.ClassId} {rdata.Name} {rdataText}".TrimEnd();
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
        int commentIndex = line.IndexOfAny([';', '#']);
        if (commentIndex >= 0)
            line = line[..commentIndex];
        line = line.Replace("(", " ").Replace(")", " ");
        var tokens = TokenRegex.Matches(line).Select(m => m.Value).ToArray();
        if (tokens.Length < 5)
            return null;
        var name = new DNSDomainName(tokens[0]);
        if (!uint.TryParse(tokens[1], out uint ttl))
            return null;
        if (!Enum.TryParse(tokens[2], true, out DNSClassId dnsClass))
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
            ClassId = dnsClass
        };
        return record;
    }

    /// <summary>
    /// Assigns a parsed value to the specified field or property on a DNS response detail instance.
    /// </summary>
    /// <param name="obj">Object that owns the member to update.</param>
    /// <param name="memberName">Name of the field or property.</param>
    /// <param name="value">Textual value to convert and assign.</param>
    private static void SetValue(object obj, string memberName, string value)
    {
        var member = obj.GetType().GetMember(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
        if (member is PropertyInfo pi)
            pi.SetValue(obj, ConvertTo(value, pi.PropertyType));
        else if (member is FieldInfo fi)
            fi.SetValue(obj, ConvertTo(value, fi.FieldType));
    }

    /// <summary>
    /// Parses records from a string containing the zone file content.
    /// </summary>
    /// <param name="text">Raw text that contains one or more zone file records.</param>
    /// <returns>A list of records parsed from the provided <paramref name="text"/>.</returns>
    public static List<DNSResponseRecord> ParseText(string text)
        => ParseLines(text.Split(["\r\n", "\n"], StringSplitOptions.None));

    /// <summary>
    /// Parses records from a <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">Reader that exposes the zone file content to parse.</param>
    /// <returns>A list of records parsed from <paramref name="reader"/>.</returns>
    public static List<DNSResponseRecord> Parse(TextReader reader)
    {
        string line;
        var lines = new List<string>();
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return ParseLines(lines);
    }


    /// <summary>
    /// Parses a sequence of lines into DNS response records, handling multi-line records.
    /// </summary>
    /// <param name="lines">Sequence of raw zone file lines to interpret.</param>
    /// <returns>A list containing each parsed <see cref="DNSResponseRecord"/>.</returns>
    private static List<DNSResponseRecord> ParseLines(IEnumerable<string> lines)
    {
        var list = new List<DNSResponseRecord>();
        var sb = new StringBuilder();
        int paren = 0;

        foreach (var raw in lines)
        {
            var line = raw;
            int idx = line.IndexOfAny([';', '#']);
            if (idx >= 0)
                line = line[..idx];

            if (sb.Length > 0 && line.Length > 0)
                sb.Append(' ');
            sb.Append(line.Trim());

            foreach (var c in line)
            {
                if (c == '(')
                    paren++;
                else if (c == ')')
                    paren--;
            }

            if (paren <= 0 && sb.Length > 0)
            {
                var rec = ParseLine(sb.ToString());
                if (rec != null)
                    list.Add(rec);
                sb.Clear();
                paren = 0;
            }
        }

        if (sb.Length > 0)
        {
            var rec = ParseLine(sb.ToString());
            if (rec != null)
                list.Add(rec);
        }

        return list;
    }

    /// <inheritdoc />
    public DNSHeader Read(string text)
    {
        var header = new DNSHeader();
        foreach (var r in ParseText(text))
            header.Responses.Add(r);
        return header;
    }

    /// <inheritdoc />
    public DNSHeader Read(TextReader reader)
    {
        var header = new DNSHeader();
        foreach (var r in Parse(reader))
            header.Responses.Add(r);
        return header;
    }

    /// <summary>
    /// Converts a textual token extracted from a zone file into the expected target type.
    /// </summary>
    /// <param name="value">Token value to convert.</param>
    /// <param name="targetType">Type expected by the DNS record field.</param>
    /// <returns>The converted value suitable for assignment to the record field.</returns>
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

    /// <summary>
    /// Formats a DNS field value back to its textual representation.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>The textual representation written in zone files.</returns>
    private static string FormatValue(object value)
            => value switch
            {
                string s => s.Contains(' ') ? $"\"{s}\"" : s,
                byte[] bytes => Convert.ToBase64String(bytes),
                _ => Convert.ToString(value)
            };

    /// <summary>
    /// Parses all records contained in a text file.
    /// </summary>
    /// <param name="path">Path to the zone file.</param>
    /// <returns>A list of <see cref="DNSResponseRecord"/> objects.</returns>
    public static List<DNSResponseRecord> ParseFile(string path) => ParseTextReader(File.OpenText(path));

    /// <summary>
    /// Parses all records contained in a string.
    /// </summary>
    /// <param name="records">Zone records content.</param>
    /// <returns>A list of <see cref="DNSResponseRecord"/> objects.</returns>
    public static List<DNSResponseRecord> ParseString(string records) => ParseTextReader(new StringReader(records));

    /// <summary>
    /// Parses all records from a text reader.
    /// </summary>
    /// <param name="reader">Text reader that provides zone records content.</param>
    /// <returns>A list of <see cref="DNSResponseRecord"/> objects.</returns>
    public static List<DNSResponseRecord> ParseTextReader(TextReader reader)
    {
        var list = new List<DNSResponseRecord>();
        var sb = new StringBuilder();
        int paren = 0;

        string raw = null;
        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw;
            int idx = line.IndexOfAny([';', '#']);
            if (idx >= 0)
                line = line[..idx];

            if (sb.Length > 0 && line.Length > 0)
                sb.Append(' ');
            sb.Append(line.Trim());

            foreach (var c in line)
            {
                if (c == '(')
                    paren++;
                else if (c == ')')
                    paren--;
            }

            if (paren <= 0 && sb.Length > 0)
            {
                var rec = ParseLine(sb.ToString());
                if (rec != null)
                    list.Add(rec);
                sb.Clear();
                paren = 0;
            }
        }

        if (sb.Length > 0)
        {
            var rec = ParseLine(sb.ToString());

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

    /// <summary>
    /// Creates the regular expression used to extract named placeholders from format strings.
    /// </summary>
    [GeneratedRegex("{(?<name>[^}]+)}", RegexOptions.Compiled)]
    private static partial Regex CreateFormatRegex();
    /// <summary>
    /// Creates the regular expression used to tokenize individual elements in zone file lines.
    /// </summary>
    [GeneratedRegex("\\\"([^\\\"]*)\\\"|\\S+", RegexOptions.Compiled)]
    private static partial Regex CreateTokenRegex();
}
