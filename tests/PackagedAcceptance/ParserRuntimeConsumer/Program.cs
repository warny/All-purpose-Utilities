using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

const string grammar = """
grammar Arithmetic;
root : A PLUS A;
A : 'a';
PLUS : '+';
""";
var definition = Antlr4GrammarConverter.Parse(grammar);
var compiled = new CompiledGrammar(definition);
var tree = compiled.Parse("a+a");
if (tree is null || compiled.Tokenize("a+a").Count == 0)
{
    throw new InvalidOperationException("Packaged parser did not execute the grammar.");
}
Console.WriteLine("omy.Utils.Parser packaged consumer passed.");
