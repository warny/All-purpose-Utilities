using System.Text;
using System.Text.RegularExpressions;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace Utils.Parser.Bootstrap;

/// <summary>
/// Thrown when converting a parse tree into a <see cref="Utils.Parser.Model.ParserDefinition"/> fails,
/// for example when the root node is not a <c>grammarSpec</c> or when a required
/// grammar element is missing.
/// </summary>
public class GrammarParseException(string message) : Exception(message);

/// <summary>
/// Converts a parse tree produced by <see cref="Antlr4Grammar.Build()"/> into a
/// fully-resolved <see cref="Utils.Parser.Model.ParserDefinition"/>.
/// <para>
/// The typical entry point is the static <see cref="Parse"/> method, which runs
/// the complete pipeline: tokenize → parse → convert → resolve.
/// </para>
/// </summary>
public sealed class Antlr4GrammarConverter
{
    /// <summary>Declaration-order counter incremented as rules are created during conversion.</summary>
    private int _order;

    /// <summary>Initialises a new converter instance. The <paramref name="sourceText"/> parameter is reserved for future use.</summary>
    /// <param name="sourceText">The original grammar source text (currently unused).</param>
    public Antlr4GrammarConverter(string sourceText) { }

    // ─── Full pipeline ────────────────────────────────────────────────────

    /// <summary>
    /// Convenience factory: compiles <paramref name="grammarText"/> into a
    /// <see cref="Utils.Parser.Runtime.CompiledGrammar"/> ready to tokenize and parse input strings.
    /// <para>
    /// Equivalent to <c>new CompiledGrammar(Antlr4GrammarConverter.Parse(grammarText))</c>.
    /// </para>
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar source (<c>.g4</c> content).</param>
    /// <returns>A <see cref="Utils.Parser.Runtime.CompiledGrammar"/> backed by the compiled definition.</returns>
    /// <exception cref="GrammarParseException">
    /// Thrown when the input cannot be parsed as a valid ANTLR4 grammar.
    /// </exception>
    public static Runtime.CompiledGrammar Compile(string grammarText)
        => new Runtime.CompiledGrammar(Parse(grammarText));

    /// <summary>
    /// Full pipeline: ANTLR4 grammar text → resolved <see cref="Utils.Parser.Model.ParserDefinition"/>.
    /// <list type="number">
    ///   <item>Tokenizes <paramref name="grammarText"/> using the meta-grammar built by <see cref="Antlr4Grammar.Build()"/>.</item>
    ///   <item>Parses the token stream into a parse tree.</item>
    ///   <item>Converts the parse tree into a <see cref="Utils.Parser.Model.ParserDefinition"/>.</item>
    ///   <item>Resolves and validates the definition via <c>RuleResolver.Resolve</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar source (<c>.g4</c> content).</param>
    /// <returns>A fully resolved <see cref="Utils.Parser.Model.ParserDefinition"/>.</returns>
    /// <exception cref="GrammarParseException">
    /// Thrown when the input cannot be parsed as a valid ANTLR4 grammar.
    /// </exception>
    public static ParserDefinition Parse(string grammarText)
    {
        var metaDefinition = RuleResolver.Resolve(Antlr4Grammar.Build());
        var lexer = new LexerEngine(metaDefinition);
        var stream = new StringCharStream(grammarText);
        var tokens = lexer.Tokenize(stream)
            .Where(t => t.RuleName is not ("WS" or "LINE_COMMENT" or "BLOCK_COMMENT" or "DOC_COMMENT"))
            .ToList();

        // Add a sentinel EOF token: grammarSpec ends with Ref("EOF") but the lexer never
        // emits an EOF token (Lit("") produces length=0 which is rejected by TryMatchRule).
        tokens.Add(new Token(new SourceSpan(grammarText.Length, 0), "EOF", "DEFAULT_MODE", ""));

        var parser = new ParserEngine(metaDefinition);
        var root = parser.Parse(tokens);

        var converter = new Antlr4GrammarConverter(grammarText);
        return converter.Convert(root);
    }

