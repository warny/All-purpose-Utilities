using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies the parser rule call-stack frame model introduced as preparatory infrastructure
/// for future rule return and argument support.
/// Covers frame entry/exit semantics, parent/depth invariants, rollback across failed
/// alternatives, quantifier failures, and memoization hits.
/// Rule returns and parameters remain metadata-only throughout.
/// </summary>
[TestClass]
public class ParserRuleCallStackTests
{
    // ── Direct stack-manager unit tests ──────────────────────────────────────────────────

    /// <summary>
    /// Entering a root rule creates a frame with no parent, depth 0, and makes it current.
    /// </summary>
    [TestMethod]
    public void StackManager_EnterRootRule_CreatesFrameWithNoParentAndDepthZero()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        Assert.IsNull(manager.Current);

        var frame = manager.Enter("start", 0);

        Assert.IsNotNull(frame);
        Assert.IsNull(frame.Parent);
        Assert.AreEqual(0, frame.Depth);
        Assert.AreSame(frame, manager.Current);
    }

    /// <summary>
    /// Entering a nested rule creates a child frame whose parent is the caller frame.
    /// </summary>
    [TestMethod]
    public void StackManager_EnterNestedRule_CreatesChildWithParentAndDepthOne()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var rootFrame = manager.Enter("start", 0);

        var childFrame = manager.Enter("child", 1);

        Assert.IsNotNull(childFrame);
        Assert.AreSame(rootFrame, childFrame.Parent);
        Assert.AreEqual(1, childFrame.Depth);
        Assert.AreSame(childFrame, manager.Current);
    }

    /// <summary>
    /// Exiting the nested rule restores the caller frame as current.
    /// </summary>
    [TestMethod]
    public void StackManager_ExitNestedRule_RestoresCallerFrameAsCurrent()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var rootFrame = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);

        manager.Exit(childFrame, succeeded: true);

        Assert.AreSame(rootFrame, manager.Current);
    }

    /// <summary>
    /// Exiting the root rule restores current to null.
    /// </summary>
    [TestMethod]
    public void StackManager_ExitRootRule_SetCurrentToNull()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var rootFrame = manager.Enter("start", 0);

        manager.Exit(rootFrame, succeeded: true);

        Assert.IsNull(manager.Current);
    }

    /// <summary>
    /// Exiting a frame that is not the current top-of-stack throws InvalidOperationException.
    /// </summary>
    [TestMethod]
    public void StackManager_MismatchedExit_ThrowsInvalidOperationException()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var rootFrame = manager.Enter("start", 0);
        _ = manager.Enter("child", 1);

        Assert.ThrowsException<InvalidOperationException>(() => manager.Exit(rootFrame, false));
    }

    /// <summary>
    /// Exiting with a null frame throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void StackManager_ExitNull_ThrowsArgumentNullException()
    {
        var manager = new StackParserRuleInvocationFrameManager();

        Assert.ThrowsException<ArgumentNullException>(() => manager.Exit(null!, false));
    }

    /// <summary>
    /// A three-level call chain produces frames with depths 0, 1, 2 and each parent link is correct.
    /// </summary>
    [TestMethod]
    public void StackManager_ThreeLevelChain_DepthAndParentChainAreCorrect()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var f0 = manager.Enter("root", 0);
        var f1 = manager.Enter("mid", 1);
        var f2 = manager.Enter("leaf", 2);

        Assert.AreEqual(0, f0.Depth);
        Assert.AreEqual(1, f1.Depth);
        Assert.AreEqual(2, f2.Depth);
        Assert.IsNull(f0.Parent);
        Assert.AreSame(f0, f1.Parent);
        Assert.AreSame(f1, f2.Parent);

        manager.Exit(f2, true);
        Assert.AreSame(f1, manager.Current);
        manager.Exit(f1, true);
        Assert.AreSame(f0, manager.Current);
        manager.Exit(f0, true);
        Assert.IsNull(manager.Current);
    }

    // ── Frame properties on NullParserRuleInvocationFrameManager ────────────────────────

    /// <summary>
    /// NullParserRuleInvocationFrameManager creates frames with Parent null and Depth 0,
    /// and Current remains null because it does not track frames.
    /// </summary>
    [TestMethod]
    public void NullManager_CreatedFrames_HaveNoParentAndDepthZero()
    {
        var manager = NullParserRuleInvocationFrameManager.Instance;

        var frame = manager.Enter("start", 0);

        Assert.IsNull(frame.Parent);
        Assert.AreEqual(0, frame.Depth);
        Assert.IsNull(manager.Current);
    }

    // ── Integration tests through ParserEngine ───────────────────────────────────────────

    /// <summary>
    /// After a successful parse, the stack manager's Current is null.
    /// </summary>
    [TestMethod]
    public void ParserEngine_AfterSuccessfulParse_CurrentIsNull()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : 'a' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var compiled = CompileWithStackManager(grammar, frameManager);

        var result = compiled.Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsNull(frameManager.Current, "Frame stack must be empty after parse completes.");
    }

    /// <summary>
    /// Failed alternatives do not leave stale frames on the stack.
    /// </summary>
    [TestMethod]
    public void ParserEngine_FailedAlternative_DoesNotLeakFrames()
    {
        // alt 0: child B — fails when input is only 'a'
        // alt 1: child   — succeeds
        const string grammar = """
            grammar P;
            start : child B | child ;
            child : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var compiled = CompileWithStackManager(grammar, frameManager);

        var result = compiled.Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsNull(frameManager.Current, "Failed alternative must not leak child frames.");
    }

    /// <summary>
    /// Quantifier failed attempts do not leave stale frames on the stack.
    /// </summary>
    [TestMethod]
    public void ParserEngine_QuantifierFailedAttempt_DoesNotLeakFrames()
    {
        // item+ tries a third iteration; that attempt enters a frame for 'item' then fails
        const string grammar = """
            grammar P;
            start : item+ ;
            item : A ;
            A : 'a' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var compiled = CompileWithStackManager(grammar, frameManager);

        var result = compiled.Parse("aa");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsNull(frameManager.Current, "Quantifier failed attempt must not leak frames.");
    }

    /// <summary>
    /// Memoization hits do not create or leave stale frames on the stack.
    /// </summary>
    [TestMethod]
    public void ParserEngine_MemoizationHit_DoesNotLeaveStaleFrames()
    {
        // alt 0: sub B — parses sub (memoized), fails on B
        // alt 1: sub   — gets a memoization hit for sub, no new frame created
        const string grammar = """
            grammar P;
            start : sub B | sub ;
            sub : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var compiled = CompileWithStackManager(grammar, frameManager);

        var result = compiled.Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsNull(frameManager.Current, "Memoization hit must not leave stale frames.");
    }

    /// <summary>
    /// The conservative generated Parse() path does not install the StackParserRuleInvocationFrameManager
    /// and therefore Current is always null from the NullParserRuleInvocationFrameManager.
    /// </summary>
    [TestMethod]
    public void DefaultPolicy_UsesNullManager_CurrentAlwaysNull()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : A ;
            A : 'a' ;
            """);
        var frameManagerObserver = NullParserRuleInvocationFrameManager.Instance;
        var policy = ParserRuntimeFeaturePolicy.Default;

        Assert.AreSame(NullParserRuleInvocationFrameManager.Instance, policy.RuleInvocationFrameManager);
        Assert.IsNull(frameManagerObserver.Current);
    }

    /// <summary>
    /// Rule returns and parameters remain metadata-only: they are not propagated or executed.
    /// </summary>
    [TestMethod]
    public void InvocationFrame_ReturnsAndParameters_AreMetadataOnly()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var frame = manager.Enter("start", 0);

        Assert.AreEqual(0, frame.Returns.Count, "Returns store must be empty — no auto-propagation.");
        Assert.AreEqual(0, frame.Parameters.Count, "Parameters store must be empty — no auto-binding.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    private static CompiledGrammar CompileWithStackManager(string grammarText, StackParserRuleInvocationFrameManager frameManager)
    {
        var definition = Antlr4GrammarConverter.Parse(grammarText);
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleInvocationFrameManager = frameManager,
        };
        return new CompiledGrammar(definition, policy);
    }
}
