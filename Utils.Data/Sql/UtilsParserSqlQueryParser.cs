using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Utils.Parser.Runtime;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses SQL statements through the <c>Utils.Parser</c> runtime using the generated ANTLR grammar.
/// </summary>
internal sealed class UtilsParserSqlQueryParser
{
    private readonly SqlSyntaxOptions syntaxOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UtilsParserSqlQueryParser"/> class.
    /// </summary>
    /// <param name="syntaxOptions">Syntax options applied to the produced SQL model.</param>
    public UtilsParserSqlQueryParser(SqlSyntaxOptions syntaxOptions)
    {
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
    }

    /// <summary>
    /// Parses the specified SQL text into a <see cref="SqlStatement"/> tree.
    /// </summary>
    /// <param name="sql">SQL text to parse.</param>
    /// <returns>The parsed statement tree.</returns>
    public SqlStatement Parse([StringSyntax(SqlQueryGrammar.StringSyntaxName)] string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var tokens = SqlQueryGrammar.Tokenize(sql).ToList();
        var root = SqlQueryGrammar.Parse(sql);
        if (root is ErrorNode error)
        {
            throw new SqlParseException(error.Message);
        }

        return ParseSqlQuery((ParserNode)root, tokens);
    }

    /// <summary>
    /// Parses a tokenized SQL statement.
    /// </summary>
    /// <param name="tokens">Tokens representing a SQL statement.</param>
    /// <returns>The parsed statement.</returns>
    public SqlStatement ParseTokens(IReadOnlyList<SqlToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        string sql = SqlStringFormatter.JoinTokens(tokens.Select(token => token.Text).ToList());
        return Parse(sql);
    }

    /// <summary>
    /// Converts the grammar root node into the first SQL statement.
    /// </summary>
    private SqlStatement ParseSqlQuery(ParserNode sqlQueryNode, IReadOnlyList<Token> tokens)
    {
        var statementNode = GetRequiredChild(sqlQueryNode, "statement");
        return ParseStatement(statementNode, tokens);
    }

    /// <summary>
    /// Converts a statement node, including an optional CTE clause.
    /// </summary>
    private SqlStatement ParseStatement(ParserNode statementNode, IReadOnlyList<Token> tokens)
    {
        WithClause? withClause = null;
        var withClauseNode = GetOptionalChild(statementNode, "withClause");
        if (withClauseNode != null)
        {
            withClause = ParseWithClause(withClauseNode, tokens);
        }

        var coreStatementNode = GetRequiredChild(statementNode, "coreStatement");
        return ParseCoreStatement(coreStatementNode, tokens, withClause);
    }

    /// <summary>
    /// Converts a core statement node into a concrete statement type.
    /// </summary>
    private SqlStatement ParseCoreStatement(ParserNode coreStatementNode, IReadOnlyList<Token> tokens, WithClause? withClause)
    {
        if (TryGetOptionalChild(coreStatementNode, "selectStatement", out var selectStatementNode))
        {
            return ParseSelectStatement(selectStatementNode, tokens, withClause);
        }

        if (TryGetOptionalChild(coreStatementNode, "insertStatement", out var insertStatementNode))
        {
            return ParseInsertStatement(insertStatementNode, tokens, withClause);
        }

        if (TryGetOptionalChild(coreStatementNode, "updateStatement", out var updateStatementNode))
        {
            return ParseUpdateStatement(updateStatementNode, tokens, withClause);
        }

        if (TryGetOptionalChild(coreStatementNode, "deleteStatement", out var deleteStatementNode))
        {
            return ParseDeleteStatement(deleteStatementNode, tokens, withClause);
        }

        throw new SqlParseException("Unsupported SQL statement.");
    }

    /// <summary>
    /// Converts a WITH clause node into the domain model.
    /// </summary>
    private WithClause ParseWithClause(ParserNode withClauseNode, IReadOnlyList<Token> tokens)
    {
        bool isRecursive = withClauseNode.Children.OfType<LexerNode>().Any(node => node.Rule.Name == "RECURSIVE");
        var definitions = GetChildren(withClauseNode, "cteDefinition")
            .Select(node => ParseCteDefinition(node, tokens))
            .ToList();
        return new WithClause(isRecursive, definitions);
    }

    /// <summary>
    /// Converts a CTE definition node into a named subquery definition.
    /// </summary>
    private CteDefinition ParseCteDefinition(ParserNode cteNode, IReadOnlyList<Token> tokens)
    {
        var identifierNode = GetRequiredChild(cteNode, "identifier");
        string name = ReadNodeSql(identifierNode, tokens);

        IReadOnlyList<string>? columns = null;
        var columnListNode = GetOptionalChild(cteNode, "columnList");
        if (columnListNode != null)
        {
            columns = GetChildren(columnListNode, "identifier")
                .Select(node => ReadNodeSql(node, tokens))
                .ToList();
        }

        var statementNode = GetRequiredChild(cteNode, "statement");
        var statement = ParseStatement(statementNode, tokens);
        return new CteDefinition(name, columns, statement);
    }