    /// <summary>
    /// Converts a parse tree whose root is a <c>grammarSpec</c> node into a
    /// fully-resolved <see cref="Utils.Parser.Model.ParserDefinition"/>.
    /// </summary>
    /// <param name="root">Root node returned by <see cref="Utils.Parser.Runtime.ParserEngine.Parse"/>.</param>
    /// <returns>A resolved <see cref="Utils.Parser.Model.ParserDefinition"/>.</returns>
    /// <exception cref="GrammarParseException">
    /// Thrown when <paramref name="root"/> is not a <c>grammarSpec</c> parser node.
    /// </exception>
    public ParserDefinition Convert(ParseNode root)
    {
        if (root is not ParserNode grammarSpec || grammarSpec.Rule?.Name != "grammarSpec")
            throw new GrammarParseException(
                $"Root must be a grammarSpec node, got: {root.Rule?.Name ?? "(null)"}");

        _order = 0;

        var declNode = Require(First(grammarSpec, "grammarDecl"), "Missing grammarDecl");
        var (grammarName, grammarType) = ConvertGrammarDecl(declNode);

        GrammarOptions? options = null;
        var imports = new List<GrammarImport>();
        var actions = new List<GrammarAction>();
        foreach (var prequel in All(grammarSpec, "prequelConstruct"))
            ProcessPrequelConstruct(prequel, ref options, imports, actions);

        var rulesNode = First(grammarSpec, "rules");
        var (lexerRules, parserRules) = rulesNode != null
            ? ConvertRules(rulesNode)
            : (new List<Rule>(), new List<Rule>());

        var extraModes = All(grammarSpec, "modeSpec")
            .Select(ConvertModeSpec)
            .ToList();

        var allModes = new List<LexerMode> { new LexerMode("DEFAULT_MODE", lexerRules) };
        allModes.AddRange(extraModes);

        var rootRule = parserRules.FirstOrDefault();

        var definition = new ParserDefinition(
            Name: grammarName,
            Type: grammarType,
            Options: options,
            Actions: actions,
            Imports: imports,
            Modes: allModes,
            ParserRules: parserRules,
            RootRule: rootRule
        );

        return RuleResolver.Resolve(definition);
    }

    // ─── grammarDecl / grammarType ────────────────────────────────────────────

    /// <summary>
    /// Extracts the grammar name and type from a <c>grammarDecl</c> node.
    /// </summary>
    private static (string Name, GrammarType Type) ConvertGrammarDecl(ParserNode node)
    {
        var typeNode = Require(First(node, "grammarType"), "Missing grammarType in grammarDecl");
        var grammarType = ConvertGrammarType(typeNode);

        var identNode = Require(First(node, "identifier"), "Missing identifier in grammarDecl");
        var name = GetIdentifierText(identNode);

        return (name, grammarType);
    }

    /// <summary>Returns the <see cref="GrammarType"/> encoded in a <c>grammarType</c> node.</summary>
    private static GrammarType ConvertGrammarType(ParserNode node)
    {
        if (HasToken(node, "LEXER")) return GrammarType.Lexer;
        if (HasToken(node, "PARSER")) return GrammarType.Parser;
        return GrammarType.Combined;
    }

    // ─── prequelConstruct ────────────────────────────────────────────────────

    /// <summary>
    /// Processes a single <c>prequelConstruct</c> node (options, imports, or top-level action)
    /// and populates the corresponding output collection.
    /// </summary>
    private static void ProcessPrequelConstruct(
        ParserNode node,
        ref GrammarOptions? options,
        List<GrammarImport> imports,
        List<GrammarAction> actions)
    {
        var optSpec = First(node, "optionsSpec");
        if (optSpec != null) { options = ConvertOptionsSpec(optSpec); return; }

        var delegGrammars = First(node, "delegateGrammars");
        if (delegGrammars != null) { imports.AddRange(ConvertDelegateGrammars(delegGrammars)); return; }

        var actionNode = First(node, "action_");
        if (actionNode != null)
        {
            var ga = ConvertGrammarAction(actionNode);
            if (ga != null) actions.Add(ga);
        }
    }

    /// <summary>Converts an <c>optionsSpec</c> node into a <see cref="GrammarOptions"/> record.</summary>
    private static GrammarOptions ConvertOptionsSpec(ParserNode node)
    {
        var values = new Dictionary<string, string>();
        foreach (var option in All(node, "option"))
        {
            var identNode = First(option, "identifier");
            var key = identNode != null ? GetIdentifierText(identNode) : "";
            var valueNode = First(option, "optionValue");
            var value = valueNode != null ? GetOptionValueText(valueNode) : "";
            values[key] = value;
        }
        return new GrammarOptions(values);
    }

    /// <summary>Yields <see cref="GrammarImport"/> records from a <c>delegateGrammars</c> node.</summary>
    private static IEnumerable<GrammarImport> ConvertDelegateGrammars(ParserNode node)
    {
        foreach (var dg in All(node, "delegateGrammar"))
        {
            var idents = FlatChildren(dg)
                .OfType<ParserNode>()
                .Where(c => c.Rule?.Name == "identifier")
                .ToList();
            if (idents.Count >= 2)
                yield return new GrammarImport(GetIdentifierText(idents[1]), GetIdentifierText(idents[0]));
            else if (idents.Count == 1)
                yield return new GrammarImport(GetIdentifierText(idents[0]));
        }
    }

    /// <summary>Converts an <c>action_</c> node into a <see cref="GrammarAction"/>, or <c>null</c> when the node lacks an identifier.</summary>
    private static GrammarAction? ConvertGrammarAction(ParserNode node)
    {
        var identNode = First(node, "identifier");
        if (identNode == null) return null;
        var name = GetIdentifierText(identNode);

        var scopeNode = First(node, "actionScopeName");
        string? target = scopeNode != null ? TryGetIdentifierText(scopeNode) : null;

        var actionBlock = First(node, "actionBlock");
        var code = actionBlock != null
            ? UnquoteAction(FirstToken(actionBlock, "ACTION")?.Text ?? "")
            : "";

        return new GrammarAction(name, code, target);
    }

