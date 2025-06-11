using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;

namespace Utils.Net;

/// <summary>
/// Represents a MIME document composed of multiple parts.
/// </summary>
public class MimeDocument
{
        /// <summary>
        /// Initializes a new empty <see cref="MimeDocument"/>.
        /// </summary>
        public MimeDocument()
        {
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Parts = new List<MimePart>();
        }

        /// <summary>
        /// Initializes a new <see cref="MimeDocument"/> by copying another instance.
        /// </summary>
        /// <param name="other">The document to copy.</param>
        public MimeDocument(MimeDocument other)
        {
                if (other == null) throw new ArgumentNullException(nameof(other));
                Headers = new Dictionary<string, string>(other.Headers, StringComparer.OrdinalIgnoreCase);
                Parts = other.Parts.Select(p => new MimePart(p)).ToList();
        }

        /// <summary>
        /// Gets the document level headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the list of MIME parts contained in this document.
        /// </summary>
        public IList<MimePart> Parts { get; }

        /// <summary>
        /// Reads a MIME document from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream containing the MIME document.</param>
        /// <param name="encoding">Optional text encoding. Defaults to UTF-8.</param>
        /// <returns>A <see cref="MimeDocument"/> instance.</returns>
        public static MimeDocument Read(Stream stream, Encoding? encoding = null)
        {
                return MimeReader.Read(stream, encoding);
        }

        /// <summary>
        /// Reads a MIME document from a <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The text reader containing the MIME document.</param>
        /// <returns>A <see cref="MimeDocument"/> instance.</returns>
        public static MimeDocument Read(TextReader reader)
        {
                return MimeReader.Read(reader);
        }

        /// <summary>
        /// Reads a MIME document from a string.
        /// </summary>
        /// <param name="text">The MIME text.</param>
        /// <returns>A <see cref="MimeDocument"/> instance.</returns>
        public static MimeDocument Read(string text)
        {
                return MimeReader.Read(text);
        }

        /// <summary>
        /// Creates a deep copy of the specified document.
        /// </summary>
        /// <param name="other">The source document.</param>
        /// <returns>A new <see cref="MimeDocument"/> instance.</returns>
        public static MimeDocument Read(MimeDocument other) => MimeReader.Read(other);

        /// <summary>
        /// Writes this document to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="encoding">Optional encoding. Defaults to UTF-8.</param>
        public void Write(Stream stream, Encoding? encoding = null)
        {
                MimeWriter.Write(this, stream, encoding);
        }

        /// <summary>
        /// Writes this document to a <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer">The text writer.</param>
        public void Write(TextWriter writer)
        {
                MimeWriter.Write(this, writer);
        }

        /// <summary>
        /// Converts this document to its textual representation.
        /// </summary>
        /// <returns>The document serialized as a string.</returns>
        public override string ToString()
        {
                return MimeWriter.Write(this);
        }
}

/// <summary>
/// Represents a single MIME part.
/// </summary>
public class MimePart : IEquatable<MimePart>, IEqualityOperators<MimePart, MimePart, bool>
{
        /// <summary>
        /// Initializes a new empty <see cref="MimePart"/>.
        /// </summary>
        public MimePart()
        {
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Body = string.Empty;
        }

        /// <summary>
        /// Initializes a new <see cref="MimePart"/> by copying another instance.
        /// </summary>
        /// <param name="other">The part to copy.</param>
        public MimePart(MimePart other)
        {
                Headers = new Dictionary<string, string>(other.Headers, StringComparer.OrdinalIgnoreCase);
                Body = other.Body;
        }

        /// <summary>
        /// Gets the headers associated with this part.
        /// </summary>
        public IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets or sets the textual body of this part.
        /// </summary>
        public string Body { get; set; }


        private static bool DictionaryEquals(IDictionary<string, string> left, IDictionary<string, string> right)
        {
                if (left.Count != right.Count)
                        return false;
                foreach (var kv in left)
                {
                        if (!right.TryGetValue(kv.Key, out var value))
                                return false;
                        if (!kv.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                                return false;
                }
                return true;
        }

        /// <inheritdoc />
        public bool Equals(MimePart? other)
        {
                if (other is null) return false;
                return DictionaryEquals(Headers, other.Headers) && Body == other.Body;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as MimePart);

        /// <inheritdoc />
        public override int GetHashCode()
        {
                int hash = 0;
                foreach (var kv in Headers.OrderBy(k => k.Key))
                {
                        hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(kv.Key));
                        hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(kv.Value));
                }
                hash = HashCode.Combine(hash, Body.GetHashCode());
                return hash;
        }

        /// <summary>
        /// Equality operator comparing two parts.
        /// </summary>
        public static bool operator ==(MimePart? left, MimePart? right) => left?.Equals(right) ?? right is null;

        /// <summary>
        /// Inequality operator comparing two parts.
        /// </summary>
        public static bool operator !=(MimePart? left, MimePart? right) => !(left == right);
}

