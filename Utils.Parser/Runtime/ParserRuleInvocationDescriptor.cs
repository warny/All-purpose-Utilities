using System.Collections.ObjectModel;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Describes passive metadata associated with a parser rule invocation.
/// The descriptor preserves grammar metadata for observation only and does not bind,
/// allocate, propagate, or execute rule-level metadata.
/// </summary>
public sealed class ParserRuleInvocationDescriptor
{
    /// <summary>
    /// Backing store for passive parameter descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleParameterDescriptor> _parameters = [];

    /// <summary>
    /// Backing store for passive return descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleReturnDescriptor> _returns = [];

    /// <summary>
    /// Backing store for passive local descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleLocalDescriptor> _locals = [];

    /// <summary>
    /// Backing store for passive rule option metadata.
    /// </summary>
    private IReadOnlyDictionary<string, string> _options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Backing store for passive exception metadata descriptors.
    /// </summary>
    private IReadOnlyList<ParserRuleExceptionDescriptor> _exceptions = [];

    /// <summary>
    /// Gets the parser rule name associated with the invocation.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the raw parameter metadata text when it is available in the parser model.
    /// </summary>
    public string? RawParameters { get; init; }

    /// <summary>
    /// Gets the raw return metadata text when it is available in the parser model.
    /// </summary>
    public string? RawReturnType { get; init; }

    /// <summary>
    /// Gets the raw locals metadata text when it is available in the parser model.
    /// </summary>
    public string? RawLocals { get; init; }