    // ─── rules ───────────────────────────────────────────────────────────────

    /// <summary>Walks a <c>rules</c> node and returns separate lists of lexer and parser rules.</summary>
    private (List<Rule> LexerRules, List<Rule> ParserRules) ConvertRules(ParserNode rulesNode)
    {
        var lexer = new List<Rule>();
        var parser = new List<Rule>();

        foreach (var ruleSpec in All(rulesNode, "ruleSpec"))
        {
            var lexerSpec = First(ruleSpec, "lexerRuleSpec");
            if (lexerSpec != null) { lexer.Add(ConvertLexerRuleSpec(lexerSpec)); continue; }

            var parserSpec = First(ruleSpec, "parserRuleSpec");
            if (parserSpec != null) parser.Add(ConvertParserRuleSpec(parserSpec));
        }

        return (lexer, parser);
    }

    // ─── modeSpec ────────────────────────────────────────────────────────────

    /// <summary>Converts a <c>modeSpec</c> node into a <see cref="LexerMode"/>.</summary>
    private LexerMode ConvertModeSpec(ParserNode node)
    {
        var identNode = Require(First(node, "identifier"), "Missing identifier in modeSpec");
        var modeName = GetIdentifierText(identNode);
        var lexerRules = All(node, "lexerRuleSpec").Select(ConvertLexerRuleSpec).ToList();
        return new LexerMode(modeName, lexerRules);
    }

    // ─── lexerRuleSpec ────────────────────────────────────────────────────────

    /// <summary>Converts a <c>lexerRuleSpec</c> node into a lexer <see cref="Rule"/>.</summary>
    private Rule ConvertLexerRuleSpec(ParserNode node)
    {
        bool isFragment = HasToken(node, "FRAGMENT");
        var nameToken = Require(FirstToken(node, "TOKEN_REF"), "Missing TOKEN_REF in lexerRuleSpec");

        var ruleBlock = Require(First(node, "lexerRuleBlock"), "Missing lexerRuleBlock");
        var content = ConvertLexerRuleBlock(ruleBlock);

        return new Rule(nameToken.Text, _order++, isFragment, content);
    }

    /// <summary>Extracts the <see cref="Alternation"/> from a <c>lexerRuleBlock</c> node.</summary>
    private Alternation ConvertLexerRuleBlock(ParserNode node)
    {
        var altList = First(node, "lexerAltList");
        return altList != null
            ? ConvertLexerAltList(altList)
            : new Alternation([]);
    }

    /// <summary>Converts a <c>lexerAltList</c> node into an <see cref="Alternation"/>.</summary>
    private Alternation ConvertLexerAltList(ParserNode node)
    {
        var alts = All(node, "lexerAlt").ToList();
        if (alts.Count == 0)
            return new Alternation([new Alternative(0, Associativity.Left, new Sequence([]))]);

        return new Alternation(alts
            .Select((a, i) => new Alternative(i, Associativity.Left, ConvertLexerAlt(a)))
            .ToList());
    }

    /// <summary>Converts a <c>lexerAlt</c> node into a <see cref="RuleContent"/>, appending any trailing lexer commands.</summary>
    private RuleContent ConvertLexerAlt(ParserNode node)
    {
        var elementsNode = First(node, "lexerElements");
        RuleContent content = elementsNode != null
            ? ConvertLexerElements(elementsNode)
            : new Sequence([]);

        var commandsNode = First(node, "lexerCommands");
        if (commandsNode != null)
        {
            var cmds = ConvertLexerCommands(commandsNode).ToList();
            if (cmds.Count > 0)
            {
                IReadOnlyList<RuleContent> items = content is Sequence seq
                    ? [.. seq.Items, .. cmds.Cast<RuleContent>()]
                    : [content, .. cmds.Cast<RuleContent>()];
                content = new Sequence(items);
            }
        }

        return content;
    }

    /// <summary>Converts a <c>lexerElements</c> node into a <see cref="RuleContent"/> (a <see cref="Sequence"/> or single element).</summary>
    private RuleContent ConvertLexerElements(ParserNode node)
    {
        var elements = All(node, "lexerElement").Select(ConvertLexerElement).ToList();
        if (elements.Count == 0) return new Sequence([]);
        if (elements.Count == 1) return elements[0];
        return new Sequence(elements);
    }

