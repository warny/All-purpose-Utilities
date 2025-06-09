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
		if (attr == null)
		{
			var fields = rdata.GetType().GetMembers()
				.Where(m => m.GetCustomAttribute<DNSFieldAttribute>() != null)
				.ToArray();

			var m = FormatRegex.Match(attr.Format);
			var name = m.Groups["name"].Value;
			var member = rdata.GetType().GetMember(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
			object value = null;
            if (member is PropertyInfo pi)
				value = pi.GetValue(rdata);
            else if (member is FieldInfo fi)
				value = fi.GetValue(rdata);
            return value != null ? FormatValue(value) : m.Value;
            
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
    public static List<DNSResponseRecord> ParseText(string text)
        => ParseLines(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));

    /// <summary>
    /// Parses records from a <see cref="TextReader"/>.
    /// </summary>
    public static List<DNSResponseRecord> Parse(TextReader reader)
    {
        string line;
        var lines = new List<string>();
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return ParseLines(lines);
    }


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

	private static object ConvertTo(string value, Type targetType) {
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
	public static List<DNSResponseRecord> ParseFile(string path) => ParseTextReader(File.OpenText(path));

	/// <summary>
	/// Parses all records contained in a string.
	/// </summary>
	/// <param name="string">Zone records content</param>
	/// <returns>A list of <see cref="DNSResponseRecord"/> objects.</returns>
  public static List<DNSResponseRecord> ParseString(string records) => ParseTextReader(new StringReader(records));

	/// <summary>
	/// Parses all records from a text reader.
	/// </summary>
	/// <param name="reader">Zone records content</param>
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

	[GeneratedRegex("{(?<name>[^}]+)}", RegexOptions.Compiled)]
	private static partial Regex CreateFormatRegex();
	[GeneratedRegex("\\\"([^\\\"]*)\\\"|\\S+", RegexOptions.Compiled)]
	private static partial Regex CreateTokenRegex();
}
