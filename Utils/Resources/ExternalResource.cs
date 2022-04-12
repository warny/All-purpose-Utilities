using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Utils.XML;

namespace Utils.Resources
{
	public class ExternalResource : IReadOnlyDictionary<string, object>
	{
		static ExternalResource() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		private readonly DirectoryInfo baseDirectory;
		private readonly Dictionary<string, IResource> resources = new Dictionary<string, IResource>();


		public ExternalResource(string resourceName) : this(AppContext.BaseDirectory, resourceName, CultureInfo.CurrentUICulture) { }
		public ExternalResource(string baseDirectory, string resourceName) : this(baseDirectory, resourceName, CultureInfo.CurrentUICulture) { }
		public ExternalResource(string resourceName, CultureInfo cultureInfo) : this(AppContext.BaseDirectory, resourceName, cultureInfo) { }

		public ExternalResource(string baseDirectory, string resourceName, CultureInfo cultureInfo)
		{
			this.baseDirectory = new DirectoryInfo(baseDirectory);
			if (!this.baseDirectory.Exists) { throw new DirectoryNotFoundException($"The directory {baseDirectory} does not exists"); }

			List<FileInfo> files = new List<FileInfo>();

			FileInfo[] resourceFiles = new FileInfo[] {
				this.baseDirectory.GetFiles(resourceName + ".resx").FirstOrDefault(),
				this.baseDirectory.GetFiles(resourceName + "." + cultureInfo.TwoLetterISOLanguageName + ".resx").FirstOrDefault(),
				this.baseDirectory.GetDirectories(cultureInfo.TwoLetterISOLanguageName).FirstOrDefault()?.GetFiles(resourceName + ".resx").FirstOrDefault(),
				this.baseDirectory.GetFiles(resourceName + "." + cultureInfo.Name + ".resx").FirstOrDefault(),
				this.baseDirectory.GetDirectories(cultureInfo.Name).FirstOrDefault()?.GetFiles(resourceName + ".resx").FirstOrDefault()
			}.Where(d=>d!=null && d.Exists).ToArray();

			foreach (var resourceFile in resourceFiles)
			{
				using (var reader = resourceFile.OpenRead())
				{
					var xmlReader = new XmlTextReader(reader);
					ReadXmlResourceFile(xmlReader);
				}
			}

		}

		public object this[string key] => resources[key].Value;

		public IEnumerable<string> Keys => resources.Keys;
		public IEnumerable<object> Values => resources.Values.Select(r=>r.Value);
		public int Count => resources.Count;

		public bool ContainsKey(string key) => resources.ContainsKey(key);

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
			=>  resources.Select(kv=> KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();

		public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
		{
			if (resources.TryGetValue(key, out var v)) {
				value = v.Value;
				return true;
			}
			value = null;
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> resources.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();

		private void ReadXmlResourceFile(XmlReader reader)
		{
			reader.ReadToDescendant("root");
			if (reader.NodeType != XmlNodeType.Element || reader.Name != "root")
			{
				return;
			}

			foreach (var r1 in reader.ReadChildElements("data"))
			{
				var name = r1.GetAttribute("name");
				var type = r1.GetAttribute("type");
				foreach (var r2 in r1.ReadChildElements("value"))
				{
					var value = r2.ReadElementContentAsString();
					if (type == null)
					{
						resources[name] = new StringResource(this, value);
					}
					else
					{
						var values = value.Split(";");
						if (values[1].StartsWith("System.String, mscorlib"))
						{
							resources[name] = new TextFileResource(this, values[0], values?[2]);
						}
						else if (values[1].StartsWith("System.Byte[], mscorlib"))
						{
							resources[name] = new BinaryFileResource(this, values[0]);
						}
						else
						{
							resources[name] = new BinaryFileResource(this, values[0], values?[1]);
						}
					}
				}
			}
		}

		public interface IResource {
			object Value { get; }
		}

		public class StringResource : IResource {
			private readonly ExternalResource externalResource;
			public object Value { get; }
			public StringResource(ExternalResource externalResource, string value)
			{
				this.externalResource = externalResource;
				Value = value;
			}
		}

		public class TextFileResource : IResource
		{
			private readonly ExternalResource externalResource;
			private readonly string filename;
			private readonly Encoding encoding;

			private object value;

			public object Value
			{
				get
				{
					value ??= File.ReadAllText(externalResource.baseDirectory.GetFiles(filename).FirstOrDefault().FullName);
					return value;
				}
			}
			public TextFileResource(ExternalResource externalResource, string filename, string encoding)
			{
				this.externalResource = externalResource;
				this.filename = filename;
				this.encoding = Encoding.GetEncoding(encoding);
			}
		}

		public class BinaryFileResource : IResource
		{
			private readonly ExternalResource externalResource;
			private readonly string filename;
			private readonly Type type;
			private object value;

			public object Value
			{
				get
				{
					if (value == null)
					{
						string fullName = externalResource.baseDirectory.GetFiles(filename).FirstOrDefault().FullName;
						if (type == null)
						{
							value = File.ReadAllBytes(fullName);
						}
						else
						{
							value = Activator.CreateInstance(type, new object[] { fullName });
						}
					}
					return value;
				}
			}
			public BinaryFileResource(ExternalResource externalResource, string filename, string typeName = null)
			{
				this.externalResource = externalResource;
				this.filename = filename;
				if (typeName != null)
				{
					type = Type.GetType(typeName);
				} 

			}
		}

	}
}
