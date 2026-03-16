using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for <see cref="ParseTreeCompiler{TContext,TResult}"/> using the Exp grammar.
///
/// Tree shape reminder for "2+5":
///   eval
///   └─ additionExp
///        ├─ multiplyExp → atomExp → LexerNode("2")
///        └─ additionExp (quantifier outer)
///             └─ additionExp (sequence '+' multiplyExp)
///                  ├─ LexerNode("+")
///                  └─ multiplyExp → atomExp → LexerNode("5")
/// </summary>
[TestClass]
public class ParseTreeCompilerTests
{
    private static readonly CompiledGrammar Grammar = new(ExpGrammar.Build());

    private static ParseNode Parse(string input) => Grammar.Parse(input);

    // ═══════════════════════════════════════════════════════════════
    // Traversal order
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_DescentBeforeAscent_ForEachNode()
    {
        // Verify that the descent handler is always called before the ascent handler
        // for the same node, and that children are compiled between the two phases.
        var log = new List<string>();

        var compiler = new ParseTreeCompiler<int, string>()
            .DefaultDescend((nav, depth) =>
            {
                log.Add($"↓ {nav.RuleName}:{depth}");
                return depth + 1;
            })
            .DefaultAscend((nav, depth, _) =>
            {
                log.Add($"↑ {nav.RuleName}:{depth}");
                return nav.RuleName;
            });

        compiler.Compile(Parse("1"), 0);

        // For every rule, the ↓ entry must precede its own ↑ entry.
        foreach (var rule in log.Select(e => e[2..e.IndexOf(':')]).Distinct())
        {
            int downIdx = log.FindIndex(e => e.StartsWith($"↓ {rule}:"));
            int upIdx   = log.FindIndex(e => e.StartsWith($"↑ {rule}:"));
            Assert.IsTrue(downIdx < upIdx,
                $"Expected ↓ before ↑ for rule '{rule}'");
        }
    }

    [TestMethod]
    public void Compiler_ChildrenCompiledBeforeParent()
    {
        // Collect ascent order; children must appear before their parent.
        var ascentOrder = new List<string>();

        var compiler = new ParseTreeCompiler<int, string>()
            .DefaultAscend((nav, _, children) =>
            {
                ascentOrder.Add(nav.RuleName);
                return nav.RuleName;
            });

        compiler.Compile(Parse("1+2"), 0);

        // "Number" (leaf) must be compiled before "atomExp", which is before "multiplyExp", etc.
        int numberIdx     = ascentOrder.IndexOf("Number");
        int atomIdx       = ascentOrder.IndexOf("atomExp");
        int multiplyIdx   = ascentOrder.IndexOf("multiplyExp");
        int additionIdx   = ascentOrder.LastIndexOf("additionExp");

        Assert.IsTrue(numberIdx     < atomIdx,     "Number before atomExp");
        Assert.IsTrue(atomIdx       < multiplyIdx, "atomExp before multiplyExp");
        Assert.IsTrue(multiplyIdx   < additionIdx, "multiplyExp before additionExp");
    }

    // ═══════════════════════════════════════════════════════════════
    // Context propagation
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_ContextPropagates_DepthIncrementsCorrectly()
    {
        // The descent handler increments depth. Verify that the depth received
        // in ascent matches the depth at which the node was entered.
        var maxDepthSeen = 0;

        var compiler = new ParseTreeCompiler<int, int>()
            .DefaultDescend((nav, depth) => depth + 1)
            .DefaultAscend((nav, depth, _) =>
            {
                if (depth > maxDepthSeen) maxDepthSeen = depth;
                return depth;
            });

        compiler.Compile(Parse("42"), 0);

        // "42" → eval(0) → additionExp(1) → multiplyExp(2) → atomExp(3) → Number(4)
        Assert.IsTrue(maxDepthSeen >= 4, $"Expected depth ≥ 4, got {maxDepthSeen}");
    }

    [TestMethod]
    public void Compiler_ContextIsolated_PathsAreDivergent()
    {
        // Verify that each leaf receives a context built from its own path from the root,
        // not contaminated by sibling traversal. We use a string path as context:
        // each descent appends the rule name.
        var leafPaths = new List<string>();

        var compiler = new ParseTreeCompiler<string, string>()
            .DefaultDescend((nav, path) => path + "/" + nav.RuleName)
            .OnAscend("Number", (nav, path) =>
            {
                leafPaths.Add(path);
                return path;
            })
            .DefaultAscend((nav, _, children) => children.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("2+5"), "root");

        Assert.AreEqual(2, leafPaths.Count, "Should find exactly 2 Number leaves");

        // Both paths must start from root and include Number.
        Assert.IsTrue(leafPaths[0].StartsWith("root/"), "Left path starts from root");
        Assert.IsTrue(leafPaths[1].StartsWith("root/"), "Right path starts from root");

        // The paths must diverge: neither is a prefix of the other
        // (because the right operand travels through extra quantifier nodes).
        Assert.AreNotEqual(leafPaths[0], leafPaths[1],
            "Left and right paths must be different due to different tree depths");
    }

