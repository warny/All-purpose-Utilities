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
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(2, attemptCount);
    }

    [TestMethod]
    public void Execute_DoesNotUseNegativeShortcutForNestedAlternation()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(2, attemptCount);
    }

    [TestMethod]
    public void Execute_UsesImmediateRejectShortcutOnly()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
        var lookaheadKey = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        cache.TryAdd(lookaheadKey, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, null, null));
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(0, attemptCount);
    }

    [TestMethod]
    public void Execute_DoesNotSkipRequiresParse()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
        var lookaheadKey = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        cache.TryAdd(lookaheadKey, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, null, null));
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(1, attemptCount);
    }


    [TestMethod]
    public void Execute_UsesProbeImmediateRejectShortcutForLiteralMismatch_WhenDiagnosticsNull()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "y")]);
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(0, attemptCount);
    }

    [TestMethod]
    public void Execute_DoesNotUseProbeImmediateRejectShortcut_WhenDiagnosticsProvided()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "y")]);
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
            diagnostics: new Utils.Parser.Diagnostics.DiagnosticBag(),
            checkPrecedence: static _ => true,
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(1, attemptCount);
    }

    [TestMethod]
    public void Execute_RequiresParseStillRunsAlternative()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(1, attemptCount);
    }
    [TestMethod]
    public void Execute_UsesCachedLookaheadBeforeReprobing()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());

        // Alternative with RuleRef: resolveRule is called only when the probe runs.
        var rule = new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new RuleRef("someRule"), null)
        ]));
        var alternative = rule.Content.Alternatives[0];

        // Seed cache with ImmediateReject so the shortcut should be applied immediately.
        var lookaheadKey = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        cache.TryAdd(lookaheadKey, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, null, null));

        // Token is present so the probe would call resolveRule if it ran.
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
        var probeCallCount = 0;
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
            resolveRule: _ => { probeCallCount++; return null; },
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(0, attemptCount, "ImmediateReject from cache should have skipped parsing.");
        Assert.AreEqual(0, probeCallCount, "Probe must not run when the cache already holds an authoritative result.");
    }



    [TestMethod]
    public void Execute_EpsilonPossible_DoesNotTriggerShortcut()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
        var lookaheadKey = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        cache.TryAdd(lookaheadKey, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.EpsilonPossible, null, null));
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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: _ =>
            {
                attemptCount++;
                return null;
            });

        Assert.AreEqual(1, attemptCount);
    }

    [TestMethod]
    public void Execute_MetadataDoesNotAuthorizeReplay_AlternativeBodyRunsPerAttempt()
    {
        var registry = new ParserStateRegistry();
        var cache = new ParserLookaheadCache();
        var executor = new ScheduledAlternativeExecutor(registry, cache, new ParserLookaheadProbe());
        var rule = CreateRule();
        var alternative = rule.Content.Alternatives[0];
        var context = new ParseContext([new Token(new SourceSpan(0, 1), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);
        var parseCalls = 0;

        ParseNode? Parse(Alternative _)
        {
            parseCalls++;
            return null;
        }

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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: Parse);

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
            resolveRule: static _ => null,
            caseInsensitive: false,
            containsPredicateOrAction: static _ => false,
            resolveDiagnosticSpan: static _ => (0, 0),
            captureAttempt: CaptureAttempt,
            restoreAttempt: RestoreAttempt,
            parseAlternative: Parse);

        Assert.AreEqual(2, parseCalls);
    }

    /// <summary>
    /// Captures a no-op attempt snapshot for scheduled-executor unit tests.
    /// </summary>
    private static ParserAttemptSnapshot CaptureAttempt(ParseContext context)
    {
        return new ParserAttemptSnapshot(context.Position, NullParserExecutionStateManager.Instance.Capture());
    }

    /// <summary>
    /// Restores a no-op attempt snapshot for scheduled-executor unit tests.
    /// </summary>
    private static void RestoreAttempt(ParseContext context, ParserAttemptSnapshot snapshot)
    {
        context.RestorePosition(snapshot.InputPosition);
        NullParserExecutionStateManager.Instance.Restore(snapshot.ExecutionStateSnapshot);
    }

    private static Rule CreateRule()
    {
        return new Rule("root", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), null)
        ]));
    }
}
