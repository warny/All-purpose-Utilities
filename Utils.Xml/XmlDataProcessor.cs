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
/// Abstract base class for XML-driven functionality.
/// The class automatically calls methods based on <see cref="MatchAttribute"/> triggers (XPath expressions).
/// </summary>
/// <remarks>
/// Derive from this class to define XML-processing logic. Annotate your handler methods
/// with <see cref="MatchAttribute"/> to match specific XPath expressions, and optionally
/// use <see cref="XmlNamespaceAttribute"/> at the class level to register namespace prefixes.
/// </remarks>
public abstract class XmlDataProcessor
{
    /// <summary>
    /// Manages XML namespaces for the XPath expressions.
    /// </summary>
    /// <remarks>
    /// Populate this using <see cref="XmlNamespaceAttribute"/> or manually by adding calls to
    /// <see cref="XmlNamespaceManager.AddNamespace"/>.
    /// </remarks>
    public XmlNamespaceManager NamespaceManager { get; }

    /// <summary>
    /// The current XML node being processed.
    /// This is updated internally while methods are being invoked.
    /// </summary>
    protected XPathNavigator Current { get; private set; }

    /// <summary>
    /// Internal struct representing a method and associated metadata.
    /// We store a delegate for faster invocation compared to direct reflection each time.
    /// </summary>
    private sealed class Method
    {
        private readonly Action<XmlDataProcessor, XPathNavigator, object[]> _invoker;

        public MethodInfo MethodInfo { get; }
        public ParameterInfo[] Parameters { get; }

        /// <summary>
        /// Constructs a new <see cref="Method"/> descriptor, caching the delegate
        /// for faster invocations.
        /// </summary>
        /// <param name="methodInfo">Reflection info of the method.</param>
        /// <param name="parameters">Parameter descriptors for the method.</param>
        public Method(MethodInfo methodInfo, ParameterInfo[] parameters)
        {
            MethodInfo = methodInfo;
            Parameters = parameters;

            // Create a cached invoker to reduce reflection overhead each time.
            // You can alternatively use Expression.Compile here, but MethodInfo.Invoke
            // in a cached delegate is usually simpler.
            _invoker = (driver, node, args) =>
            {
                MethodInfo.Invoke(driver, args);
            };
        }

        /// <summary>
        /// Invokes the cached method delegate.
        /// </summary>
        /// <param name="driver">The current <see cref="XmlDataProcessor"/> instance.</param>
        /// <param name="node">The current XML node.</param>
        /// <param name="args">Parameters passed to the invoked method.</param>
        public void Invoke(XmlDataProcessor driver, XPathNavigator node, object[] args)
            => _invoker(driver, node, args);
    }

    /// <summary>
    /// A dictionary mapping compiled XPath expressions to their associated handler <see cref="Method"/>.
    /// </summary>
    private readonly Dictionary<XPathExpression, Method> _triggers;

    /// <summary>
    /// Initializes the <see cref="XmlDataProcessor"/>, sets up namespace management, and prepares method triggers
    /// based on <see cref="MatchAttribute"/> usage.
    /// </summary>
    protected XmlDataProcessor()
    {
        NamespaceManager = new XmlNamespaceManager(new NameTable());

        // Register namespaces from XmlNamespaceAttribute on the derived class.
        foreach (var namespaceAttribute in GetType().GetCustomAttributes<XmlNamespaceAttribute>(true))
        {
            NamespaceManager.AddNamespace(namespaceAttribute.Prefix, namespaceAttribute.Namespace);
        }

        // Build the trigger dictionary by finding all methods that have MatchAttribute.
        _triggers = new Dictionary<XPathExpression, Method>();
        foreach (MethodInfo method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (MatchAttribute matchAttribute in method.GetCustomAttributes<MatchAttribute>())
            {
                XPathExpression expression = XPathExpression.Compile(matchAttribute.XPathExpression, NamespaceManager);
                var methodMeta = new Method(method, method.GetParameters());
                _triggers.Add(expression, methodMeta);
            }
        }
    }

    /// <summary>
    /// Creates and compiles an <see cref="XPathExpression"/> using the current <see cref="XmlNamespaceManager"/>.
    /// </summary>
    /// <param name="xPath">The XPath string to be compiled.</param>
    /// <returns>A compiled <see cref="XPathExpression"/>.</returns>
    protected XPathExpression CreateExpression(string xPath)
        => XPathExpression.Compile(xPath, NamespaceManager);

    /// <summary>
    /// Evaluates the specified compiled <paramref name="xPath"/> from the current node
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="xPath">The pre-compiled XPath expression.</param>
    /// <param name="parameters">Parameters passed to the invoked methods.</param>
    protected void Apply(XPathExpression xPath, params object[] parameters)
    {
        XPathNodeIterator nodes = Current.Select(xPath);
        InvokeTriggersOnNodes(nodes, parameters);
    }

