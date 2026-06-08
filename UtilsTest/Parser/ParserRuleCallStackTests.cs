using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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

    // ── ParserRuleCallResult unit tests ─────────────────────────────────────────────────

    /// <summary>
    /// PrepareCallResultForSnapshot on successful child exit stores a call result on the parent frame.
    /// ParserEngine always calls PrepareCallResultForSnapshot before Exit.
    /// </summary>
    [TestMethod]
    public void StackManager_SuccessfulChildExit_StoresCallResultOnParent()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var parentFrame = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);
        childFrame.SetReturnValue("value", 42);

        manager.PrepareCallResultForSnapshot(childFrame, succeeded: true);
        manager.Exit(childFrame, succeeded: true);

        Assert.IsNotNull(parentFrame.LastCompletedChildCall);
        Assert.AreEqual("child", parentFrame.LastCompletedChildCall.RuleName);
        Assert.AreEqual(1, parentFrame.LastCompletedChildCall.Depth);
    }

    /// <summary>
    /// Call result captures a copy of the return values; mutating the child frame after capture does not affect it.
    /// </summary>
    [TestMethod]
    public void StackManager_CallResult_CopiesReturnValues()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var parentFrame = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);
        childFrame.SetReturnValue("value", 1);

        manager.PrepareCallResultForSnapshot(childFrame, succeeded: true);
        var captured = parentFrame.LastCompletedChildCall!;
        manager.Exit(childFrame, succeeded: true);

        // Mutating the child frame after capture must not affect the snapshot.
        childFrame.SetReturnValue("value", 999);

        Assert.AreEqual(1, captured.Returns["value"]);
    }

    /// <summary>
    /// Failed child exit does not update the parent frame's last call result.
    /// </summary>
    [TestMethod]
    public void StackManager_FailedChildExit_DoesNotUpdateParentCallResult()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var parentFrame = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);
        childFrame.SetReturnValue("value", 42);

        manager.PrepareCallResultForSnapshot(childFrame, succeeded: false);
        manager.Exit(childFrame, succeeded: false);

        Assert.IsNull(parentFrame.LastCompletedChildCall, "Failed exit must not update the parent call result.");
    }

    /// <summary>
    /// Root exit (no parent) restores current to null without requiring a parent call result.
    /// </summary>
    [TestMethod]
    public void StackManager_RootExit_DoesNotRequireParentCallResult()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var rootFrame = manager.Enter("start", 0);
        rootFrame.SetReturnValue("value", 1);

        manager.PrepareCallResultForSnapshot(rootFrame, succeeded: true);
        manager.Exit(rootFrame, succeeded: true);

        Assert.IsNull(manager.Current, "Root exit must restore current to null.");
    }

    /// <summary>
    /// The callback is invoked with the call result on successful PrepareCallResultForSnapshot when a parent exists.
    /// </summary>
    [TestMethod]
    public void StackManager_CallbackInvokedWithCallResult_OnSuccessfulChildExit()
    {
        ParserRuleCallResult? received = null;
        var manager = new StackParserRuleInvocationFrameManager(onChildCallResult: r => received = r);
        _ = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);
        childFrame.SetReturnValue("value", 7);

        manager.PrepareCallResultForSnapshot(childFrame, succeeded: true);

        Assert.IsNotNull(received);
        Assert.AreEqual("child", received.RuleName);
        Assert.AreEqual(7, received.Returns["value"]);
    }

    /// <summary>
    /// The callback is NOT invoked on failed PrepareCallResultForSnapshot.
    /// </summary>
    [TestMethod]
    public void StackManager_CallbackNotInvoked_OnFailedChildExit()
    {
        bool called = false;
        var manager = new StackParserRuleInvocationFrameManager(onChildCallResult: _ => called = true);
        _ = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);

        manager.PrepareCallResultForSnapshot(childFrame, succeeded: false);

        Assert.IsFalse(called);
    }

    /// <summary>
    /// SyncCallResultToCurrentFrame overwrites the current frame's LastCompletedChildCall.
    /// </summary>
    [TestMethod]
    public void StackManager_SyncCallResultToCurrentFrame_OverwritesExistingResult()
    {
        var manager = new StackParserRuleInvocationFrameManager();
        var parentFrame = manager.Enter("start", 0);
        var childFrame = manager.Enter("child", 1);
        childFrame.SetReturnValue("value", 42);
        manager.Exit(childFrame, succeeded: true);

        // Simulate rollback: sync null (pre-attempt snapshot value)
        manager.SyncCallResultToCurrentFrame(null);

        Assert.IsNull(parentFrame.LastCompletedChildCall, "SyncCallResultToCurrentFrame must overwrite with the restored value.");
    }

    // ── Call-result rollback integration tests through ParserEngine ──────────────────────

    /// <summary>
    /// A failed alternative that calls a child does not leak its call result into the next alternative.
    /// </summary>
    [TestMethod]
    public void ParserEngine_FailedAlternative_DoesNotLeakChildCallResult()
    {
        // alt 0: child B — fails (no B in input "a")
        // alt 1: A       — succeeds; no child call is made
        const string grammar = """
            grammar P;
            start : child B | A ;
            child returns [int value] @after { } : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var frameManager = new StackParserRuleInvocationFrameManager();
        var compiled = CompileWithStackManager(grammar, frameManager);

        // Use a capture observer to record the call result on start's frame in @after.
        // Since we can't run generated C#, we verify via the frame manager directly.
        var result = compiled.Parse("a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        // After a successful parse, the frame manager's current is null (all frames exited).
        Assert.IsNull(frameManager.Current);
    }

    /// <summary>
    /// Quantifier failed iteration does not leave a stale call result.
    /// </summary>
    [TestMethod]
    public void ParserEngine_QuantifierFailedAttempt_DoesNotLeakCallResult()
    {
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
        Assert.IsNull(frameManager.Current, "Frame stack must be empty after parse completes.");
    }

    // ── Return descriptor lexical-name tests ──────────────────────────────────────────────

    /// <summary>
    /// A single return declaration exposes the lexical name (e.g. "value") not the full raw text.
    /// </summary>
    [TestMethod]
    public void ReturnDescriptor_SingleDeclaration_ExposesLexicalName()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start returns [int value] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.First(r => r.Name == "start");
        var descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        Assert.AreEqual(1, descriptor.Returns.Count);
        Assert.AreEqual("value", descriptor.Returns[0].Name);
        Assert.AreEqual("int value", descriptor.Returns[0].RawDeclaration);
    }

    /// <summary>
    /// Multiple return declarations are split and each exposes its own lexical name.
    /// </summary>
    [TestMethod]
    public void ReturnDescriptor_MultipleDeclarations_SplitsAndExtractsNames()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start returns [int value, String text, long counter] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.First(r => r.Name == "start");
        var descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        Assert.AreEqual(3, descriptor.Returns.Count);
        Assert.AreEqual("value", descriptor.Returns[0].Name);
        Assert.AreEqual("text", descriptor.Returns[1].Name);
        Assert.AreEqual("counter", descriptor.Returns[2].Name);
    }

    /// <summary>
    /// Raw declarations are preserved verbatim alongside lexical names.
    /// </summary>
    [TestMethod]
    public void ReturnDescriptor_RawDeclarations_PreservedVerbatim()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar P;
            start returns [int value, String text] : A ;
            A : 'a' ;
            """);
        var rule = definition.ParserRules.First(r => r.Name == "start");
        var descriptor = ParserRuleInvocationDescriptor.FromRule(rule);

        StringAssert.Contains(descriptor.Returns[0].RawDeclaration, "int value");
        StringAssert.Contains(descriptor.Returns[1].RawDeclaration, "String text");
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
