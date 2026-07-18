using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
/// <para>
/// Instances of this class are <b>not safe for concurrent use</b>. If two threads call any
/// <c>Read</c> overload simultaneously on the same instance, the second call will throw
/// <see cref="InvalidOperationException"/>. Create separate processor instances per thread
/// or serialize access externally.
/// </para>
/// <para>
/// Handler methods must be non-generic, return <see cref="void"/>, and have no <c>ref</c>, <c>out</c>,
/// or pointer parameters. Async methods (returning <see cref="System.Threading.Tasks.Task"/> or
/// <see cref="System.Threading.Tasks.ValueTask"/>) are rejected at construction time.
/// </para>
/// </remarks>
public abstract class XmlDataProcessor
{
    /// <summary>
    /// Manages XML namespaces for the XPath expressions.
    /// </summary>
    /// <remarks>
    /// Populate this using <see cref="XmlNamespaceAttribute"/> or manually by calling
    /// <see cref="XmlNamespaceManager.AddNamespace"/>.
    /// </remarks>
    public XmlNamespaceManager NamespaceManager { get; }

    /// <summary>
    /// The current XML node being processed. <see langword="null"/> when no dispatch is active.
    /// </summary>
    /// <remarks>
    /// This property is updated to the matched node immediately before a handler is invoked and
    /// restored to its previous value in a <see langword="finally"/> block, even when the handler throws.
    /// </remarks>
    protected XPathNavigator? Current { get; private set; }

    /// <summary>
    /// Returns <see cref="Current"/> when a dispatch context is active, or throws
    /// <see cref="InvalidOperationException"/> when called outside a handler.
    /// </summary>
    private XPathNavigator RequireCurrent()
        => Current ?? throw new InvalidOperationException(
            "This method can only be called from within an active handler dispatch context.");

    /// <summary>
    /// Descriptor for a discovered handler method, holding the reflection metadata and parameter info.
    /// </summary>
    private sealed record TriggerDescriptor(
        XPathExpression Expression,
        MethodInfo Method,
        ParameterInfo[] Parameters);

    /// <summary>
    /// Ordered list of trigger descriptors built at construction time.
    /// Handlers in the most-derived class are registered first (derived-to-base traversal),
    /// giving derived-class handlers priority over same-XPath base-class handlers.
    /// Within a single class, methods appear in <c>MetadataToken</c> order, which corresponds
    /// to their declaration order in the compiled assembly. Only the first matching handler per
    /// node fires; subsequent entries in the list are skipped.
    /// </summary>
    private readonly List<TriggerDescriptor> _triggers;

    /// <summary>
    /// Guards against concurrent or re-entrant <c>Read</c> operations on the same instance.
    /// </summary>
    private int _activeOperations;

    /// <summary>
    /// Tracks the current dispatch call depth to detect runaway redispatch cycles.
    /// </summary>
    private int _dispatchDepth;

    /// <summary>
    /// Maximum dispatch depth before a <see cref="InvalidOperationException"/> is raised.
    /// A low value prevents stack overflow under recursive dispatch patterns while still
    /// allowing reasonable handler call chains.
    /// </summary>
    private const int MaxDispatchDepth = 20;

