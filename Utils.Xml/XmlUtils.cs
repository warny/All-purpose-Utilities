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
    /// An enumerable sequence yielding an isolated <see cref="XmlReader"/> sub-reader for each
    /// immediate child element. The sub-reader is managed by the iterator: partially consumed
    /// sub-readers are drained automatically when the enumerator advances to the next child,
    /// so consumers need not read or skip remaining content before the next iteration.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reader is not positioned at an element node.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Depth-based tracking ensures that only elements at exactly one level below the starting
    /// element are yielded, regardless of element names at any depth. An empty parent element
    /// (<c>&lt;parent /&gt;</c>) yields no children without advancing the outer reader past sibling
    /// elements.
    /// </para>
    /// <para>
    /// <b>Reader-state contract:</b> each yielded reader is an independent sub-reader created by
    /// <see cref="XmlReader.ReadSubtree"/>. When the iterator advances to the next sibling,
    /// <see cref="XmlReader.Dispose()"/> is called on the previous sub-reader, which reads through
    /// any unconsumed content and positions the outer reader immediately after the child's closing
    /// tag. The consumer must not advance the outer reader directly; only the sub-reader should be
    /// used to read the child's content.
    /// </para>
    /// </remarks>
    public static IEnumerable<XmlReader> ReadChildElements(this XmlReader reader, string? elementName = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.NodeType != XmlNodeType.Element)
        {
            throw new InvalidOperationException(
                $"Current reader {reader.NodeType} '{reader.Name}' is not of type Element.");
        }

        if (reader.IsEmptyElement)
            yield break;

        int parentDepth = reader.Depth;

        // Move past the parent's opening tag into its content.
        if (!reader.Read())
            yield break;

        while (true)
        {
            // Exit when the parent's closing tag is reached.
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == parentDepth)
                break;

            if (reader.NodeType == XmlNodeType.Element && reader.Depth == parentDepth + 1)
            {
                if (elementName is null || reader.Name == elementName)
                {
                    using (XmlReader subtree = reader.ReadSubtree())
                    {
                        subtree.Read(); // advance from Initial to Interactive so Name/NodeType are populated
                        yield return subtree;
                    }
                    // XmlSubtreeReader.Close() does not advance the outer reader past empty elements
                    // when the sub-reader is in Interactive state (it only calls Read() for non-empty
                    // elements). Advance manually so the loop does not re-yield the same element.
                    if (reader.NodeType == XmlNodeType.Element && reader.IsEmptyElement)
                        reader.Read();
                }
                else
                {
                    // Wrong name: skip the entire child subtree. Skip() leaves the outer
                    // reader on the node immediately following the child's end element.
                    reader.Skip();
                }
            }
            else
            {
                // Non-element node (text, comment, whitespace) or a node at an unexpected depth:
                // advance one step.
                if (!reader.Read())
                    break;
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
