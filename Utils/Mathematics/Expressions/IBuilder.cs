using System.Collections.Generic;
using System.Globalization;
using static Utils.Expressions.ExpressionBuilders.MemberBuilder;

namespace Utils.Expressions;

public interface IBuilder
{
    char InstructionSeparator { get; }
    char[] SpaceSymbols { get; }
    char ListSeparator { get; }
    IEnumerable<TryReadToken> TokenReaders { get; }
    IEnumerable<StringTransformer> StringTransformers { get; }
    string[] AdditionalSymbols { get; }
    IStartExpressionBuilder NumberBuilder { get; }
    public IReadOnlyDictionary<string, int> IntegerPrefixes { get; }
    IReadOnlyDictionary<string, IStartExpressionBuilder> StartExpressionBuilders { get; }
    IStartExpressionBuilder FallbackUnaryBuilder { get; }
    IReadOnlyDictionary<string, IFollowUpExpressionBuilder> FollowUpExpressionBuilder { get; }
    IFollowUpExpressionBuilder FallbackBinaryOrTernaryBuilder { get; }
    IEnumerable<string> Symbols { get; }
}

public delegate bool TryReadToken(string content, int index, out int length);
public delegate bool StringTransformer(string token, out string result);
