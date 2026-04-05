using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ModelTests
{
    [TestMethod]
    public void RuleContent_LiteralMatch_StoresValue()
    {
        var lit = new LiteralMatch("hello");
        Assert.AreEqual("hello", lit.Value);
        Assert.IsInstanceOfType<TokenizerContent>(lit);
    }

    [TestMethod]
    public void RuleContent_RangeMatch_StoresFromTo()
    {
        var range = new RangeMatch('a', 'z');
        Assert.AreEqual('a', range.From);
        Assert.AreEqual('z', range.To);
    }

    [TestMethod]
    public void RuleContent_CharSetMatch_StoresCharsAndNegated()
    {
        var chars = new HashSet<char>("abc");
        var cs = new CharSetMatch(chars, Negated: true);

        Assert.IsTrue(cs.Negated);
        Assert.IsTrue(cs.Chars.Contains('a'));
        Assert.IsTrue(cs.Chars.Contains('b'));
        Assert.IsTrue(cs.Chars.Contains('c'));
        Assert.IsFalse(cs.Chars.Contains('d'));
    }

    [TestMethod]
    public void RuleContent_Sequence_StoresItems()
    {
        var seq = new Sequence(new RuleContent[]
        {
            new LiteralMatch("a"),
            new LiteralMatch("b"),
            new LiteralMatch("c")
        });
        Assert.AreEqual(3, seq.Items.Count);
    }

    [TestMethod]
    public void RuleContent_Alternation_StoresAlternatives()
    {
        var alt = new Alternation(new[]
        {
            new Alternative(0, Associativity.Left, new LiteralMatch("x")),
            new Alternative(1, Associativity.Left, new LiteralMatch("y")),
        });
        Assert.AreEqual(2, alt.Alternatives.Count);
        Assert.AreEqual(0, alt.Alternatives[0].Priority);
        Assert.AreEqual(1, alt.Alternatives[1].Priority);
    }

    [TestMethod]
    public void RuleContent_Quantifier_Star()
    {
        var q = new Quantifier(new LiteralMatch("a"), Min: 0, Max: null, Greedy: true);
        Assert.AreEqual(0, q.Min);
        Assert.IsNull(q.Max);
        Assert.IsTrue(q.Greedy);
    }

    [TestMethod]
    public void RuleContent_Quantifier_Plus()
    {
        var q = new Quantifier(new LiteralMatch("a"), Min: 1, Max: null, Greedy: true);
        Assert.AreEqual(1, q.Min);
        Assert.IsNull(q.Max);
    }

    [TestMethod]
    public void RuleContent_Quantifier_Optional()
    {
        var q = new Quantifier(new LiteralMatch("a"), Min: 0, Max: 1, Greedy: true);
        Assert.AreEqual(0, q.Min);
        Assert.AreEqual(1, q.Max);
    }

    [TestMethod]
    public void RuleContent_Quantifier_NonGreedy()
    {
        var q = new Quantifier(new LiteralMatch("a"), Min: 0, Max: null, Greedy: false);
        Assert.IsFalse(q.Greedy);
    }

    [TestMethod]
    public void RuleContent_Negation_StoresInner()
    {
        var neg = new Negation(new LiteralMatch("x"));
        Assert.IsInstanceOfType<LiteralMatch>(neg.Inner);
    }

    [TestMethod]
    public void RuleContent_RuleRef_StoresName()
    {
        var r = new RuleRef("SomeRule");
        Assert.AreEqual("SomeRule", r.RuleName);
        Assert.IsNull(r.Label);
    }

    [TestMethod]
    public void RuleContent_RuleRef_WithLabel()
    {
        var label = new RuleLabel("e", "expr", false);
        var r = new RuleRef("expr", label);
        Assert.IsNotNull(r.Label);
        Assert.AreEqual("e", r.Label.Label);
        Assert.AreEqual("expr", r.Label.RuleName);
        Assert.IsFalse(r.Label.IsAdditive);
    }

    [TestMethod]
    public void RuleContent_RuleRef_WithAdditiveLabel()
    {
        var label = new RuleLabel("ids", "ID", true);
        var r = new RuleRef("ID", label);
        Assert.IsTrue(r.Label!.IsAdditive);
    }

    [TestMethod]
    public void RuleContent_LexerCommand_Types()
    {
        var skip = new LexerCommand(LexerCommandType.Skip, null);
        Assert.AreEqual(LexerCommandType.Skip, skip.Type);
        Assert.IsNull(skip.Argument);

        var channel = new LexerCommand(LexerCommandType.Channel, "HIDDEN");
        Assert.AreEqual("HIDDEN", channel.Argument);

        var pushMode = new LexerCommand(LexerCommandType.PushMode, "STRING_MODE");
        Assert.AreEqual("STRING_MODE", pushMode.Argument);
    }

    [TestMethod]
    public void RuleContent_EmbeddedAction_StoresFields()
    {
        var action = new EmbeddedAction(
            "$value + 1",
            ActionContext.Alternative,
            ActionPosition.Inline,
            new[] { new LabelRef(null, "value") });

        Assert.AreEqual("$value + 1", action.RawCode);
        Assert.AreEqual(ActionContext.Alternative, action.Context);
        Assert.AreEqual(1, action.Labels.Count);
    }

    [TestMethod]
    public void Rule_DefaultKindIsUnresolved()
    {
        var rule = new Rule("test", 0, false,
            new Alternation(new[] {
                new Alternative(0, Associativity.Left, new LiteralMatch("x"))
            }));

        Assert.AreEqual(RuleKind.Unresolved, rule.Kind);
    }

    [TestMethod]
    public void Rule_IsFragment()
    {
        var rule = new Rule("DIGIT", 0, true,
            new Alternation(new[] {
                new Alternative(0, Associativity.Left, new RangeMatch('0', '9'))
            }));

        Assert.IsTrue(rule.IsFragment);
    }

    [TestMethod]
    public void Rule_WithParameters()
    {
        var rule = new Rule("myRule", 0, false,
            new Alternation(new[] {
                new Alternative(0, Associativity.Left, new LiteralMatch("x"))
            }),
            Parameters: new[] { new RuleParameter("int", "x"), new RuleParameter("String", "y") });

        Assert.AreEqual(2, rule.Parameters!.Count);
        Assert.AreEqual("int", rule.Parameters[0].Type);
        Assert.AreEqual("x", rule.Parameters[0].Name);
    }

    [TestMethod]
    public void Rule_WithReturns()
    {
        var rule = new Rule("myRule", 0, false,
            new Alternation(new[] {
                new Alternative(0, Associativity.Left, new LiteralMatch("x"))
            }),
            Returns: new[] { new RuleReturn("int", "value") });

        Assert.AreEqual(1, rule.Returns!.Count);
        Assert.AreEqual("value", rule.Returns[0].Name);
    }

    [TestMethod]
    public void GrammarMeta_GrammarOptions()
    {
        var opts = new GrammarOptions(new Dictionary<string, string>
        {
            ["tokenVocab"] = "MyLexer",
            ["superClass"] = "MyBase"
        });

        Assert.AreEqual("MyLexer", opts.Values["tokenVocab"]);
        Assert.AreEqual(2, opts.Values.Count);
    }

    [TestMethod]
    public void GrammarMeta_GrammarAction()
    {
        var action = new GrammarAction("header", "import java.util.*;", "lexer");
        Assert.AreEqual("header", action.Name);
        Assert.AreEqual("lexer", action.Target);
    }

    [TestMethod]
    public void GrammarMeta_GrammarImport()
    {
        var import = new GrammarImport("CommonLexer", "CL");
        Assert.AreEqual("CommonLexer", import.GrammarName);
        Assert.AreEqual("CL", import.Alias);
    }

    [TestMethod]
    public void ExpGrammar_BuildsSuccessfully()
    {
        var definition = ExpGrammar.Build();
        Assert.AreEqual("Exp", definition.Name);
        Assert.AreEqual(GrammarType.Combined, definition.Type);
        Assert.IsNotNull(definition.RootRule);
        Assert.AreEqual("eval", definition.RootRule!.Name);
    }

    [TestMethod]
    public void ExpGrammar_AllRulesPopulated()
    {
        var definition = ExpGrammar.Build();
        Assert.IsTrue(definition.AllRules.Count > 0);
        Assert.IsTrue(definition.AllRules.ContainsKey("Number"));
        Assert.IsTrue(definition.AllRules.ContainsKey("WS"));
        Assert.IsTrue(definition.AllRules.ContainsKey("eval"));
        Assert.IsTrue(definition.AllRules.ContainsKey("additionExp"));
        Assert.IsTrue(definition.AllRules.ContainsKey("multiplyExp"));
        Assert.IsTrue(definition.AllRules.ContainsKey("atomExp"));
    }

    [TestMethod]
    public void ExpGrammar_RuleKindsInferred()
    {
        var definition = ExpGrammar.Build();

        // Lexer rules
        Assert.AreEqual(RuleKind.Lexer, definition.AllRules["Number"].Kind);
        Assert.AreEqual(RuleKind.Lexer, definition.AllRules["WS"].Kind);
        Assert.AreEqual(RuleKind.Lexer, definition.AllRules["PLUS"].Kind);
        Assert.AreEqual(RuleKind.Lexer, definition.AllRules["MINUS"].Kind);

        // Parser rules
        Assert.AreEqual(RuleKind.Parser, definition.AllRules["eval"].Kind);
        Assert.AreEqual(RuleKind.Parser, definition.AllRules["additionExp"].Kind);
        Assert.AreEqual(RuleKind.Parser, definition.AllRules["multiplyExp"].Kind);
        Assert.AreEqual(RuleKind.Parser, definition.AllRules["atomExp"].Kind);
    }

    [TestMethod]
    public void RuleResolver_DuplicateRuleThrows()
    {
        var rule1 = new Rule("SAME", 0, false,
            new Alternation(new[] { new Alternative(0, Associativity.Left, new LiteralMatch("a")) }));
        var rule2 = new Rule("SAME", 1, false,
            new Alternation(new[] { new Alternative(0, Associativity.Left, new LiteralMatch("b")) }));

        var definition = new ParserDefinition(
            "Test", GrammarType.Lexer, null, [], [],
            [new LexerMode("DEFAULT_MODE", [rule1, rule2])],
            [], null);

        Assert.ThrowsException<GrammarValidationException>(() => RuleResolver.Resolve(definition));
    }

    [TestMethod]
    public void RuleResolver_UnknownRuleRefThrows()
    {
        var rule = new Rule("test", 0, false,
            new Alternation(new[] {
                new Alternative(0, Associativity.Left, new RuleRef("DOES_NOT_EXIST"))
            }));

        var definition = new ParserDefinition(
            "Test", GrammarType.Lexer, null, [], [],
            [new LexerMode("DEFAULT_MODE", [rule])],
            [], null);

        Assert.ThrowsException<GrammarValidationException>(() => RuleResolver.Resolve(definition));
    }

    [TestMethod]
    public void RuleResolver_LexerCycle_ResolvesAndProducesErrorTokens()
    {
        // A direct lexer cycle (A: B; B: A;) is not rejected at resolution time.
        // The LexerEngine runtime guard detects the cycle at the same stream position
        // and breaks it, causing the engine to fall through to panic mode which
        // emits ERROR tokens — no StackOverflowException.
        var ruleA = new Rule("A", 0, false,
            new Alternation(new[]
            {
                new Alternative(0, Associativity.Left, new RuleRef("B"))
            }));

        var ruleB = new Rule("B", 1, false,
            new Alternation(new[]
            {
                new Alternative(0, Associativity.Left, new RuleRef("A"))
            }));

        var definition = new ParserDefinition(
            "Test", GrammarType.Lexer, null, [], [],
            [new LexerMode("DEFAULT_MODE", [ruleA, ruleB])],
            [], null);

        // Resolution must succeed.
        ParserDefinition resolved = RuleResolver.Resolve(definition);

        // Tokenizing any input should produce ERROR tokens, not a StackOverflowException.
        var lexer = new LexerEngine(resolved);
        List<Token> tokens = lexer.Tokenize(new StringCharStream("x")).ToList();
        Assert.IsTrue(tokens.Count > 0, "Expected at least one ERROR token");
        Assert.IsTrue(tokens.All(t => t.RuleName == "ERROR"), "Expected all tokens to be ERROR");
    }
}
