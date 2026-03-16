namespace Utils.Parser.Resolution;

/// <summary>
/// Thrown by <see cref="RuleResolver"/> when a grammar definition is structurally invalid,
/// for example when a rule references an unknown rule name, when a fragment rule is of
/// parser kind, or when a lexer rule mixes lexer and parser content.
/// </summary>
public class GrammarValidationException(string message) : Exception(message);
