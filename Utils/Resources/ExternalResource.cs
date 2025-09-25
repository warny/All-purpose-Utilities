using System.Collections;
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
	private readonly Dictionary<string, object> _resources;

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
	/// <exception cref="DirectoryNotFoundException">If <paramref name="baseDirectory"/> does not exist.</exception>
	public ExternalResource(string baseDirectory, string resourceName, CultureInfo cultureInfo)
	{
		var dir = new DirectoryInfo(baseDirectory);
		if (!dir.Exists)
		{
			throw new DirectoryNotFoundException(
				$"The directory '{dir.FullName}' does not exist.");
		}

		_resources = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		// This replicates typical resource fallback layering: 
		//   1. base resx (resourceName.resx)
		//   2. resourceName.[lang].resx
		//   3. subfolder lang -> resourceName.resx
		//   4. resourceName.[lang-region].resx
		//   5. subfolder lang-region -> resourceName.resx
		var candidateFiles = new List<FileInfo?>();
		candidateFiles.AddRange(dir.GetFiles($"{resourceName}.resx"));
		candidateFiles.AddRange(dir.GetFiles($"{resourceName}.{cultureInfo.TwoLetterISOLanguageName}.resx"));
		candidateFiles.AddRange(dir.GetDirectories(cultureInfo.TwoLetterISOLanguageName).SelectMany(d => d.GetFiles($"{resourceName}.resx")));
		candidateFiles.AddRange(dir.GetFiles($"{resourceName}.{cultureInfo.Name}.resx"));
		candidateFiles.AddRange(dir.GetDirectories(cultureInfo.Name).SelectMany(d => d.GetFiles($"{resourceName}.resx")));

		// Merge each file (later entries overwrite earlier ones if key collisions occur).
		foreach (var file in candidateFiles.Where(f => f?.Exists ?? false))
		{
			MergeResxFile(file!);
		}
	}

	#endregion

	#region Core Parsing

	/// <summary>
	/// Reads and merges one .resx file into the dictionary. If the same key 
	/// was already present, it is overwritten by this file's entry.
	/// </summary>
	/// <param name="file">The .resx file to parse.</param>
	private void MergeResxFile(FileInfo file)
	{
		using var stream = file.OpenRead();
		using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
		{
			// You can customize XML settings if needed
			IgnoreComments = true,
			IgnoreWhitespace = true
		});

		// We expect something like:
		// <root>
		//   <resheader>...</resheader>
		//   <data name="key" xml:space="preserve" ...>
		//     <value>some text</value>
		//   </data>
		//   <data name="fileRefKey" type="System.Resources.ResXFileRef, System.Windows.Forms"> 
		//     <value>filename;System.Text.UTF8Encoding</value>
		//   </data>
		// </root>
                string basePath = Path.GetDirectoryName(file.FullName) ?? string.Empty;
                while (xmlReader.Read())
                {
                        // Advance to "data" elements
                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "data")
                        {
                                ProcessDataElement(xmlReader, basePath);
                        }
                }
	}

	/// <summary>
	/// Processes a single &lt;data&gt; element from the .resx, 
	/// reading "name", "type" attributes, and nested &lt;value&gt;.
	/// </summary>
	/// <param name="xmlReader">The XmlReader positioned on &lt;data&gt; start.</param>
        private void ProcessDataElement(XmlReader xmlReader, string basePath)
	{
		// We want to read the attributes of <data> before we move on:
		// e.g. <data name="MyKey" type="System.Resources.ResXFileRef, ...">
		string? name = xmlReader.GetAttribute("name");
		string? type = xmlReader.GetAttribute("type");

		if (string.IsNullOrEmpty(name))
		{
			// No key => skip
			return;
		}

		string? rawValue = null;

		// We have to move the reader forward so we can read child <value> elements
		while (xmlReader.Read())
		{
			if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "value")
			{
				// Grab the text inside <value></value>
				rawValue = xmlReader.ReadElementContentAsString();
				// After reading <value> content, the reader is on the end tag, 
				// so we can break if we want only the first <value> child
				break;
			}
			else if (xmlReader.NodeType == XmlNodeType.EndElement && xmlReader.Name == "data")
			{
				// We reached the end of this <data> element without finding <value>, so break
				break;
			}
		}

		if (rawValue is null)
		{
			// No <value> found => skip
			return;
		}

		// Check if it's a ResXFileRef or just a plain string
		// The "type" attribute might be e.g. "System.Resources.ResXFileRef, System.Windows.Forms"
		// But we do NOT want to rely on Windows.Forms, so let's parse it ourselves.
		// A standard ResXFileRef <value> looks like "filename;System.Text.UTF8Encoding"
		// or possibly "filename;System.Byte[], mscorlib" for binary data, etc.
		if (type != null && type.Contains("ResXFileRef", StringComparison.OrdinalIgnoreCase))
		{
			// It's an external file reference
			// Typically "filename;[type]" or "filename;[type];[extra params]"
			var splitted = rawValue.Split(';');

			if (splitted.Length >= 2)
			{
				// splitted[0] = path
				// splitted[1] = type or encoding
				// splitted[2..] = more optional parameters (e.g. for text encoding or reflection type info)
                                string relativePath = splitted[0].Replace('\\', Path.DirectorySeparatorChar);
                                string candidatePath = Path.GetFullPath(Path.Combine(basePath, relativePath));
                                if (!File.Exists(candidatePath))
                                {
                                        candidatePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(basePath) ?? string.Empty, relativePath));
                                }
                                string filePath = candidatePath;
				string typeOrEncoding = splitted[1];

				// If it’s a recognized encoding name (like "System.Text.UTF8Encoding"), 
				// treat it as a text file reference:
				if (typeOrEncoding.Contains("Encoding", StringComparison.OrdinalIgnoreCase))
				{
					// Some ResXFileRefs for text look like "myFile.txt;System.Text.UTF8Encoding"
					// or "myFile.txt;System.Text.ASCIIEncoding"
					// We can parse the type name to get an actual Encoding instance. 
					// If you only care about a subset, or want a direct match, adapt as needed.
					Encoding enc = GetEncodingFromTypeName(typeOrEncoding);

					// Optionally check if splitted[2] is something else. 
					// For now, we treat it simply as text.
					_resources[name] = new LazyTextFile(filePath, enc);
				}
				else if (typeOrEncoding.Contains("Byte[]", StringComparison.OrdinalIgnoreCase))
				{
					// It's presumably binary data => store as lazy bytes
					_resources[name] = new LazyBinaryFile(filePath);
				}
				else
				{
					// Possibly a custom type. The .resx might say "filename;MyAssembly.MyCustomType, MyAssembly"
					// We'll store it as a lazy "object from file constructor" if you want:
					var customType = Type.GetType(typeOrEncoding, throwOnError: false);
					_resources[name] = new LazyCustomObject(filePath, customType);
				}
			}
			else
			{
				// Malformed? We'll just store the rawValue as plain text
				_resources[name] = rawValue;
			}
		}
		else
		{
			// Plain inline string data => store as-is
			_resources[name] = rawValue;
		}
	}