    /// <summary>
    /// Gets passive parameter descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleParameterDescriptor> Parameters
    {
        get => _parameters;
        init => _parameters = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive return descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleReturnDescriptor> Returns
    {
        get => _returns;
        init => _returns = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive local descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleLocalDescriptor> Locals
    {
        get => _locals;
        init => _locals = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Gets passive rule option metadata keyed by option name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options
    {
        get => _options;
        init => _options = value is null
            ? throw new ArgumentNullException(nameof(value))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(value, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets passive exception metadata descriptors declared by the parser rule.
    /// </summary>
    public IReadOnlyList<ParserRuleExceptionDescriptor> Exceptions
    {
        get => _exceptions;
        init => _exceptions = value is null
            ? throw new ArgumentNullException(nameof(value))
            : value.ToArray();
    }

    /// <summary>
    /// Creates a passive invocation descriptor from the metadata currently exposed by a parser rule.
    /// </summary>
    /// <param name="rule">Parser rule whose available metadata should be described.</param>
    /// <returns>A passive descriptor containing only currently represented rule metadata.</returns>
    public static ParserRuleInvocationDescriptor FromRule(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var parameterDescriptors = rule.Parameters?
            .Select(static parameter => new ParserRuleParameterDescriptor
            {
                Name = parameter.Name,
                RawDeclaration = GetRawDeclaration(parameter.Type, parameter.Name)
            })
            .ToArray() ?? [];
        var returnDescriptors = rule.Returns?
            .Select(static ruleReturn => new ParserRuleReturnDescriptor
            {
                Name = ruleReturn.Name,
                RawDeclaration = GetRawDeclaration(ruleReturn.Type, ruleReturn.Name)
            })
            .ToArray() ?? [];
        var localDescriptors = rule.Locals?
            .SelectMany(static local => BuildLocalDescriptors(local.RawDeclaration))
            .ToArray() ?? [];
        var exceptionDescriptors = BuildExceptionDescriptors(rule.ExceptionMetadata);

        return new ParserRuleInvocationDescriptor
        {
            RuleName = rule.Name,
            RawParameters = JoinRawDeclarations(parameterDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawReturnType = JoinRawDeclarations(returnDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            RawLocals = JoinRawDeclarations(localDescriptors.Select(static descriptor => descriptor.RawDeclaration)),
            Parameters = parameterDescriptors,
            Returns = returnDescriptors,
            Locals = localDescriptors,
            Options = rule.Options?.Values ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Exceptions = exceptionDescriptors
        };
    }

    /// <summary>
    /// Builds passive local descriptors by separating top-level declarations and capturing only their names.
    /// </summary>
    /// <param name="rawLocals">Raw contents of a rule <c>locals [...]</c> clause.</param>
    /// <returns>Untyped local descriptors preserving each raw declaration and its lexical name.</returns>
    private static IEnumerable<ParserRuleLocalDescriptor> BuildLocalDescriptors(string rawLocals)
    {
        foreach (string declaration in SplitTopLevelLocalDeclarations(rawLocals))
        {
            yield return new ParserRuleLocalDescriptor
            {
                Name = GetLocalDeclarationName(declaration),
                RawDeclaration = declaration
            };
        }
    }

    /// <summary>
    /// Splits raw local metadata at top-level commas without interpreting target-language types.
    /// </summary>
    /// <param name="rawLocals">Raw local declaration list to split.</param>
    /// <returns>Trimmed declarations in source order.</returns>
    private static IEnumerable<string> SplitTopLevelLocalDeclarations(string rawLocals)
    {
        int start = 0;
        int squareDepth = 0;
        int roundDepth = 0;
        int braceDepth = 0;
        int angleDepth = 0;
        bool assignmentSeen = false;

        for (int index = 0; index < rawLocals.Length; index++)
        {
            if (TrySkipCommentOrLiteral(rawLocals, ref index))
            {
                continue;
            }

            char current = rawLocals[index];
            if (current == '[')
            {
                squareDepth++;
            }
            else if (current == ']')
            {
                squareDepth = int.Max(0, squareDepth - 1);
            }
            else if (current == '(')
            {
                roundDepth++;
            }
            else if (current == ')')
            {
                roundDepth = int.Max(0, roundDepth - 1);
            }
            else if (current == '{')
            {
                braceDepth++;
            }
            else if (current == '}')
            {
                braceDepth = int.Max(0, braceDepth - 1);
            }
            else if (current == '<' && IsLikelyGenericOpening(rawLocals, index, assignmentSeen))
            {
                angleDepth++;
            }
            else if (current == '>' && angleDepth > 0)
            {
                angleDepth--;
            }
            else if (current == '=' && IsAssignmentOperator(rawLocals, index)
                && squareDepth == 0 && roundDepth == 0 && braceDepth == 0 && angleDepth == 0)
            {
                assignmentSeen = true;
            }
            else if (current == ','
                && squareDepth == 0 && roundDepth == 0 && braceDepth == 0 && angleDepth == 0)
            {
                string declaration = rawLocals[start..index].Trim();
                if (declaration.Length > 0)
                {
                    yield return declaration;
                }

                start = index + 1;
                assignmentSeen = false;
            }
        }

        string finalDeclaration = rawLocals[start..].Trim();
        if (finalDeclaration.Length > 0)
        {
            yield return finalDeclaration;
        }
    }

    /// <summary>
    /// Gets the final identifier before a top-level initializer without binding or validating its type.
    /// </summary>
    /// <param name="declaration">One raw local declaration.</param>
    /// <returns>The lexical local name, or <c>null</c> when no identifier is available.</returns>
    private static string? GetLocalDeclarationName(string declaration)
    {
        int end = FindTopLevelAssignment(declaration);
        string? name = null;

        for (int index = 0; index < end; index++)
        {
            if (TrySkipCommentOrLiteral(declaration, ref index))
            {
                continue;
            }

            if (!IsIdentifierStart(declaration[index]))
            {
                continue;
            }

            int identifierStart = index;
            while (index + 1 < end && IsIdentifierPart(declaration[index + 1]))
            {
                index++;
            }

            name = declaration[identifierStart..(index + 1)];
        }

        return name;
    }

    /// <summary>
    /// Finds the first top-level assignment operator in one raw declaration.
    /// </summary>
    /// <param name="declaration">Raw local declaration to inspect.</param>
    /// <returns>The assignment index, or the declaration length when no initializer is present.</returns>
    private static int FindTopLevelAssignment(string declaration)
    {
        int squareDepth = 0;
        int roundDepth = 0;
        int braceDepth = 0;
        int angleDepth = 0;

        for (int index = 0; index < declaration.Length; index++)
        {
            if (TrySkipCommentOrLiteral(declaration, ref index))
            {
                continue;
            }

            char current = declaration[index];
            if (current == '[')
            {
                squareDepth++;
            }
            else if (current == ']')
            {
                squareDepth = int.Max(0, squareDepth - 1);
            }
            else if (current == '(')
            {
                roundDepth++;
            }
            else if (current == ')')
            {
                roundDepth = int.Max(0, roundDepth - 1);
            }
            else if (current == '{')
            {
                braceDepth++;
            }
            else if (current == '}')
            {
                braceDepth = int.Max(0, braceDepth - 1);
            }
            else if (current == '<' && IsLikelyGenericOpening(declaration, index, assignmentSeen: false))
            {
                angleDepth++;
            }
            else if (current == '>' && angleDepth > 0)
            {
                angleDepth--;
            }
            else if (current == '=' && IsAssignmentOperator(declaration, index)
                && squareDepth == 0 && roundDepth == 0 && braceDepth == 0 && angleDepth == 0)
            {
                return index;
            }
        }

        return declaration.Length;
    }

    /// <summary>
    /// Determines whether a less-than token is likely to open generic syntax rather than represent an operator.
    /// </summary>
    /// <param name="text">Raw declaration text.</param>
    /// <param name="index">Index of the less-than token.</param>
    /// <param name="assignmentSeen">Whether the current declaration has already entered its initializer.</param>
    /// <returns><c>true</c> when a matching generic close token is lexically plausible; otherwise, <c>false</c>.</returns>
    private static bool IsLikelyGenericOpening(string text, int index, bool assignmentSeen)
    {
        if (index + 1 >= text.Length || text[index + 1] is '=' or '<')
        {
            return false;
        }

        int previous = FindPreviousNonWhitespace(text, index - 1);
        if (previous < 0 || (!IsIdentifierPart(text[previous]) && text[previous] is not ']' and not ')' and not '>'))
        {
            return false;
        }

        int close = FindMatchingAngleBracket(text, index);
        if (close < 0)
        {
            return false;
        }

        if (!assignmentSeen)
        {
            return true;
        }

        int next = FindNextNonWhitespace(text, close + 1);
        return next < text.Length && text[next] is '(' or '.' or '[' or '?' or '!';
    }

    /// <summary>
    /// Finds a matching angle-bracket close while ignoring comments and literals.
    /// </summary>
    /// <param name="text">Raw declaration text.</param>
    /// <param name="openingIndex">Index of the possible generic opening token.</param>
    /// <returns>The matching close index, or <c>-1</c> when none is found.</returns>
    private static int FindMatchingAngleBracket(string text, int openingIndex)
    {
        int depth = 1;
        for (int index = openingIndex + 1; index < text.Length; index++)
        {
            if (TrySkipCommentOrLiteral(text, ref index))
            {
                continue;
            }

            if (depth == 1 && text[index] == '=' && IsAssignmentOperator(text, index))
            {
                return -1;
            }

            if (text[index] == '<' && index + 1 < text.Length && text[index + 1] != '=')
            {
                depth++;
            }
            else if (text[index] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Skips a comment, character literal, or string literal beginning at the supplied index.
    /// </summary>
    /// <param name="text">Raw declaration text.</param>
    /// <param name="index">Current index, updated to the final character of skipped text.</param>
    /// <returns><c>true</c> when non-code text was skipped; otherwise, <c>false</c>.</returns>
    private static bool TrySkipCommentOrLiteral(string text, ref int index)
    {
        if (text[index] == '/' && index + 1 < text.Length)
        {
            if (text[index + 1] == '/')
            {
                index = SkipLineComment(text, index + 2);
                return true;
            }

            if (text[index + 1] == '*')
            {
                index = SkipBlockComment(text, index + 2);
                return true;
            }
        }

        if (text[index] == '\'')
        {
            index = SkipRegularQuotedText(text, index, '\'');
            return true;
        }

        if (text[index] != '"')
        {
            return false;
        }

        int quoteCount = CountConsecutiveCharacters(text, index, '"');
        if (quoteCount >= 3)
        {
            index = SkipRawString(text, index, quoteCount);
        }
        else if (HasVerbatimStringPrefix(text, index))
        {
            index = SkipVerbatimString(text, index);
        }
        else
        {
            index = SkipRegularQuotedText(text, index, '"');
        }

        return true;
    }

    /// <summary>
    /// Skips a line comment and returns the final skipped index.
    /// </summary>
    private static int SkipLineComment(string text, int index)
    {
        while (index < text.Length && text[index] is not '\r' and not '\n')
        {
            index++;
        }

        return int.Min(index, text.Length - 1);
    }

    /// <summary>
    /// Skips a block comment and returns the final skipped index.
    /// </summary>
    private static int SkipBlockComment(string text, int index)
    {
        while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/'))
        {
            index++;
        }

        return int.Min(index + 1, text.Length - 1);
    }

    /// <summary>
    /// Skips a regular quoted literal and returns its closing delimiter index.
    /// </summary>
    private static int SkipRegularQuotedText(string text, int openingIndex, char delimiter)
    {
        for (int index = openingIndex + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == delimiter)
            {
                return index;
            }
        }

        return text.Length - 1;
    }

    /// <summary>
    /// Skips a verbatim string literal and returns its closing quote index.
    /// </summary>
    private static int SkipVerbatimString(string text, int openingIndex)
    {
        for (int index = openingIndex + 1; index < text.Length; index++)
        {
            if (text[index] != '"')
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '"')
            {
                index++;
                continue;
            }

            return index;
        }

        return text.Length - 1;
    }

    /// <summary>
    /// Skips a raw string literal and returns the final closing quote index.
    /// </summary>
    private static int SkipRawString(string text, int openingIndex, int quoteCount)
    {
        for (int index = openingIndex + quoteCount; index < text.Length; index++)
        {
            if (text[index] == '"' && CountConsecutiveCharacters(text, index, '"') >= quoteCount)
            {
                return index + quoteCount - 1;
            }
        }

        return text.Length - 1;
    }

    /// <summary>
    /// Determines whether a quote belongs to a verbatim string prefix.
    /// </summary>
    private static bool HasVerbatimStringPrefix(string text, int quoteIndex)
        => quoteIndex > 0 && text[quoteIndex - 1] == '@'
            || quoteIndex > 1 && text[quoteIndex - 2] == '@' && text[quoteIndex - 1] == '$';

    /// <summary>
    /// Counts consecutive occurrences of one character.
    /// </summary>
    private static int CountConsecutiveCharacters(string text, int index, char value)
    {
        int count = 0;
        while (index + count < text.Length && text[index + count] == value)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Determines whether an equals token is an assignment rather than a comparison, lambda, or compound operator.
    /// </summary>
    private static bool IsAssignmentOperator(string text, int index)
    {
        char previous = index > 0 ? text[index - 1] : '\0';
        char next = index + 1 < text.Length ? text[index + 1] : '\0';
        return previous is not '=' and not '!' and not '<' and not '>'
            && next is not '=' and not '>';
    }

    /// <summary>
    /// Finds the previous non-whitespace character index.
    /// </summary>
    private static int FindPreviousNonWhitespace(string text, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index;
    }

    /// <summary>
    /// Finds the next non-whitespace character index.
    /// </summary>
    private static int FindNextNonWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Determines whether a character can begin a lexical identifier.
    /// </summary>
    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    /// <summary>
    /// Determines whether a character can continue a lexical identifier.
    /// </summary>
    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';

    /// <summary>
    /// Builds passive exception descriptors from metadata preserved by the parser rule model.
    /// </summary>
    /// <param name="metadata">Rule exception metadata, or <c>null</c> when none is represented.</param>
    /// <returns>Passive exception descriptors preserving only available raw text.</returns>
    private static ParserRuleExceptionDescriptor[] BuildExceptionDescriptors(RuleExceptionMetadata? metadata)
    {
        if (metadata is null)
        {
            return [];
        }

        var descriptors = new List<ParserRuleExceptionDescriptor>();
        descriptors.AddRange(metadata.Throws.Select(static declaration => new ParserRuleExceptionDescriptor
        {
            Kind = "throws",
            RawDeclaration = declaration
        }));
        descriptors.AddRange(metadata.CatchClauses.Select(static clause => new ParserRuleExceptionDescriptor
        {
            Kind = "catch",
            RawDeclaration = $"[{clause.RawArgument}] {{ {clause.RawAction} }}"
        }));

        if (metadata.FinallyAction is not null)
        {
            descriptors.Add(new ParserRuleExceptionDescriptor
            {
                Kind = "finally",
                RawDeclaration = $"{{ {metadata.FinallyAction} }}"
            });
        }

        return descriptors.ToArray();
    }

    /// <summary>
    /// Preserves a declaration as raw metadata without inferring target-language types.
    /// </summary>
    /// <param name="type">Type text currently exposed by the parser model.</param>
    /// <param name="name">Name text currently exposed by the parser model.</param>
    /// <returns>The raw declaration text available to the descriptor.</returns>
    private static string GetRawDeclaration(string type, string name)
    {
        return string.Equals(type, name, StringComparison.Ordinal)
            ? type
            : $"{type} {name}";
    }

    /// <summary>
    /// Joins represented metadata declarations into a raw descriptor field.
    /// </summary>
    /// <param name="descriptors">Descriptors whose raw declarations should be joined.</param>
    /// <returns>A comma-separated raw metadata string, or <c>null</c> when no metadata is represented.</returns>
    private static string? JoinRawDeclarations(IEnumerable<string> rawDeclarations)
    {
        var raw = string.Join(", ", rawDeclarations);
        return raw.Length == 0 ? null : raw;
    }
}

/// <summary>
/// Describes a parser rule parameter declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleParameterDescriptor
{
    /// <summary>
    /// Gets the parameter name text currently exposed by the parser model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw parameter declaration text without semantic type binding.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes a parser rule return declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleReturnDescriptor
{
    /// <summary>
    /// Gets the return value name text currently exposed by the parser model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw return declaration text without semantic type binding.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes a parser rule local declaration as passive metadata only.
/// </summary>
public sealed class ParserRuleLocalDescriptor
{
    /// <summary>
    /// Gets the local name text when it is available in the parser model.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the raw local declaration text without semantic type binding or allocation.
    /// </summary>
    public required string RawDeclaration { get; init; }
}

/// <summary>
/// Describes parser rule exception metadata as passive metadata only.
/// </summary>
public sealed class ParserRuleExceptionDescriptor
{
    /// <summary>
    /// Gets the exception metadata kind, such as <c>throws</c>, <c>catch</c>, or <c>finally</c>, when available.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Gets the raw exception metadata text without exception handling semantics.
    /// </summary>
    public required string RawDeclaration { get; init; }
}
