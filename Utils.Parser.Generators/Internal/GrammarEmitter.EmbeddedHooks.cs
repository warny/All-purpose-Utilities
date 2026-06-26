using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Collects rule lifecycle (<c>@init</c> and <c>@after</c>) hooks from parser rules.
    /// Only parser rules are considered; lexer rule lifecycle hooks are not generated.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="transformer">Parser embedded-code transformer used for lifecycle action bodies.</param>
    /// <returns>Deterministic lifecycle hook metadata for parser rules.</returns>
    private static IReadOnlyList<LifecycleHook> CollectLifecycleHooks(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
    {
        var hooks = new List<LifecycleHook>();
        foreach (var rule in grammar.ParserRules)
        {
            if (rule.InitAction is not null)
            {
                string code = TransformEmbeddedCode(transformer, rule.InitAction.Code, ParserEmbeddedCodeLocation.RuleInit, grammar, rule);
                hooks.Add(new LifecycleHook(rule.Name, code, isInit: true, $"__Init_{Sanitize(rule.Name)}"));
            }

            if (rule.AfterAction is not null)
            {
                string code = TransformEmbeddedCode(transformer, rule.AfterAction.Code, ParserEmbeddedCodeLocation.RuleAfter, grammar, rule);
                hooks.Add(new LifecycleHook(rule.Name, code, isInit: false, $"__After_{Sanitize(rule.Name)}"));
            }
        }

        return hooks;
    }


    /// <summary>
    /// Collects executable lexer inline action and predicate hooks for the generated C# opt-in path.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="transformer">Embedded-code transformer used for supported lexer action bodies.</param>
    /// <returns>Deterministic lexer action hook metadata.</returns>
    private static IReadOnlyList<LexerEmbeddedCodeHook> CollectLexerEmbeddedCodeHooks(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
    {
        var hooks = new List<LexerEmbeddedCodeHook>();
        foreach (var rule in grammar.LexerRules)
        {
            CollectLexerEmbeddedCodeHooks(rule.Name, rule.Content, hooks, -1, -1);
        }

        foreach (var mode in grammar.ExtraModes)
        {
            foreach (var rule in mode.Rules)
            {
                CollectLexerEmbeddedCodeHooks(rule.Name, rule.Content, hooks, -1, -1);
            }
        }

        foreach (var hook in hooks)
        {
            hook.EmittedCode = TransformEmbeddedCode(transformer, hook.Code, hook.IsPredicate ? ParserEmbeddedCodeLocation.SemanticPredicate : ParserEmbeddedCodeLocation.LexerInlineAction, grammar, null);
        }

        return hooks;
    }

    /// <summary>Recursively collects lexer inline actions from lexer rule content.</summary>
    private static void CollectLexerEmbeddedCodeHooks(string ruleName, G4Content content, List<LexerEmbeddedCodeHook> hooks, int alternativeIndex, int elementIndex)
    {
        switch (content)
        {
            case G4Alternation alternation:
                for (int index = 0; index < alternation.Alternatives.Count; index++)
                {
                    CollectLexerEmbeddedCodeHooks(ruleName, alternation.Alternatives[index], hooks, index, -1);
                }
                break;
            case G4Alternative alternative:
                for (int index = 0; index < alternative.Items.Count; index++)
                {
                    CollectLexerEmbeddedCodeHooks(ruleName, alternative.Items[index], hooks, alternativeIndex, index);
                }
                break;
            case G4Sequence sequence:
                for (int index = 0; index < sequence.Items.Count; index++)
                {
                    CollectLexerEmbeddedCodeHooks(ruleName, sequence.Items[index], hooks, alternativeIndex, index);
                }
                break;
            case G4Quantifier quantifier:
                CollectLexerEmbeddedCodeHooks(ruleName, quantifier.Inner, hooks, alternativeIndex, elementIndex);
                break;
            case G4Negation negation:
                CollectLexerEmbeddedCodeHooks(ruleName, negation.Inner, hooks, alternativeIndex, elementIndex);
                break;
            case G4EmbeddedAction action:
                string prefix = action.IsPredicate ? "__LexerPredicate" : "__LexerAction";
                string methodName = $"{prefix}_{Sanitize(ruleName)}_{NormalizeIndexForName(alternativeIndex)}_{NormalizeIndexForName(elementIndex)}_{hooks.Count}";
                hooks.Add(new LexerEmbeddedCodeHook(ruleName, action.Code, action.IsPredicate, alternativeIndex, elementIndex, methodName));
                break;
        }
    }

    /// <summary>
    /// Collects parser-rule embedded predicates and inline actions using indexes aligned with the parser runtime.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="transformer">Parser embedded-code transformer used for inline action and predicate bodies.</param>
    /// <returns>Deterministic hook metadata for embedded parser code.</returns>
    private static IReadOnlyList<EmbeddedCodeHook> CollectEmbeddedCodeHooks(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
    {
        var hooks = new List<EmbeddedCodeHook>();
        foreach (var rule in grammar.ParserRules)
        {
            int firstHookIndex = hooks.Count;
            CollectRuleEmbeddedCodeHooks(rule, hooks);
            for (int index = firstHookIndex; index < hooks.Count; index++)
            {
                EmbeddedCodeHook hook = hooks[index];
                hook.EmittedCode = TransformEmbeddedCode(transformer, hook.Code, hook.IsPredicate ? ParserEmbeddedCodeLocation.SemanticPredicate : ParserEmbeddedCodeLocation.InlineAction, grammar, rule);
            }
        }

        return hooks;
    }

    /// <summary>
    /// Collects embedded-code hooks from a parser rule using the same alternative split that
    /// <c>ParserEngine</c> applies to direct-left-recursive rules.
    /// </summary>
    /// <param name="rule">Parser rule to inspect.</param>
    /// <param name="hooks">Destination hook collection.</param>
    private static void CollectRuleEmbeddedCodeHooks(G4Rule rule, List<EmbeddedCodeHook> hooks)
    {
        var orderedAlternatives = rule.Content.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
        var recursiveAlternatives = orderedAlternatives.Where(alternative => StartsWithRuleRef(alternative, rule.Name)).ToList();
        if (recursiveAlternatives.Count == 0)
        {
            CollectEmbeddedCodeHooks(rule.Name, rule.Content, hooks, -1, -1);
            return;
        }

        var baseAlternatives = orderedAlternatives.Where(alternative => !StartsWithRuleRef(alternative, rule.Name)).ToList();
        for (int index = 0; index < baseAlternatives.Count; index++)
        {
            CollectEmbeddedCodeHooks(rule.Name, baseAlternatives[index], hooks, index, -1);
        }

        for (int index = 0; index < recursiveAlternatives.Count; index++)
        {
            CollectLeftRecursiveTailEmbeddedCodeHooks(rule.Name, recursiveAlternatives[index], hooks, index);
        }
    }

    /// <summary>
    /// Recursively collects embedded-code hooks from a grammar element using runtime-compatible alternative and element indexes.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="content">Grammar content to inspect.</param>
    /// <param name="hooks">Destination hook collection.</param>
    /// <param name="alternativeIndex">Current runtime alternative index, or <c>-1</c> when unavailable.</param>
    /// <param name="elementIndex">Current runtime element index, or <c>-1</c> when unavailable.</param>
    private static void CollectEmbeddedCodeHooks(string ruleName, G4Content content, List<EmbeddedCodeHook> hooks, int alternativeIndex, int elementIndex)
    {
        switch (content)
        {
            case G4Alternation alternation:
                var orderedAlternatives = alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
                for (int index = 0; index < orderedAlternatives.Count; index++)
                {
                    CollectEmbeddedCodeHooks(ruleName, orderedAlternatives[index], hooks, index, -1);
                }
                break;
            case G4Alternative alternative:
                CollectAlternativeEmbeddedCodeHooks(ruleName, alternative, hooks, alternativeIndex);
                break;
            case G4Sequence sequence:
                CollectSequenceEmbeddedCodeHooks(ruleName, sequence.Items, hooks, alternativeIndex);
                break;
            case G4Quantifier quantifier:
                // ParserEngine reparses quantified content with the current alternative index
                // as the inner element index, rather than with the quantifier parent's
                // sequence position. The generated dispatcher must mirror that runtime key.
                CollectEmbeddedCodeHooks(ruleName, quantifier.Inner, hooks, alternativeIndex, alternativeIndex);
                break;
            case G4Negation negation:
                // ParserEngine probes negated content with the current alternative index
                // as the inner element index. This preserves dispatch for predicates that
                // are evaluated during the negation probe.
                CollectEmbeddedCodeHooks(ruleName, negation.Inner, hooks, alternativeIndex, alternativeIndex);
                break;
            case G4EmbeddedAction action:
                AddEmbeddedCodeHook(ruleName, action, hooks, alternativeIndex, elementIndex);
                break;
        }
    }

    /// <summary>
    /// Collects hooks from one alternative, mirroring whether the emitter creates a sequence wrapper.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="alternative">Alternative to inspect.</param>
    /// <param name="hooks">Destination hook collection.</param>
    /// <param name="alternativeIndex">Current runtime alternative index.</param>
    private static void CollectAlternativeEmbeddedCodeHooks(string ruleName, G4Alternative alternative, List<EmbeddedCodeHook> hooks, int alternativeIndex)
    {
        if (alternative.Items.Count == 1 && alternative.Items[0] is not G4Sequence)
        {
            CollectEmbeddedCodeHooks(ruleName, alternative.Items[0], hooks, alternativeIndex, -1);
            return;
        }

        CollectSequenceEmbeddedCodeHooks(ruleName, alternative.Items, hooks, alternativeIndex);
    }

    /// <summary>
    /// Collects hooks from a generated sequence and assigns each item its zero-based runtime element index.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="items">Sequence items to inspect.</param>
    /// <param name="hooks">Destination hook collection.</param>
    /// <param name="alternativeIndex">Current runtime alternative index.</param>
    private static void CollectSequenceEmbeddedCodeHooks(string ruleName, IReadOnlyList<G4Content> items, List<EmbeddedCodeHook> hooks, int alternativeIndex)
    {
        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            CollectEmbeddedCodeHooks(ruleName, items[itemIndex], hooks, alternativeIndex, itemIndex);
        }
    }


    /// <summary>
    /// Collects hooks from a direct-left-recursive alternative after removing the leading
    /// self-reference, matching the runtime tail view used by <c>ParserEngine</c>.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="alternative">Direct-left-recursive source alternative.</param>
    /// <param name="hooks">Destination hook collection.</param>
    /// <param name="alternativeIndex">Runtime index inside the recursive-alternative set.</param>
    private static void CollectLeftRecursiveTailEmbeddedCodeHooks(string ruleName, G4Alternative alternative, List<EmbeddedCodeHook> hooks, int alternativeIndex)
    {
        if (alternative.Items.Count == 0 || !IsRuleRef(alternative.Items[0], ruleName))
        {
            return;
        }

        var tailItems = alternative.Items.Skip(1).ToList();
        if (tailItems.Count == 1 && tailItems[0] is not G4Sequence)
        {
            CollectEmbeddedCodeHooks(ruleName, tailItems[0], hooks, alternativeIndex, -1);
            return;
        }

        CollectSequenceEmbeddedCodeHooks(ruleName, tailItems, hooks, alternativeIndex);
    }

    /// <summary>
    /// Determines whether an alternative begins with a direct reference to the supplied rule.
    /// </summary>
    /// <param name="alternative">Alternative to inspect.</param>
    /// <param name="ruleName">Rule name expected as the leading reference.</param>
    /// <returns><see langword="true"/> when the first item is a direct self-reference.</returns>
    private static bool StartsWithRuleRef(G4Alternative alternative, string ruleName)
    {
        return alternative.Items.Count > 0 && IsRuleRef(alternative.Items[0], ruleName);
    }

    /// <summary>
    /// Determines whether a grammar content item is a rule reference to the supplied rule.
    /// </summary>
    /// <param name="content">Content item to inspect.</param>
    /// <param name="ruleName">Expected rule name.</param>
    /// <returns><see langword="true"/> when the content is a matching rule reference.</returns>
    private static bool IsRuleRef(G4Content content, string ruleName)
    {
        return content is G4RuleRef ruleRef && string.Equals(ruleRef.RuleName, ruleName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Adds one embedded-code hook with a deterministic, collision-resistant method name.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="action">Embedded action or predicate AST node.</param>
    /// <param name="hooks">Destination hook collection.</param>
    /// <param name="alternativeIndex">Runtime alternative index.</param>
    /// <param name="elementIndex">Runtime element index.</param>
    private static void AddEmbeddedCodeHook(string ruleName, G4EmbeddedAction action, List<EmbeddedCodeHook> hooks, int alternativeIndex, int elementIndex)
    {
        string prefix = action.IsPredicate ? "__Predicate" : "__Action";
        string methodName = $"{prefix}_{Sanitize(ruleName)}_{NormalizeIndexForName(alternativeIndex)}_{NormalizeIndexForName(elementIndex)}_{hooks.Count}";
        hooks.Add(new EmbeddedCodeHook(ruleName, action.Code, action.IsPredicate, alternativeIndex, elementIndex, methodName));
    }

    /// <summary>
    /// Converts runtime indexes into stable identifier fragments.
    /// </summary>
    /// <param name="index">Runtime index value.</param>
    /// <returns>Identifier-safe index fragment.</returns>
    private static string NormalizeIndexForName(int index)
    {
        return index < 0 ? "m1" : index.ToString();
    }


    /// <summary>
    /// Metadata for one generated lexer inline action hook.
    /// </summary>
    private sealed class LexerEmbeddedCodeHook
    {
        /// <summary>Initializes lexer embedded-code hook metadata.</summary>
        public LexerEmbeddedCodeHook(string ruleName, string code, bool isPredicate, int alternativeIndex, int elementIndex, string methodName)
        {
            RuleName = ruleName;
            Code = code;
            EmittedCode = code;
            IsPredicate = isPredicate;
            AlternativeIndex = alternativeIndex;
            ElementIndex = elementIndex;
            MethodName = methodName;
        }

        /// <summary>Gets the owning lexer rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets the raw embedded source code without ANTLR braces.</summary>
        public string Code { get; }

        /// <summary>Gets or sets the rewritten C# source emitted into the hook method.</summary>
        public string EmittedCode { get; set; }

        /// <summary>Gets whether this hook is a lexer predicate rather than a lexer action.</summary>
        public bool IsPredicate { get; }

        /// <summary>Gets the best-effort alternative index.</summary>
        public int AlternativeIndex { get; }

        /// <summary>Gets the best-effort element index.</summary>
        public int ElementIndex { get; }

        /// <summary>Gets the generated C# hook method name.</summary>
        public string MethodName { get; }
    }

    /// <summary>
    /// Metadata for one generated embedded-code hook.
    /// </summary>
    private sealed class EmbeddedCodeHook
    {
        /// <summary>
        /// Initializes embedded-code hook metadata.
        /// </summary>
        /// <param name="ruleName">Owning parser rule name.</param>
        /// <param name="code">Raw embedded source code without ANTLR braces.</param>
        /// <param name="isPredicate">Whether the hook is a semantic predicate.</param>
        /// <param name="alternativeIndex">Runtime alternative index used for dispatch.</param>
        /// <param name="elementIndex">Runtime element index used for dispatch.</param>
        /// <param name="methodName">Generated C# hook method name.</param>
        public EmbeddedCodeHook(string ruleName, string code, bool isPredicate, int alternativeIndex, int elementIndex, string methodName)
        {
            RuleName = ruleName;
            Code = code;
            EmittedCode = code;
            IsPredicate = isPredicate;
            AlternativeIndex = alternativeIndex;
            ElementIndex = elementIndex;
            MethodName = methodName;
        }

        /// <summary>Gets the owning parser rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets the raw embedded source code without ANTLR braces.</summary>
        public string Code { get; }

        /// <summary>Gets or sets the rewritten C# source emitted into the hook method.</summary>
        public string EmittedCode { get; set; }

        /// <summary>Gets a value indicating whether the hook is a semantic predicate.</summary>
        public bool IsPredicate { get; }

        /// <summary>Gets the runtime alternative index used for dispatch.</summary>
        public int AlternativeIndex { get; }

        /// <summary>Gets the runtime element index used for dispatch.</summary>
        public int ElementIndex { get; }

        /// <summary>Gets the generated C# hook method name.</summary>
        public string MethodName { get; }
    }

    /// <summary>
    /// Metadata for one generated rule lifecycle hook (<c>@init</c> or <c>@after</c>).
    /// </summary>
    private sealed class LifecycleHook
    {
        /// <summary>
        /// Initializes lifecycle hook metadata.
        /// </summary>
        /// <param name="ruleName">Owning parser rule name.</param>
        /// <param name="code">Raw lifecycle action body without ANTLR braces.</param>
        /// <param name="isInit"><see langword="true"/> for <c>@init</c>; <see langword="false"/> for <c>@after</c>.</param>
        /// <param name="methodName">Generated C# hook method name.</param>
        public LifecycleHook(string ruleName, string code, bool isInit, string methodName)
        {
            RuleName = ruleName;
            Code = code;
            IsInit = isInit;
            MethodName = methodName;
        }

        /// <summary>Gets the owning parser rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets the raw lifecycle action body without ANTLR braces.</summary>
        public string Code { get; }

        /// <summary>Gets a value indicating whether this is an <c>@init</c> hook (<see langword="false"/> means <c>@after</c>).</summary>
        public bool IsInit { get; }

        /// <summary>Gets the generated C# hook method name.</summary>
        public string MethodName { get; }
    }
}