    /// <summary>Converts a single <c>lexerElement</c> node into a <see cref="RuleContent"/>.</summary>
    private RuleContent ConvertLexerElement(ParserNode node)
    {
        var actionBlock = First(node, "actionBlock");
        if (actionBlock != null)
        {
            var code = UnquoteAction(FirstToken(actionBlock, "ACTION")?.Text ?? "");
            if (HasToken(node, "QUESTION"))
            {
                if (IsPrecpred(code)) return new PrecedencePredicate(ParsePrecpredLevel(code));
                return new ValidatingPredicate(code);
            }
            return new EmbeddedAction(code, ActionContext.Alternative, ActionPosition.Inline, ExtractLabels(code));
        }

        var lexerBlock = First(node, "lexerBlock");
        if (lexerBlock != null)
        {
            var content = ConvertLexerBlock(lexerBlock);
            var suffix = First(node, "ebnfSuffix");
            return suffix != null ? ApplyEbnfSuffix(suffix, content) : content;
        }

        var lexerAtom = First(node, "lexerAtom");
        if (lexerAtom != null)
        {
            var content = ConvertLexerAtom(lexerAtom);
            var suffix = First(node, "ebnfSuffix");
            return suffix != null ? ApplyEbnfSuffix(suffix, content) : content;
        }

        throw new GrammarParseException("Unknown lexerElement structure");
    }

    /// <summary>Converts a <c>lexerBlock</c> (parenthesised group) into an <see cref="Alternation"/>.</summary>
    private Alternation ConvertLexerBlock(ParserNode node)
    {
        var altList = Require(First(node, "lexerAltList"), "Missing lexerAltList in lexerBlock");
        return ConvertLexerAltList(altList);
    }

    /// <summary>Converts a <c>lexerAtom</c> node into an atomic <see cref="RuleContent"/>.</summary>
    private RuleContent ConvertLexerAtom(ParserNode node)
    {
        var charRange = First(node, "characterRange");
        if (charRange != null) return ConvertCharacterRange(charRange);

        var termDef = First(node, "terminalDef");
        if (termDef != null) return ConvertTerminalDef(termDef);

        var notSet = First(node, "notSet");
        if (notSet != null) return ConvertNotSet(notSet);

        var lexerCharSetToken = FirstToken(node, "LEXER_CHAR_SET");
        if (lexerCharSetToken != null) return ParseLexerCharSet(lexerCharSetToken.Text);

        if (First(node, "wildcard") != null) return new AnyChar();

        throw new GrammarParseException("Unknown lexerAtom structure");
    }

    /// <summary>Yields all <see cref="LexerCommand"/> records from a <c>lexerCommands</c> node.</summary>
    private IEnumerable<LexerCommand> ConvertLexerCommands(ParserNode node)
        => All(node, "lexerCommand").Select(ConvertLexerCommand);

    /// <summary>Converts a single <c>lexerCommand</c> node into a <see cref="LexerCommand"/>.</summary>
    private LexerCommand ConvertLexerCommand(ParserNode node)
    {
        var cmdName = Require(First(node, "lexerCommandName"), "Missing lexerCommandName");
        var nameText = GetLexerCommandName(cmdName);

        var cmdExpr = First(node, "lexerCommandExpr");
        var arg = cmdExpr != null ? GetLexerCommandExpr(cmdExpr) : null;

        var type = nameText switch
        {
            "skip"     => LexerCommandType.Skip,
            "more"     => LexerCommandType.More,
            "channel"  => LexerCommandType.Channel,
            "type"     => LexerCommandType.Type,
            "pushMode" => LexerCommandType.PushMode,
            "popMode"  => LexerCommandType.PopMode,
            "mode"     => LexerCommandType.Mode,
            _          => throw new GrammarParseException($"Unknown lexer command: '{nameText}'")
        };

        return new LexerCommand(type, arg);
    }

    // ─── parserRuleSpec ───────────────────────────────────────────────────────

    /// <summary>Converts a <c>parserRuleSpec</c> node into a parser <see cref="Rule"/>.</summary>
    private Rule ConvertParserRuleSpec(ParserNode node)
    {
        var name = Require(FirstToken(node, "RULE_REF"), "Missing RULE_REF in parserRuleSpec").Text;

        List<RuleReturn>? returns = null;
        var returnsNode = First(node, "ruleReturns");
        if (returnsNode != null)
        {
            var argBlock = First(returnsNode, "argActionBlock");
            if (argBlock != null)
            {
                var raw = GetArgActionBlockText(argBlock);
                if (raw.Length > 0) returns = [new RuleReturn(raw, raw)];
            }
        }

        EmbeddedAction? initAction = null, afterAction = null;
        foreach (var prequel in All(node, "rulePrequel"))
        {
            var ruleAction = First(prequel, "ruleAction");
            if (ruleAction == null) continue;

            var identNode = First(ruleAction, "identifier");
            var actionName = identNode != null ? GetIdentifierText(identNode) : "";
            var actionBlock = First(ruleAction, "actionBlock");
            if (actionBlock == null) continue;

            var code = UnquoteAction(FirstToken(actionBlock, "ACTION")?.Text ?? "");
            var pos = actionName == "init" ? ActionPosition.Before : ActionPosition.After;
            var action = new EmbeddedAction(code, ActionContext.Rule, pos, []);

            if (actionName == "init") initAction = action;
            else if (actionName == "after") afterAction = action;
        }

        var ruleBlock = Require(First(node, "ruleBlock"), "Missing ruleBlock");
        var ruleAltList = Require(First(ruleBlock, "ruleAltList"), "Missing ruleAltList");
        var content = ConvertRuleAltList(ruleAltList);

        return new Rule(name, _order++, false, content, null, null, returns, initAction, afterAction);
    }

