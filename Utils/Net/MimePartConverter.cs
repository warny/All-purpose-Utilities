using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <returns><see langword="true"/> if conversion succeeded; otherwise, <see langword="false"/>.</returns>
    bool TryConvert<T>(string rawContent, out T? content)
    {
        if (this is IMimePartConverter<T> typed)
            return typed.TryConvert(rawContent, out content);
        content = default;
        return false;
    }
}

/// <summary>
/// Generic interface implemented by converters supporting a specific target type.
/// </summary>
/// <typeparam name="T">Target type handled by the converter.</typeparam>
public interface IMimePartConverter<T> : IMimePartConverter
{
    /// <summary>
    /// Attempts to convert the raw content to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="rawContent">The raw textual content.</param>
    /// <param name="content">When this method returns, contains the converted content.</param>
    /// <returns><see langword="true"/> if conversion succeeded; otherwise, <see langword="false"/>.</returns>
    bool TryConvert(string rawContent, out T? content);
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
    /// <returns><see langword="true"/> if a converter exists; otherwise, <see langword="false"/>.</returns>
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
    /// <returns><see langword="true"/> if a converter exists; otherwise, <see langword="false"/>.</returns>
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
    /// <returns><see langword="true"/> if conversion succeeded; otherwise, <see langword="false"/>.</returns>
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

internal sealed class TextPartConverter :
    IMimePartConverter<string>,
    IMimePartConverter<TextReader>
{
    public string MimeType => "text/*";

    public Type[] SupportedTypes { get; } = [typeof(string), typeof(TextReader)];

    bool IMimePartConverter<string>.TryConvert(string rawContent, out string? content)
    {
        content = rawContent;
        return true;
    }

    bool IMimePartConverter<TextReader>.TryConvert(string rawContent, out TextReader? content)
    {
        content = new StringReader(rawContent);
        return true;
    }
}

internal sealed class XmlPartConverter :
    IMimePartConverter<XDocument>,
    IMimePartConverter<XmlDocument>
{
    public string MimeType => "text/xml";

    public Type[] SupportedTypes { get; } = [typeof(XDocument), typeof(XmlDocument)];

    bool IMimePartConverter<XDocument>.TryConvert(string rawContent, out XDocument? content)
    {
        content = XDocument.Parse(rawContent);
        return true;
    }

    bool IMimePartConverter<XmlDocument>.TryConvert(string rawContent, out XmlDocument? content)
    {
        var doc = new XmlDocument();
        doc.LoadXml(rawContent);
        content = doc;
        return true;
    }
}

internal sealed class JsonPartConverter : IMimePartConverter<JsonDocument>
{
    public string MimeType => "*/json";

    public Type[] SupportedTypes { get; } = [typeof(JsonDocument)];

    bool IMimePartConverter<JsonDocument>.TryConvert(string rawContent, out JsonDocument? content)
    {
        try
        {
            content = JsonDocument.Parse(rawContent);
            return true;
        }
        catch
        {
            content = default;
            return false;
        }
    }
}

internal sealed class MultipartPartConverter : IMimePartConverter<MimeDocument>
{
    public string MimeType => "multipart/*";

    public Type[] SupportedTypes { get; } = [typeof(MimeDocument)];

    bool IMimePartConverter<MimeDocument>.TryConvert(string rawContent, out MimeDocument? content)
    {
        content = MimeReader.Read(rawContent);
        return true;
    }
}

internal sealed class BinaryPartConverter :
    IMimePartConverter<byte[]>,
    IMimePartConverter<Stream>
{
    public string MimeType => "application/*";

    public Type[] SupportedTypes { get; } = [typeof(byte[]), typeof(Stream)];

    bool IMimePartConverter<byte[]>.TryConvert(string rawContent, out byte[]? content)
    {
        if (string.IsNullOrEmpty(rawContent))
        {
            content = Array.Empty<byte>();
            return true;
        }
        try
        {
            content = Convert.FromBase64String(rawContent);
            return true;
        }
        catch
        {
            content = default;
            return false;
        }
    }

    bool IMimePartConverter<Stream>.TryConvert(string rawContent, out Stream? content)
    {
        if (string.IsNullOrEmpty(rawContent))
        {
            content = new MemoryStream();
            return true;
        }
        try
        {
            var bytes = Convert.FromBase64String(rawContent);
            content = new MemoryStream(bytes);
            return true;
        }
        catch
        {
            content = default;
            return false;
        }
    }
}