#pragma warning disable SYSLIB0001 // Le type ou le membre est obsolète
	private static readonly Dictionary<string, Encoding> _encodings = new(StringComparer.InvariantCultureIgnoreCase)
	{
		{"System.Text.ASCIIEncoding", Encoding.ASCII },
		{"System.Text.UTF8Encoding", Encoding.UTF8},
		{"System.Text.UnicodeEncoding", Encoding.Unicode},
		{"System.Text.UTF7Encoding", Encoding.UTF7},
		{ "System.Text.UTF32Encoding", Encoding.UTF32},
	};
#pragma warning restore SYSLIB0001 // Le type ou le membre est obsolète

	/// <summary>
	/// Tries to instantiate an <see cref="Encoding"/> from a type name,
	/// e.g. "System.Text.UTF8Encoding" or "System.Text.ASCIIEncoding".
	/// Returns <see cref="Encoding.Default"/> if unknown.
	/// </summary>
	private static Encoding GetEncodingFromTypeName(string typeName)
	{
		// This is a naive approach. 
		// In a real library, you'd map known type names more robustly:
		// "System.Text.UTF8Encoding" => Encoding.UTF8
		// "System.Text.ASCIIEncoding" => Encoding.ASCII
		// ...
		try
		{
			if (_encodings.TryGetValue(typeName, out var result)) return result;
			result = (Encoding)Activator.CreateInstance(Type.GetType(typeName, throwOnError: false));
			result ??= Encoding.Default;
			_encodings.Add(typeName, result);
			return result;
		}
		catch
		{
			// ignore
		}
		return Encoding.Default;
	}

	#endregion

	#region IReadOnlyDictionary<string, object> Implementation

	/// <summary>
	/// Gets the resource value for the specified key.
	/// </summary>
	public object this[string key] => _resources[key] is IResXValue value ? value.Value : _resources[key];

	/// <summary>
	/// Gets all resource keys in the dictionary.
	/// </summary>
	public IEnumerable<string> Keys => _resources.Keys;

	/// <summary>
	/// Gets all resource values in the dictionary.
	/// </summary>
	public IEnumerable<object> Values => _resources.Values;

	/// <summary>
	/// Gets the total number of resources loaded.
	/// </summary>
	public int Count => _resources.Count;

	/// <summary>
	/// Checks if the specified key is present in the resources.
	/// </summary>
	public bool ContainsKey(string key) => _resources.ContainsKey(key);

	/// <summary>
	/// Tries to get the resource value for the specified key.
	/// </summary>
	public bool TryGetValue(string key, out object value) => _resources.TryGetValue(key, out value);

	/// <summary>
	/// Returns an enumerator of key-value pairs for all resources in the dictionary.
	/// </summary>
	public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _resources.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	#endregion

	private interface IResXValue {
		public object Value { get; }
	}

	#region Lazy Wrappers

	/// <summary>
	/// Represents a lazily loaded text file resource, 
	/// created when its <see cref="Value"/> is first accessed.
	/// </summary>
	private sealed class LazyTextFile : IResXValue
	{
		private readonly string _filePath;
		private readonly Encoding _encoding;
		private string? _cachedContent;

		public LazyTextFile(string filePath, Encoding encoding)
		{
			_filePath = filePath;
			_encoding = encoding;
		}

		/// <summary>
		/// Retrieves the text from the file, loading it only once.
		/// </summary>
		public object Value
		{
			get {
				if (_cachedContent is null)
				{
					// Resolve path relative to current directory if needed
					// or leave it as-is if absolute
					string fullPath = Path.GetFullPath(_filePath);
					_cachedContent = File.ReadAllText(fullPath, _encoding);
				}
				return _cachedContent;
			}
		}

		public override string ToString() => Value?.ToString() ?? "";
	}

	/// <summary>
	/// Represents a lazily loaded binary file resource (returns a <c>byte[]</c>).
	/// </summary>
	private sealed class LazyBinaryFile : IResXValue
	{
		private readonly string _filePath;
		private byte[]? _cachedBytes;

		public LazyBinaryFile(string filePath)
		{
			_filePath = filePath;
		}

		/// <summary>
		/// Reads all bytes from the file, loading them only once.
		/// </summary>
		public object Value
		{
			get {
				if (_cachedBytes is null)
				{
					string fullPath = Path.GetFullPath(_filePath);
					_cachedBytes = File.ReadAllBytes(fullPath);
				}
				return _cachedBytes;
			}
		}
	}

	/// <summary>
	/// Represents a lazily constructed object from a file, 
	/// e.g. if the .resx specified <c>filename;MyNamespace.MyType</c>.
	/// </summary>
	private sealed class LazyCustomObject : IResXValue
	{
		private readonly string _filePath;
		private readonly Type? _type;
		private object? _instance;

		public LazyCustomObject(string filePath, Type? customType)
		{
			_filePath = filePath;
			_type = customType;
		}

		/// <summary>
		/// If a type was provided, this instantiates the type via reflection, 
		/// passing the file path to its constructor. 
		/// Otherwise, returns a byte[].
		/// </summary>
		public object Value
		{
			get {
				if (_instance is null)
				{
					string fullPath = Path.GetFullPath(_filePath);
					if (_type == null)
					{
						// If we have no type, default to raw binary
						_instance = File.ReadAllBytes(fullPath);
					}
					else if (_type == typeof(string))
					{
						// If the type has a constructor that takes (string path), 
						// create an instance:
                                                _instance = File.ReadAllText(fullPath);
					}
					else
					{
						// If the type has a constructor that takes (string path), 
						// create an instance:
						var datas = File.ReadAllBytes(fullPath);
						_instance = Activator.CreateInstance(_type, datas);
					}
				}
				return _instance;
			}
		}
	}

	#endregion
}
