using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Data.Sql;

/// <summary>
/// Formats SQL token streams according to the selected <see cref="SqlFormattingMode"/>.
/// </summary>
internal static class SqlPrettyPrinter
{
    /// <summary>
    /// Represents the clause currently being formatted.
    /// </summary>
    private enum ClauseContext
    {
        /// <summary>
        /// No special clause formatting is active.
        /// </summary>
        None,

        /// <summary>
        /// The clause contains SELECT list items.
        /// </summary>
        SelectList,

        /// <summary>
        /// The clause contains GROUP BY expressions.
        /// </summary>
        GroupByList,

        /// <summary>
        /// The clause contains ORDER BY expressions.
        /// </summary>
        OrderByList,

        /// <summary>
        /// The clause contains VALUES rows.
        /// </summary>
        ValuesList,

        /// <summary>
        /// The clause contains SET assignments.
        /// </summary>
        SetList,

        /// <summary>
        /// The clause contains RETURNING expressions.
        /// </summary>
        ReturningList,
    }

    /// <summary>
    /// Represents a formatted line made of SQL tokens.
    /// </summary>
    private sealed class FormattedLine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedLine"/> class.
        /// </summary>
        /// <param name="indentSpaces">The indentation applied to the line expressed in spaces.</param>
        public FormattedLine(int indentSpaces)
        {
            IndentSpaces = indentSpaces;
            Tokens = new List<string>();
        }

        /// <summary>
        /// Gets the number of leading spaces applied to the line.
        /// </summary>
        public int IndentSpaces { get; }

        /// <summary>
        /// Gets the SQL tokens forming the line.
        /// </summary>
        public List<string> Tokens { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the first comma should be rendered without a trailing space.
        /// </summary>
        public bool SuppressSpaceAfterLeadingComma { get; set; }
    }

