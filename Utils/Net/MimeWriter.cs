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
    /// <param name="encoding">
    /// Optional text encoding used for the overall wire transport. Must be an ASCII-compatible
    /// encoding (UTF-8, US-ASCII, or any single-byte encoding that maps ASCII code points
    /// identically). Non-ASCII-compatible encodings such as UTF-16 will corrupt MIME framing and
    /// boundary bytes. Defaults to <see cref="Encoding.UTF8"/> (#23).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="encoding"/> is not ASCII-compatible (#23).
    /// </exception>
    public static void Write(MimeDocument document, Stream stream, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        encoding ??= Encoding.UTF8;

        // MIME framing (headers, boundaries) requires an ASCII-compatible transport encoding.
        // Reject encodings that cannot round-trip ASCII byte values identically (#23).
        // The canonical test: encoding a single ASCII character must produce a single byte
        // whose value equals the ASCII code point.
        if (!IsAsciiCompatible(encoding))
            throw new ArgumentException(
                $"The encoding '{encoding.EncodingName}' is not ASCII-compatible and cannot be used " +
                "for MIME wire serialization. Use UTF-8 or another ASCII-compatible encoding. " +
                "Individual MIME body parts control their own content-transfer encoding and charset.",
                nameof(encoding));

        using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
        Write(document, writer);
        writer.Flush();
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="encoding"/> preserves every printable
    /// ASCII character (0x20–0x7E, inclusive) as a single byte with the same numeric value (#23).
    /// </summary>
    /// <remarks>
    /// Testing a single character (e.g. 'A') is not sufficient: some encodings (e.g. UTF-7)
    /// preserve most ASCII code points but apply special treatment to characters like '+' that
    /// are used in MIME framing.
    /// </remarks>
    private static bool IsAsciiCompatible(Encoding encoding)
    {
        // Build the 95-character printable ASCII range in one allocation.
        char[] chars = new char[0x7F - 0x20]; // 95 characters: 0x20..0x7E
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)(0x20 + i);

        byte[] bytes = encoding.GetBytes(chars);

        if (bytes.Length != chars.Length)
            return false;

        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != (byte)chars[i])
                return false;
        }

        return true;
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
