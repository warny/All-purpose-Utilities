namespace Utils.Parser.Runtime;

/// <summary>
/// Provides fluent, index- and name-based navigation over a <see cref="ParseNode"/> tree
/// produced by <see cref="ParserEngine"/>.
/// <para>
/// Each navigation method returns a new <see cref="ParseTreeNavigator"/> wrapping the
/// target node so that calls can be chained:
/// <code>
/// var token = nav[0].Child("additionExp")[1][0].Token;
/// </code>
/// </para>
/// <para>
/// Methods that cannot guarantee a result have a <c>Try</c> variant that returns
/// <c>null</c> instead of throwing.
/// </para>
/// </summary>
public sealed class ParseTreeNavigator
{
    /// <summary>The node currently wrapped by this navigator.</summary>
    public ParseNode Node { get; }

    /// <summary>Initialises a navigator wrapping <paramref name="node"/>.</summary>
    public ParseTreeNavigator(ParseNode node) => Node = node;

    // ── State queries ────────────────────────────────────────────────

    /// <summary><c>true</c> when the current node is an <see cref="ErrorNode"/>.</summary>
    public bool IsError => Node is ErrorNode;

    /// <summary><c>true</c> when the current node is a <see cref="LexerNode"/>.</summary>
    public bool IsLexer => Node is LexerNode;

    /// <summary><c>true</c> when the current node is a <see cref="ParserNode"/>.</summary>
    public bool IsParser => Node is ParserNode;

    /// <summary>Name of the grammar rule that produced the current node.</summary>
    public string RuleName => Node.Rule.Name;

    /// <summary>
    /// The token of the current node when it is a <see cref="LexerNode"/>;
    /// <c>null</c> otherwise.
    /// </summary>
    public Token? Token => (Node as LexerNode)?.Token;

    /// <summary>
    /// The direct children of the current node when it is a <see cref="ParserNode"/>;
    /// <c>null</c> otherwise.
    /// </summary>
    public IReadOnlyList<ParseNode>? RawChildren => (Node as ParserNode)?.Children;

    // ── Indexer ──────────────────────────────────────────────────────

    /// <summary>
    /// Shorthand for <see cref="Child(int)"/>: navigates to the child at
    /// <paramref name="index"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current node is not a <see cref="ParserNode"/> or the index is
    /// out of range.
    /// </exception>
    public ParseTreeNavigator this[int index] => Child(index);

    // ── Navigation by index ──────────────────────────────────────────

    /// <summary>
    /// Navigates to the child at position <paramref name="index"/> among the direct
    /// children of the current node.
    /// </summary>
    /// <param name="index">Zero-based child index.</param>
    /// <returns>A new navigator wrapping the selected child.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current node is not a <see cref="ParserNode"/> or the index is
    /// out of range.
    /// </exception>
    public ParseTreeNavigator Child(int index)
    {
        var children = RequireChildren();
        if ((uint)index >= (uint)children.Count)
            throw new InvalidOperationException(
                $"Child index {index} is out of range for rule '{RuleName}' which has {children.Count} children.");
        return new ParseTreeNavigator(children[index]);
    }

    /// <summary>
    /// Navigates to the child at position <paramref name="index"/>, or returns
    /// <c>null</c> when the current node is not a <see cref="ParserNode"/> or the
    /// index is out of range.
    /// </summary>
    public ParseTreeNavigator? TryChild(int index)
    {
        var children = (Node as ParserNode)?.Children;
        if (children is null || (uint)index >= (uint)children.Count)
            return null;
        return new ParseTreeNavigator(children[index]);
    }

    // ── Navigation by rule name (direct children + quantifier wrappers) ─────────

    /// <summary>
    /// Navigates to the first child whose rule name equals <paramref name="ruleName"/>.
    /// Direct children are searched first; <see cref="QuantifierNode"/> wrappers are
    /// then searched recursively so that optional or repeated grammar elements
    /// (<c>rule?</c>, <c>rule*</c>, <c>rule+</c>) and grouped sequences
    /// (<c>(a b)?</c>) are reachable by name.
    /// </summary>
    /// <param name="ruleName">Rule name to search for.</param>
    /// <returns>A new navigator wrapping the matching child.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no matching child is found or the current node is not a
    /// <see cref="ParserNode"/>.
    /// </exception>
    public ParseTreeNavigator Child(string ruleName)
    {
        return TryChild(ruleName)
            ?? throw new InvalidOperationException(
                $"No child with rule name '{ruleName}' found under '{RuleName}'.");
    }

