using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Utils.XML;

/// <summary>
/// Abstract base class for XML-driven functionality. The class automatically calls methods based on XML node triggers.
/// </summary>
public abstract class XmlDriver
{
	/// <summary>
	/// Dictionary mapping XPath expressions to methods triggered when XML nodes are matched.
	/// </summary>
	private readonly Dictionary<XPathExpression, Method> triggers;

	/// <summary>
	/// Manages XML namespaces for the XPath expressions.
	/// </summary>
	public XmlNamespaceManager NamespaceManager { get; }

	/// <summary>
	/// The current XML node being processed.
	/// </summary>
	protected XPathNavigator Current { get; private set; }

	/// <summary>
	/// Class representing a method and its parameters.
	/// </summary>
	private sealed class Method
	{
		public MethodInfo MethodInfo { get; }
		public ParameterInfo[] Parameters { get; }

		public Method(MethodInfo methodInfo, ParameterInfo[] parameters)
		{
			MethodInfo = methodInfo;
			Parameters = parameters;
		}
	}

	/// <summary>
	/// Initializes the XmlDriver, sets up namespace management, and prepares method triggers based on XPath expressions.
	/// </summary>
	protected XmlDriver()
	{
		Type t = GetType();
		NamespaceManager = new XmlNamespaceManager(new NameTable());

		// Add XML namespaces from XmlNamespaceAttribute.
		foreach (XmlNamespaceAttribute namespaceAttribute in t.GetCustomAttributes<XmlNamespaceAttribute>(true))
		{
			NamespaceManager.AddNamespace(namespaceAttribute.Prefix, namespaceAttribute.Namespace);
		}

		triggers = new Dictionary<XPathExpression, Method>();

		// Add methods marked with the MatchAttribute.
		foreach (MethodInfo method in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
		{
			foreach (MatchAttribute matchAttribute in method.GetCustomAttributes<MatchAttribute>())
			{
				XPathExpression expression = XPathExpression.Compile(matchAttribute.XPathExpression, NamespaceManager);
				triggers.Add(expression, new Method(method, method.GetParameters()));
			}
		}
	}

	/// <summary>
	/// Creates an XPath expression using the namespace manager.
	/// </summary>
	/// <param name="xPath">The XPath string.</param>
	/// <returns>A compiled XPathExpression.</returns>
	protected XPathExpression CreateExpression(string xPath)
	{
		return XPathExpression.Compile(xPath, NamespaceManager);
	}

	/// <summary>
	/// Calls methods associated with the nodes found by the given XPath expression.
	/// </summary>
	/// <param name="xPath">The XPath expression to evaluate.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	protected void Apply(XPathExpression xPath, params object[] objects)
	{
		XPathNodeIterator nodes = Current.Select(xPath);
		InvokeMethods(nodes, objects);
	}

	/// <summary>
	/// Calls methods associated with the nodes found by the given XPath string.
	/// </summary>
	/// <param name="xPath">The XPath string to evaluate.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	protected void Apply(string xPath, params object[] objects)
	{
		XPathNodeIterator nodes = Current.Select(xPath, NamespaceManager);
		InvokeMethods(nodes, objects);
	}

	/// <summary>
	/// Calls methods associated with the given XML node.
	/// </summary>
	/// <param name="node">The XML node to process.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	protected void Apply(XPathNavigator node, params object[] objects)
	{
		Type[] types = objects.Select(o => o.GetType()).ToArray();
		InvokeMethod(node, objects, types);
	}

	/// <summary>
	/// Selects and invokes methods based on XPath expression starting from a given node.
	/// </summary>
	/// <param name="node">The starting XML node.</param>
	/// <param name="xPath">XPath expression to evaluate.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	protected void Apply(XPathNavigator node, XPathExpression xPath, params object[] objects)
	{
		XPathNodeIterator nodes = node.Select(xPath);
		InvokeMethods(nodes, objects);
	}

	/// <summary>
	/// Selects and invokes methods based on XPath string starting from a given node.
	/// </summary>
	/// <param name="node">The starting XML node.</param>
	/// <param name="xPath">XPath string to evaluate.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	protected void Apply(XPathNavigator node, string xPath, params object[] objects)
	{
		XPathNodeIterator nodes = node.Select(xPath, NamespaceManager);
		InvokeMethods(nodes, objects);
	}

	/// <summary>
	/// Retrieves nodes matching the specified XPath expression.
	/// </summary>
	/// <param name="xPath">The XPath expression to evaluate.</param>
	/// <returns>An XPathNodeIterator over the matching nodes.</returns>
	protected XPathNodeIterator GetNodes(XPathExpression xPath)
	{
		return Current.Select(xPath);
	}

	/// <summary>
	/// Retrieves nodes matching the specified XPath string.
	/// </summary>
	/// <param name="xPath">The XPath string to evaluate.</param>
	/// <returns>An XPathNodeIterator over the matching nodes.</returns>
	protected XPathNodeIterator GetNodes(string xPath)
	{
		return Current.Select(xPath, NamespaceManager);
	}

	/// <summary>
	/// Returns the value of the current node or the node at the specified XPath.
	/// </summary>
	/// <param name="xPath">The XPath string to evaluate.</param>
	/// <returns>The value of the matching node, or null if no node is found.</returns>
	protected string ValueOf(string xPath = ".")
	{
		XPathNavigator node = Current.SelectSingleNode(xPath, NamespaceManager);
		return node?.Value;
	}

	/// <summary>
	/// Invokes methods for each node in the node iterator.
	/// </summary>
	/// <param name="nodes">The iterator over matching nodes.</param>
	/// <param name="objects">Parameters passed to the invoked methods.</param>
	private void InvokeMethods(XPathNodeIterator nodes, params object[] objects)
	{
		Type[] types = objects.Select(o => o.GetType()).ToArray();
		foreach (XPathNavigator node in nodes)
		{
			InvokeMethod(node, objects, types);
		}
	}

	/// <summary>
	/// Invokes a method for a given node based on the registered triggers.
	/// </summary>
	/// <param name="node">The current XML node.</param>
	/// <param name="objects">Parameters passed to the invoked method.</param>
	/// <param name="types">The types of the parameters passed.</param>
	private void InvokeMethod(XPathNavigator node, object[] objects, Type[] types)
	{
		foreach (var trigger in triggers)
		{
			if (node.Matches(trigger.Key))
			{
				var method = trigger.Value;
				var parameters = method.Parameters;

				// Validate the parameters and invoke the method.
				bool isOk = ValidateParameters(parameters, types, ref objects);
				if (isOk)
				{
					var oldContext = Current;
					Current = node;
					method.MethodInfo.Invoke(this, objects);
					Current = oldContext;
					break;
				}
			}
		}
	}

	/// <summary>
	/// Validates method parameters to ensure they match the expected types.
	/// </summary>
	/// <param name="parameters">The method parameters to validate.</param>
	/// <param name="types">The actual argument types provided.</param>
	/// <param name="objects">The actual argument values provided.</param>
	/// <returns><c>true</c> if the parameters are valid; otherwise, <c>false</c>.</returns>
	private bool ValidateParameters(ParameterInfo[] parameters, Type[] types, ref object[] objects)
	{
		for (int i = 0; i < parameters.Length; i++)
		{
			var parameter = parameters[i];
			if (parameter.ParameterType.IsAssignableFrom(types[i])) continue;
			if (!parameter.IsOptional) return false;
		}
		return true;
	}

	/// <summary>
	/// Reads and processes an XML document from an XPathNavigator.
	/// </summary>
	public void Read(XPathNavigator navigator)
	{
		InvokeMethods(navigator.Select("/"));
	}

	// Various overloads to read XML from different sources (URI, stream, reader, etc.)
	public void Read(string uri) => Read(new XPathDocument(uri).CreateNavigator());
	public void Read(Stream stream) => Read(new XPathDocument(stream).CreateNavigator());
	public void Read(XmlReader reader) => Read(new XPathDocument(reader).CreateNavigator());

	/// <summary>
	/// Handles the root node of the XML document.
	/// </summary>
	[Match("/")]
	protected abstract void Root();
}

/// <summary>
/// Attribute for defining XML namespace mappings used in the XPath expressions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
class XmlNamespaceAttribute : Attribute
{
	private static readonly Regex ValidatePrefix = new Regex("^[A-Za-z]\\w*$");

	public string Prefix { get; }
	public string Namespace { get; }

	public XmlNamespaceAttribute(string prefix, string @namespace)
	{
		if (!ValidatePrefix.IsMatch(prefix))
		{
			throw new ArgumentException($"Invalid prefix: {prefix}", nameof(prefix));
		}

		Prefix = prefix;
		Namespace = @namespace;
	}
}

/// <summary>
/// Attribute for associating XPath expressions with methods in the XmlDriver.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
class MatchAttribute : Attribute
{
	public string XPathExpression { get; }

	public MatchAttribute(string xPathExpression)
	{
		XPathExpression = xPathExpression;
	}
}
