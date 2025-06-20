using System;
using System.IO;
using System.Text;

namespace Utils.Net;

/// <summary>
/// Provides methods for writing <see cref="MimeDocument"/> instances to various targets.
/// </summary>
public static class MimeWriter
{
    /// <summary>
    /// Writes the document to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="encoding">Optional text encoding. Defaults to UTF-8.</param>
    public static void Write(MimeDocument document, Stream stream, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        encoding ??= Encoding.UTF8;
        using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
        Write(document, writer);
        writer.Flush();
    }

    /// <summary>
    /// Writes the document to a <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="document">Document to write.</param>
    /// <param name="writer">Destination writer.</param>
    public static void Write(MimeDocument document, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        var formatter = new SimpleMimeFormatter();
        formatter.Write(document, writer);
    }

    /// <summary>
    /// Serializes the document to a string.
    /// </summary>
    /// <param name="document">Document to serialize.</param>
    /// <returns>The textual representation.</returns>
    public static string Write(MimeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var sw = new StringWriter();
        Write(document, sw);
        return sw.ToString();
    }
}
