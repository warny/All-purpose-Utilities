using System.Linq.Expressions;

namespace Utils.Expressions.ExpressionBuilders
{
	/// <summary>
	/// Provides an implementation of both <see cref="IStartExpressionBuilder"/> and
	/// <see cref="IFollowUpExpressionBuilder"/> that throws a <see cref="ParseUnknownException"/>
	/// whenever an expression is to be built, indicating that the token or symbol cannot be parsed.
	/// </summary>
	public class ThrowParseException : IStartExpressionBuilder, IFollowUpExpressionBuilder
	{
		/// <inheritdoc/>
		public Expression Build(
			ExpressionParserCore parser,
			ParserContext context,
			string val,
			int priorityLevel,
			Parenthesis markers,
			ref bool isClosedWrap)
		{
			// Always throw, signaling an unrecognized or invalid token for starting an expression.
			throw new ParseUnknownException(".", context.Tokenizer.Position.Index);
		}

		/// <inheritdoc/>
		public Expression Build(
			ExpressionParserCore parser,
			ParserContext context,
			Expression currentExpression,
			string val,
			string nextVal,
			int priorityLevel,
			ref int nextLevel,
			Parenthesis markers,
			ref bool isClosedWrap)
		{
			// Always throw, signaling an unrecognized or invalid token encountered in follow-up operation.
			throw new ParseUnknownException(nextVal, context.Tokenizer.Position.Index);
		}
	}
}
