using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ScheduledAlternativeExecutorTests
{
    [TestMethod]
    public void Execute_UsesNegativeShortcutForRuleRootWhenDiagnosticsAreNull()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache);
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([]);
        var attemptCount = 0;

        _ = executor.Execute(
            context,
            rule,
            alternative,
            alternativeIndex: 0,
            startPosition: 0,
            precedence: 0,
            cursorKind: ScheduledAlternativeCursorKinds.RuleRoot,
            cursorIndex: -1,
            diagnostics: null,
            checkPrecedence: static _ => true,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        _ = executor.Execute(
            context,
            rule,
            alternative,
            alternativeIndex: 0,
            startPosition: 0,
            precedence: 0,
            cursorKind: ScheduledAlternativeCursorKinds.RuleRoot,
            cursorIndex: -1,
            diagnostics: null,
            checkPrecedence: static _ => true,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(1, attemptCount);
    }

    [TestMethod]
    public void Execute_DoesNotUseNegativeShortcutForNestedAlternation()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache);
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([]);
        var attemptCount = 0;

        _ = executor.Execute(
            context,
            rule,
            alternative,
            alternativeIndex: 0,
            startPosition: 0,
            precedence: 0,
            cursorKind: ScheduledAlternativeCursorKinds.Alternation,
            cursorIndex: 1,
            diagnostics: null,
            checkPrecedence: static _ => true,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        _ = executor.Execute(
            context,
            rule,
            alternative,
            alternativeIndex: 0,
            startPosition: 0,
            precedence: 0,
            cursorKind: ScheduledAlternativeCursorKinds.Alternation,
            cursorIndex: 1,
            diagnostics: null,
            checkPrecedence: static _ => true,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(2, attemptCount);
    }

    private static Rule CreateRule()
    {
        return new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), null)
        ]));
    }
}
