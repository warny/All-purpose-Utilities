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
    /// <param name="elementName">
    /// Optional name filter. When specified, only child elements with this qualified name are yielded.
    /// When <see langword="null"/>, all immediate child elements are yielded.
    /// </param>
    /// <returns>
    /// An enumerable sequence of child element positions represented by the same <see cref="XmlReader"/>
    /// instance. Each yielded position is valid only until the next iteration step or reader mutation.
    /// Consume or skip each subtree before advancing the iterator.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reader is not positioned at an element node.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The iterator uses <see cref="XmlReader.Depth"/> to track nesting, so elements with different
    /// names at any depth are handled correctly. An empty parent element (<c>&lt;parent /&gt;</c>)
    /// returns no children and does not advance the reader past sibling elements.
    /// </para>
    /// <para>
    /// <b>Reader-state contract:</b> the underlying <see cref="XmlReader"/> is shared across
    /// iterations. If the loop body partially consumes the current element's subtree, the iterator
    /// calls <see cref="XmlReader.Skip"/> to advance past it before yielding the next sibling.
    /// Early disposal of the enumerator leaves the reader positioned at the end-element of the
    /// parent or inside an unconsumed child subtree.
    /// </para>
    /// </remarks>
    public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader, string? elementName = null)
    {
        if (reader.NodeType != XmlNodeType.Element)
        {
            throw new InvalidOperationException(
                $"Current reader {reader.NodeType} '{reader.Name}' is not of type Element.");
        }

        // Fix #2: empty parent elements have no children.
        if (reader.IsEmptyElement)
        {
            yield break;
        }

        // Fix #1: capture depth to use reader.Depth instead of name-based tracking.
        int parentDepth = reader.Depth;

        while (reader.Read())
        {
            // Stop when the matching end element at the parent depth is reached.
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == parentDepth)
            {
                break;
            }

            // Yield direct children only; skip deeper descendants automatically.
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == parentDepth + 1)
            {
                if (elementName == null || reader.Name == elementName)
                {
                    yield return reader;

                    // Fix #3: if the consumer did not advance past this child, skip it so the
                    // outer loop stays on track.
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == parentDepth + 1)
                    {
                        reader.Skip();
                    }
                }
                else
                {
                    // Wrong name: skip the subtree so we don't accidentally yield its descendants.
                    reader.Skip();
                }
            }
        }
    }

    /// <summary>
    /// Gets the XPath expression that uniquely identifies the given XML node in its document.
    /// </summary>
    /// <param name="node">The XML node for which to generate the XPath.</param>
    /// <returns>
    /// A non-empty XPath string for <see cref="XmlDocument"/>, <see cref="XmlElement"/>, and
    /// <see cref="XmlAttribute"/> nodes. The document root returns <c>"/"</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown for node kinds that do not have a meaningful unique XPath representation, such as
    /// text, comment, processing-instruction, CDATA, and entity-reference nodes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Generated paths use <see cref="XmlNode.Name"/>, which includes any document-specific namespace
    /// prefix (e.g. <c>/ns:root/ns:child</c>). The returned XPath can only be evaluated correctly
    /// when the same prefix-to-URI mappings are registered in an <see cref="XmlNamespaceManager"/>
    /// used at evaluation time.
    /// </para>
    /// <para>
    /// Elements that have a sibling with the same qualified name — either before or after — always
    /// include a positional predicate (e.g. <c>/root/item[1]</c>) to uniquely identify the node.
    /// </para>
    /// </remarks>
    public static string GetXPath(this XmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.NodeType switch
        {
            XmlNodeType.Document => "/",
            XmlNodeType.Element => GetElementXPath((XmlElement)node),
            XmlNodeType.Attribute => GetAttributeXPath((XmlAttribute)node),
            _ => throw new NotSupportedException(
                $"XPath generation is not supported for {node.NodeType} nodes.")
        };
    }

    /// <summary>
    /// Gets the XPath expression that uniquely identifies the given <see cref="XmlElement"/>.
    /// </summary>
    /// <param name="element">The element for which to generate the XPath.</param>
    /// <returns>
    /// A non-empty XPath string such as <c>/root/parent/child[2]</c>. Positional predicates are
    /// included whenever another sibling with the same name exists before or after this element.
    /// </returns>
    /// <remarks>
    /// <inheritdoc cref="GetXPath(XmlNode)" path="/remarks"/>
    /// </remarks>
    public static string GetXPath(this XmlElement element) => GetElementXPath(element);

    /// <summary>
    /// Builds the absolute XPath for an element by walking up to the document root.
    /// </summary>
    /// <param name="element">The element to start from; may be <see langword="null"/> for detached nodes.</param>
    /// <returns>The absolute XPath string, or an empty string when the element is <see langword="null"/>.</returns>
    private static string GetElementXPath(XmlElement? element)
    {
        var xpath = string.Empty;
        var current = element;

        while (current != null)
        {
            var elementName = current.Name;
            var index = GetElementIndex(current);
            var hasSameNameSibling = HasSameNameSibling(current);

            xpath = (hasSameNameSibling
                ? $"/{elementName}[{index}]"
                : $"/{elementName}") + xpath;

            current = current.ParentNode as XmlElement;
        }

        return xpath;
    }

    /// <summary>
    /// Builds the XPath for an attribute node, combining the owner element path with the attribute name.
    /// </summary>
    /// <param name="attr">The attribute node.</param>
    /// <returns>An XPath string such as <c>/root/@id</c>.</returns>
    private static string GetAttributeXPath(XmlAttribute attr)
        => GetElementXPath(attr.OwnerElement) + "/@" + attr.Name;

    /// <summary>
    /// Determines whether the element has any sibling element with the same qualified name,
    /// either before or after it in the parent's child list.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>
    /// <see langword="true"/> if at least one preceding or following sibling shares the same qualified name;
    /// otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasSameNameSibling(XmlElement element)
    {
        var sibling = element.PreviousSibling;
        while (sibling != null)
        {
            if (sibling is XmlElement sibEl && sibEl.Name == element.Name)
                return true;
            sibling = sibling.PreviousSibling;
        }

        sibling = element.NextSibling;
        while (sibling != null)
        {
            if (sibling is XmlElement sibEl && sibEl.Name == element.Name)
                return true;
            sibling = sibling.NextSibling;
        }

        return false;
    }

    /// <summary>
    /// Gets the 1-based position of the element among its preceding siblings with the same qualified name.
    /// </summary>
    /// <param name="element">The element whose position to determine.</param>
    /// <returns>
    /// 1 when no preceding sibling has the same name, or a higher value reflecting its position in the sequence.
    /// </returns>
    private static int GetElementIndex(XmlElement element)
    {
        var index = 1;
        var sibling = element.PreviousSibling;

        while (sibling != null)
        {
            if (sibling is XmlElement sibEl && sibEl.Name == element.Name)
            {
                index++;
            }
            sibling = sibling.PreviousSibling;
        }

        return index;
    }
}