    // ─── ruleAltList / labeledAlt / alternative ───────────────────────────────

    /// <summary>Converts a <c>ruleAltList</c> node into an <see cref="Alternation"/>.</summary>
    private Alternation ConvertRuleAltList(ParserNode node)
    {
        var labeledAlts = All(node, "labeledAlt").ToList();
        return new Alternation(labeledAlts
            .Select((la, i) => ConvertLabeledAlt(la, i))
            .ToList());
    }

    /// <summary>Converts a <c>labeledAlt</c> node (optionally carrying a <c># Label</c>) into an <see cref="Alternative"/>.</summary>
    private Alternative ConvertLabeledAlt(ParserNode node, int index)
    {
        var altNode = Require(First(node, "alternative"), "Missing alternative in labeledAlt");
        var content = ConvertAlternative(altNode);

        string? label = null;
        if (HasToken(node, "POUND"))
        {
            var identNode = First(node, "identifier");
            label = identNode != null ? GetIdentifierText(identNode) : null;
        }

        return new Alternative(index, Associativity.Left, content, label);
    }

    /// <summary>Converts an <c>alternative</c> node into a <see cref="RuleContent"/> (sequence or single element).</summary>
    private RuleContent ConvertAlternative(ParserNode node)
    {
        var elements = All(node, "element").Select(ConvertElement).ToList();
        if (elements.Count == 0) return new Sequence([]);
        if (elements.Count == 1) return elements[0];
        return new Sequence(elements);
    }

    /// <summary>Converts an <c>altList</c> node (inside a parser block) into an <see cref="Alternation"/>.</summary>
    private Alternation ConvertAltList(ParserNode node)
    {
        var alts = All(node, "alternative").ToList();
        return new Alternation(alts
            .Select((a, i) => new Alternative(i, Associativity.Left, ConvertAlternative(a)))
            .ToList());
    }

    // ─── element ─────────────────────────────────────────────────────────────

    /// <summary>Converts a parser <c>element</c> node into a <see cref="RuleContent"/>.</summary>
    private RuleContent ConvertElement(ParserNode node)
    {
        var actionBlock = First(node, "actionBlock");
        if (actionBlock != null)
        {
            var code = UnquoteAction(FirstToken(actionBlock, "ACTION")?.Text ?? "");
            if (HasToken(node, "QUESTION"))
            {
                if (IsPrecpred(code)) return new PrecedencePredicate(ParsePrecpredLevel(code));
                return new ValidatingPredicate(code);
            }
            return new EmbeddedAction(code, ActionContext.Alternative, ActionPosition.Inline, ExtractLabels(code));
        }

        var ebnf = First(node, "ebnf");
        if (ebnf != null) return ConvertEbnf(ebnf);

        var labeled = First(node, "labeledElement");
        if (labeled != null)
        {
            var content = ConvertLabeledElement(labeled);
            var suffix = First(node, "ebnfSuffix");
            return suffix != null ? ApplyEbnfSuffix(suffix, content) : content;
        }

        var atom = First(node, "atom");
        if (atom != null)
        {
            var content = ConvertAtom(atom);
            var suffix = First(node, "ebnfSuffix");
            return suffix != null ? ApplyEbnfSuffix(suffix, content) : content;
        }

        throw new GrammarParseException("Unknown element structure");
    }

    /// <summary>Converts a <c>labeledElement</c> node (e.g. <c>e=sub</c> or <c>ids+=sub</c>) into a labeled <see cref="RuleRef"/>.</summary>
    private RuleContent ConvertLabeledElement(ParserNode node)
    {
        var identNode = Require(First(node, "identifier"), "Missing identifier in labeledElement");
        var labelName = GetIdentifierText(identNode);
        bool isAdditive = HasToken(node, "PLUS_ASSIGN");

        var atom = First(node, "atom");
        if (atom != null)
        {
            var content = ConvertAtom(atom);
            if (content is RuleRef ruleRef)
                return ruleRef with { Label = new RuleLabel(labelName, ruleRef.RuleName, isAdditive) };
            return content; // label on non-ref content: ignored
        }

        var block = First(node, "block");
        if (block != null) return ConvertBlock(block);

        throw new GrammarParseException("Unknown labeledElement content");
    }

    /// <summary>Converts an <c>ebnf</c> node (a block with optional quantifier suffix) into a <see cref="RuleContent"/>.</summary>
    private RuleContent ConvertEbnf(ParserNode node)
    {
        var block = Require(First(node, "block"), "Missing block in ebnf");
        var content = ConvertBlock(block);

        var blockSuffix = First(node, "blockSuffix");
        if (blockSuffix != null)
        {
            var ebnfSuffix = First(blockSuffix, "ebnfSuffix");
            if (ebnfSuffix != null) return ApplyEbnfSuffix(ebnfSuffix, content);
        }

        return content;
    }

