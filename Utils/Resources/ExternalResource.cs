using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Linq;
using System;

namespace Utils.Resources;

/// <summary>
/// A cross-platform .resx reader that does not rely on <c>System.Windows.Forms</c>.
/// It manually parses .resx XML to load both inline string data
/// and external file references (similar to ResXFileRef).
///
/// This class implements <see cref="IReadOnlyDictionary{TKey,TValue}"/>
/// so that it behaves like a read-only resource collection.
/// </summary>
public sealed class ExternalResource : IReadOnlyDictionary<string, object>
{
    /// <summary>
    /// Default maximum size in bytes for any single external file resource.
    /// Override via the <c>maxExternalFileBytes</c> constructor parameter.
    /// </summary>
    public const long DefaultMaxExternalFileBytes = 10 * 1024 * 1024; // 10 MB

    private readonly Dictionary<string, object> _resources;
    private readonly long _maxExternalFileBytes;
    private readonly List<string> _diagnostics = [];

    /// <summary>
    /// Gets diagnostic messages recorded during parsing.
    /// Each message describes a skipped or malformed .resx entry, including the resource name
    /// (when available) and the reason it was not loaded (#47).
    /// </summary>
    /// <remarks>
    /// This collection is populated in permissive mode (the default). An empty collection
    /// indicates that all entries were processed successfully.
    /// </remarks>
    public IReadOnlyList<string> DiagnosticMessages => _diagnostics;

    static ExternalResource()
    {
        // Needed if you use code-page encodings on non-Windows platforms
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalResource"/> class,
    /// searching for .resx files in the current application's base directory,
    /// using the current UI culture.
    /// </summary>
    /// <param name="resourceName">The root name of the .resx file (without extension).</param>
    public ExternalResource(string resourceName)
        : this(AppContext.BaseDirectory, resourceName, CultureInfo.CurrentUICulture)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalResource"/> class,
    /// searching in a specific base directory for .resx files,
    /// using the current UI culture.
    /// </summary>
    /// <param name="baseDirectory">The folder path containing .resx files.</param>
    /// <param name="resourceName">The root name of the .resx file (without extension).</param>
    public ExternalResource(string baseDirectory, string resourceName)
        : this(baseDirectory, resourceName, CultureInfo.CurrentUICulture)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalResource"/> class,
    /// searching the current application's base directory for .resx files,
    /// but using a specified <see cref="CultureInfo"/>.
    /// </summary>
    /// <param name="resourceName">The root name of the .resx file (without extension).</param>
    /// <param name="cultureInfo">The culture to load resources for.</param>
    public ExternalResource(string resourceName, CultureInfo cultureInfo)
        : this(AppContext.BaseDirectory, resourceName, cultureInfo)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalResource"/> class,
    /// searching a specified base directory for .resx files (base and culture-specific),
    /// and merging them into a single read-only collection.
    /// </summary>
    /// <param name="baseDirectory">The folder path containing .resx files or subfolders.</param>
    /// <param name="resourceName">The root name of the .resx file (without extension).</param>
    /// <param name="cultureInfo">The culture to load resources for.</param>
    /// <param name="maxExternalFileBytes">
    /// Maximum number of bytes that may be read from any single external file resource.
    /// Defaults to <see cref="DefaultMaxExternalFileBytes"/> (#43).
    /// </param>
    /// <exception cref="DirectoryNotFoundException">If <paramref name="baseDirectory"/> does not exist.</exception>
    public ExternalResource(string baseDirectory, string resourceName, CultureInfo cultureInfo,
        long maxExternalFileBytes = DefaultMaxExternalFileBytes)
    {
        var dir = new DirectoryInfo(baseDirectory);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The directory '{dir.FullName}' does not exist.");
        }

        if (maxExternalFileBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExternalFileBytes), maxExternalFileBytes,
                "maxExternalFileBytes must be a positive value.");

        _maxExternalFileBytes = maxExternalFileBytes;
        _resources = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var candidateFiles = new List<FileInfo?>();
        candidateFiles.AddRange(dir.GetFiles($"{resourceName}.resx"));
        candidateFiles.AddRange(dir.GetFiles($"{resourceName}.{cultureInfo.TwoLetterISOLanguageName}.resx"));
        candidateFiles.AddRange(dir.GetDirectories(cultureInfo.TwoLetterISOLanguageName).SelectMany(d => d.GetFiles($"{resourceName}.resx")));
        candidateFiles.AddRange(dir.GetFiles($"{resourceName}.{cultureInfo.Name}.resx"));
        candidateFiles.AddRange(dir.GetDirectories(cultureInfo.Name).SelectMany(d => d.GetFiles($"{resourceName}.resx")));