    /// <summary>
    /// Converts a SELECT statement node.
    /// </summary>
    private SqlSelectStatement ParseSelectStatement(ParserNode selectNode, IReadOnlyList<Token> tokens, WithClause? withClause)
    {
        var selectSegment = BuildRequiredSegment(selectNode, tokens, "selectElements", "Select");
        var fromSegment = BuildOptionalSegment(selectNode, tokens, "fromClause", "fromElements", "From");
        var whereSegment = BuildOptionalSegment(selectNode, tokens, "whereClause", "predicateElements", "Where");
        var groupBySegment = BuildOptionalSegment(selectNode, tokens, "groupByClause", "clauseElements", "GroupBy");
        var havingSegment = BuildOptionalSegment(selectNode, tokens, "havingClause", "predicateElements", "Having");
        var orderBySegment = BuildOptionalSegment(selectNode, tokens, "orderByClause", "clauseElements", "OrderBy");
        var limitSegment = BuildOptionalSegment(selectNode, tokens, "limitClause", "clauseElements", "Limit");
        var offsetSegment = BuildOptionalSegment(selectNode, tokens, "offsetClause", "clauseElements", "Offset");
        var tailSegment = BuildSetOperatorSegment(selectNode, tokens);
        bool isDistinct = selectNode.Children.OfType<LexerNode>().Any(node => node.Rule.Name == "DISTINCT");

        return new SqlSelectStatement(
            selectSegment,
            fromSegment,
            whereSegment,
            groupBySegment,
            havingSegment,
            orderBySegment,
            limitSegment,
            offsetSegment,
            tailSegment,
            withClause,
            isDistinct);
    }

    /// <summary>
    /// Converts an INSERT statement node.
    /// </summary>
    private SqlInsertStatement ParseInsertStatement(ParserNode insertNode, IReadOnlyList<Token> tokens, WithClause? withClause)
    {
        var target = BuildRequiredSegment(insertNode, tokens, "targetSegment", "Target");
        var output = BuildOptionalSegment(insertNode, tokens, "outputClause", "clauseElements", "Output");
        var values = BuildOptionalSegment(insertNode, tokens, "valuesClause", "valuesElements", "Values");
        var returning = BuildOptionalSegment(insertNode, tokens, "returningClause", "clauseElements", "Returning");
        SqlStatement? sourceQuery = null;

        if (values == null && TryGetOptionalChild(insertNode, "selectStatement", out var selectNode))
        {
            sourceQuery = ParseSelectStatement(selectNode, tokens, null);
        }

        return new SqlInsertStatement(target, values, sourceQuery, output, returning, withClause);
    }

    /// <summary>
    /// Converts an UPDATE statement node.
    /// </summary>
    private SqlUpdateStatement ParseUpdateStatement(ParserNode updateNode, IReadOnlyList<Token> tokens, WithClause? withClause)
    {
        var target = BuildRequiredSegment(updateNode, tokens, "targetSegment", "Target");
        var set = BuildRequiredSegment(updateNode, tokens, "setClause", "setElements", "Set");
        var from = BuildOptionalSegment(updateNode, tokens, "fromClause", "fromElements", "From");
        var where = BuildOptionalSegment(updateNode, tokens, "whereClause", "predicateElements", "Where");
        var output = BuildOptionalSegment(updateNode, tokens, "outputClause", "clauseElements", "Output");
        var returning = BuildOptionalSegment(updateNode, tokens, "returningClause", "clauseElements", "Returning");
        return new SqlUpdateStatement(target, set, from, where, output, returning, withClause);
    }

    /// <summary>
    /// Converts a DELETE statement node.
    /// </summary>
    private SqlDeleteStatement ParseDeleteStatement(ParserNode deleteNode, IReadOnlyList<Token> tokens, WithClause? withClause)
    {
        var target = BuildOptionalSegment(deleteNode, tokens, "deleteTarget", "targetSegment", "Target");
        var from = BuildRequiredSegment(deleteNode, tokens, "fromElements", "From");
        var usingSegment = BuildOptionalSegment(deleteNode, tokens, "usingClause", "fromElements", "Using");
        var where = BuildOptionalSegment(deleteNode, tokens, "whereClause", "predicateElements", "Where");
        var output = BuildOptionalSegment(deleteNode, tokens, "outputClause", "clauseElements", "Output");
        var returning = BuildOptionalSegment(deleteNode, tokens, "returningClause", "clauseElements", "Returning");
        return new SqlDeleteStatement(target, from, usingSegment, where, output, returning, withClause);
    }

