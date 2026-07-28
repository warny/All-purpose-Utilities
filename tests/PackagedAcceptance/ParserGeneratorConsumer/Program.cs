using Packaged.Generated;

namespace PackagedAcceptance.ParserGenerator;

/// <summary>Exercises the package-hosted generator's effective imported grammar facade.</summary>
internal static class Program
{
    /// <summary>Builds and executes the generated root facade, then checks expected effective rules.</summary>
    private static void Main(string[] args)
    {
        string input = args.Length > 0 ? args[0] : "a";
        string expectedRule = args.Length > 1 ? args[1] : "importedLeaf";
        string? absentRule = args.Length > 2 ? args[2] : null;
        bool expectEmptyMode = args.Length <= 3 || bool.Parse(args[3]);
        var definition = RootGrammar.Build();
        if (!definition.AllRules.ContainsKey("middleRule") || !definition.AllRules.ContainsKey(expectedRule))
        {
            throw new InvalidOperationException($"The generated effective composition is missing '{expectedRule}'.");
        }
        if (absentRule is not null && definition.AllRules.ContainsKey(absentRule))
        {
            throw new InvalidOperationException($"Stale imported rule '{absentRule}' remains after rebuild.");
        }
        bool hasEmptyMode = definition.Modes.Any(mode => mode.Name == "EMPTY" && mode.Rules.Count == 0);
        if (hasEmptyMode != expectEmptyMode)
        {
            throw new InvalidOperationException($"Empty lexer mode expectation was '{expectEmptyMode}', actual '{hasEmptyMode}'.");
        }
        if (definition.ParserRules.Count(rule => rule.Name == "middleRule") != 1)
        {
            throw new InvalidOperationException("Imported collision did not resolve to exactly one effective rule.");
        }
        if (RootGrammar.Parse(input) is null)
        {
            throw new InvalidOperationException("The generated imported grammar did not execute.");
        }
        Console.WriteLine($"Packaged generator composition passed with '{expectedRule}'.");
    }
}