        foreach (var file in candidateFiles.Where(f => f?.Exists ?? false))
        {
            MergeResxFile(file!);
        }
    }

    #endregion

    #region Core Parsing

    /// <summary>
    /// Reads and merges one .resx file into the dictionary.
    /// </summary>
    private void MergeResxFile(FileInfo file)
    {
        using var stream = file.OpenRead();
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true
        });

        string basePath = Path.GetDirectoryName(file.FullName) ?? string.Empty;
        while (xmlReader.Read())
        {
            if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "data")
            {
                ProcessDataElement(xmlReader, basePath);
            }
        }
    }

    /// <summary>
    /// Processes a single &lt;data&gt; element from the .resx.
    /// </summary>
    private void ProcessDataElement(XmlReader xmlReader, string basePath)
    {
        string? name = xmlReader.GetAttribute("name");
        string? type = xmlReader.GetAttribute("type");

        if (string.IsNullOrEmpty(name))
        {
            // Record a diagnostic so callers can distinguish malformed entries from absent resources (#47).
            _diagnostics.Add("Skipped <data> element: missing or empty 'name' attribute.");
            return;
        }

        string? rawValue = null;

        while (xmlReader.Read())
        {
            if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "value")
            {
                rawValue = xmlReader.ReadElementContentAsString();
                break;
            }
            else if (xmlReader.NodeType == XmlNodeType.EndElement && xmlReader.Name == "data")
            {
                break;
            }
        }

        if (rawValue is null)
        {
            _diagnostics.Add($"Skipped resource '{name}': no <value> element found in <data> block (#47).");
            return;
        }

        if (type != null && type.Contains("ResXFileRef", StringComparison.OrdinalIgnoreCase))
        {
            var splitted = rawValue.Split(';');

            if (splitted.Length >= 2)
            {
                string relativePath = splitted[0].Replace('\\', Path.DirectorySeparatorChar);
                string candidatePath = Path.GetFullPath(Path.Combine(basePath, relativePath));

                // Guard against path traversal (#40). Use platform-appropriate comparison:
                // OrdinalIgnoreCase on Windows (case-insensitive FS), Ordinal on case-sensitive systems.
                StringComparison pathComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                string allowedRoot = Path.GetFullPath(basePath).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!candidatePath.StartsWith(allowedRoot, pathComparison))
                {
                    // Path traversal attempt detected: record a diagnostic and skip (#40, #47).
                    _diagnostics.Add(
                        $"Skipped resource '{name}': file reference '{relativePath}' resolves outside " +
                        $"the allowed directory '{allowedRoot}' (path-traversal guard).");
                    return;
                }

                // Reject paths that contain a symlink or junction point anywhere in the hierarchy.
                // A lexical containment check is not sufficient because a symlink inside allowedRoot
                // can redirect to any arbitrary location on the file system (#40).
                if (ContainsSymlinkOrJunction(candidatePath))
                {
                    _diagnostics.Add(
                        $"Skipped resource '{name}': file reference '{relativePath}' contains a " +
                        "symbolic link or junction point (security policy).");
                    return;
                }

                string filePath = candidatePath;
                string typeOrEncoding = splitted[1];

                // ResXFileRef value format: "path;type[;encoding]"
                // Recognized type patterns (allowlist to prevent reflective construction - #41):
                //   1. "System.String..." — text file.  Encoding comes from splitted[2] if present.
                //   2. "...Encoding..."  — legacy text-file shorthand using an encoding class name.
                //   3. "...Byte[]..."   — binary file.
                // Any other type string is silently rejected (#41).

                if (typeOrEncoding.Contains("System.String", StringComparison.OrdinalIgnoreCase))
                {
                    // Standard ResXFileRef text format: the encoding is in splitted[2] if provided.
                    Encoding enc = splitted.Length >= 3
                        ? GetEncodingFromCodePageOrName(splitted[2])
                        : Encoding.UTF8;
                    _resources[name] = new LazyTextFile(filePath, enc, _maxExternalFileBytes);
                }
                else if (typeOrEncoding.Contains("Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    // Legacy shorthand: encoding class name is the type itself.
                    Encoding enc = GetEncodingFromTypeName(typeOrEncoding);
                    _resources[name] = new LazyTextFile(filePath, enc, _maxExternalFileBytes);
                }
                else if (typeOrEncoding.Contains("Byte[]", StringComparison.OrdinalIgnoreCase))
                {
                    _resources[name] = new LazyBinaryFile(filePath, _maxExternalFileBytes);
                }
                else
                {
                    // Reject arbitrary custom type construction (#41).
                    // Unknown ResXFileRef types are treated as unsupported data rather
                    // than being constructed via reflection (code-execution risk).
                    // Record a diagnostic so callers can detect unsupported configurations (#47).
                    _diagnostics.Add(
                        $"Skipped resource '{name}': ResXFileRef type '{typeOrEncoding}' is not " +
                        "in the supported allowlist (System.String, Encoding, Byte[]). " +
                        "Reflective construction is disabled (#41).");
                    return;
                }
            }
            else
            {
                _resources[name] = rawValue;
            }
        }
        else
        {
            _resources[name] = rawValue;
        }
    }