/// <summary>
/// Defines a serializer for <see cref="MimeDocument"/> instances.
/// </summary>
public interface IMimeFormatter
{
        /// <summary>
        /// Reads a <see cref="MimeDocument"/> from the specified reader.
        /// </summary>
        /// <param name="reader">The text reader containing the MIME document.</param>
        /// <returns>The parsed document.</returns>
        MimeDocument Read(TextReader reader);

        /// <summary>
        /// Writes the provided <see cref="MimeDocument"/> to a writer.
        /// </summary>
        /// <param name="document">The document to write.</param>
        /// <param name="writer">The writer that receives the output.</param>
        void Write(MimeDocument document, TextWriter writer);
}

/// <summary>
/// Basic implementation of <see cref="IMimeFormatter"/> supporting simple multipart documents.
/// </summary>
internal class SimpleMimeFormatter : IMimeFormatter
{
        /// <inheritdoc />
        public MimeDocument Read(TextReader reader)
        {
                var document = new MimeDocument();
                ReadHeaders(reader, document.Headers);

                if (!document.Headers.TryGetValue("Content-Type", out var ctValue))
                {
                        // If no content type, treat entire body as a single part of text/plain
                        var body = reader.ReadToEnd();
                        var part = new MimePart();
                        part.Headers["Content-Type"] = "text/plain";
                        part.Body = body;
                        document.Parts.Add(part);
                        return document;
                }

                var mime = MimeType.Parse(ctValue);
                if (mime.Type.Equals("multipart", StringComparison.OrdinalIgnoreCase)
                        && mime.TryGetParameter("boundary", out var boundary))
                {
                        ReadMultipart(reader, boundary!, document);
                }
                else
                {
                        var part = new MimePart();
                        part.Headers["Content-Type"] = mime.ToString();
                        part.Body = reader.ReadToEnd();
                        document.Parts.Add(part);
                }
                return document;
        }

        private static void ReadHeaders(TextReader reader, IDictionary<string, string> headers)
        {
                string? line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                        var index = line.IndexOf(':');
                        if (index > 0)
                        {
                                var name = line[..index].Trim();
                                var value = line[(index + 1)..].Trim();
                                headers[name] = value;
                        }
                }
        }

        private static void ReadMultipart(TextReader reader, string boundary, MimeDocument document)
        {
                string boundaryStart = "--" + boundary;
                string boundaryEnd = boundaryStart + "--";

                string? line = reader.ReadLine();
                while (line != null)
                {
                        if (line == boundaryStart)
                        {
                                var part = new MimePart();
                                ReadHeaders(reader, part.Headers);
                                var sb = new StringBuilder();
                                while ((line = reader.ReadLine()) != null && line != boundaryStart && line != boundaryEnd)
                                {
                                        sb.AppendLine(line);
                                }
                                part.Body = sb.ToString();
                                document.Parts.Add(part);
                                if (line == boundaryEnd || line == null)
                                        break;
                                continue; // process boundary line in next iteration
                        }

                        if (line == boundaryEnd)
                                break;

                        line = reader.ReadLine();
                }
        }

        /// <inheritdoc />
        public void Write(MimeDocument document, TextWriter writer)
        {
                foreach (var h in document.Headers)
                {
                        writer.WriteLine($"{h.Key}: {h.Value}");
                }
                writer.WriteLine();

                if (!document.Headers.TryGetValue("Content-Type", out var ctValue))
                {
                        // Write first part only
                        if (document.Parts.Count > 0)
                        {
                                var part = document.Parts[0];
                                foreach (var h in part.Headers)
                                        writer.WriteLine($"{h.Key}: {h.Value}");
                                writer.WriteLine();
                                writer.Write(part.Body);
                        }
                        return;
                }

                var mime = MimeType.Parse(ctValue);
                if (!mime.TryGetParameter("boundary", out var boundary))
                {
                        boundary = Guid.NewGuid().ToString("N");
                        mime.SetParameter("boundary", boundary);
                        document.Headers["Content-Type"] = mime.ToString();
                }

                foreach (var part in document.Parts)
                {
                        writer.WriteLine("--" + boundary);
                        foreach (var h in part.Headers)
                                writer.WriteLine($"{h.Key}: {h.Value}");
                        writer.WriteLine();
                        writer.Write(part.Body);
                        if (!part.Body.EndsWith("\n"))
                                writer.WriteLine();
                }
                writer.WriteLine("--" + boundary + "--");
        }
}

