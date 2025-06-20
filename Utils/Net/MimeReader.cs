using System;
using System.IO;
using System.Text;

namespace Utils.Net;

/// <summary>
/// Provides methods for reading <see cref="MimeDocument"/> instances from various sources.
/// </summary>
public static class MimeReader
{
    /// <summary>
    /// Reads a MIME document from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream containing the MIME data.</param>
    /// <param name="encoding">Optional text encoding. Defaults to UTF-8.</param>
    /// <returns>The parsed <see cref="MimeDocument"/>.</returns>
    public static MimeDocument Read(Stream stream, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        encoding ??= Encoding.UTF8;
        using var reader = new StreamReader(stream, encoding, leaveOpen: true);
        return Read(reader);
    }

    /// <summary>
    /// Reads a MIME document from a <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">Reader containing MIME text.</param>
    /// <returns>The parsed <see cref="MimeDocument"/>.</returns>
    public static MimeDocument Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var formatter = new SimpleMimeFormatter();
        return formatter.Read(reader);
    }

    /// <summary>
    /// Reads a MIME document from a string.
    /// </summary>
    /// <param name="text">The MIME formatted string.</param>
    /// <returns>The parsed <see cref="MimeDocument"/>.</returns>
    public static MimeDocument Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var reader = new StringReader(text);
        return Read(reader);
    }

    /// <summary>
    /// Returns a deep copy of the provided document.
    /// </summary>
    /// <param name="document">The document to clone.</param>
    /// <returns>A new <see cref="MimeDocument"/> instance.</returns>
    public static MimeDocument Read(MimeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new MimeDocument(document);
    }
}