    /// <summary>
    /// Navigates to the first child whose rule name equals <paramref name="ruleName"/>,
    /// or returns <c>null</c> when none is found or the current node is not a
    /// <see cref="ParserNode"/>.
    /// Direct children have priority; <see cref="QuantifierNode"/> wrapper contents are
    /// searched only when no direct match exists. Grouped-sequence frames inside a
    /// <see cref="QuantifierNode"/> are traversed recursively.
    /// </summary>
    public ParseTreeNavigator? TryChild(string ruleName)
    {
        var children = (Node as ParserNode)?.Children;
        if (children is null) return null;

        var ownerRuleName = Node.Rule.Name;

        // When the current node IS a QuantifierNode, delegate to the recursive search.
        // It finds direct items, nested quantifiers, and grouped-sequence wrappers
        // without a redundant first scan.
        if (Node is QuantifierNode)
            return SearchQuantifierContent(children, ruleName, ownerRuleName);

        // Pass 1 — direct named children have priority over wrapper contents.
        foreach (var child in children)
            if (child.Rule.Name == ruleName)
                return new ParseTreeNavigator(child);

        // Pass 2 — search inside each QuantifierNode child.
        foreach (var child in children)
            if (child is QuantifierNode childQn)
            {
                var found = SearchQuantifierContent(childQn.Children, ruleName, childQn.Rule.Name);
                if (found != null) return found;
            }

        return null;
    }

    // ── Enumerating direct children ──────────────────────────────────

    /// <summary>
    /// Returns navigators for all direct children of the current node.
    /// Returns an empty sequence when the current node is not a <see cref="ParserNode"/>.
    /// </summary>
    public IEnumerable<ParseTreeNavigator> Children()
    {
        var children = (Node as ParserNode)?.Children;
        if (children is null) yield break;
        foreach (var child in children)
            yield return new ParseTreeNavigator(child);
    }

    /// <summary>
    /// Returns navigators for all children whose rule name equals
    /// <paramref name="ruleName"/>.
    /// Direct children are yielded first; <see cref="QuantifierNode"/> wrapper contents
    /// are then enumerated (recursively for grouped sequences) so that optional or
    /// repeated grammar elements are reachable by name.
    /// Returns an empty sequence when the current node is not a <see cref="ParserNode"/>
    /// or no matching child exists.
    /// </summary>
    public IEnumerable<ParseTreeNavigator> Children(string ruleName)
    {
        var children = (Node as ParserNode)?.Children;
        if (children is null) yield break;

        var ownerRuleName = Node.Rule.Name;

        // When the current node IS a QuantifierNode, delegate entirely to the recursive
        // search. This avoids double-counting: pass 1 would yield direct children and
        // SearchQuantifierContentAll would yield them a second time.
        if (Node is QuantifierNode)
        {
            foreach (var nav in SearchQuantifierContentAll(children, ruleName, ownerRuleName))
                yield return nav;
            yield break;
        }

        // Pass 1 — direct named children.
        foreach (var child in children)
            if (child.Rule.Name == ruleName)
                yield return new ParseTreeNavigator(child);

        // Pass 2 — search inside each QuantifierNode child.
        foreach (var child in children)
            if (child is QuantifierNode childQn)
                foreach (var nav in SearchQuantifierContentAll(childQn.Children, ruleName, childQn.Rule.Name))
                    yield return nav;
    }

    // ── Descendants (depth-first) ────────────────────────────────────

    /// <summary>
    /// Returns navigators for every descendant of the current node in depth-first
    /// pre-order (the current node itself is excluded).
    /// </summary>
    public IEnumerable<ParseTreeNavigator> Descendants()
    {
        foreach (var child in Children())
        {
            yield return child;
            foreach (var grandchild in child.Descendants())
                yield return grandchild;
        }
    }

