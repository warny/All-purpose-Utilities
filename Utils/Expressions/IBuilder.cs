namespace Utils.Expressions;

/// <summary>
/// Describes the configuration and components required to parse
/// and build expressions (e.g., token readers, whitespace handling, etc.).
/// </summary>
public interface IBuilder
{
	/// <summary>
	/// Gets the character used to separate instructions or statements.
	/// </summary>
	char InstructionSeparator { get; }

	/// <summary>
	/// Gets the array of characters recognized as whitespace.
	/// </summary>
	char[] SpaceSymbols { get; }

	/// <summary>
	/// Gets the character used to separate items in lists (e.g., comma).
	/// </summary>
	char ListSeparator { get; }

	/// <summary>
	/// Gets a collection of functions that attempt to read tokens
	/// from raw content at a given index.
	/// </summary>
	IEnumerable<TryReadToken> TokenReaders { get; }

	/// <summary>
	/// Gets a collection of transformers that can modify or rewrite tokens
	/// before they are processed (e.g., for escaping or case transformation).
	/// </summary>
	IEnumerable<StringTransformer> StringTransformers { get; }

	/// <summary>
	/// Gets an array of any additional symbols recognized by this parser.
	/// </summary>
	string[] AdditionalSymbols { get; }

	/// <summary>
	/// Gets the expression builder for numeric values.
	/// </summary>
	IStartExpressionBuilder NumberBuilder { get; }

	/// <summary>
	/// Gets a read-only dictionary mapping integer prefixes
	/// (e.g., '0x' for hexadecimal) to base or priority level.
	/// </summary>
	IReadOnlyDictionary<string, int> IntegerPrefixes { get; }

	/// <summary>
	/// Gets a read-only dictionary of expression builders that handle
	/// specific start tokens (e.g., '(' for subexpressions, '{' for blocks).
	/// </summary>
	IReadOnlyDictionary<string, IStartExpressionBuilder> StartExpressionBuilders { get; }

	/// <summary>
	/// Gets the builder used when no other builder matches,
	/// typically handling unary operators or fallback cases.
	/// </summary>
	IStartExpressionBuilder FallbackUnaryBuilder { get; }

	/// <summary>
	/// Gets a read-only dictionary of expression builders for follow-up operations,
	/// e.g. handling binary or ternary operators after an initial expression is parsed.
	/// </summary>
	IReadOnlyDictionary<string, IFollowUpExpressionBuilder> FollowUpExpressionBuilder { get; }

	/// <summary>
	/// Gets the expression builder to use when no other follow-up builder matches,
	/// typically handling unrecognized operators or fallback logic.
	/// </summary>
	IFollowUpExpressionBuilder FallbackBinaryOrTernaryBuilder { get; }

	/// <summary>
	/// Gets a collection of token strings (e.g., operators or symbols)
	/// recognized by this builder's parsing logic.
	/// </summary>
	IEnumerable<string> Symbols { get; }
}

/// <summary>
/// Represents a function that attempts to read a token from a given content string at the specified index.
/// If successful, it outputs the token's length.
/// </summary>
/// <param name="content">
/// The raw content string from which tokens are read.
/// </param>
/// <param name="index">The current position within the string.</param>
/// <param name="length">
/// When this method returns, contains the length of the token read, if the read succeeded.
/// Otherwise, 0 or an undefined value.
/// </param>
/// <returns><see langword="true"/> if a token was successfully read; otherwise <see langword="false"/>.</returns>
public delegate bool TryReadToken(string content, int index, out int length);

/// <summary>
/// Represents a function that attempts to transform or rewrite a token string.
/// If successful, outputs a new token result.
/// </summary>
/// <param name="token">The original token string.</param>
/// <param name="result">
/// When this method returns, contains the transformed token string, if the transform succeeded.
/// Otherwise, the parameter is ignored.
/// </param>
/// <returns><see langword="true"/> if the token was successfully transformed; otherwise <see langword="false"/>.</returns>
public delegate bool StringTransformer(string token, out string result);
