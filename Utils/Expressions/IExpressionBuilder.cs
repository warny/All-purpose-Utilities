using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Defines a contract for building the initial portion of an expression,
/// typically when the parser encounters a token indicating a new expression start.
/// </summary>
public interface IStartExpressionBuilder
{
	/// <summary>
	/// Constructs an expression from the given parser context and token data.
	/// This is usually invoked when a token signals the beginning of a new expression.
	/// </summary>
	/// <param name="parser">An <see cref="ExpressionParserCore"/> that orchestrates parsing.</param>
	/// <param name="context">The current <see cref="ParserContext"/>, maintaining tokens, parameters, etc.</param>
	/// <param name="val">The token value that triggered this builder.</param>
	/// <param name="priorityLevel">The current parsing priority level.</param>
	/// <param name="markers">
	/// A <see cref="Parenthesis"/> instance representing any grouping symbols (e.g., '(' or '{').
	/// </param>
	/// <param name="isClosedWrap">
	/// A reference boolean indicating whether the builder encountered (and closed) a parenthesis or bracket marker.
	/// </param>
	/// <returns>An <see cref="Expression"/> representing the constructed portion of the parse tree.</returns>
	Expression Build(
		ExpressionParserCore parser,
		ParserContext context,
		string val,
		int priorityLevel,
		Parenthesis markers,
		ref bool isClosedWrap);
}

/// <summary>
/// Defines a contract for building follow-up portions of an expression,
/// typically when the parser encounters an operator or token after an initial expression.
/// </summary>
public interface IFollowUpExpressionBuilder
{
	/// <summary>
	/// Constructs an expression, given the previously parsed expression and a new operator/token.
	/// Often used for appending binary or ternary operators to the expression tree.
	/// </summary>
	/// <param name="parser">An <see cref="ExpressionParserCore"/> orchestrating the overall parsing.</param>
	/// <param name="context">The current <see cref="ParserContext"/> state.</param>
	/// <param name="currentExpression">The existing expression subtree built so far.</param>
	/// <param name="val">The token most recently read.</param>
	/// <param name="nextVal">The next token to process (e.g., an operator).</param>
	/// <param name="priorityLevel">The current parsing priority.</param>
	/// <param name="nextLevel">
	/// A reference integer indicating the next operator's priority level.
	/// It may be updated if a new operator has higher precedence.
	/// </param>
	/// <param name="markers">
	/// A <see cref="Parenthesis"/> instance for grouping symbols (if any).
	/// </param>
	/// <param name="isClosedWrap">
	/// A reference boolean indicating whether the parser has encountered and closed a grouping wrapper.
	/// </param>
	/// <returns>An <see cref="Expression"/> representing the extended parse tree.</returns>
	Expression Build(
		ExpressionParserCore parser,
		ParserContext context,
		Expression currentExpression,
		string val,
		string nextVal,
		int priorityLevel,
		ref int nextLevel,
		Parenthesis markers,
		ref bool isClosedWrap);
}

/// <summary>
/// Represents an entity that provides additional tokens beyond the standard set,
/// useful for customizing lexical analysis or parser behavior.
/// </summary>
public interface IAdditionalTokens
{
	/// <summary>
	/// Gets a collection of extra tokens recognized by this entity's parsing logic.
	/// </summary>
	IEnumerable<string> AdditionalTokens { get; }
}