    /// <summary>
    /// Builds the trailing set-operator segment when present.
    /// </summary>
    private SqlSegment? BuildSetOperatorSegment(ParserNode selectNode, IReadOnlyList<Token> tokens)
    {
        var setOperatorClause = GetOptionalChild(selectNode, "setOperatorClause");
        if (setOperatorClause == null)
        {
            return null;
        }

        return BuildSegment("Tail", setOperatorClause, tokens);
    }

    /// <summary>
    /// Builds a required segment from the specified direct child node.
    /// </summary>
    private SqlSegment BuildRequiredSegment(ParserNode parentNode, IReadOnlyList<Token> tokens, string childRuleName, string segmentName)
    {
        return BuildSegment(segmentName, GetRequiredChild(parentNode, childRuleName), tokens);
    }

    /// <summary>
    /// Builds a required segment from a nested child node.
    /// </summary>
    private SqlSegment BuildRequiredSegment(ParserNode parentNode, IReadOnlyList<Token> tokens, string clauseRuleName, string childRuleName, string segmentName)
    {
        var clauseNode = GetRequiredChild(parentNode, clauseRuleName);
        return BuildSegment(segmentName, GetRequiredChild(clauseNode, childRuleName), tokens);
    }

    /// <summary>
    /// Builds an optional segment from a nested child node.
    /// </summary>
    private SqlSegment? BuildOptionalSegment(ParserNode parentNode, IReadOnlyList<Token> tokens, string clauseRuleName, string childRuleName, string segmentName)
    {
        var clauseNode = GetOptionalChild(parentNode, clauseRuleName);
        if (clauseNode == null)
        {
            return null;
        }

        return BuildSegment(segmentName, GetRequiredChild(clauseNode, childRuleName), tokens);
    }

    /// <summary>
    /// Builds an optional segment from a nested child node under an optional wrapper.
    /// </summary>
    private SqlSegment? BuildOptionalSegment(ParserNode parentNode, IReadOnlyList<Token> tokens, string wrapperRuleName, string clauseRuleName, string childRuleName, string segmentName)
    {
        var wrapperNode = GetOptionalChild(parentNode, wrapperRuleName);
        if (wrapperNode == null)
        {
            return null;
        }

        return BuildSegment(segmentName, GetRequiredChild(GetRequiredChild(wrapperNode, clauseRuleName), childRuleName), tokens);
    }

    /// <summary>
    /// Builds a segment by converting runtime tokens into the existing SQL segment model.
    /// </summary>
    private SqlSegment BuildSegment(string segmentName, ParseNode node, IReadOnlyList<Token> tokens)
    {
        var sqlTokens = tokens
            .Where(token => token.Span.Position >= node.Span.Position && token.Span.Position < node.Span.Position + node.Span.Length)
            .Select(ToSqlToken)
            .ToList();
        return SqlParsingInfrastructure.BuildSegment(segmentName, sqlTokens, syntaxOptions);
    }

    /// <summary>
    /// Reads a node back into its canonical SQL text.
    /// </summary>
    private string ReadNodeSql(ParseNode node, IReadOnlyList<Token> tokens)
    {
        return BuildSegment("Temp", node, tokens).ToSql();
    }

    /// <summary>
    /// Converts a <see cref="Utils.Parser.Runtime.Token"/> into the legacy SQL token model.
    /// </summary>
    private static SqlToken ToSqlToken(Token token)
    {
        bool isIdentifier = token.RuleName is "IDENTIFIER" or "QUOTED_IDENTIFIER" or "BRACKET_IDENTIFIER";
        bool isKeyword = token.RuleName.All(char.IsUpper) && token.RuleName != "PARAMETER" && token.RuleName != "NUMBER" && token.RuleName != "STRING";
        string normalized = isKeyword ? token.Text.ToUpperInvariant() : token.Text;
        return new SqlToken(token.Text, normalized, isIdentifier, isKeyword);
    }

    /// <summary>
    /// Gets a required parser child by rule name.
    /// </summary>
    private static ParserNode GetRequiredChild(ParserNode node, string ruleName)
    {
        return GetOptionalChild(node, ruleName) ?? throw new SqlParseException($"Missing '{ruleName}' clause in SQL parse tree.");
    }

    /// <summary>
    /// Gets an optional parser child by rule name.
    /// </summary>
    private static ParserNode? GetOptionalChild(ParserNode node, string ruleName)
    {
        return node.Children.OfType<ParserNode>().FirstOrDefault(child => child.Rule.Name == ruleName);
    }

    /// <summary>
    /// Attempts to get an optional parser child by rule name.
    /// </summary>
    private static bool TryGetOptionalChild(ParserNode node, string ruleName, out ParserNode child)
    {
        child = GetOptionalChild(node, ruleName)!;
        return child != null;
    }

    /// <summary>
    /// Gets all direct parser children that match the specified rule name.
    /// </summary>
    private static IEnumerable<ParserNode> GetChildren(ParserNode node, string ruleName)
    {
        return node.Children.OfType<ParserNode>().Where(child => child.Rule.Name == ruleName);
    }
}