    [TestMethod]
    public void Compiler_DescentContext_OverriddenPerRule()
    {
        // A per-rule descent handler overrides the default for that specific rule.
        int depthAtMultiply = -1;

        var compiler = new ParseTreeCompiler<int, int>()
            .DefaultDescend((nav, depth) => depth + 1)
            .OnDescend("multiplyExp", (nav, depth) =>
            {
                // Jump context forward by 100 when entering multiplyExp.
                return depth + 100;
            })
            .OnAscend("Number", (nav, depth) =>
            {
                depthAtMultiply = depth;
                return depth;
            })
            .DefaultAscend((nav, _, __) => 0);

        compiler.Compile(Parse("3*4"), 0);

        // eval(0)→ additionExp(1) → multiplyExp(2) [override: +100] → atomExp(102) → Number(103)
        Assert.IsTrue(depthAtMultiply > 100,
            $"Expected depth > 100 after multiplyExp override, got {depthAtMultiply}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Collecting results
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_CollectsAllNumberTokens()
    {
        // Use the ascent phase to collect all Number token texts in order.
        var numbers = new List<string>();

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend("Number", (nav, _) =>
            {
                numbers.Add(nav.Token!.Text);
                return nav.Token.Text;
            })
            .DefaultAscend((nav, _, children) =>
                children.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("1+2*3"), 0);

        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, numbers);
    }

    [TestMethod]
    public void Compiler_ChildResultsPassedToParent()
    {
        // Verify that the ascent handler for "atomExp" receives the Number result.
        string? resultFromAtom = null;

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend("Number", (nav, _) => nav.Token!.Text)
            .OnAscend("atomExp", (nav, _, children) =>
            {
                resultFromAtom = children.FirstOrDefault(c => c is not null);
                return resultFromAtom;
            })
            .DefaultAscend((nav, _, children) =>
                children.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("7"), 0);

        Assert.AreEqual("7", resultFromAtom);
    }

    // ═══════════════════════════════════════════════════════════════
    // Error handling
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_ErrorNode_ReturnsDefaultWithoutHandler()
    {
        var compiler = new ParseTreeCompiler<int, string>()
            .DefaultAscend((nav, _, __) => "ok");

        // Empty input produces an ErrorNode.
        var result = compiler.Compile(Parse(""), 0);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Compiler_ErrorNode_CallsRegisteredHandler()
    {
        var compiler = new ParseTreeCompiler<int, string>()
            .OnError((nav, _) => "error-handled")
            .DefaultAscend((nav, _, __) => "ok");

        var result = compiler.Compile(Parse(""), 0);
        Assert.AreEqual("error-handled", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Default handlers
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_DefaultAscend_CalledForUnregisteredRules()
    {
        int defaultCallCount = 0;

        var compiler = new ParseTreeCompiler<int, int>()
            .DefaultAscend((nav, _, __) =>
            {
                defaultCallCount++;
                return 0;
            });

        compiler.Compile(Parse("1"), 0);

        Assert.IsTrue(defaultCallCount > 0,
            "DefaultAscend should have been called at least once");
    }

    [TestMethod]
    public void Compiler_NoHandlers_ReturnsDefault()
    {
        var compiler = new ParseTreeCompiler<int, string>();
        var result = compiler.Compile(Parse("1"), 0);
        Assert.IsNull(result, "Without any ascent handler, result should be default(string)");
    }

    // ═══════════════════════════════════════════════════════════════
    // Predicate-based handlers
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compiler_PredicateAscend_MatchesExpectedNodes()
    {
        // Collect rule names of all lexer nodes via a predicate handler.
        var lexerRules = new List<string>();

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend(nav => nav.IsLexer, (nav, _) =>
            {
                lexerRules.Add(nav.RuleName);
                return nav.Token?.Text;
            })
            .DefaultAscend((_, _, kids) => kids.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("1+2"), 0);

        // "Number" leaves are matched by TryParseRuleRef, so their RuleName == "Number".
        // Operator literals are matched by TryParseLiteral, which tags them with the parent
        // parser rule name — so the list will also contain parser rule names like "additionExp".
        Assert.IsTrue(lexerRules.Count > 0, "Expected at least one lexer node");
        CollectionAssert.Contains(lexerRules, "Number");
        // Three LexerNode leaves for "1", "+", "2".
        Assert.AreEqual(3, lexerRules.Count, "Expected exactly 3 leaf nodes for '1+2'");
    }

    [TestMethod]
    public void Compiler_PredicateAscend_WithChildResults_ReceivesKids()
    {
        // Use a predicate handler to sum children results at every parser node.
        var compiler = new ParseTreeCompiler<int, double>()
            .OnAscend(nav => nav.IsLexer,  (nav, _) =>
                double.TryParse(nav.Token?.Text, out var v) ? v : 0)
            .OnAscend(nav => nav.IsParser, (nav, _, kids) =>
                kids.Sum(k => k));

        var result = compiler.Compile(Parse("3+4"), 0);

        Assert.AreEqual(7.0, result);
    }

    [TestMethod]
    public void Compiler_PredicateDescend_AdjustsContext()
    {
        // A predicate-based descent handler adds 100 to the depth for every parser node.
        int depthAtNumber = -1;

        var compiler = new ParseTreeCompiler<int, int>()
            .OnDescend(nav => nav.IsParser, (nav, depth) => depth + 100)
            .OnAscend("Number", (nav, depth) => { depthAtNumber = depth; return depth; })
            .DefaultAscend((_, _, __) => 0);

        compiler.Compile(Parse("1"), 0);

        // eval(100) → additionExp(200) → multiplyExp(300) → atomExp(400) → Number(400)
        Assert.IsTrue(depthAtNumber >= 400,
            $"Expected depth ≥ 400 via predicate descent, got {depthAtNumber}");
    }

    [TestMethod]
    public void Compiler_RuleNameHandler_TakesPriorityOverPredicate()
    {
        // A rule-name handler for "Number" must win over a predicate that also matches it.
        string? winner = null;

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend("Number",           (nav, _) => { winner = "by-name";      return "by-name"; })
            .OnAscend(nav => nav.IsLexer, (nav, _) => { winner = "by-predicate"; return "by-predicate"; })
            .DefaultAscend((_, _, kids) => kids.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("5"), 0);

        Assert.AreEqual("by-name", winner,
            "Rule-name handler must take priority over a matching predicate");
    }

    [TestMethod]
    public void Compiler_FirstMatchingPredicate_Wins()
    {
        // Two predicate handlers both match lexer nodes; only the first must fire.
        int firstCount  = 0;
        int secondCount = 0;

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend(nav => nav.IsLexer, (nav, _) => { firstCount++;  return "first"; })
            .OnAscend(nav => nav.IsLexer, (nav, _) => { secondCount++; return "second"; })
            .DefaultAscend((_, _, kids) => kids.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("1+2"), 0);

        Assert.IsTrue(firstCount  > 0, "First predicate handler must have fired");
        Assert.AreEqual(0, secondCount, "Second predicate handler must not fire when first matches");
    }

    [TestMethod]
    public void Compiler_Predicate_DoesNotFireWhenReturnsFalse()
    {
        // A predicate that always returns false must never invoke its handler.
        bool fired = false;

        var compiler = new ParseTreeCompiler<int, string>()
            .OnAscend(_ => false, (nav, _) => { fired = true; return "x"; })
            .DefaultAscend((_, _, kids) => kids.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("1"), 0);

        Assert.IsFalse(fired, "Handler must not be invoked when predicate returns false");
    }

    // ═══════════════════════════════════════════════════════════════
    // Guard: duplicate registration
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Compiler_DuplicateDescentRegistration_Throws()
    {
        new ParseTreeCompiler<int, string>()
            .OnDescend("eval", (nav, ctx) => ctx)
            .OnDescend("eval", (nav, ctx) => ctx);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Compiler_DuplicateAscentRegistration_Throws()
    {
        new ParseTreeCompiler<int, string>()
            .OnAscend("eval", (nav, ctx) => "x")
            .OnAscend("eval", (nav, ctx) => "y");
    }

    // ═══════════════════════════════════════════════════════════════
    // Context-enrichment example: variable bindings
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Demonstrates the descent phase enriching a variable dictionary.
    /// A custom grammar with a let-like construct is simulated by pre-populating
    /// the context and verifying that Number leaves can see injected values.
    /// Here we simply verify that the context passed to leaves is the one built
    /// during the descent from the root.
    /// </summary>
    [TestMethod]
    public void Compiler_ContextEnrichment_VariablesBecomeAvailableToLeaves()
    {
        // Inject a variable "x=42" at the eval level and verify Number leaves see it.
        var initialContext = new Dictionary<string, double> { ["x"] = 42.0 };
        string? observedAtLeaf = null;

        var compiler = new ParseTreeCompiler<Dictionary<string, double>, string>()
            .OnDescend("eval", (nav, ctx) =>
            {
                // Could add new bindings here in a real compiler.
                return ctx;
            })
            .OnAscend("Number", (nav, ctx) =>
            {
                // The leaf sees the same context that was established during descent.
                observedAtLeaf = ctx.ContainsKey("x") ? "has-x" : "no-x";
                return observedAtLeaf;
            })
            .DefaultAscend((nav, _, children) =>
                children.FirstOrDefault(c => c is not null));

        compiler.Compile(Parse("9"), initialContext);

        Assert.AreEqual("has-x", observedAtLeaf,
            "The context enriched during descent should be visible to leaves");
    }
}