    /// <summary>Converts a parenthesised parser <c>block</c> into an <see cref="Alternation"/>.</summary>
    private Alternation ConvertBlock(ParserNode node)
    {
        var altList = Require(First(node, "altList"), "Missing altList in block");
        return ConvertAltList(altList);
    }

    // ─── atom ────────────────────────────────────────────────────────────────

    /// <summary>Converts a parser <c>atom</c> node into an atomic <see cref="RuleContent"/>.</summary>
    private RuleContent ConvertAtom(ParserNode node)
    {
        var termDef = First(node, "terminalDef");
        if (termDef != null) return ConvertTerminalDef(termDef);

        var ruleref = First(node, "ruleref");
        if (ruleref != null) return ConvertRuleRef(ruleref);

        var notSet = First(node, "notSet");
        if (notSet != null) return ConvertNotSet(notSet);

        if (First(node, "wildcard") != null) return new AnyChar();

        throw new GrammarParseException("Unknown atom structure");
    }

    /// <summary>Converts a <c>ruleref</c> node into a <see cref="RuleRef"/>.</summary>
    private static RuleRef ConvertRuleRef(ParserNode node)
    {
        var name = Require(FirstToken(node, "RULE_REF"), "Missing RULE_REF in ruleref").Text;
        return new RuleRef(name);
    }

    /// <summary>Converts a <c>terminalDef</c> node into a <see cref="RuleRef"/> (token reference) or <see cref="LiteralMatch"/>.</summary>
    private static RuleContent ConvertTerminalDef(ParserNode node)
    {
        var tokenRef = FirstToken(node, "TOKEN_REF");
        if (tokenRef != null) return new RuleRef(tokenRef.Text);

        var strLit = FirstToken(node, "STRING_LITERAL");
        if (strLit != null) return new LiteralMatch(UnquoteString(strLit.Text));

        throw new GrammarParseException("Unknown terminalDef structure");
    }

    /// <summary>Converts a <c>characterRange</c> node (<c>'a'..'z'</c>) into a <see cref="RangeMatch"/>.</summary>
    private static RangeMatch ConvertCharacterRange(ParserNode node)
    {
        var tokens = FlatChildren(node)
            .OfType<LexerNode>()
            .Where(n => n.Rule?.Name == "STRING_LITERAL")
            .Select(n => n.Token.Text)
            .ToList();

        if (tokens.Count < 2)
            throw new GrammarParseException("characterRange requires 2 STRING_LITERALs");

        var from = UnquoteString(tokens[0]);
        var to = UnquoteString(tokens[1]);

        if (from.Length == 0 || to.Length == 0)
            throw new GrammarParseException("Empty char range boundary");

        return new RangeMatch(from[0], to[0]);
    }

    /// <summary>Converts a <c>notSet</c> node (<c>~setElement</c> or <c>~blockSet</c>) into a <see cref="Negation"/>.</summary>
    private static Negation ConvertNotSet(ParserNode node)
    {
        var setElem = First(node, "setElement");
        if (setElem != null) return new Negation(ConvertSetElement(setElem));

        var blockSet = First(node, "blockSet");
        if (blockSet != null) return new Negation(ConvertBlockSet(blockSet));

        throw new GrammarParseException("Unknown notSet content");
    }

    /// <summary>Converts a <c>setElement</c> node into a token reference, literal, range, or char-set match.</summary>
    private static RuleContent ConvertSetElement(ParserNode node)
    {
        var tokenRef = FirstToken(node, "TOKEN_REF");
        if (tokenRef != null) return new RuleRef(tokenRef.Text);

        var strLit = FirstToken(node, "STRING_LITERAL");
        if (strLit != null) return new LiteralMatch(UnquoteString(strLit.Text));

        var charRange = First(node, "characterRange");
        if (charRange != null) return ConvertCharacterRange(charRange);

        var lexerCharSet = FirstToken(node, "LEXER_CHAR_SET");
        if (lexerCharSet != null) return ParseLexerCharSet(lexerCharSet.Text);

        throw new GrammarParseException("Unknown setElement structure");
    }

    /// <summary>Converts a <c>blockSet</c> node (a parenthesised list of set elements) into an <see cref="Alternation"/>.</summary>
    private static Alternation ConvertBlockSet(ParserNode node)
    {
        var elements = All(node, "setElement").ToList();
        return new Alternation(elements
            .Select((e, i) => new Alternative(i, Associativity.Left, ConvertSetElement(e)))
            .ToList());
    }

    // ─── ebnfSuffix ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="inner"/> in a <see cref="Quantifier"/> according to
    /// the operator token(s) in <paramref name="suffixNode"/> (<c>?</c>, <c>*</c>, <c>+</c>,
    /// optionally followed by <c>?</c> for non-greedy variants).
    /// </summary>
    private static Quantifier ApplyEbnfSuffix(ParserNode suffixNode, RuleContent inner)
    {
        var tokens = FlatChildren(suffixNode).OfType<LexerNode>().ToList();
        if (tokens.Count == 0)
            throw new GrammarParseException("Empty ebnfSuffix");

        bool nonGreedy = tokens.Count > 1 && tokens[1].Rule?.Name == "QUESTION";
        bool greedy = !nonGreedy;

        return tokens[0].Rule?.Name switch
        {
            "QUESTION" => new Quantifier(inner, 0, 1, greedy),
            "STAR"     => new Quantifier(inner, 0, null, greedy),
            "PLUS"     => new Quantifier(inner, 1, null, greedy),
            var name   => throw new GrammarParseException($"Unknown ebnfSuffix token: {name}")
        };
    }