    /// <summary>
    /// Returns navigators for every descendant whose rule name equals
    /// <paramref name="ruleName"/>, in depth-first pre-order.
    /// </summary>
    public IEnumerable<ParseTreeNavigator> Descendants(string ruleName)
        => Descendants().Where(n => n.RuleName == ruleName);

    /// <summary>
    /// Navigates to the first descendant whose rule name equals
    /// <paramref name="ruleName"/> (depth-first).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no matching descendant is found.
    /// </exception>
    public ParseTreeNavigator Descendant(string ruleName)
        => TryDescendant(ruleName)
            ?? throw new InvalidOperationException(
                $"No descendant with rule name '{ruleName}' found under '{RuleName}'.");

    /// <summary>
    /// Navigates to the first descendant whose rule name equals
    /// <paramref name="ruleName"/>, or returns <c>null</c> when none is found.
    /// </summary>
    public ParseTreeNavigator? TryDescendant(string ruleName)
        => Descendants().FirstOrDefault(n => n.RuleName == ruleName);

    // ── Implicit conversions ─────────────────────────────────────────

    /// <summary>Wraps a <see cref="ParseNode"/> in a new navigator.</summary>
    public static implicit operator ParseTreeNavigator(ParseNode node) => new(node);

    /// <summary>Unwraps the underlying <see cref="ParseNode"/>.</summary>
    public static implicit operator ParseNode(ParseTreeNavigator nav) => nav.Node;

    // ── Object overrides ─────────────────────────────────────────────

    /// <inheritdoc/>
    public override string ToString() =>
        Node switch
        {
            LexerNode ln  => $"LexerNode[{ln.Rule.Name}] \"{ln.Token.Text}\"",
            ParserNode pn => $"ParserNode[{pn.Rule.Name}] ({pn.Children.Count} children)",
            ErrorNode  en => $"ErrorNode \"{en.Message}\"",
            _             => Node.ToString()!
        };

    // ── Private helpers ──────────────────────────────────────────────

    private IReadOnlyList<ParseNode> RequireChildren()
    {
        if (Node is ParserNode pn) return pn.Children;
        throw new InvalidOperationException(
            $"Cannot navigate into children: current node is a {Node.GetType().Name} (rule '{RuleName}').");
    }

    /// <summary>
    /// Returns the first node named <paramref name="ruleName"/> inside quantifier content,
    /// recursing through same-rule <see cref="ParserNode"/> frames (synthetic sequence
    /// wrappers from grouped quantifiers) and nested <see cref="QuantifierNode"/> wrappers.
    /// </summary>
    private static ParseTreeNavigator? SearchQuantifierContent(
        IReadOnlyList<ParseNode> nodes, string ruleName, string ownerRuleName)
    {
        foreach (var node in nodes)
        {
            if (node.Rule.Name == ruleName)
                return new ParseTreeNavigator(node);
            if (node is QuantifierNode innerQn)
            {
                var found = SearchQuantifierContent(innerQn.Children, ruleName, ownerRuleName);
                if (found != null) return found;
            }
            else if (node is ParserNode pn && pn.Rule.Name == ownerRuleName)
            {
                // Synthetic sequence wrapper produced by TryParseSequence inside a quantifier.
                var found = SearchQuantifierContent(pn.Children, ruleName, ownerRuleName);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Yields all nodes named <paramref name="ruleName"/> inside quantifier content,
    /// recursing through same-rule <see cref="ParserNode"/> frames and nested
    /// <see cref="QuantifierNode"/> wrappers.
    /// </summary>
    private static IEnumerable<ParseTreeNavigator> SearchQuantifierContentAll(
        IReadOnlyList<ParseNode> nodes, string ruleName, string ownerRuleName)
    {
        foreach (var node in nodes)
        {
            if (node.Rule.Name == ruleName)
            {
                yield return new ParseTreeNavigator(node);
            }
            else if (node is QuantifierNode innerQn)
            {
                foreach (var nav in SearchQuantifierContentAll(innerQn.Children, ruleName, ownerRuleName))
                    yield return nav;
            }
            else if (node is ParserNode pn && pn.Rule.Name == ownerRuleName)
            {
                foreach (var nav in SearchQuantifierContentAll(pn.Children, ruleName, ownerRuleName))
                    yield return nav;
            }
        }
    }
}
