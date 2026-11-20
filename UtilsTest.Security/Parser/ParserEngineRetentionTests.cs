using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

namespace UtilsTest.Security.Parser;

/// <summary>
/// Engine-reuse audit: a long-lived <see cref="CompiledGrammar"/> (a generated <c>Grammar</c> property, the CSyntax and
/// VBSyntax shared grammars) or a reused <see cref="ParserEngine"/> must not retain the last input text, nor the
/// capacity that one large input made the per-parse collections grow to. Before the fix, a grammar kept its last input
/// alive and about 600 bytes per token of emptied collection capacity until its next call, and a reused parser engine
/// kept the capacity of its largest parse for as long as it lived.
/// </summary>
[TestClass]
public class ParserEngineRetentionTests
{
    /// <summary>A flat grammar whose parse retains several states per token.</summary>
    private const string Grammar = """
        grammar R;
        start : item* ;
        item : A | B ;
        A : 'a' ;
        B : 'b' ;
        WS : ' ' -> skip ;
        """;

    /// <summary>The per-parse collections of a <see cref="ParserEngine"/>, as paths of private fields.</summary>
    private static readonly string[][] ParserCollections =
    [
        ["_activeRuleFrames"],
        ["_stateRegistry", "_visitedStates"],
        ["_stateRegistry", "_continuations"],
        ["_stateRegistry", "_completedResults"],
        ["_lookaheadCache", "_results"],
    ];

    /// <summary>The input text is unreachable from the grammar once <see cref="CompiledGrammar.Parse"/> returns.</summary>
    [TestMethod]
    public void CompiledGrammar_Parse_DoesNotRetainTheInput()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        WeakReference input = Run(text => grammar.Parse(text));
        Collect();
        Assert.IsFalse(input.IsAlive, "the last parsed input must not be retained by the grammar");
        GC.KeepAlive(grammar);
    }

    /// <summary>The input text is unreachable from the grammar once <see cref="CompiledGrammar.Tokenize"/> returns.</summary>
    [TestMethod]
    public void CompiledGrammar_Tokenize_DoesNotRetainTheInput()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        WeakReference input = Run(text => grammar.Tokenize(text));
        Collect();
        Assert.IsFalse(input.IsAlive, "the last tokenized input must not be retained by the grammar");
        GC.KeepAlive(grammar);
    }

    /// <summary>
    /// A lexer engine releases its input when an enumeration is disposed before its end, not only when it completes.
    /// </summary>
    [TestMethod]
    public void LexerEngine_AbandonedEnumeration_DoesNotRetainTheInput()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var lexer = new LexerEngine(grammar.Definition);
        WeakReference input = Run(text =>
        {
            using IEnumerator<Token> tokens = lexer.Tokenize(new StringReader(text)).GetEnumerator();
            Assert.IsTrue(tokens.MoveNext());
        });
        Collect();
        Assert.IsFalse(input.IsAlive, "an abandoned enumeration must not leave its input reachable from the engine");
        GC.KeepAlive(lexer);
    }

    /// <summary>
    /// After a parse that retained far more states than <see cref="ParserStateRetention.MaxRetainedEntries"/>, the
    /// emptied per-parse collections of the reused engine are shrunk, so the engine no longer holds memory proportional
    /// to its largest input.
    /// </summary>
    [TestMethod]
    public void ReusedParserEngine_ReleasesCapacityAfterALargeParse()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var engine = new ParserEngine(grammar.Definition);
        engine.Parse(grammar.Tokenize(string.Join(" ", Enumerable.Repeat("a b", 20_000))));

        Assert.IsTrue(engine.LastResourceUsage!.RetainedStates > 10 * ParserStateRetention.MaxRetainedEntries, "the parse is large enough to grow the collections");
        foreach (string[] path in ParserCollections)
        {
            int capacity = Capacity(Read(engine, path));
            Assert.IsTrue(capacity <= 4 * ParserStateRetention.MaxRetainedEntries, $"{string.Join(".", path)} keeps a capacity of {capacity}");
        }
    }

    /// <summary>
    /// Ordinary parses below the threshold keep the capacity of their collections, so sequential reuse does not shrink
    /// and regrow them on every parse.
    /// </summary>
    [TestMethod]
    public void ReusedParserEngine_KeepsCapacityOfSmallParses()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var engine = new ParserEngine(grammar.Definition);
        IReadOnlyList<Token> tokens = grammar.Tokenize("a b a b a b");
        engine.Parse(tokens);
        Assert.IsTrue(engine.LastResourceUsage!.RetainedStates <= ParserStateRetention.MaxRetainedEntries);
        int[] first = ParserCollections.Select(path => Capacity(Read(engine, path))).ToArray();
        engine.Parse(tokens);
        int[] second = ParserCollections.Select(path => Capacity(Read(engine, path))).ToArray();

        CollectionAssert.AreEqual(first, second);
        Assert.IsTrue(first.Any(capacity => capacity > 0), "a small parse leaves reusable capacity");
    }

    /// <summary>Runs an action on a fresh input string and returns a weak reference to that string.</summary>
    /// <param name="action">The operation on the input.</param>
    /// <returns>A weak reference to the input.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Run(Action<string> action)
    {
        string input = string.Join(" ", Enumerable.Repeat("a b", 5_000));
        action(input);
        return new WeakReference(input);
    }

    /// <summary>Runs two full blocking collections around the pending finalizers.</summary>
    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>Reads a chain of private instance fields.</summary>
    /// <param name="instance">The first owner.</param>
    /// <param name="path">The field names.</param>
    /// <returns>The value of the last field.</returns>
    private static object Read(object instance, string[] path)
    {
        object current = instance;
        foreach (string name in path)
        {
            FieldInfo field = current.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(current.GetType().Name, name);
            current = field.GetValue(current)!;
        }

        return current;
    }

    /// <summary>Gets the capacity of a <see cref="HashSet{T}"/> or a <see cref="Dictionary{TKey, TValue}"/> without changing it.</summary>
    /// <param name="collection">The collection.</param>
    /// <returns>Its current capacity.</returns>
    private static int Capacity(object collection)
    {
        MethodInfo ensure = collection.GetType().GetMethod("EnsureCapacity", [typeof(int)])
            ?? throw new MissingMethodException(collection.GetType().Name, "EnsureCapacity");
        return (int)ensure.Invoke(collection, [0])!;
    }
}