    // ─── Helper extraction methods ────────────────────────────────────────────

    /// <summary>Returns the text of the first token inside an <c>identifier</c> node.</summary>
    private static string GetIdentifierText(ParserNode node) =>
        FlatChildren(node).OfType<LexerNode>().FirstOrDefault()?.Token.Text
        ?? throw new GrammarParseException("Expected identifier, found no token");

    /// <summary>Returns the first token text inside an <c>identifier</c> node, or <c>null</c> if none.</summary>
    private static string? TryGetIdentifierText(ParserNode node) =>
        FlatChildren(node).OfType<LexerNode>().FirstOrDefault()?.Token.Text;

    /// <summary>Returns the lexer command name string from a <c>lexerCommandName</c> node.</summary>
    private static string GetLexerCommandName(ParserNode node)
    {
        var identNode = First(node, "identifier");
        if (identNode != null) return GetIdentifierText(identNode);

        var modeToken = FirstToken(node, "MODE");
        if (modeToken != null) return "mode";

        throw new GrammarParseException("Unknown lexerCommandName");
    }

    /// <summary>Returns the lexer command argument string from a <c>lexerCommandExpr</c> node.</summary>
    private static string GetLexerCommandExpr(ParserNode node)
    {
        var identNode = First(node, "identifier");
        if (identNode != null) return GetIdentifierText(identNode);

        var intToken = FirstToken(node, "INT");
        if (intToken != null) return intToken.Text;

        throw new GrammarParseException("Unknown lexerCommandExpr");
    }

    /// <summary>Concatenates all <c>ARGUMENT_CONTENT</c> token texts inside an <c>argActionBlock</c> node.</summary>
    private static string GetArgActionBlockText(ParserNode node) =>
        string.Concat(FlatChildren(node)
            .OfType<LexerNode>()
            .Where(n => n.Rule?.Name == "ARGUMENT_CONTENT")
            .Select(n => n.Token.Text))
        .Trim();

    /// <summary>Extracts the string representation of an option value from an <c>optionValue</c> node.</summary>
    private static string GetOptionValueText(ParserNode node)
    {
        var idents = All(node, "identifier").ToList();
        if (idents.Count > 0)
            return string.Join(".", idents.Select(GetIdentifierText));

        var strLit = FirstToken(node, "STRING_LITERAL");
        if (strLit != null) return UnquoteString(strLit.Text);

        var intTok = FirstToken(node, "INT");
        if (intTok != null) return intTok.Text;

        var actionBlock = First(node, "actionBlock");
        if (actionBlock != null)
            return UnquoteAction(FirstToken(actionBlock, "ACTION")?.Text ?? "");

        return "";
    }

    // ─── String utilities ─────────────────────────────────────────────────────

