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
    /// <summary>
    /// Stores the registered converters used to transform MIME body content.
    /// </summary>
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

    /// <summary>
    /// Returns converters matching the supplied MIME type, ordered from the most specific pattern to the least specific one.
    /// </summary>
    /// <param name="mime">The MIME type to match.</param>
    /// <returns>An ordered sequence of converters that can handle the MIME type.</returns>
    private IEnumerable<IMimePartConverter> GetConverters(MimeType mime)
    {
        return _converters
            .Where(c => Matches(c.MimeType, mime))
            .OrderByDescending(c => Specificity(c.MimeType));
    }

    /// <summary>
    /// Determines whether a converter MIME mask matches a concrete MIME type.
    /// </summary>
    /// <param name="pattern">The converter MIME mask that can include wildcards.</param>
    /// <param name="mime">The concrete MIME type to evaluate.</param>
    /// <returns><see langword="true"/> when the mask matches; otherwise, <see langword="false"/>.</returns>
    private static bool Matches(string pattern, MimeType mime)
    {
        var parts = pattern.Split('/', 2);
        var pt = parts[0];
        var ps = parts.Length > 1 ? parts[1] : "*";
        bool typeMatch = pt == "*" || mime.Type.Equals(pt, StringComparison.OrdinalIgnoreCase);
        bool subMatch = ps == "*" || mime.SubType.Equals(ps, StringComparison.OrdinalIgnoreCase);
        return typeMatch && subMatch;
    }

    /// <summary>
    /// Computes a specificity score for a MIME mask to support converter ordering.
    /// </summary>
    /// <param name="pattern">The converter MIME mask.</param>
    /// <returns>
    /// A score between 0 and 2 where higher values indicate more specific masks.
    /// </returns>
    private static int Specificity(string pattern)
    {
        var parts = pattern.Split('/', 2);
        int score = 0;
        if (parts[0] != "*") score++;
        if (parts.Length > 1 && parts[1] != "*") score++;
        return score;
    }
}

/// <summary>
/// Converts textual MIME parts to string-based representations.
/// </summary>
internal sealed class TextPartConverter :
    IMimePartConverter<string>,
    IMimePartConverter<TextReader>
{
    /// <inheritdoc/>
    public string MimeType => "text/*";

    /// <inheritdoc/>
    public Type[] SupportedTypes { get; } = [typeof(string), typeof(TextReader)];

    /// <summary>
    /// Returns the raw text body unchanged.
    /// </summary>
    /// <param name="rawContent">The MIME part body as text.</param>
    /// <param name="content">The resulting string content.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    bool IMimePartConverter<string>.TryConvert(string rawContent, out string? content)
    {
        content = rawContent;
        return true;
    }

    /// <summary>
    /// Wraps the raw text body into a <see cref="TextReader"/> instance.
    /// </summary>
    /// <param name="rawContent">The MIME part body as text.</param>
    /// <param name="content">The resulting reader that can stream text sequentially.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    bool IMimePartConverter<TextReader>.TryConvert(string rawContent, out TextReader? content)
    {
        content = new StringReader(rawContent);
        return true;
    }
}

/// <summary>
/// Converts XML MIME parts to XML document objects.
/// </summary>
internal sealed class XmlPartConverter :
    IMimePartConverter<XDocument>,
    IMimePartConverter<XmlDocument>
{
    /// <inheritdoc/>
    public string MimeType => "text/xml";

    /// <inheritdoc/>
    public Type[] SupportedTypes { get; } = [typeof(XDocument), typeof(XmlDocument)];

    /// <summary>
    /// Parses XML text into an <see cref="XDocument"/> instance.
    /// </summary>
    /// <param name="rawContent">The MIME part body containing XML text.</param>
    /// <param name="content">The parsed LINQ-to-XML document.</param>
    /// <returns>Always <see langword="true"/> when parsing does not throw.</returns>
    bool IMimePartConverter<XDocument>.TryConvert(string rawContent, out XDocument? content)
    {
        content = XDocument.Parse(rawContent);
        return true;
    }

    /// <summary>
    /// Parses XML text into an <see cref="XmlDocument"/> instance.
    /// </summary>
    /// <param name="rawContent">The MIME part body containing XML text.</param>
    /// <param name="content">The parsed DOM XML document.</param>
    /// <returns>Always <see langword="true"/> when parsing does not throw.</returns>
    bool IMimePartConverter<XmlDocument>.TryConvert(string rawContent, out XmlDocument? content)
    {
        var doc = new XmlDocument();
        doc.LoadXml(rawContent);
        content = doc;
        return true;
    }
}

/// <summary>
/// Converts JSON MIME parts to <see cref="JsonDocument"/>.
/// </summary>
internal sealed class JsonPartConverter : IMimePartConverter<JsonDocument>
{
    /// <inheritdoc/>
    public string MimeType => "*/json";

    /// <inheritdoc/>
    public Type[] SupportedTypes { get; } = [typeof(JsonDocument)];

    /// <summary>
    /// Parses JSON text into a JSON DOM document.
    /// </summary>
    /// <param name="rawContent">The MIME part body containing JSON text.</param>
    /// <param name="content">The parsed JSON document when conversion succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
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

/// <summary>
/// Converts multipart MIME parts into a <see cref="MimeDocument"/> model.
/// </summary>
internal sealed class MultipartPartConverter : IMimePartConverter<MimeDocument>
{
    /// <inheritdoc/>
    public string MimeType => "multipart/*";

    /// <inheritdoc/>
    public Type[] SupportedTypes { get; } = [typeof(MimeDocument)];

    /// <summary>
    /// Parses the multipart payload using the MIME reader.
    /// </summary>
    /// <param name="rawContent">The MIME part body containing multipart content.</param>
    /// <param name="content">The resulting <see cref="MimeDocument"/>.</param>
    /// <returns>Always <see langword="true"/> when parsing does not throw.</returns>
    bool IMimePartConverter<MimeDocument>.TryConvert(string rawContent, out MimeDocument? content)
    {
        content = MimeReader.Read(rawContent);
        return true;
    }
}

/// <summary>
/// Converts application MIME parts containing Base64-encoded binary payloads.
/// </summary>
internal sealed class BinaryPartConverter :
    IMimePartConverter<byte[]>,
    IMimePartConverter<Stream>
{
    /// <inheritdoc/>
    public string MimeType => "application/*";

    /// <inheritdoc/>
    public Type[] SupportedTypes { get; } = [typeof(byte[]), typeof(Stream)];

    /// <summary>
    /// Converts Base64 text into a binary byte array.
    /// </summary>
    /// <param name="rawContent">The MIME part body encoded as Base64 text.</param>
    /// <param name="content">Decoded binary payload bytes when conversion succeeds.</param>
    /// <returns><see langword="true"/> when the payload is empty or valid Base64; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method handles binary data transformation by decoding the textual transport representation into raw bytes.
    /// </remarks>
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

    /// <summary>
    /// Converts Base64 text into a readable in-memory stream.
    /// </summary>
    /// <param name="rawContent">The MIME part body encoded as Base64 text.</param>
    /// <param name="content">A stream exposing the decoded binary payload.</param>
    /// <returns><see langword="true"/> when the payload is empty or valid Base64; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This stream-oriented conversion first decodes binary bytes, then exposes them through <see cref="MemoryStream"/> for sequential reading.
    /// </remarks>
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
