
namespace Utils.Expressions;

public interface IParserOptions
{
	/// <summary>
	/// Gets a read-only dictionary mapping common type keywords (e.g., "int", "string")
	/// to their corresponding <see cref="System.Type"/> instances.
	/// </summary>
	IReadOnlyDictionary<string, Type> DefaultTypes { get; }

	/// <summary>
	/// Gets a read-only dictionary mapping numeric suffixes (e.g., "l", "m") to delegates
	/// that parse a string into the corresponding numeric type.
	/// </summary>
	IReadOnlyDictionary<string, Func<string, object>> NumberSuffixes { get; }

	/// <summary>
	/// Gets a read-only dictionary mapping <see cref="System.Type"/> objects to an integer
	/// level, used for upgrading (or comparing) numeric types during parsing.
	/// </summary>
	IReadOnlyDictionary<Type, int> NumberTypeLevel { get; }

	/// <summary>
	/// Gets a read-only dictionary mapping operator symbols (e.g., "+", "(", "??") to their
	/// priority levels, which the parser uses to determine evaluation order.
	/// </summary>
	IReadOnlyDictionary<string, int> OperatorPriorityLevel { get; }
}