    /// <summary>
    /// Strips the surrounding single quotes from an ANTLR4 string literal and
    /// resolves all recognised escape sequences (<c>\n</c>, <c>\t</c>, <c>\uXXXX</c>, etc.).
    /// </summary>
    private static string UnquoteString(string text)
    {
        if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'')
            text = text[1..^1];

        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '\\') { sb.Append(text[i++]); continue; }

            i++; // skip backslash
            if (i >= text.Length) { sb.Append('\\'); break; }

            char esc = text[i++];
            switch (esc)
            {
                case 'n':  sb.Append('\n'); break;
                case 'r':  sb.Append('\r'); break;
                case 't':  sb.Append('\t'); break;
                case 'f':  sb.Append('\f'); break;
                case 'b':  sb.Append('\b'); break;
                case '\\': sb.Append('\\'); break;
                case '\'': sb.Append('\''); break;
                case '"':  sb.Append('"');  break;
                case 'u' when i + 3 < text.Length:
                    sb.Append((char)System.Convert.ToInt32(text.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: sb.Append(esc); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses an ANTLR4 lexer char set literal such as [a-zA-Z_0-9] or [^0-9].
    /// </summary>
    internal static RuleContent ParseLexerCharSet(string text)
    {
        if (text.Length >= 1 && text[0] == '[') text = text[1..];
        if (text.Length >= 1 && text[^1] == ']') text = text[..^1];

        bool negated = false;
        int i = 0;
        if (i < text.Length && text[i] == '^') { negated = true; i++; }

        var chars = new HashSet<char>();
        while (i < text.Length)
        {
            char c = ReadCharSetChar(text, ref i);
            if (i < text.Length && text[i] == '-' && i + 1 < text.Length && text[i + 1] != ']')
            {
                i++; // consume '-'
                char c2 = ReadCharSetChar(text, ref i);
                for (char r = c; r <= c2; r++) chars.Add(r);
            }
            else
            {
                chars.Add(c);
            }
        }

        return new CharSetMatch(chars, negated);
    }

    /// <summary>
    /// Reads and returns a single (possibly escaped) character from a lexer char-set string,
    /// advancing <paramref name="i"/> past the consumed character(s).
    /// </summary>
    private static char ReadCharSetChar(string text, ref int i)
    {
        if (i >= text.Length) throw new GrammarParseException("Unexpected end of char set");
        if (text[i] != '\\') return text[i++];

        i++; // skip backslash
        if (i >= text.Length) return '\\';

        char esc = text[i++];
        if (esc == 'u' && i + 3 < text.Length)
        {
            char result = (char)System.Convert.ToInt32(text.Substring(i, 4), 16);
            i += 4;
            return result;
        }

        return esc switch
        {
            'n'  => '\n',
            'r'  => '\r',
            't'  => '\t',
            'f'  => '\f',
            'b'  => '\b',
            '\\' => '\\',
            ']'  => ']',
            '-'  => '-',
            _    => esc
        };
    }

    /// <summary>Strips the surrounding curly braces from an action block token and trims whitespace.</summary>
    private static string UnquoteAction(string text)
    {
        if (text.Length >= 2 && text[0] == '{' && text[^1] == '}')
            text = text[1..^1];
        return text.Trim();
    }

    /// <summary>Returns <c>true</c> when <paramref name="code"/> contains a <c>precpred()</c> call (case-insensitive).</summary>
    private static bool IsPrecpred(string code) =>
        code.Contains("precpred", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts the numeric level from a <c>precpred(_ctx, n)</c> call, returning 0 on parse failure.</summary>
    private static int ParsePrecpredLevel(string code)
    {
        var match = Regex.Match(code, @"precpred\s*\(\s*\w+\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>Scans <paramref name="code"/> for <c>$label</c> and <c>$label.property</c> references and returns them.</summary>
    private static IReadOnlyList<LabelRef> ExtractLabels(string code)
    {
        var labels = new List<LabelRef>();
        foreach (Match match in Regex.Matches(code, @"\$(\w+)(?:\.(\w+))?"))
        {
            var ruleLabel = match.Groups[1].Value;
            var property = match.Groups[2].Success ? match.Groups[2].Value : null;
            labels.Add(property is null
                ? new LabelRef(null, ruleLabel)
                : new LabelRef(ruleLabel, property));
        }
        return labels;
    }

    // ─── Tree navigation helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns the direct children of <paramref name="node"/>, transparently flattening
    /// any child <see cref="ParserNode"/> whose rule name is identical to the parent's.
    /// Such wrapper nodes are produced by quantifier matches in the parser engine and
    /// are an implementation artefact that callers should not need to handle explicitly.
    /// </summary>
    private static IEnumerable<ParseNode> FlatChildren(ParserNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is ParserNode pn && pn.Rule?.Name == node.Rule?.Name)
            {
                foreach (var gc in FlatChildren(pn))
                    yield return gc;
            }
            else
            {
                yield return child;
            }
        }
    }

    /// <summary>Returns the first direct child of <paramref name="node"/> whose rule name equals <paramref name="ruleName"/>, or <c>null</c>.</summary>
    private static ParserNode? First(ParseNode node, string ruleName)
    {
        if (node is not ParserNode pn) return null;
        return FlatChildren(pn).OfType<ParserNode>().FirstOrDefault(c => c.Rule?.Name == ruleName);
    }

    /// <summary>Returns all direct children of <paramref name="node"/> whose rule name equals <paramref name="ruleName"/>.</summary>
    private static IEnumerable<ParserNode> All(ParseNode node, string ruleName)
    {
        if (node is not ParserNode pn) return [];
        return FlatChildren(pn).OfType<ParserNode>().Where(c => c.Rule?.Name == ruleName);
    }

    /// <summary>Returns the first <see cref="Token"/> among direct <see cref="LexerNode"/> children of <paramref name="node"/> whose rule name equals <paramref name="tokenRuleName"/>, or <c>null</c>.</summary>
    private static Token? FirstToken(ParseNode node, string tokenRuleName)
    {
        if (node is not ParserNode pn) return null;
        return FlatChildren(pn)
            .OfType<LexerNode>()
            .FirstOrDefault(c => c.Rule?.Name == tokenRuleName)
            ?.Token;
    }

    /// <summary>Returns <c>true</c> when <paramref name="node"/> has at least one direct <see cref="LexerNode"/> child with rule name <paramref name="tokenRuleName"/>.</summary>
    private static bool HasToken(ParseNode node, string tokenRuleName) =>
        FirstToken(node, tokenRuleName) is not null;

    /// <summary>Returns <paramref name="value"/> when it is non-null; otherwise throws <see cref="GrammarParseException"/> with <paramref name="message"/>.</summary>
    private static T Require<T>(T? value, string message) where T : class
    {
        if (value is null) throw new GrammarParseException(message);
        return value;
    }
}
