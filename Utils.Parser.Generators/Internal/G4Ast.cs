using System.Collections.Generic;

namespace Utils.Parser.Generators.Internal;

// ── Grammar-level ────────────────────────────────────────────────────────────

internal enum G4GrammarKind { Combined, Lexer, Parser }

internal sealed class G4Grammar
{
    public string                  Name        { get; set; } = "";
    public G4GrammarKind           Kind        { get; set; }
    /// <summary>Rules in DEFAULT_MODE.</summary>
    public List<G4Rule>            LexerRules  { get; } = new List<G4Rule>();
    public List<G4Rule>            ParserRules { get; } = new List<G4Rule>();
    /// <summary>Extra lexer modes declared via <c>mode Name;</c>.</summary>
    public List<G4LexerMode>       ExtraModes  { get; } = new List<G4LexerMode>();
}

internal sealed class G4LexerMode
{
    public string           Name  { get; set; } = "";
    public List<G4Rule>     Rules { get; } = new List<G4Rule>();
}

internal sealed class G4Rule
{
    public string            Name       { get; set; } = "";
    public bool              IsFragment { get; set; }
    public G4Alternation     Content    { get; set; } = new G4Alternation();
}

// ── Rule content ─────────────────────────────────────────────────────────────

/// <summary>Abstract base for all grammar content elements.</summary>
internal abstract class G4Content { }

internal sealed class G4Alternation : G4Content
{
    public List<G4Alternative> Alternatives { get; } = new List<G4Alternative>();
}

internal sealed class G4Alternative : G4Content
{
    public int            Priority { get; set; }
    public List<G4Content>  Items  { get; } = new List<G4Content>();
    public string?          Label  { get; set; }
}

internal sealed class G4Sequence : G4Content
{
    public List<G4Content> Items { get; } = new List<G4Content>();
}

internal sealed class G4Quantifier : G4Content
{
    public G4Content Inner   { get; set; } = null!;
    public int       Min     { get; set; }
    public int?      Max     { get; set; }
    public bool      Greedy  { get; set; } = true;
}

internal sealed class G4LiteralMatch : G4Content
{
    public string Value { get; set; } = "";
}

internal sealed class G4RangeMatch : G4Content
{
    public char From { get; set; }
    public char To   { get; set; }
}

/// <summary>Character class <c>[...]</c>: list of (char, char?) pairs for ranges + singles.</summary>
internal sealed class G4CharClassMatch : G4Content
{
    /// <summary>
    /// Each entry is either (c, null) for a single char or (lo, hi) for a range.
    /// </summary>
    public List<(char Lo, char? Hi)> Entries  { get; } = new List<(char, char?)>();
    public bool                      Negated  { get; set; }
}

internal sealed class G4AnyCharMatch : G4Content { }

internal sealed class G4RuleRef : G4Content
{
    public string RuleName { get; set; } = "";
}

internal sealed class G4Negation : G4Content
{
    public G4Content Inner { get; set; } = null!;
}

internal sealed class G4LexerCommand : G4Content
{
    public string  Name { get; set; } = "";
    public string? Arg  { get; set; }
}

internal sealed class G4EmbeddedAction : G4Content
{
    public string Code { get; set; } = "";
    /// <summary>True when the block ends with '?' (validating predicate).</summary>
    public bool IsPredicate { get; set; }
}