    /// <summary>
    /// Evaluates the specified <paramref name="xPath"/> string from the current node
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="xPath">The XPath string to evaluate.</param>
    /// <param name="parameters">Parameters passed to the invoked methods.</param>
    protected void Apply(string xPath, params object[] parameters)
    {
        XPathNodeIterator nodes = Current.Select(xPath, NamespaceManager);
        InvokeTriggersOnNodes(nodes, parameters);
    }

    /// <summary>
    /// Invokes any matching handler method(s) on the single <paramref name="node"/>.
    /// </summary>
    /// <param name="node">The XML node to process.</param>
    /// <param name="parameters">Parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, params object[] parameters)
    {
        InvokeSingleNode(node, parameters);
    }

    /// <summary>
    /// Evaluates the specified pre-compiled <paramref name="xPath"/> from the given <paramref name="node"/>
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="node">The starting node.</param>
    /// <param name="xPath">A pre-compiled XPath expression.</param>
    /// <param name="parameters">Parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, XPathExpression xPath, params object[] parameters)
    {
        XPathNodeIterator nodes = node.Select(xPath);
        InvokeTriggersOnNodes(nodes, parameters);
    }

    /// <summary>
    /// Evaluates the specified <paramref name="xPath"/> string from the given <paramref name="node"/>
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="node">The starting node.</param>
    /// <param name="xPath">XPath string to evaluate.</param>
    /// <param name="parameters">Parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, string xPath, params object[] parameters)
    {
        XPathNodeIterator nodes = node.Select(xPath, NamespaceManager);
        InvokeTriggersOnNodes(nodes, parameters);
    }

    /// <summary>
    /// Retrieves nodes matching the specified pre-compiled <paramref name="xPath"/> from the current node.
    /// </summary>
    /// <param name="xPath">The compiled XPath expression.</param>
    /// <returns>An <see cref="XPathNodeIterator"/> over the matching nodes.</returns>
    protected XPathNodeIterator GetNodes(XPathExpression xPath)
        => Current.Select(xPath);

    /// <summary>
    /// Retrieves nodes matching the specified <paramref name="xPath"/> from the current node.
    /// </summary>
    /// <param name="xPath">The XPath string to evaluate.</param>
    /// <returns>An <see cref="XPathNodeIterator"/> over the matching nodes.</returns>
    protected XPathNodeIterator GetNodes(string xPath)
        => Current.Select(xPath, NamespaceManager);

    /// <summary>
    /// Returns the value of the current node, or the node matched by
    /// <paramref name="xPath"/> (if specified).
    /// </summary>
    /// <param name="xPath">An optional XPath string to evaluate (defaults to ".").</param>
    /// <returns>The textual value of the matching node, or <c>null</c> if no match is found.</returns>
    protected string ValueOf(string xPath = ".")
    {
        XPathNavigator node = Current.SelectSingleNode(xPath, NamespaceManager);
        return node?.Value;
    }

    /// <summary>
    /// Reads and processes an XML document from a given <see cref="XPathNavigator"/>.
    /// Invokes the <see cref="Root"/> method (or any other triggers matching "/") on the root node.
    /// </summary>
    /// <param name="navigator">The <see cref="XPathNavigator"/> to process.</param>
    public void Read(XPathNavigator navigator)
    {
        // Start from the document root ("/")
        XPathNodeIterator root = navigator.Select("/");
        InvokeTriggersOnNodes(root);
    }

    /// <summary>
    /// Reads and processes an XML document from the specified URI path.
    /// </summary>
    /// <param name="uri">The URI to the XML document.</param>
    public void Read(string uri)
        => Read(new XPathDocument(uri).CreateNavigator());

    /// <summary>
    /// Reads and processes an XML document from the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing the XML document.</param>
    public void Read(Stream stream)
        => Read(new XPathDocument(stream).CreateNavigator());

    /// <summary>
    /// Reads and processes an XML document from the specified <see cref="XmlReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="XmlReader"/> positioned at the start of the XML document.</param>
    public void Read(XmlReader reader)
        => Read(new XPathDocument(reader).CreateNavigator());

    /// <summary>
    /// Abstract method that gets invoked for the XML root ("/").
    /// Derived classes must implement this to handle root-level logic.
    /// </summary>
    [Match("/")]
    protected abstract void Root();

