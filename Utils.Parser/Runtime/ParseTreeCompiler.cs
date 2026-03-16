namespace Utils.Parser.Runtime;

/// <summary>
/// Generic depth-first parse-tree compiler that separates traversal into two ordered phases
/// for each node:
/// <list type="number">
///   <item>
///     <term>Descent (top-down)</term>
///     <description>
///       Called when the node is first reached. Handlers can enrich the context for the
///       subtree below — for example pushing a new scope, recording parameter names, or
///       resolving a function signature before its body is compiled.
///     </description>
///   </item>
///   <item>
///     <term>Ascent (bottom-up)</term>
///     <description>
///       Called after all children have been compiled. Handlers receive the ordered list
///       of child results and must return the compiled result for this node — for example
///       folding two sub-expressions into a binary operation.
///     </description>
///   </item>
/// </list>
/// <para>
/// Handlers are registered per grammar-rule name via the fluent <c>OnDescend</c> /
/// <c>OnAscend</c> methods, with optional fall-backs for unregistered rules.
/// </para>
/// <para>
/// The context is treated as <em>immutable per descent step</em>: each descent handler
/// receives the context that was active when the node was entered and must return the
/// context to hand to the node's children. Returning the same instance is always valid;
/// creating a derived instance (e.g. a new scope) isolates sibling subtrees.
/// </para>
/// </summary>
/// <typeparam name="TContext">
/// Descending context type (e.g. a symbol table, a scope, a type environment).
/// </typeparam>
/// <typeparam name="TResult">
/// Result type returned by the ascent phase (e.g. an AST node, an expression, a value).
/// </typeparam>
public sealed class ParseTreeCompiler<TContext, TResult>
{
    // ── Handler stores ───────────────────────────────────────────────

    /// <summary>
    /// Per-rule descent handlers: (navigator, contextAtEntry) → contextForChildren.
    /// </summary>
    private readonly Dictionary<string, Func<ParseTreeNavigator, TContext, TContext>>
        _descentHandlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Per-rule ascent handlers:
    /// (navigator, contextAtEntry, orderedChildResults) → result.
    /// </summary>
    private readonly Dictionary<string, Func<ParseTreeNavigator, TContext, IReadOnlyList<TResult?>, TResult?>>
        _ascentHandlers = new(StringComparer.Ordinal);

    private Func<ParseTreeNavigator, TContext, TContext>?
        _defaultDescent;

    private Func<ParseTreeNavigator, TContext, IReadOnlyList<TResult?>, TResult?>?
        _defaultAscent;

    private Func<ParseTreeNavigator, TContext, TResult?>?
        _errorHandler;

    // ── Registration ─────────────────────────────────────────────────

    /// <summary>
    /// Registers a context-enrichment handler called when <em>descending into</em>
    /// nodes whose rule name equals <paramref name="ruleName"/>.
    /// <para>
    /// The handler receives the node navigator and the context inherited from the
    /// parent, and must return the context to pass to this node's children.
    /// Returning the parent context unchanged is always valid.
    /// </para>
    /// </summary>
    /// <param name="ruleName">Grammar rule name to match.</param>
    /// <param name="handler">
    /// Function <c>(navigator, parentContext) → contextForChildren</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a descent handler is already registered for <paramref name="ruleName"/>.
    /// </exception>
    public ParseTreeCompiler<TContext, TResult> OnDescend(
        string ruleName,
        Func<ParseTreeNavigator, TContext, TContext> handler)
    {
        if (_descentHandlers.ContainsKey(ruleName))
            throw new InvalidOperationException(
                $"A descent handler is already registered for rule '{ruleName}'.");
        _descentHandlers[ruleName] = handler;
        return this;
    }

    /// <summary>
    /// Registers a compilation handler called when <em>ascending from</em> nodes
    /// whose rule name equals <paramref name="ruleName"/>.
    /// <para>
    /// The handler receives the node navigator, the context that was active when the
    /// node was entered, and the ordered list of results produced by its children
    /// (<c>null</c> entries appear when a child produced no result).
    /// </para>
    /// </summary>
    /// <param name="ruleName">Grammar rule name to match.</param>
    /// <param name="handler">
    /// Function <c>(navigator, contextAtEntry, childResults) → result</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an ascent handler is already registered for <paramref name="ruleName"/>.
    /// </exception>
    public ParseTreeCompiler<TContext, TResult> OnAscend(
        string ruleName,
        Func<ParseTreeNavigator, TContext, IReadOnlyList<TResult?>, TResult?> handler)
    {
        if (_ascentHandlers.ContainsKey(ruleName))
            throw new InvalidOperationException(
                $"An ascent handler is already registered for rule '{ruleName}'.");
        _ascentHandlers[ruleName] = handler;
        return this;
    }