    private static readonly HashSet<string> ClauseKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP",
        "HAVING",
        "ORDER",
        "LIMIT",
        "OFFSET",
        "VALUES",
        "RETURNING",
        "SET",
        "INSERT",
        "UPDATE",
        "DELETE",
        "UNION",
        "INTERSECT",
        "EXCEPT",
    };

    private static readonly HashSet<string> JoinLeadingModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "INNER",
        "LEFT",
        "RIGHT",
        "FULL",
        "CROSS",
    };

    /// <summary>
    /// Formats the provided SQL text according to the specified options.
    /// </summary>
    /// <param name="sql">The inline SQL string.</param>
    /// <param name="options">Formatting options controlling the output.</param>
    /// <param name="syntaxOptions">Syntax options influencing tokenization.</param>
    /// <returns>The formatted SQL text.</returns>
    public static string Format(string sql, SqlFormattingOptions options, SqlSyntaxOptions? syntaxOptions = null)
    {
        if (options.Mode == SqlFormattingMode.Inline)
        {
            return sql;
        }

        syntaxOptions ??= SqlSyntaxOptions.Default;
        var tokenizer = new SqlTokenizer(sql, syntaxOptions);
        IReadOnlyList<SqlToken> tokens = tokenizer.Tokenize();
        return options.Mode switch
        {
            SqlFormattingMode.Prefixed => FormatList(tokens, options, true),
            SqlFormattingMode.Suffixed => FormatList(tokens, options, false),
            _ => sql,
        };
    }

    /// <summary>
    /// Formats the supplied SQL tokens into multi-line text.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="options">Formatting options driving indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <returns>The formatted SQL text.</returns>
    private static string FormatList(IReadOnlyList<SqlToken> tokens, SqlFormattingOptions options, bool commaAtLineStart)
    {
        var lines = new List<FormattedLine>();
        FormattedLine? currentLine = null;
        var parenthesisStack = new Stack<bool>();
        int indentLevel = 0;
        ClauseContext clause = ClauseContext.None;
        bool firstItem = false;
        bool pendingComma = false;
        int clauseIndent = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            SqlToken token = tokens[i];
            string text = token.Text;
            string upper = token.Normalized;

            if (TryHandleClauseStart(tokens, options, commaAtLineStart, ref i, ref currentLine, lines, ref indentLevel, ref clause, ref firstItem, ref pendingComma, ref clauseIndent, upper, text))
            {
                continue;
            }

            if (clause != ClauseContext.None && text != ",")
            {
                PrepareClauseLine(options, commaAtLineStart, ref currentLine, lines, ref clause, ref firstItem, ref pendingComma, clauseIndent);
            }

            if (text == "," && clause != ClauseContext.None)
            {
                if (commaAtLineStart)
                {
                    pendingComma = true;
                }
                else
                {
                    AppendToken(ref currentLine, lines, clauseIndent + options.IndentSize, text);
                    CommitLine(ref currentLine, lines);
                    firstItem = true;
                }

                continue;
            }

            int baseIndent = indentLevel * options.IndentSize;
            int effectiveIndent;
            if (clause != ClauseContext.None)
            {
                effectiveIndent = currentLine?.IndentSpaces ?? clauseIndent + options.IndentSize;
            }
            else
            {
                effectiveIndent = baseIndent;
            }

            if (text == "(")
            {
                if (clause != ClauseContext.None && firstItem)
                {
                    PrepareClauseLine(options, commaAtLineStart, ref currentLine, lines, ref clause, ref firstItem, ref pendingComma, clauseIndent);
                }

                AppendToken(ref currentLine, lines, effectiveIndent, text);
                bool multiline = ShouldExpandParenthesis(tokens, i + 1);
                parenthesisStack.Push(multiline);
                indentLevel++;
                if (multiline)
                {
                    CommitLine(ref currentLine, lines);
                }

                continue;
            }

            if (text == ")")
            {
                bool multiline = parenthesisStack.Count > 0 && parenthesisStack.Pop();
                indentLevel = Math.Max(0, indentLevel - 1);
                if (multiline)
                {
                    CommitLine(ref currentLine, lines);
                }

                baseIndent = indentLevel * options.IndentSize;
                if (clause != ClauseContext.None)
                {
                    effectiveIndent = currentLine?.IndentSpaces ?? clauseIndent + options.IndentSize;
                }
                else
                {
                    effectiveIndent = baseIndent;
                }
                AppendToken(ref currentLine, lines, effectiveIndent, text);
                continue;
            }

            AppendToken(ref currentLine, lines, effectiveIndent, text);
        }

        CommitLine(ref currentLine, lines);
        return BuildFormattedText(lines);
    }

    /// <summary>
    /// Detects and formats clause keywords encountered while walking the token list.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="options">Formatting options driving indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <param name="index">Index of the current token. The value may be advanced when multiple tokens are consumed.</param>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentLevel">The current indentation level represented by parenthesis depth.</param>
    /// <param name="clause">The active clause context.</param>
    /// <param name="firstItem">Indicates whether the next clause item is the first one.</param>
    /// <param name="pendingComma">Indicates whether a comma should be emitted at the beginning of the next line.</param>
    /// <param name="clauseIndent">Stores the base indentation of the active clause.</param>
    /// <param name="upper">Uppercase representation of the current token.</param>
    /// <param name="text">Original token text.</param>
    /// <returns><c>true</c> when the token has been fully handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleClauseStart(
        IReadOnlyList<SqlToken> tokens,
        SqlFormattingOptions options,
        bool commaAtLineStart,
        ref int index,
        ref FormattedLine? currentLine,
        List<FormattedLine> lines,
        ref int indentLevel,
        ref ClauseContext clause,
        ref bool firstItem,
        ref bool pendingComma,
        ref int clauseIndent,
        string upper,
        string text)
    {
        int baseIndent = indentLevel * options.IndentSize;

        switch (upper)
        {
            case "WITH":
            case "UNION":
            case "INTERSECT":
            case "EXCEPT":
                clause = ClauseContext.None;
                firstItem = false;
                pendingComma = false;
                StartNewLine(ref currentLine, lines, baseIndent);
                AppendToken(ref currentLine, lines, baseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "SELECT":
                clause = ClauseContext.SelectList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "GROUP":
                if (index + 1 < tokens.Count && tokens[index + 1].Normalized.Equals("BY", StringComparison.OrdinalIgnoreCase))
                {
                    clause = ClauseContext.GroupByList;
                    firstItem = true;
                    pendingComma = false;
                    clauseIndent = baseIndent;
                    StartNewLine(ref currentLine, lines, clauseIndent);
                    AppendToken(ref currentLine, lines, clauseIndent, text);
                    index++;
                    AppendToken(ref currentLine, lines, clauseIndent, tokens[index].Text);
                    CommitLine(ref currentLine, lines);
                    return true;
                }

                break;

            case "ORDER":
                if (index + 1 < tokens.Count && tokens[index + 1].Normalized.Equals("BY", StringComparison.OrdinalIgnoreCase))
                {
                    clause = ClauseContext.OrderByList;
                    firstItem = true;
                    pendingComma = false;
                    clauseIndent = baseIndent;
                    StartNewLine(ref currentLine, lines, clauseIndent);
                    AppendToken(ref currentLine, lines, clauseIndent, text);
                    index++;
                    AppendToken(ref currentLine, lines, clauseIndent, tokens[index].Text);
                    CommitLine(ref currentLine, lines);
                    return true;
                }

                break;

            case "VALUES":
                clause = ClauseContext.ValuesList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "RETURNING":
                clause = ClauseContext.ReturningList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "SET":
                clause = ClauseContext.SetList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "FROM":
            case "WHERE":
            case "HAVING":
            case "LIMIT":
            case "OFFSET":
            case "USING":
            case "INSERT":
            case "UPDATE":
            case "DELETE":
                clause = ClauseContext.None;
                firstItem = false;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                return true;
        }

        if (JoinLeadingModifiers.Contains(upper))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            StartNewLine(ref currentLine, lines, clauseIndent);
            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        if (upper.Equals("OUTER", StringComparison.OrdinalIgnoreCase))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            EnsureLine(ref currentLine, lines, clauseIndent);
            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        if (upper.Equals("JOIN", StringComparison.OrdinalIgnoreCase))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            if (currentLine == null || currentLine.Tokens.Count == 0)
            {
                StartNewLine(ref currentLine, lines, clauseIndent);
            }
            else
            {
                string lastToken = currentLine.Tokens[^1].ToUpperInvariant();
                if (!JoinLeadingModifiers.Contains(lastToken) && lastToken != "OUTER")
                {
                    StartNewLine(ref currentLine, lines, clauseIndent);
                }
            }

            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures a line is available for clause content and emits comma prefixes when required.
    /// </summary>
    /// <param name="options">Formatting options controlling indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="clause">The active clause context.</param>
    /// <param name="firstItem">Indicates whether the next clause item is the first one.</param>
    /// <param name="pendingComma">Indicates whether a comma should be emitted before the next token.</param>
    /// <param name="clauseIndent">Stores the base indentation of the active clause.</param>
    private static void PrepareClauseLine(
        SqlFormattingOptions options,
        bool commaAtLineStart,
        ref FormattedLine? currentLine,
        List<FormattedLine> lines,
        ref ClauseContext clause,
        ref bool firstItem,
        ref bool pendingComma,
        int clauseIndent)
    {
        if (clause == ClauseContext.None)
        {
            return;
        }

        if (firstItem)
        {
            StartNewLine(ref currentLine, lines, clauseIndent + options.IndentSize);
            firstItem = false;
            return;
        }

        if (commaAtLineStart && pendingComma)
        {
            int indent = clauseIndent + Math.Max(options.IndentSize - 1, 0);
            StartNewLine(ref currentLine, lines, indent);
            AppendToken(ref currentLine, lines, indent, ",");
            if (currentLine != null)
            {
                currentLine.SuppressSpaceAfterLeadingComma = true;
            }
            pendingComma = false;
            return;
        }

        if (currentLine == null)
        {
            StartNewLine(ref currentLine, lines, clauseIndent + options.IndentSize);
        }
    }

    /// <summary>
    /// Determines whether the contents enclosed by a parenthesis warrant multi-line formatting.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="startIndex">Index immediately after the opening parenthesis.</param>
    /// <returns><c>true</c> when the content includes clause keywords; otherwise <c>false</c>.</returns>
    private static bool ShouldExpandParenthesis(IReadOnlyList<SqlToken> tokens, int startIndex)
    {
        int depth = 1;
        for (int i = startIndex; i < tokens.Count; i++)
        {
            string text = tokens[i].Text;
            if (text == "(")
            {
                depth++;
                continue;
            }

            if (text == ")")
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if (depth == 1 && ClauseKeywords.Contains(tokens[i].Normalized))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends a token to the current line, creating a new one when necessary.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    /// <param name="token">Token to append.</param>
    private static void AppendToken(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces, string token)
    {
        EnsureLine(ref currentLine, lines, indentSpaces);
        currentLine!.Tokens.Add(token);
    }

    /// <summary>
    /// Starts a new line with the specified indentation.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    private static void StartNewLine(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces)
    {
        CommitLine(ref currentLine, lines);
        currentLine = new FormattedLine(indentSpaces);
    }

    /// <summary>
    /// Ensures that a line exists with the specified indentation before appending tokens.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    private static void EnsureLine(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces)
    {
        if (currentLine == null)
        {
            currentLine = new FormattedLine(indentSpaces);
            return;
        }

        if (currentLine.Tokens.Count == 0 && currentLine.IndentSpaces != indentSpaces)
        {
            currentLine = new FormattedLine(indentSpaces);
            return;
        }

        if (currentLine.IndentSpaces != indentSpaces)
        {
            StartNewLine(ref currentLine, lines, indentSpaces);
        }
    }

    /// <summary>
    /// Commits the current line to the output collection when it contains tokens.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    private static void CommitLine(ref FormattedLine? currentLine, List<FormattedLine> lines)
    {
        if (currentLine != null && currentLine.Tokens.Count > 0)
        {
            lines.Add(currentLine);
        }

        currentLine = null;
    }

    /// <summary>
    /// Builds the formatted SQL text from the prepared lines.
    /// </summary>
    /// <param name="lines">Lines composing the final output.</param>
    /// <returns>The formatted SQL text.</returns>
    private static string BuildFormattedText(List<FormattedLine> lines)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            FormattedLine line = lines[i];
            builder.Append(' ', line.IndentSpaces);
            string text = SqlStringFormatter.JoinTokens(line.Tokens);
            if (line.SuppressSpaceAfterLeadingComma && text.StartsWith(", ", StringComparison.Ordinal))
            {
                text = "," + text.Substring(2);
            }

            builder.Append(text);
            if (i < lines.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
