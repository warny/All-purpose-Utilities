using Utils.Parser.Bootstrap;
using Utils.Parser.Expressions;

var definition = Antlr4GrammarConverter.Parse("grammar P; root: 'ok';");
var options = new PreparedExpressionRuntimePolicyBuilderOptions { GrammarName = definition.Name };
if (options.GrammarName != "P" || typeof(ExpressionSemanticPredicateEvaluator).Assembly.GetName().Name != "Utils.Parser.Expressions")
{
    throw new InvalidOperationException("The packaged expressions adapter is unavailable.");
}
Console.WriteLine("Parser.Expressions packaged consumer passed.");