    /// <summary>
    /// Convenience overload of <see cref="OnAscend(string, Func{ParseTreeNavigator, TContext, IReadOnlyList{TResult?}, TResult?})"/>
    /// for nodes that do not need to inspect child results — typically leaf (lexer) nodes
    /// or transparent pass-through rules.
    /// </summary>
    /// <param name="ruleName">Grammar rule name to match.</param>
    /// <param name="handler">
    /// Function <c>(navigator, contextAtEntry) → result</c>.
    /// </param>
    public ParseTreeCompiler<TContext, TResult> OnAscend(
        string ruleName,
        Func<ParseTreeNavigator, TContext, TResult?> handler)
        => OnAscend(ruleName, (nav, ctx, _) => handler(nav, ctx));

    /// <summary>
    /// Sets the fallback descent handler used when no rule-specific handler is registered.
    /// <para>
    /// Defaults to passing the parent context through to children unchanged.
    /// </para>
    /// </summary>
    public ParseTreeCompiler<TContext, TResult> DefaultDescend(
        Func<ParseTreeNavigator, TContext, TContext> handler)
    {
        _defaultDescent = handler;
        return this;
    }

    /// <summary>
    /// Sets the fallback ascent handler used when no rule-specific handler is registered.
    /// <para>
    /// Defaults to returning <c>default(<typeparamref name="TResult"/>)</c>.
    /// </para>
    /// </summary>
    public ParseTreeCompiler<TContext, TResult> DefaultAscend(
        Func<ParseTreeNavigator, TContext, IReadOnlyList<TResult?>, TResult?> handler)
    {
        _defaultAscent = handler;
        return this;
    }

    /// <summary>
    /// Sets the handler invoked when an <see cref="ErrorNode"/> is encountered anywhere
    /// in the tree during traversal.
    /// <para>
    /// The default behaviour is to return <c>default(<typeparamref name="TResult"/>)</c>
    /// without throwing.
    /// </para>
    /// </summary>
    public ParseTreeCompiler<TContext, TResult> OnError(
        Func<ParseTreeNavigator, TContext, TResult?> handler)
    {
        _errorHandler = handler;
        return this;
    }

    // ── Compilation entry point ──────────────────────────────────────

    /// <summary>
    /// Compiles the parse tree rooted at <paramref name="root"/>, starting with
    /// <paramref name="initialContext"/> as the top-level context.
    /// </summary>
    /// <param name="root">Root node of the parse tree.</param>
    /// <param name="initialContext">Context to pass to the root node's descent handler.</param>
    /// <returns>The result produced by the root node's ascent handler.</returns>
    public TResult? Compile(ParseNode root, TContext initialContext)
        => Visit(new ParseTreeNavigator(root), initialContext);

    // ── Recursive traversal ──────────────────────────────────────────

    private TResult? Visit(ParseTreeNavigator nav, TContext context)
    {
        // Error nodes are not traversed further.
        if (nav.IsError)
            return _errorHandler is not null ? _errorHandler(nav, context) : default;

        // ── Phase 1 – Descent ────────────────────────────────────────
        // Compute the context to pass down to children.
        TContext childContext = Descend(nav, context);

        // ── Recurse into children ────────────────────────────────────
        IReadOnlyList<TResult?> childResults;
        if (nav.IsParser)
        {
            var results = new List<TResult?>();
            foreach (var child in nav.Children())
                results.Add(Visit(child, childContext));
            childResults = results;
        }
        else
        {
            // Lexer nodes are leaves — no children to visit.
            childResults = [];
        }

        // ── Phase 2 – Ascent ─────────────────────────────────────────
        // Compile the result from the node and its children's results.
        // Note: we pass `context` (the entry context), not `childContext`,
        // so the ascent handler sees the same scope that was active on arrival.
        return Ascend(nav, context, childResults);
    }

    private TContext Descend(ParseTreeNavigator nav, TContext context)
    {
        if (_descentHandlers.TryGetValue(nav.RuleName, out var handler))
            return handler(nav, context);
        return _defaultDescent is not null ? _defaultDescent(nav, context) : context;
    }

    private TResult? Ascend(
        ParseTreeNavigator nav,
        TContext context,
        IReadOnlyList<TResult?> childResults)
    {
        if (_ascentHandlers.TryGetValue(nav.RuleName, out var handler))
            return handler(nav, context, childResults);
        return _defaultAscent is not null
            ? _defaultAscent(nav, context, childResults)
            : default;
    }
}
