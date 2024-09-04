using System;
using System.Collections.Generic;
using System.Xml;

namespace Utils.XML;

/// <summary>
/// Utility class for XML-related helper methods.
/// </summary>
public static class XmlUtils
{
	/// <summary>
	/// Reads and yields the immediate child elements of the current element in the XML reader.
	/// </summary>
	/// <param name="reader">The XML reader positioned at the parent element.</param>
	/// <returns>An enumerable collection of child elements represented by the XML reader.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the reader is not positioned at an element node.</exception>
	public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader)
	{
		if (reader.NodeType != XmlNodeType.Element)
		{
			throw new InvalidOperationException($"Current reader {reader.NodeType} '{reader.Name}' is not of type Element.");
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
				if (depth == 0) break;
			}

			if (reader.NodeType == XmlNodeType.Element && depth == 1)
			{
				yield return reader;
			}
		}
	}

	/// <summary>
	/// Reads and yields the immediate child elements of the current element in the XML reader with a specific name.
	/// </summary>
	/// <param name="reader">The XML reader positioned at the parent element.</param>
	/// <param name="elementName">The name of the child element to filter and return.</param>
	/// <returns>An enumerable collection of child elements with the specified name.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the reader is not positioned at an element node.</exception>
	public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader, string elementName)
	{
		if (reader.NodeType != XmlNodeType.Element)
		{
			throw new InvalidOperationException($"Current reader {reader.NodeType} '{reader.Name}' is not of type Element.");
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
				if (depth == 0) break;
			}

			if (reader.NodeType == XmlNodeType.Element && depth == 1 && reader.Name == elementName)
			{
				yield return reader;
			}
		}
	}

	/// <summary>
	/// Gets the XPath for the given XmlElement, including indexes for repeated elements at the same level.
	/// </summary>
	/// <param name="element">The XmlElement for which to generate the XPath.</param>
	/// <returns>The XPath string for the given XmlElement.</returns>
	public static string GetXPath(this XmlElement element)
	{
		if (element == null) throw new ArgumentNullException(nameof(element));

		var xpath = string.Empty;
		var current = element;

		// Traverse up to the root to build the XPath expression
		while (current != null)
		{
			var elementName = current.Name;

			// Determine the index of this element among its siblings
			var index = GetElementIndex(current);
			if (index > 1)
			{
				xpath = $"/{elementName}[{index}]" + xpath;
			}
			else
			{
				xpath = $"/{elementName}" + xpath;
			}

			// Move to the parent element
			current = current.ParentNode as XmlElement;
		}

		return xpath;
	}

	/// <summary>
	/// Gets the index of the current element among its siblings with the same name.
	/// </summary>
	/// <param name="element">The XmlElement to get the index for.</param>
	/// <returns>The index of the element (1-based index).</returns>
	private static int GetElementIndex(XmlElement element)
	{
		var index = 1;
		var sibling = element.PreviousSibling;

		// Count how many siblings with the same name come before this element
		while (sibling != null)
		{
			if (sibling is XmlElement && sibling.Name == element.Name)
			{
				index++;
			}
			sibling = sibling.PreviousSibling;
		}

		return index;
	}


}
