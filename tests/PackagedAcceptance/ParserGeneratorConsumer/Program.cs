using Packaged.Generated;

var definition = PackagedGrammar.Build();
var tree = PackagedGrammar.Parse("package");
if (definition.ParserRules.Count == 0 || tree is null)
{
    throw new InvalidOperationException("The packaged analyzer did not generate an executable facade.");
}
Console.WriteLine("Packaged generator consumer passed.");
