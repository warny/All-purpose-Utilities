using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Utils.XML
{
	public static class XmlUtils
	{
		public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader)
		{
			if (reader.NodeType != XmlNodeType.Element)
			{
				throw new InvalidOperationException($"Current reader {reader.NodeType} {reader.Name} is not of type Element");
			}
			var currentName = reader.Name;
			var depth = 1;

			while (reader.Read())
			{
				if (reader.NodeType == XmlNodeType.Element && reader.Name == currentName)
				{
					depth++;
				}
				else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == currentName)
				{
					depth--;
					if (depth == 0) { break; }
				}
				if (reader.NodeType == XmlNodeType.Element && depth == 1)
				{
					yield return reader;
				}
			}
		}

		public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader, string elementName)
		{
			if (reader.NodeType != XmlNodeType.Element)
			{
				throw new InvalidOperationException($"Current reader {reader.NodeType} {reader.Name} is not of type Element");
			}
			var currentName = reader.Name;
			var depth = 1;

			while (reader.Read())
			{
				if (reader.NodeType == XmlNodeType.Element && reader.Name == currentName)
				{
					depth++;
				}
				else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == currentName)
				{
					depth--;
					if (depth == 0) { break; }
				}
				if (reader.NodeType == XmlNodeType.Element && depth == 1 && reader.Name == elementName)
				{
					yield return reader;
				}
			}
		}

	}
}