#pragma warning disable SYSLIB0001 // UTF-7 is obsolete but kept for backward compatibility with existing .resx files
    /// <summary>
    /// Immutable map of known encoding type names to <see cref="Encoding"/> instances.
    /// A <see cref="ConcurrentDictionary{TKey,TValue}"/> is used for safe concurrent extension (#44).
    /// </summary>
    private static readonly ConcurrentDictionary<string, Encoding> _encodings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Text.ASCIIEncoding"] = Encoding.ASCII,
            ["System.Text.UTF8Encoding"] = Encoding.UTF8,
            ["System.Text.UnicodeEncoding"] = Encoding.Unicode,
            ["System.Text.UTF7Encoding"] = Encoding.UTF7,
            ["System.Text.UTF32Encoding"] = Encoding.UTF32,
        };
#pragma warning restore SYSLIB0001

    /// <summary>
    /// Returns an <see cref="Encoding"/> for the given type name, or
    /// <see cref="Encoding.UTF8"/> when the name is not recognized (#44, #45).
    /// The result is cached in a thread-safe dictionary.
    /// </summary>
    private static Encoding GetEncodingFromTypeName(string typeName)
    {
        // Try the pre-populated known-name map first (fast, no reflection).
        if (_encodings.TryGetValue(typeName, out Encoding? result))
            return result;

        // Unknown encoding name: fall back to UTF-8 (deterministic across platforms).
        return Encoding.UTF8;
    }

    /// <summary>
    /// Returns an <see cref="Encoding"/> identified by a code-page name (e.g. "Windows-1252") or
    /// by the standard <see cref="Encoding.WebName"/> identifier.  Falls back to
    /// <see cref="Encoding.UTF8"/> when the identifier is unrecognized.
    /// </summary>
    private static Encoding GetEncodingFromCodePageOrName(string encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
            return Encoding.UTF8;

        string key = encodingName.Trim();
        return _encodings.GetOrAdd(key, k =>
        {
            try { return Encoding.GetEncoding(k); }
            catch { return Encoding.UTF8; }
        });
    }

    #endregion

    #region IReadOnlyDictionary<string, object> — uniform value resolution (#42)

    /// <summary>
    /// Resolves a stored entry to its logical value, unwrapping lazy wrappers.
    /// </summary>
    private static object ResolveValue(object raw) => raw is IResXValue v ? v.Value : raw;

    /// <summary>
    /// Gets the resource value for the specified key.
    /// </summary>
    public object this[string key] => ResolveValue(_resources[key]);

    /// <summary>
    /// Gets all resource keys in the dictionary.
    /// </summary>
    public IEnumerable<string> Keys => _resources.Keys;

    /// <summary>
    /// Gets all resolved resource values in the dictionary (#42).
    /// </summary>
    public IEnumerable<object> Values => _resources.Values.Select(ResolveValue);

    /// <summary>
    /// Gets the total number of resources loaded.
    /// </summary>
    public int Count => _resources.Count;

    /// <summary>
    /// Checks if the specified key is present in the resources.
    /// </summary>
    public bool ContainsKey(string key) => _resources.ContainsKey(key);

    /// <summary>
    /// Tries to get the resolved resource value for the specified key (#42).
    /// </summary>
    public bool TryGetValue(string key, out object value)
    {
        if (_resources.TryGetValue(key, out object? raw))
        {
            value = ResolveValue(raw);
            return true;
        }
        value = null!;
        return false;
    }

    /// <summary>
    /// Returns an enumerator of key-resolved-value pairs for all resources (#42).
    /// </summary>
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        => _resources.Select(kv => new KeyValuePair<string, object>(kv.Key, ResolveValue(kv.Value))).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    private interface IResXValue
    {
        object Value { get; }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> or any of its ancestor
    /// directories in the file system is a symbolic link or a reparse-point junction (#40).
    /// </summary>
    private static bool ContainsSymlinkOrJunction(string path)
    {
        string? current = path;
        while (!string.IsNullOrEmpty(current))
        {
            // Check as a file first (the leaf node), then as a directory (intermediate nodes).
            var fi = new FileInfo(current);
            if (fi.Exists && fi.LinkTarget is not null)
                return true;

            var di = new DirectoryInfo(current);
            if (di.Exists && di.LinkTarget is not null)
                return true;

            string? parent = Path.GetDirectoryName(current);
            if (parent == current) break; // reached the root
            current = parent;
        }
        return false;
    }

    #region Lazy Wrappers

    /// <summary>
    /// Represents a lazily loaded text file resource.
    /// </summary>
    private sealed class LazyTextFile : IResXValue
    {
        private readonly string _filePath;
        private readonly Encoding _encoding;
        private readonly long _maxBytes;
        private string? _cachedContent;

        public LazyTextFile(string filePath, Encoding encoding, long maxBytes)
        {
            _filePath = filePath;
            _encoding = encoding;
            _maxBytes = maxBytes;
        }

        /// <summary>
        /// Retrieves the text from the file, loading it only once.
        /// </summary>
        public object Value
        {
            get
            {
                if (_cachedContent is null)
                {
                    string fullPath = Path.GetFullPath(_filePath);
                    // Open the file once so the length check and the read share the same
                    // file-system handle — eliminates the TOCTOU window (#43).
                    using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (fs.Length > _maxBytes)
                        throw new InvalidOperationException(
                            $"External resource file '{fullPath}' ({fs.Length} bytes) exceeds the " +
                            $"configured maximum of {_maxBytes} bytes.");

                    // Read up to _maxBytes + 1 raw bytes from the same stream.
                    // Reading one extra byte lets us detect in-flight file growth.
                    byte[] buffer = new byte[_maxBytes + 1];
                    int total = 0, read;
                    while ((read = fs.Read(buffer, total, buffer.Length - total)) > 0)
                        total += read;

                    if (total > _maxBytes)
                        throw new InvalidOperationException(
                            $"External resource file '{fullPath}' grew beyond {_maxBytes} bytes during reading.");

                    _cachedContent = _encoding.GetString(buffer, 0, total);
                }
                return _cachedContent;
            }
        }

        public override string ToString() => Value?.ToString() ?? "";
    }

    /// <summary>
    /// Represents a lazily loaded binary file resource.
    /// </summary>
    private sealed class LazyBinaryFile : IResXValue
    {
        private readonly string _filePath;
        private readonly long _maxBytes;
        private byte[]? _cachedBytes;

        public LazyBinaryFile(string filePath, long maxBytes)
        {
            _filePath = filePath;
            _maxBytes = maxBytes;
        }

        /// <summary>
        /// Reads all bytes from the file, loading them only once.
        /// Returns a defensive copy on each access so callers cannot mutate the cache (#46).
        /// </summary>
        public object Value
        {
            get
            {
                if (_cachedBytes is null)
                {
                    string fullPath = Path.GetFullPath(_filePath);
                    // Open the file once so the length check and the read share the same
                    // file-system handle — eliminates the TOCTOU window (#43).
                    using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (fs.Length > _maxBytes)
                        throw new InvalidOperationException(
                            $"External resource file '{fullPath}' ({fs.Length} bytes) exceeds the " +
                            $"configured maximum of {_maxBytes} bytes.");

                    // Read up to _maxBytes + 1 bytes from the same stream.
                    // Reading one extra byte lets us detect in-flight file growth.
                    byte[] buffer = new byte[_maxBytes + 1];
                    int total = 0, read;
                    while ((read = fs.Read(buffer, total, buffer.Length - total)) > 0)
                        total += read;

                    if (total > _maxBytes)
                        throw new InvalidOperationException(
                            $"External resource file '{fullPath}' grew beyond {_maxBytes} bytes during reading.");

                    _cachedBytes = new byte[total];
                    Buffer.BlockCopy(buffer, 0, _cachedBytes, 0, total);
                }
                // Return a copy so callers cannot mutate the cached array (#46).
                return (byte[])_cachedBytes.Clone();
            }
        }
    }

    #endregion
}