    /// <summary>
    /// Initializes the <see cref="XmlDataProcessor"/>, sets up namespace management, and prepares method
    /// triggers based on <see cref="MatchAttribute"/> usage.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown during construction when a handler method has an unsupported signature (generic, non-void,
    /// <c>ref</c>/<c>out</c>/pointer parameters), or when duplicate or conflicting namespace declarations
    /// are detected.
    /// </exception>
    protected XmlDataProcessor()
    {
        NamespaceManager = new XmlNamespaceManager(new NameTable());

        // Item 24: collect namespace declarations first, detect conflicts before adding.
        RegisterNamespaces();

        // Items 7 & 23: walk the type hierarchy with DeclaredOnly to include inherited private methods.
        _triggers = [];
        var type = GetType();
        var seenSignatures = new HashSet<string>();

        while (type != null && type != typeof(object))
        {
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(m => m.MetadataToken))
            {
                foreach (MatchAttribute matchAttr in method.GetCustomAttributes<MatchAttribute>())
                {
                    // Items 8 & 20: validate at construction time.
                    ValidateHandlerSignature(method, matchAttr.XPathExpression);

                    var expression = XPathExpression.Compile(matchAttr.XPathExpression, NamespaceManager);

                    // Deduplicate: a method declared on a subtype shadows the same-named base method.
                    var sigKey = $"{method.DeclaringType!.FullName}::{method.Name}::{matchAttr.XPathExpression}";
                    if (seenSignatures.Add(sigKey))
                    {
                        _triggers.Add(new TriggerDescriptor(expression, method, method.GetParameters()));
                    }
                }
            }

            type = type.BaseType;
        }
    }

    /// <summary>
    /// Collects <see cref="XmlNamespaceAttribute"/> declarations from the type hierarchy,
    /// validates for conflicts, and registers them in <see cref="NamespaceManager"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two declarations use the same prefix but different namespace URIs.
    /// </exception>
    private void RegisterNamespaces()
    {
        // Item 24: gather all declarations and detect conflicting prefixes.
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var nsAttr in GetType().GetCustomAttributes<XmlNamespaceAttribute>(true))
        {
            if (seen.TryGetValue(nsAttr.Prefix, out var existingUri))
            {
                if (!string.Equals(existingUri, nsAttr.Namespace, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Conflicting namespace declarations for prefix '{nsAttr.Prefix}': " +
                        $"'{existingUri}' vs '{nsAttr.Namespace}'.");
                }
                // Exact duplicate: silently skip.
                continue;
            }

            seen[nsAttr.Prefix] = nsAttr.Namespace;
            NamespaceManager.AddNamespace(nsAttr.Prefix, nsAttr.Namespace);
        }
    }

    /// <summary>
    /// Validates that a handler method has a supported signature.
    /// </summary>
    /// <param name="method">The method to validate.</param>
    /// <param name="xpathExpression">The XPath expression from the attribute, used in error messages.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the method is generic, has a non-<see langword="void"/> return type,
    /// or has unsupported parameter kinds (<c>ref</c>, <c>out</c>, or pointer).
    /// </exception>
    private static void ValidateHandlerSignature(MethodInfo method, string xpathExpression)
    {
        if (method.IsGenericMethod)
        {
            throw new InvalidOperationException(
                $"Handler '{method.Name}' for XPath '{xpathExpression}' cannot be generic.");
        }

        // Items 8 & 20: non-void return type also rejects Task/ValueTask async handlers.
        if (method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"Handler '{method.Name}' for XPath '{xpathExpression}' must return void. " +
                $"Async handlers (Task/ValueTask) are not supported.");
        }

        foreach (var p in method.GetParameters())
        {
            if (p.IsOut || p.ParameterType.IsByRef || p.ParameterType.IsPointer)
            {
                throw new InvalidOperationException(
                    $"Handler '{method.Name}' parameter '{p.Name}' uses an unsupported type " +
                    $"(out, ref, or pointer parameters are not allowed).");
            }
        }
    }

    /// <summary>
    /// Creates and compiles an <see cref="XPathExpression"/> using the current <see cref="NamespaceManager"/>.
    /// </summary>
    /// <param name="xPath">The XPath string to compile.</param>
    /// <returns>A compiled <see cref="XPathExpression"/>.</returns>
    protected XPathExpression CreateExpression(string xPath)
        => XPathExpression.Compile(xPath, NamespaceManager);

    /// <summary>
    /// Selects nodes using a compiled expression relative to <paramref name="contextNode"/>,
    /// falling back to <see cref="RequireCurrent"/> when no context is provided.
    /// </summary>
    private XPathNodeIterator SelectNodes(XPathExpression xPath, XPathNavigator? contextNode)
        => (contextNode ?? RequireCurrent()).Select(xPath);

    /// <summary>
    /// Selects nodes using an XPath string relative to <paramref name="contextNode"/>,
    /// falling back to <see cref="RequireCurrent"/> when no context is provided.
    /// </summary>
    private XPathNodeIterator SelectNodes(string xPath, XPathNavigator? contextNode)
        => (contextNode ?? RequireCurrent()).Select(xPath, NamespaceManager);

    /// <summary>
    /// Evaluates the specified compiled <paramref name="xPath"/> from the current node
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="xPath">The pre-compiled XPath expression.</param>
    /// <param name="parameters">Additional parameters passed to the invoked methods.</param>
    protected void Apply(XPathExpression xPath, params object?[] parameters)
    {
        // Item 26: current must be active.
        InvokeTriggersOnNodes(SelectNodes(xPath, null), parameters);
    }

    /// <summary>
    /// Evaluates the specified <paramref name="xPath"/> string from the current node
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="xPath">The XPath string to evaluate.</param>
    /// <param name="parameters">Additional parameters passed to the invoked methods.</param>
    protected void Apply(string xPath, params object?[] parameters)
    {
        // Item 11: reject null/whitespace XPath to prevent injection.
        ArgumentException.ThrowIfNullOrWhiteSpace(xPath);
        InvokeTriggersOnNodes(SelectNodes(xPath, null), parameters);
    }

    /// <summary>
    /// Invokes any matching handler method(s) on the single <paramref name="node"/>.
    /// </summary>
    /// <param name="node">The XML node to process.</param>
    /// <param name="parameters">Additional parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, params object?[] parameters)
    {
        // Item 26: null check.
        ArgumentNullException.ThrowIfNull(node);
        InvokeSingleNode(node, parameters);
    }

    /// <summary>
    /// Evaluates the specified pre-compiled <paramref name="xPath"/> from the given <paramref name="node"/>
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="node">The starting node.</param>
    /// <param name="xPath">A pre-compiled XPath expression.</param>
    /// <param name="parameters">Additional parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, XPathExpression xPath, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(node);
        InvokeTriggersOnNodes(SelectNodes(xPath, node), parameters);
    }

    /// <summary>
    /// Evaluates the specified <paramref name="xPath"/> string from the given <paramref name="node"/>
    /// and invokes any matching handler methods on each resulting node.
    /// </summary>
    /// <param name="node">The starting node.</param>
    /// <param name="xPath">XPath string to evaluate.</param>
    /// <param name="parameters">Additional parameters passed to the invoked methods.</param>
    protected void Apply(XPathNavigator node, string xPath, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(xPath);
        InvokeTriggersOnNodes(SelectNodes(xPath, node), parameters);
    }

    /// <summary>
    /// Retrieves nodes matching the specified pre-compiled <paramref name="xPath"/> from the current node.
    /// </summary>
    /// <param name="xPath">The compiled XPath expression.</param>
    /// <returns>An <see cref="XPathNodeIterator"/> over the matching nodes.</returns>
    protected XPathNodeIterator GetNodes(XPathExpression xPath)
        => SelectNodes(xPath, null);

    /// <summary>
    /// Retrieves nodes matching the specified <paramref name="xPath"/> from the current node.
    /// </summary>
    /// <param name="xPath">The XPath string to evaluate.</param>
    /// <returns>An <see cref="XPathNodeIterator"/> over the matching nodes.</returns>
    protected XPathNodeIterator GetNodes(string xPath)
    {
        // Item 11 & 26.
        ArgumentException.ThrowIfNullOrWhiteSpace(xPath);
        return SelectNodes(xPath, null);
    }

    /// <summary>
    /// Returns the value of the current node, or the node matched by <paramref name="xPath"/> (if specified).
    /// </summary>
    /// <param name="xPath">An optional XPath string to evaluate (defaults to <c>"."</c>).</param>
    /// <returns>The textual value of the matching node, or <see langword="null"/> if no match is found.</returns>
    protected string? ValueOf(string xPath = ".")
    {
        // Item 26: require active context.
        var nav = RequireCurrent().SelectSingleNode(xPath, NamespaceManager);
        return nav?.Value;
    }

    /// <summary>
    /// Reads and processes an XML document from the given <see cref="XPathNavigator"/>.
    /// Invokes the <see cref="Root"/> method (or any other triggers matching <c>"/"</c>) on the root node.
    /// </summary>
    /// <param name="navigator">The <see cref="XPathNavigator"/> to process.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="navigator"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when another Read operation is already in progress on this instance.</exception>
    public void Read(XPathNavigator navigator)
    {
        // Item 26: null check.
        ArgumentNullException.ThrowIfNull(navigator);

        // Item 18: concurrency guard.
        BeginRead();
        try
        {
            XPathNodeIterator root = navigator.Select("/");
            InvokeTriggersOnNodes(root);
        }
        finally
        {
            EndRead();
        }
    }

    /// <summary>
    /// Reads and processes an XML document from the specified URI path.
    /// </summary>
    /// <param name="uri">The URI to the XML document.</param>
    /// <remarks>
    /// This legacy entry point keeps the historical behavior for backward compatibility.
    /// It does not restrict URI schemes, enforce document limits, or harden parser settings.
    /// For untrusted sources, prefer <see cref="ReadSecure(Stream)"/> which enforces secure reader settings.
    /// </remarks>
    [Obsolete("Read(string) keeps legacy XML reader behavior. Use ReadSecure(Stream) for untrusted XML sources.", false)]
    public void Read(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Read(new XPathDocument(uri).CreateNavigator()!);
    }

    /// <summary>
    /// Reads and processes an XML document from the specified URI using hardened parser settings.
    /// </summary>
    /// <param name="uri">The URI to the XML document.</param>
    /// <remarks>
    /// <para>
    /// This method disables DTD processing, clears the XML resolver, and limits entity expansion
    /// to reduce XML external entity and expansion attacks when processing untrusted inputs.
    /// </para>
    /// <para>
    /// <b>SSRF and local-file access are not prevented.</b> The <paramref name="uri"/> is passed
    /// directly to <see cref="XmlReader.Create(string, XmlReaderSettings?)"/>. An attacker who
    /// controls the URI can still target local files, UNC paths, or loopback services.
    /// Prefer <see cref="ReadSecure(Stream)"/>, acquire the stream yourself with appropriate
    /// redirect and scheme policies, and do not pass untrusted URIs to this method.
    /// </para>
    /// <para>
    /// <b>No cancellation or timeout:</b> network or file I/O is delegated to the XML parser with
    /// no acquisition timeout, redirect limit, or maximum response size prior to parsing.
    /// </para>
    /// </remarks>
    public void ReadSecure(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var settings = CreateSecureReaderSettings();
        using var xmlReader = XmlReader.Create(uri, settings);
        Read(new XPathDocument(xmlReader).CreateNavigator()!);
    }

    /// <summary>
    /// Reads and processes an XML document from the specified <see cref="Stream"/>
    /// using hardened parser settings.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing the XML document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// DTD processing is disabled, the XML resolver is cleared, and document size is limited
    /// to prevent entity expansion and other XML-based resource attacks.
    /// </remarks>
    public void ReadSecure(Stream stream)
    {
        // Item 9 & 26.
        ArgumentNullException.ThrowIfNull(stream);

        var settings = CreateSecureReaderSettings();
        using var xmlReader = XmlReader.Create(stream, settings);
        Read(new XPathDocument(xmlReader).CreateNavigator()!);
    }

    /// <summary>
    /// Reads and processes an XML document from the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing the XML document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This overload does not apply hardened parser settings. For untrusted input streams,
    /// use <see cref="ReadSecure(Stream)"/> instead.
    /// </remarks>
    public void Read(Stream stream)
    {
        // Item 26: null check.
        ArgumentNullException.ThrowIfNull(stream);
        Read(new XPathDocument(stream).CreateNavigator()!);
    }

    /// <summary>
    /// Reads and processes an XML document from the specified <see cref="XmlReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="XmlReader"/> positioned at the start of the XML document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <b>Caller-owned security policy:</b> this overload wraps the provided reader without inspecting
    /// its <see cref="XmlReaderSettings"/>. DTD processing, external resolution, and document limits
    /// depend entirely on how the caller configured the reader. Do not assume this path is equivalent
    /// to <see cref="ReadSecure(Stream)"/> unless the reader was created with equivalent hardened settings.
    /// </remarks>
    public void Read(XmlReader reader)
    {
        // Item 26 & 21: null check; caller owns security settings.
        ArgumentNullException.ThrowIfNull(reader);
        Read(new XPathDocument(reader).CreateNavigator()!);
    }

    /// <summary>
    /// Creates hardened XML reader settings for processing untrusted XML sources.
    /// </summary>
    /// <returns>A configured <see cref="XmlReaderSettings"/> instance with secure defaults.</returns>
    private static XmlReaderSettings CreateSecureReaderSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 1024,
            MaxCharactersInDocument = 10 * 1024 * 1024
        };
    }

    /// <summary>
    /// Abstract method that gets invoked for the XML root (<c>"/"</c>).
    /// Derived classes must implement this to handle root-level logic.
    /// </summary>
    [Match("/")]
    protected abstract void Root();

    /// <summary>
    /// Increments the operation counter and throws <see cref="InvalidOperationException"/> if
    /// another <c>Read</c> is already in progress on this instance.
    /// </summary>
    private void BeginRead()
    {
        if (Interlocked.Increment(ref _activeOperations) != 1)
        {
            Interlocked.Decrement(ref _activeOperations);
            throw new InvalidOperationException(
                "XmlDataProcessor does not support concurrent Read operations. " +
                "Use separate processor instances for parallel processing.");
        }
    }

    /// <summary>
    /// Decrements the operation counter, releasing the concurrency gate.
    /// </summary>
    private void EndRead() => Interlocked.Decrement(ref _activeOperations);

    /// <summary>
    /// Iterates over a set of nodes and checks each node against all triggers.
    /// If a node matches a trigger, the associated method is invoked according to the deterministic
    /// declaration order established at construction.
    /// </summary>
    /// <param name="nodes">Iterator over matching nodes.</param>
    /// <param name="parameters">Additional parameters to be passed to the invoked method.</param>
    private void InvokeTriggersOnNodes(XPathNodeIterator nodes, object?[]? parameters = null)
    {
        parameters ??= [];

        foreach (XPathNavigator nav in nodes)
        {
            InvokeSingleNode(nav, parameters);
        }
    }

    /// <summary>
    /// Checks a single <paramref name="node"/> against all triggers in declaration order.
    /// If there is a match, invokes the associated method and returns after the first match.
    /// </summary>
    /// <param name="node">The node to process.</param>
    /// <param name="parameters">Additional parameters to be passed to the invoked method.</param>
    private void InvokeSingleNode(XPathNavigator node, object?[] parameters)
    {
        foreach (var descriptor in _triggers)
        {
            if (!node.Matches(descriptor.Expression))
                continue;

            if (!TryBuildArguments(descriptor.Parameters, parameters, out var args))
                continue;

            // Item 19: detect runaway redispatch cycles.
            if (Interlocked.Increment(ref _dispatchDepth) > MaxDispatchDepth)
            {
                Interlocked.Decrement(ref _dispatchDepth);
                throw new InvalidOperationException(
                    $"Maximum dispatch depth of {MaxDispatchDepth} exceeded. " +
                    "Check for recursive XPath dispatch cycles in your handlers.");
            }

            // Item 4: restore Current in finally even when the handler throws.
            var oldContext = Current;
            Current = node;

            try
            {
                descriptor.Method.Invoke(this, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                // Unwrap reflection wrapper to preserve original stack trace.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(tie.InnerException)
                    .Throw();
            }
            finally
            {
                Current = oldContext;
                Interlocked.Decrement(ref _dispatchDepth);
            }

            break; // Only the first matching trigger per node.
        }
    }

    /// <summary>
    /// Attempts to build a complete argument array for a method invocation, filling in default values
    /// for optional parameters and validating null compatibility.
    /// </summary>
    /// <param name="expected">Parameter descriptors from reflection.</param>
    /// <param name="actual">The actual argument values supplied by the caller.</param>
    /// <param name="args">
    /// When this method returns <see langword="true"/>, contains the correctly sized and populated
    /// argument array ready for <see cref="MethodBase.Invoke(object?, object?[])"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the actual arguments are compatible with the expected parameters;
    /// <see langword="false"/> otherwise.
    /// </returns>
    private static bool TryBuildArguments(ParameterInfo[] expected, object?[] actual, out object?[] args)
    {
        // Items 5 & 6: handle optional params and null args.
        int mandatory = expected.Count(p => !p.IsOptional);

        if (actual.Length < mandatory || actual.Length > expected.Length)
        {
            args = [];
            return false;
        }

        args = new object?[expected.Length];

        for (int i = 0; i < expected.Length; i++)
        {
            if (i < actual.Length)
            {
                if (actual[i] is null)
                {
                    var pt = expected[i].ParameterType;

                    // Null is incompatible with non-nullable value types.
                    if (pt.IsValueType && Nullable.GetUnderlyingType(pt) is null)
                    {
                        args = [];
                        return false;
                    }

                    args[i] = null;
                }
                else
                {
                    if (!expected[i].ParameterType.IsAssignableFrom(actual[i]!.GetType()))
                    {
                        args = [];
                        return false;
                    }

                    args[i] = actual[i];
                }
            }
            else
            {
                // Optional parameter: use the declared default value.
                args[i] = expected[i].HasDefaultValue ? expected[i].DefaultValue : Type.Missing;
            }
        }

        return true;
    }
}