    /// <summary>
    /// Iterates over a set of nodes and checks each node against all triggers.
    /// If a node matches a trigger (i.e. <c>node.Matches(expression)</c>),
    /// the associated method is invoked.
    /// </summary>
    /// <param name="nodes">Iterator over matching nodes.</param>
    /// <param name="parameters">Parameters to be passed to the invoked method.</param>
    private void InvokeTriggersOnNodes(XPathNodeIterator nodes, object[] parameters = null)
    {
        if (parameters == null) parameters = Array.Empty<object>();

        // We gather the types once for all invocations below.
        var argumentTypes = parameters.Select(o => o.GetType()).ToArray();

        foreach (XPathNavigator nav in nodes)
        {
            InvokeSingleNode(nav, parameters, argumentTypes);
        }
    }

    /// <summary>
    /// Checks a single <paramref name="node"/> against all triggers.
    /// If there's a match, invokes the associated method (and returns immediately after
    /// the first match to avoid multiple triggers on the same node).
    /// </summary>
    /// <param name="node">The node to process.</param>
    /// <param name="parameters">Parameters to be passed to the invoked method.</param>
    /// <param name="argumentTypes">
    /// (Optional) Array of argument types. Passing this avoids recomputing them
    /// repeatedly in <see cref="InvokeTriggersOnNodes"/>.
    /// </param>
    private void InvokeSingleNode(XPathNavigator node, object[] parameters, Type[] argumentTypes = null)
    {
        if (parameters == null) parameters = Array.Empty<object>();
        argumentTypes ??= parameters.Select(o => o.GetType()).ToArray();

        // Attempt to find a matching trigger.
        foreach (var (expression, method) in _triggers)
        {
            if (node.Matches(expression) && ParametersMatch(method.Parameters, argumentTypes))
            {
                // Temporarily update the "Current" context before invoking.
                var oldContext = Current;
                Current = node;

                // Invoke the handler method.
                method.Invoke(this, node, parameters);

                Current = oldContext;
                break; // Only handle the first matching trigger per node.
            }
        }
    }

    /// <summary>
    /// Verifies that the argument types match (or are compatible with) the method's parameters.
    /// Considers optional parameters, but does not handle default parameter values.
    /// </summary>
    /// <param name="expected">Parameter definitions from the reflection-based <see cref="MethodInfo"/>.</param>
    /// <param name="actualTypes">The actual types of arguments passed in.</param>
    /// <returns><see langword="true"/> if the arguments are acceptable; otherwise <see langword="false"/>.</returns>
    private static bool ParametersMatch(ParameterInfo[] expected, Type[] actualTypes)
    {
        // Quick check: if you don't have enough args for non-optional params, fail fast.
        int mandatoryCount = expected.Count(p => !p.IsOptional);
        if (actualTypes.Length < mandatoryCount || actualTypes.Length > expected.Length)
            return false;

        // Now check if each provided argument is compatible with the corresponding parameter.
        for (int i = 0; i < actualTypes.Length; i++)
        {
            var paramType = expected[i].ParameterType;
            var argType = actualTypes[i];

            if (!paramType.IsAssignableFrom(argType))
                return false;
        }

        // If we get here, the arguments are good enough.
        return true;
    }
}

/// <summary>
/// Attribute to define XML namespace mappings for XPath expressions.
/// Use multiple attributes if you need multiple prefix-to-namespace bindings.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed partial class XmlNamespaceAttribute : Attribute
{
    private static readonly Regex _validatePrefix = PrefixValidatorRegex();

    /// <summary>
    /// The namespace prefix used in XPath expressions.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// The actual namespace URI associated with <see cref="Prefix"/>.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Defines a prefix-to-namespace mapping for XPath expressions.
    /// </summary>
    /// <param name="prefix">The namespace prefix to register.</param>
    /// <param name="namespace">The namespace URI corresponding to the prefix.</param>
    /// <exception cref="ArgumentException">Thrown if the prefix is invalid.</exception>
    public XmlNamespaceAttribute(string prefix, string @namespace)
    {
        if (!_validatePrefix.IsMatch(prefix))
        {
            throw new ArgumentException($"Invalid prefix: {prefix}. Prefix must match {_validatePrefix}.", nameof(prefix));
        }

        Prefix = prefix;
        Namespace = @namespace;
    }

    [GeneratedRegex(@"^[A-Za-z]\w*$", RegexOptions.Compiled)]
    private static partial Regex PrefixValidatorRegex();
}

/// <summary>
/// Attribute for associating a method with a specific XPath expression.
/// When the driver encounters a node matching <see cref="XPathExpression"/>,
/// it invokes the method tagged with this attribute.
/// </summary>
/// <remarks>
/// Associates an XPath expression with a method.
/// Multiple attributes can be placed on the same method
/// to match multiple expressions.
/// </remarks>
/// <param name="xPathExpression">The XPath expression to match against.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class MatchAttribute(string xPathExpression) : Attribute
{
    /// <summary>
    /// The XPath expression used to match XML nodes.
    /// </summary>
    public string XPathExpression { get; } = xPathExpression;
}
