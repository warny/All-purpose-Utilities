using Utils.Parser.Model;
using Utils.Parser.Source;

namespace Utils.Parser.Runtime;

/// <summary>
/// A trie (prefix tree) that indexes all pure-literal lexer rules within one lexer mode.
/// <para>
/// A "pure-literal" rule is one whose every alternative consists of a single
/// <see cref="LiteralMatch"/> — in other words, a fixed keyword or operator token
/// with no character-class patterns, quantifiers, or lexer commands.
/// </para>
/// <para>
/// The trie is built once at <see cref="LexerEngine"/> initialisation time and then
/// used in <c>MatchLongest</c> to replace the O(n_rules × keyword_length) loop over
/// individual keyword rules with a single O(keyword_length) trie traversal.
/// Because the trie naturally walks as deep as possible before backtracking,
/// it always returns the longest matching keyword — no declaration-order constraint
/// is needed in the grammar source.
/// </para>
/// </summary>
internal sealed class KeywordLookup
{
    private readonly bool _caseInsensitive;
    private readonly Dictionary<char, KeywordNode> _root = [];

    /// <summary>
    /// Returns <see langword="true"/> when no rules have been added to this lookup.
    /// </summary>
    public bool IsEmpty => _root.Count == 0;

    /// <summary>
    /// Initialises a new <see cref="KeywordLookup"/> with the specified case-sensitivity.
    /// </summary>
    /// <param name="caseInsensitive">
    /// When <see langword="true"/>, keyword characters are normalised to upper-case
    /// during both insertion and lookup.
    /// </param>
    public KeywordLookup(bool caseInsensitive) => _caseInsensitive = caseInsensitive;

    /// <summary>
    /// Inserts <paramref name="keyword"/> into the trie and maps it to
    /// <paramref name="rule"/>. When the same keyword is inserted twice the rule
    /// with the lower <see cref="Rule.DeclarationOrder"/> is kept, matching the
    /// tie-breaking behaviour of <see cref="LexerEngine"/>.
    /// </summary>
    /// <param name="keyword">Exact literal string from the grammar.</param>
    /// <param name="rule">Lexer rule that emits this literal as a token.</param>
    public void Add(string keyword, Rule rule)
    {
        if (string.IsNullOrEmpty(keyword)) return;

        var nodes = _root;
        for (int i = 0; i < keyword.Length; i++)
        {
            char key = Normalize(keyword[i]);
            if (!nodes.TryGetValue(key, out var node))
                nodes[key] = node = new KeywordNode();

            if (i == keyword.Length - 1)
            {
                // Tie-break: lower DeclarationOrder wins, matching LexerEngine semantics.
                if (node.Rule is null || rule.DeclarationOrder < node.Rule.DeclarationOrder)
                    node.Rule = rule;
            }

            nodes = node.Children;
        }
    }

    /// <summary>
    /// Attempts to match the longest keyword starting at the current stream position.
    /// The stream is not advanced; the caller is responsible for consuming
    /// <c>token.Span.Length</c> characters on success.
    /// </summary>
    /// <param name="stream">Character stream positioned at the start of the potential token.</param>
    /// <param name="modeName">Active lexer mode name, embedded in the returned token.</param>
    /// <returns>
    /// The matched <see cref="Token"/> and its <see cref="Rule"/> on success,
    /// or <c>(null, null)</c> when no keyword matches at the current position.
    /// </returns>
    public (SourceSpan? Span, Rule? Rule) TryMatch(TextReaderLookahead stream, string modeName, string? filePath)
    {
        var nodes = _root;
        Rule? bestRule = null;
        int bestLength = 0;
        int offset = 0;

        while (true)
        {
            int peeked = stream.Peek(offset);
            if (peeked < 0) break;                                // end of stream
            char c = (char)peeked;                                // safe: buffer stores char (UTF-16 units)

            char key = Normalize(c);
            if (!nodes.TryGetValue(key, out var node)) break;   // no further trie path

            offset++;
            if (node.Rule is not null)
            {
                bestRule = node.Rule;
                bestLength = offset;
            }

            nodes = node.Children;
            if (nodes.Count == 0) break;                        // leaf — no deeper path
        }

        if (bestRule is null) return (null, null);
        return (new SourceSpan(stream.Position, bestLength, stream.Line, stream.Column, filePath), bestRule);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private char Normalize(char c) =>
        _caseInsensitive ? char.ToUpperInvariant(c) : c;

    // ── Node ──────────────────────────────────────────────────────────────────────

    private sealed class KeywordNode
    {
        /// <summary>
        /// The rule that terminates at this node, or <see langword="null"/> when this
        /// node is only an intermediate step in longer keywords.
        /// </summary>
        public Rule? Rule;

        /// <summary>Child nodes keyed by the next (normalised) character.</summary>
        public readonly Dictionary<char, KeywordNode> Children = [];
    }
}
