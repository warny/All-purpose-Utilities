using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace Utils.Net;

/// <summary>
/// Defines a converter capable of converting a MIME part body to a target type.
/// </summary>
public interface IMimePartConverter
{
    /// <summary>
    /// Gets the MIME type mask handled by this converter. Wildcards are allowed.
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Gets the list of target types this converter can produce.
    /// </summary>
    Type[] SupportedTypes { get; }

    /// <summary>
    /// Attempts to convert the raw textual content to the specified type.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="rawContent">The raw body content.</param>
    /// <param name="content">When this method returns, contains the converted content.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    bool TryConvert<T>(string rawContent, out T? content);
}

/// <summary>
/// Provides conversion of <see cref="MimePart"/> contents based on registered converters.
/// </summary>
public class MimePartConverter
{
    private readonly List<IMimePartConverter> _converters = new();

    /// <summary>
    /// Gets a default <see cref="MimePartConverter"/> instance with common converters registered.
    /// </summary>
    public static MimePartConverter Default { get; } = CreateDefault();

    /// <summary>
    /// Adds a new converter to the list of available converters.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    public void Add(IMimePartConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters.Add(converter);
    }

    /// <summary>
    /// Determines whether content of the specified MIME type can be converted to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Desired target type.</typeparam>
    /// <param name="mimeType">The MIME type to test.</param>
    /// <returns><c>true</c> if a converter exists; otherwise, <c>false</c>.</returns>
    public bool CanConvertTo<T>(MimeType mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        var requested = typeof(T);
        return GetConverters(mimeType)
            .Any(c => c.SupportedTypes.Any(t => requested.IsAssignableFrom(t)));
    }

    /// <summary>
    /// Determines whether content of the specified MIME type can be converted to the provided type.
    /// </summary>
    /// <param name="type">Desired target type.</param>
    /// <param name="mimeType">The MIME type to test.</param>
    /// <returns><c>true</c> if a converter exists; otherwise, <c>false</c>.</returns>
    public bool CanConvertTo(Type type, MimeType mimeType)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(mimeType);
        return GetConverters(mimeType)
            .Any(c => c.SupportedTypes.Any(t => type.IsAssignableFrom(t)));
    }

    /// <summary>
    /// Attempts to convert the specified MIME part to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="part">The MIME part.</param>
    /// <param name="content">When this method returns, contains the converted content if successful.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryConvertTo<T>(MimePart part, out T? content)
    {
        ArgumentNullException.ThrowIfNull(part);
        content = default;
        if (!part.Headers.TryGetValue("Content-Type", out var ct))
            return false;
        var mime = MimeType.Parse(ct);
        foreach (var conv in GetConverters(mime))
        {
            if (conv.TryConvert(part.Body, out content))
                return true;
        }
        return false;
    }

    private static MimePartConverter CreateDefault()
    {
        var conv = new MimePartConverter();
        conv.Add(new TextPartConverter());
        conv.Add(new XmlPartConverter());
        conv.Add(new JsonPartConverter());
        conv.Add(new MultipartPartConverter());
        conv.Add(new BinaryPartConverter());
        return conv;
    }

    private IEnumerable<IMimePartConverter> GetConverters(MimeType mime)
    {
        return _converters
            .Where(c => Matches(c.MimeType, mime))
            .OrderByDescending(c => Specificity(c.MimeType));
    }

    private static bool Matches(string pattern, MimeType mime)
    {
        var parts = pattern.Split('/', 2);
        var pt = parts[0];
        var ps = parts.Length > 1 ? parts[1] : "*";
        bool typeMatch = pt == "*" || mime.Type.Equals(pt, StringComparison.OrdinalIgnoreCase);
        bool subMatch = ps == "*" || mime.SubType.Equals(ps, StringComparison.OrdinalIgnoreCase);
        return typeMatch && subMatch;
    }

    private static int Specificity(string pattern)
    {
        var parts = pattern.Split('/', 2);
        int score = 0;
        if (parts[0] != "*") score++;
        if (parts.Length > 1 && parts[1] != "*") score++;
        return score;
    }
}

internal sealed class TextPartConverter : IMimePartConverter
{
    public string MimeType => "text/*";

    public Type[] SupportedTypes { get; } = [typeof(string), typeof(TextReader)];

    public bool TryConvert<T>(string rawContent, out T? content)
    {
        content = default;
        var target = typeof(T);
        if (target == typeof(string))
        {
            content = (T)(object)rawContent;
            return true;
        }
        if (typeof(TextReader).IsAssignableFrom(target))
        {
            content = (T)(object)new StringReader(rawContent);
            return true;
        }
        return false;
    }
}

internal sealed class XmlPartConverter : IMimePartConverter
{
    public string MimeType => "text/xml";

    public Type[] SupportedTypes { get; } = [typeof(XDocument), typeof(XmlDocument)];

    public bool TryConvert<T>(string rawContent, out T? content)
    {
        content = default;
        var target = typeof(T);
        if (target == typeof(XDocument))
        {
            content = (T)(object)XDocument.Parse(rawContent);
            return true;
        }
        if (target == typeof(XmlDocument))
        {
            var doc = new XmlDocument();
            doc.LoadXml(rawContent);
            content = (T)(object)doc;
            return true;
        }
        return false;
    }
}

internal sealed class JsonPartConverter : IMimePartConverter
{
    public string MimeType => "*/json";

    public Type[] SupportedTypes { get; } = [typeof(JsonDocument)];

    public bool TryConvert<T>(string rawContent, out T? content)
    {
        content = default;
        var target = typeof(T);
        if (target == typeof(JsonDocument))
        {
            try
            {
                content = (T)(object)JsonDocument.Parse(rawContent);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}

internal sealed class MultipartPartConverter : IMimePartConverter
{
    public string MimeType => "multipart/*";

    public Type[] SupportedTypes { get; } = [typeof(MimeDocument)];

    public bool TryConvert<T>(string rawContent, out T? content)
    {
        content = default;
        var target = typeof(T);
        if (target == typeof(MimeDocument) || target.IsAssignableFrom(typeof(MimeDocument)))
        {
            content = (T)(object)MimeReader.Read(rawContent);
            return true;
        }
        return false;
    }
}

internal sealed class BinaryPartConverter : IMimePartConverter
{
    public string MimeType => "application/*";

    public Type[] SupportedTypes { get; } = [typeof(byte[]), typeof(Stream)];

    public bool TryConvert<T>(string rawContent, out T? content)
    {
        content = default;
        var target = typeof(T);
        if (target == typeof(byte[]))
        {
            if (string.IsNullOrEmpty(rawContent))
                return true;
            try
            {
                content = (T)(object)Convert.FromBase64String(rawContent);
                return true;
            }
            catch
            {
                return false;
            }
        }
        if (typeof(Stream).IsAssignableFrom(target))
        {
            if (string.IsNullOrEmpty(rawContent))
                return true;
            try
            {
                var bytes = Convert.FromBase64String(rawContent);
                content = (T)(object)new MemoryStream(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}
