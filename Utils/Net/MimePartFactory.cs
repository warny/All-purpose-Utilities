using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace Utils.Net;

/// <summary>
/// Defines a converter that creates <see cref="MimePart"/> instances from objects.
/// </summary>
public interface IMimePartSerializer
{
    /// <summary>
    /// Gets the MIME type produced by this serializer.
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Gets the list of types this serializer can handle.
    /// </summary>
    Type[] SupportedTypes { get; }

    /// <summary>
    /// Attempts to create a <see cref="MimePart"/> from the specified value.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="part">When this method returns, contains the resulting part.</param>
    /// <returns><c>true</c> if the value was converted; otherwise, <c>false</c>.</returns>
    bool TrySerialize<T>(T value, out MimePart? part)
    {
        if (this is IMimePartSerializer<T> typed)
            return typed.TrySerialize(value, out part);
        part = null;
        return false;
    }
}

/// <summary>
/// Typed serializer for values of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Type handled by the serializer.</typeparam>
public interface IMimePartSerializer<T> : IMimePartSerializer
{
    /// <summary>
    /// Attempts to create a MIME part from the specified value.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="part">The resulting part if successful.</param>
    /// <returns><c>true</c> if serialization succeeded; otherwise, <c>false</c>.</returns>
    bool TrySerialize(T value, out MimePart? part);
}

/// <summary>
/// Factory used to convert objects to <see cref="MimePart"/> instances.
/// </summary>
public class MimePartFactory
{
    private readonly List<IMimePartSerializer> _serializers = new();

    /// <summary>
    /// Gets a default factory with common serializers registered.
    /// </summary>
    public static MimePartFactory Default { get; } = CreateDefault();

    /// <summary>
    /// Adds a serializer to this factory.
    /// </summary>
    /// <param name="serializer">Serializer to register.</param>
    public void Add(IMimePartSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializers.Add(serializer);
    }

    /// <summary>
    /// Attempts to create a <see cref="MimePart"/> from <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="part">When this method returns, contains the converted part if successful.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryCreatePart<T>(T value, out MimePart? part)
    {
        ArgumentNullException.ThrowIfNull(value);
        var type = value.GetType();
        foreach (var s in _serializers.Where(s => s.SupportedTypes.Any(t => t.IsAssignableFrom(type))))
        {
            if (s.TrySerialize(value, out part))
            {
                if (!part!.Headers.ContainsKey("Content-Type"))
                    part.Headers["Content-Type"] = s.MimeType;
                return true;
            }
        }
        part = null;
        return false;
    }

    private static MimePartFactory CreateDefault()
    {
        var factory = new MimePartFactory();
        factory.Add(new TextPartSerializer());
        factory.Add(new XmlPartSerializer());
        factory.Add(new JsonPartSerializer());
        factory.Add(new MultipartPartSerializer());
        factory.Add(new BinaryPartSerializer());
        return factory;
    }
}

internal sealed class TextPartSerializer :
    IMimePartSerializer<string>,
    IMimePartSerializer<TextReader>
{
    public string MimeType => "text/plain";

    public Type[] SupportedTypes { get; } = [typeof(string), typeof(TextReader)];

    bool IMimePartSerializer<string>.TrySerialize(string value, out MimePart? part)
    {
        part = new MimePart { Body = value };
        return true;
    }

    bool IMimePartSerializer<TextReader>.TrySerialize(TextReader value, out MimePart? part)
    {
        part = new MimePart { Body = value.ReadToEnd() };
        return true;
    }
}

internal sealed class XmlPartSerializer :
    IMimePartSerializer<XDocument>,
    IMimePartSerializer<XmlDocument>
{
    public string MimeType => "text/xml";

    public Type[] SupportedTypes { get; } = [typeof(XDocument), typeof(XmlDocument)];

    bool IMimePartSerializer<XDocument>.TrySerialize(XDocument value, out MimePart? part)
    {
        part = new MimePart { Body = value.ToString() };
        return true;
    }

    bool IMimePartSerializer<XmlDocument>.TrySerialize(XmlDocument value, out MimePart? part)
    {
        part = new MimePart { Body = value.OuterXml };
        return true;
    }
}

internal sealed class JsonPartSerializer : IMimePartSerializer<JsonDocument>
{
    public string MimeType => "application/json";

    public Type[] SupportedTypes { get; } = [typeof(JsonDocument)];

    bool IMimePartSerializer<JsonDocument>.TrySerialize(JsonDocument value, out MimePart? part)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        value.WriteTo(writer);
        writer.Flush();
        part = new MimePart { Body = System.Text.Encoding.UTF8.GetString(stream.ToArray()) };
        return true;
    }
}

internal sealed class MultipartPartSerializer : IMimePartSerializer<MimeDocument>
{
    public string MimeType => "multipart/mixed";

    public Type[] SupportedTypes { get; } = [typeof(MimeDocument)];

    bool IMimePartSerializer<MimeDocument>.TrySerialize(MimeDocument value, out MimePart? part)
    {
        var text = MimeWriter.Write(value);
        part = new MimePart { Body = text };
        if (value.Headers.TryGetValue("Content-Type", out var ct))
            part.Headers["Content-Type"] = ct;
        return true;
    }
}

internal sealed class BinaryPartSerializer :
    IMimePartSerializer<byte[]>,
    IMimePartSerializer<Stream>
{
    public string MimeType => "application/octet-stream";

    public Type[] SupportedTypes { get; } = [typeof(byte[]), typeof(Stream)];

    bool IMimePartSerializer<byte[]>.TrySerialize(byte[] value, out MimePart? part)
    {
        part = new MimePart { Body = Convert.ToBase64String(value) };
        return true;
    }

    bool IMimePartSerializer<Stream>.TrySerialize(Stream value, out MimePart? part)
    {
        using var ms = new MemoryStream();
        value.CopyTo(ms);
        part = new MimePart { Body = Convert.ToBase64String(ms.ToArray()) };
        return true;
    }
}