/// <summary>
/// Attribute to define XML namespace mappings for XPath expressions.
/// Use multiple attributes if you need multiple prefix-to-namespace bindings.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class XmlNamespaceAttribute : Attribute
{
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
    /// <param name="prefix">The namespace prefix to register. Must be a valid XML NCName.</param>
    /// <param name="namespace">The namespace URI corresponding to the prefix. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="prefix"/> is not a valid XML NCName, when <paramref name="prefix"/> is
    /// <c>xml</c> or <c>xmlns</c> (reserved by the XML specification), or when <paramref name="namespace"/>
    /// is null or empty.
    /// </exception>
    public XmlNamespaceAttribute(string prefix, string @namespace)
    {
        // Item 15: use XmlConvert.VerifyNCName instead of a custom regex.
        try
        {
            XmlConvert.VerifyNCName(prefix);
        }
        catch (XmlException ex)
        {
            throw new ArgumentException(
                $"Invalid XML NCName prefix: '{prefix}'.", nameof(prefix), ex);
        }

        if (prefix is "xml" or "xmlns")
        {
            throw new ArgumentException(
                $"The prefix '{prefix}' is reserved by the XML specification.", nameof(prefix));
        }

        ArgumentException.ThrowIfNullOrEmpty(@namespace, nameof(@namespace));

        Prefix = prefix;
        Namespace = @namespace;
    }
}

/// <summary>
/// Attribute for associating a method with a specific XPath expression.
/// When the processor encounters a node matching <see cref="XPathExpression"/>,
/// it invokes the method tagged with this attribute.
/// </summary>
/// <remarks>
/// Multiple attributes can be placed on the same method to match multiple expressions.
/// The attribute value is validated at declaration time: null, empty, and whitespace-only
/// strings are rejected immediately.
/// </remarks>
/// <param name="xPathExpression">The XPath expression to match against. Must not be null, empty, or whitespace.</param>
/// <exception cref="ArgumentException">
/// Thrown when <paramref name="xPathExpression"/> is null, empty, or whitespace.
/// </exception>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class MatchAttribute(string xPathExpression) : Attribute
{
    /// <summary>
    /// The XPath expression used to match XML nodes.
    /// </summary>
    public string XPathExpression { get; } = string.IsNullOrWhiteSpace(xPathExpression)
        ? throw new ArgumentException(
            "XPath expression cannot be null, empty, or whitespace.", nameof(xPathExpression))
        : xPathExpression;
}
