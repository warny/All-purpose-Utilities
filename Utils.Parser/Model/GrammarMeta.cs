namespace Utils.Parser.Model;

/// <summary>
/// Indicates the kind of grammar declared in a <c>.g4</c> file.
/// </summary>
public enum GrammarType
{
    /// <summary>Grammar contains both lexer and parser rules (<c>grammar Foo;</c>).</summary>
    Combined,
    /// <summary>Grammar contains only lexer rules (<c>lexer grammar Foo;</c>).</summary>
    Lexer,
    /// <summary>Grammar contains only parser rules (<c>parser grammar Foo;</c>).</summary>
    Parser
}

/// <summary>
/// Represents the key/value pairs declared in an <c>options { ... }</c> block,
/// such as <c>tokenVocab=MyLexer</c> or <c>superClass=MyBase</c>.
/// </summary>
public record GrammarOptions(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Represents normalized and runtime-consumable grammar options.
/// </summary>
public sealed record EffectiveGrammarOptions
{
    /// <summary>Gets the optional token vocabulary dependency grammar name.</summary>
    public string? TokenVocab { get; init; }

    /// <summary>Gets the optional parser base class declared through <c>superClass</c>.</summary>
    public string? ParserSuperClass { get; init; }

    /// <summary>Gets the optional lexer base class declared through <c>superClass</c>.</summary>
    public string? LexerSuperClass { get; init; }

    /// <summary>Gets a value indicating whether matching is case-insensitive.</summary>
    public bool CaseInsensitive { get; init; }
}

/// <summary>
/// Represents a named action block declared at grammar level,
/// such as <c>@header { import ... }</c> or <c>@members { int count = 0; }</c>.
/// </summary>
public record GrammarAction(
    /// <summary>Action name: <c>"header"</c>, <c>"members"</c>, or a custom name.</summary>
    string Name,
    /// <summary>Raw code content of the action block (without surrounding braces).</summary>
    string RawCode,
    /// <summary>
    /// Optional scope target for scoped actions like <c>@header::lexer { }</c>.
    /// <c>null</c> when not scoped.
    /// </summary>
    string? Target = null
);

/// <summary>
/// Represents a grammar import directive: <c>import CommonLexer;</c>
/// or an aliased import: <c>import alias=CommonLexer;</c>.
/// </summary>
public record GrammarImport(
    /// <summary>Name of the imported grammar.</summary>
    string GrammarName,
    /// <summary>Optional local alias assigned to the imported grammar.</summary>
    string? Alias = null
);
