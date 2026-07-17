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
                TransformedEmbeddedCode code = TransformEmbeddedCode(transformer, new RawEmbeddedCode(rule.InitAction.Code), ParserEmbeddedCodeLocation.RuleInit, grammar, rule);
                hooks.Add(new LifecycleHook(rule.Name, code, isInit: true, $"__Init_{Sanitize(rule.Name)}"));
            }

            if (rule.AfterAction is not null)
            {
                TransformedEmbeddedCode code = TransformEmbeddedCode(transformer, new RawEmbeddedCode(rule.AfterAction.Code), ParserEmbeddedCodeLocation.RuleAfter, grammar, rule);
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
    private static IReadOnlyList<EmbeddedCodeHook> CollectLexerEmbeddedCodeHooks(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
    {
        return EmbeddedHookCollector.Collect(grammar, transformer, LexerEmbeddedHookCollectionStrategy.Instance);
    }

    /// <summary>
    /// Collects parser-rule embedded predicates and inline actions using indexes aligned with the parser runtime.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="transformer">Parser embedded-code transformer used for inline action and predicate bodies.</param>
    /// <returns>Deterministic hook metadata for embedded parser code.</returns>
    private static IReadOnlyList<EmbeddedCodeHook> CollectEmbeddedCodeHooks(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
    {
        return EmbeddedHookCollector.Collect(grammar, transformer, ParserEmbeddedHookCollectionStrategy.Instance);
    }

    /// <summary>
    /// Common embedded-code hook collection engine for parser and lexer generated-C# hooks.
    /// </summary>
    private sealed class EmbeddedHookCollector
    {
        private readonly IEmbeddedHookCollectionStrategy _strategy;

        /// <summary>
        /// Initializes the collector with the supplied parser or lexer strategy.
        /// </summary>
        /// <param name="strategy">Strategy that owns parser- or lexer-specific traversal differences.</param>
        private EmbeddedHookCollector(IEmbeddedHookCollectionStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>
        /// Collects, orders, and transforms embedded-code hooks using the shared traversal algorithm.
        /// </summary>
        /// <param name="grammar">Parsed grammar AST.</param>
        /// <param name="transformer">Embedded-code transformer used for hook bodies.</param>
        /// <param name="strategy">Parser or lexer strategy supplying real variation points.</param>
        /// <returns>Deterministic embedded-code hook metadata.</returns>
        public static IReadOnlyList<EmbeddedCodeHook> Collect(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer, IEmbeddedHookCollectionStrategy strategy)
        {
            return new EmbeddedHookCollector(strategy).Collect(grammar, transformer);
        }

        /// <summary>
        /// Collects hooks for every strategy-provided traversal root and transforms each hook once in collection order.
        /// </summary>
        /// <param name="grammar">Parsed grammar AST.</param>
        /// <param name="transformer">Embedded-code transformer used for hook bodies.</param>
        /// <returns>Deterministic embedded-code hook metadata.</returns>
        private IReadOnlyList<EmbeddedCodeHook> Collect(G4Grammar grammar, IParserEmbeddedCodeTransformer transformer)
        {
            var hooks = new List<EmbeddedCodeHook>();
            foreach (G4Rule rule in _strategy.EnumerateRules(grammar))
            {
                int firstHookIndex = hooks.Count;
                foreach (HookTraversalRoot root in _strategy.EnumerateTraversalRoots(rule))
                {
                    VisitContent(rule.Name, root.Content, root.InitialPosition, hooks);
                }

                for (int index = firstHookIndex; index < hooks.Count; index++)
                {
                    EmbeddedCodeHook hook = hooks[index];
                    hooks[index] = hook with
                    {
                        TransformedCode = TransformEmbeddedCode(transformer, hook.RawCode, _strategy.GetLocation(hook.Kind), grammar, _strategy.GetTransformationRule(rule))
                    };
                }
            }

            return hooks;
        }

        /// <summary>
        /// Recursively visits grammar content nodes that can contain executable embedded code.
        /// </summary>
        /// <param name="ruleName">Owning rule name.</param>
        /// <param name="content">Grammar content to inspect.</param>
        /// <param name="position">Current runtime-compatible traversal position.</param>
        /// <param name="hooks">Destination hook collection.</param>
        private void VisitContent(string ruleName, G4Content content, HookTraversalPosition position, List<EmbeddedCodeHook> hooks)
        {
            switch (content)
            {
                case G4Alternation alternation:
                    IReadOnlyList<G4Alternative> alternatives = _strategy.OrderAlternatives(alternation);
                    for (int index = 0; index < alternatives.Count; index++)
                    {
                        VisitContent(ruleName, alternatives[index], _strategy.EnterAlternative(position, index), hooks);
                    }
                    break;
                case G4Alternative alternative:
                    VisitAlternative(ruleName, alternative, position, hooks);
                    break;
                case G4Sequence sequence:
                    VisitSequence(ruleName, sequence.Items, position, hooks);
                    break;
                case G4Quantifier quantifier:
                    VisitContent(ruleName, quantifier.Inner, _strategy.EnterQuantifier(position), hooks);
                    break;
                case G4Negation negation:
                    VisitContent(ruleName, negation.Inner, _strategy.EnterNegation(position), hooks);
                    break;
                case G4EmbeddedAction action:
                    AddEmbeddedCodeHook(ruleName, action, position, hooks);
                    break;
            }
        }

        /// <summary>
        /// Visits one alternative while preserving parser and lexer element-index conventions.
        /// </summary>
        /// <param name="ruleName">Owning rule name.</param>
        /// <param name="alternative">Alternative to inspect.</param>
        /// <param name="position">Current runtime-compatible traversal position.</param>
        /// <param name="hooks">Destination hook collection.</param>
        private void VisitAlternative(string ruleName, G4Alternative alternative, HookTraversalPosition position, List<EmbeddedCodeHook> hooks)
        {
            IReadOnlyList<G4Content> items = _strategy.GetAlternativeItems(alternative);
            if (_strategy.ShouldVisitSingleAlternativeItemDirectly(items))
            {
                VisitContent(ruleName, items[0], _strategy.EnterSingleAlternativeItem(position), hooks);
                return;
            }

            VisitSequence(ruleName, items, position, hooks);
        }

        /// <summary>
        /// Visits sequence items and assigns each child position through the active strategy.
        /// </summary>
        /// <param name="ruleName">Owning rule name.</param>
        /// <param name="items">Sequence items to inspect.</param>
        /// <param name="position">Current runtime-compatible traversal position.</param>
        /// <param name="hooks">Destination hook collection.</param>
        private void VisitSequence(string ruleName, IReadOnlyList<G4Content> items, HookTraversalPosition position, List<EmbeddedCodeHook> hooks)
        {
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                VisitContent(ruleName, items[itemIndex], _strategy.EnterSequenceItem(position, itemIndex), hooks);
            }
        }

        /// <summary>
        /// Adds one embedded-code hook with a deterministic method name and strategy-selected owner.
        /// </summary>
        /// <param name="ruleName">Owning rule name.</param>
        /// <param name="action">Embedded action or predicate AST node.</param>
        /// <param name="position">Runtime-compatible traversal position.</param>
        /// <param name="hooks">Destination hook collection.</param>
        private void AddEmbeddedCodeHook(string ruleName, G4EmbeddedAction action, HookTraversalPosition position, List<EmbeddedCodeHook> hooks)
        {
            EmbeddedCodeHookKind kind = action.IsPredicate ? EmbeddedCodeHookKind.SemanticPredicate : EmbeddedCodeHookKind.InlineAction;
            string methodName = _strategy.BuildMethodName(ruleName, kind, position, hooks.Count);
            hooks.Add(_strategy.CreateHook(ruleName, action.Code, kind, position, methodName));
        }
    }

    /// <summary>
    /// Parser- or lexer-specific strategy for the shared embedded-code hook collector.
    /// </summary>
    private interface IEmbeddedHookCollectionStrategy
    {
        /// <summary>Enumerates rules in the order used by the generated hook family.</summary>
        IEnumerable<G4Rule> EnumerateRules(G4Grammar grammar);

        /// <summary>Enumerates traversal roots for one rule, including parser left-recursive specialization.</summary>
        IEnumerable<HookTraversalRoot> EnumerateTraversalRoots(G4Rule rule);

        /// <summary>Orders alternatives for the active domain.</summary>
        IReadOnlyList<G4Alternative> OrderAlternatives(G4Alternation alternation);

        /// <summary>Creates the child position used when entering an alternation alternative.</summary>
        HookTraversalPosition EnterAlternative(HookTraversalPosition current, int alternativeIndex);

        /// <summary>Returns the content items that form an alternative traversal view.</summary>
        IReadOnlyList<G4Content> GetAlternativeItems(G4Alternative alternative);

        /// <summary>Determines whether a one-item alternative keeps the historical direct-item sentinel.</summary>
        bool ShouldVisitSingleAlternativeItemDirectly(IReadOnlyList<G4Content> items);

        /// <summary>Creates the child position used for a directly visited single alternative item.</summary>
        HookTraversalPosition EnterSingleAlternativeItem(HookTraversalPosition current);

        /// <summary>Creates the child position used when entering a sequence item.</summary>
        HookTraversalPosition EnterSequenceItem(HookTraversalPosition current, int itemIndex);

        /// <summary>Creates the child position used when entering quantified content.</summary>
        HookTraversalPosition EnterQuantifier(HookTraversalPosition current);

        /// <summary>Creates the child position used when entering negated content.</summary>
        HookTraversalPosition EnterNegation(HookTraversalPosition current);

        /// <summary>Builds the generated C# method name for one hook.</summary>
        string BuildMethodName(string ruleName, EmbeddedCodeHookKind kind, HookTraversalPosition position, int hookIndex);

        /// <summary>Gets the transformation location for the hook category.</summary>
        ParserEmbeddedCodeLocation GetLocation(EmbeddedCodeHookKind kind);

        /// <summary>Gets the rule metadata shape exposed to the transformer.</summary>
        G4Rule GetTransformationRule(G4Rule rule);

        /// <summary>Creates the parser- or lexer-owned hook through the typed factory.</summary>
        EmbeddedCodeHook CreateHook(string ruleName, string code, EmbeddedCodeHookKind kind, HookTraversalPosition position, string methodName);
    }

    /// <summary>
    /// Immutable traversal root passed from a strategy to the shared collector.
    /// </summary>
    private readonly struct HookTraversalRoot
    {
        /// <summary>
        /// Initializes an immutable traversal root.
        /// </summary>
        /// <param name="content">Grammar content root to visit.</param>
        /// <param name="initialPosition">Initial runtime-compatible traversal position.</param>
        public HookTraversalRoot(G4Content content, HookTraversalPosition initialPosition)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            InitialPosition = initialPosition;
        }

        /// <summary>Gets the grammar content root to visit.</summary>
        public G4Content Content { get; }

        /// <summary>Gets the initial runtime-compatible traversal position.</summary>
        public HookTraversalPosition InitialPosition { get; }
    }

    /// <summary>
    /// Immutable runtime-compatible alternative and element indexes for hook traversal.
    /// </summary>
    private readonly struct HookTraversalPosition
    {
        /// <summary>
        /// Initializes an immutable traversal position.
        /// </summary>
        /// <param name="alternativeIndex">Current alternative index, or <c>-1</c> when unavailable.</param>
        /// <param name="elementIndex">Current element index, or <c>-1</c> when unavailable.</param>
        public HookTraversalPosition(int alternativeIndex, int elementIndex)
        {
            AlternativeIndex = alternativeIndex;
            ElementIndex = elementIndex;
        }

        /// <summary>Gets the current alternative index, or <c>-1</c> when unavailable.</summary>
        public int AlternativeIndex { get; }

        /// <summary>Gets the current element index, or <c>-1</c> when unavailable.</summary>
        public int ElementIndex { get; }

        /// <summary>Gets the historical sentinel position used before an alternative or element is selected.</summary>
        public static HookTraversalPosition Unspecified { get; } = new HookTraversalPosition(-1, -1);
    }

    /// <summary>
    /// Parser hook collection strategy preserving parser priority, left-recursion, and runtime index rules.
    /// </summary>
    private sealed class ParserEmbeddedHookCollectionStrategy : IEmbeddedHookCollectionStrategy
    {
        /// <summary>Gets the singleton parser strategy instance.</summary>
        public static ParserEmbeddedHookCollectionStrategy Instance { get; } = new();

        /// <summary>Prevents external construction of the singleton strategy.</summary>
        private ParserEmbeddedHookCollectionStrategy()
        {
        }

        /// <inheritdoc />
        public IEnumerable<G4Rule> EnumerateRules(G4Grammar grammar) => grammar.ParserRules;

        /// <inheritdoc />
        public IEnumerable<HookTraversalRoot> EnumerateTraversalRoots(G4Rule rule)
        {
            IReadOnlyList<G4Alternative> orderedAlternatives = OrderAlternatives(rule.Content);
            var recursiveAlternatives = orderedAlternatives.Where(alternative => StartsWithRuleRef(alternative, rule.Name)).ToList();
            if (recursiveAlternatives.Count == 0)
            {
                yield return new HookTraversalRoot(rule.Content, HookTraversalPosition.Unspecified);
                yield break;
            }

            var baseAlternatives = orderedAlternatives.Where(alternative => !StartsWithRuleRef(alternative, rule.Name)).ToList();
            for (int index = 0; index < baseAlternatives.Count; index++)
            {
                yield return new HookTraversalRoot(baseAlternatives[index], new HookTraversalPosition(index, -1));
            }

            for (int index = 0; index < recursiveAlternatives.Count; index++)
            {
                G4Alternative recursiveAlternative = recursiveAlternatives[index];
                if (recursiveAlternative.Items.Count == 0 || !IsRuleRef(recursiveAlternative.Items[0], rule.Name))
                {
                    continue;
                }

                yield return new HookTraversalRoot(CreateTailAlternative(recursiveAlternative), new HookTraversalPosition(index, -1));
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<G4Alternative> OrderAlternatives(G4Alternation alternation) => alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();

        /// <inheritdoc />
        public HookTraversalPosition EnterAlternative(HookTraversalPosition current, int alternativeIndex) => new(alternativeIndex, -1);

        /// <inheritdoc />
        public IReadOnlyList<G4Content> GetAlternativeItems(G4Alternative alternative) => alternative.Items;

        /// <inheritdoc />
        public bool ShouldVisitSingleAlternativeItemDirectly(IReadOnlyList<G4Content> items) => items.Count == 1 && items[0] is not G4Sequence;

        /// <inheritdoc />
        public HookTraversalPosition EnterSingleAlternativeItem(HookTraversalPosition current) => new(current.AlternativeIndex, -1);

        /// <inheritdoc />
        public HookTraversalPosition EnterSequenceItem(HookTraversalPosition current, int itemIndex) => new(current.AlternativeIndex, itemIndex);

        /// <inheritdoc />
        public HookTraversalPosition EnterQuantifier(HookTraversalPosition current) => new(current.AlternativeIndex, current.AlternativeIndex);

        /// <inheritdoc />
        public HookTraversalPosition EnterNegation(HookTraversalPosition current) => new(current.AlternativeIndex, current.AlternativeIndex);

        /// <inheritdoc />
        public string BuildMethodName(string ruleName, EmbeddedCodeHookKind kind, HookTraversalPosition position, int hookIndex)
        {
            string prefix = kind == EmbeddedCodeHookKind.SemanticPredicate ? "__Predicate" : "__Action";
            return $"{prefix}_{Sanitize(ruleName)}_{NormalizeIndexForName(position.AlternativeIndex)}_{NormalizeIndexForName(position.ElementIndex)}_{hookIndex}";
        }

        /// <inheritdoc />
        public ParserEmbeddedCodeLocation GetLocation(EmbeddedCodeHookKind kind)
        {
            return kind == EmbeddedCodeHookKind.SemanticPredicate ? ParserEmbeddedCodeLocation.SemanticPredicate : ParserEmbeddedCodeLocation.InlineAction;
        }

        /// <inheritdoc />
        public G4Rule GetTransformationRule(G4Rule rule) => rule;

        /// <inheritdoc />
        public EmbeddedCodeHook CreateHook(string ruleName, string code, EmbeddedCodeHookKind kind, HookTraversalPosition position, string methodName)
        {
            return EmbeddedCodeHook.CreateParser(ruleName, code, kind, position.AlternativeIndex, position.ElementIndex, methodName);
        }

        /// <summary>
        /// Creates the parser runtime tail view for a direct-left-recursive alternative.
        /// </summary>
        /// <param name="alternative">Source alternative that starts with a direct self-reference.</param>
        /// <returns>Alternative containing only the recursive tail items.</returns>
        private static G4Alternative CreateTailAlternative(G4Alternative alternative)
        {
            var tail = new G4Alternative { Priority = alternative.Priority };
            foreach (G4Content item in alternative.Items.Skip(1))
            {
                tail.Items.Add(item);
            }

            return tail;
        }
    }

    /// <summary>
    /// Lexer hook collection strategy preserving source order, mode traversal, and lexer index rules.
    /// </summary>
    private sealed class LexerEmbeddedHookCollectionStrategy : IEmbeddedHookCollectionStrategy
    {
        /// <summary>Gets the singleton lexer strategy instance.</summary>
        public static LexerEmbeddedHookCollectionStrategy Instance { get; } = new();

        /// <summary>Prevents external construction of the singleton strategy.</summary>
        private LexerEmbeddedHookCollectionStrategy()
        {
        }

        /// <inheritdoc />
        public IEnumerable<G4Rule> EnumerateRules(G4Grammar grammar)
        {
            foreach (G4Rule rule in grammar.LexerRules)
            {
                yield return rule;
            }

            foreach (G4LexerMode mode in grammar.ExtraModes)
            {
                foreach (G4Rule rule in mode.Rules)
                {
                    yield return rule;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<HookTraversalRoot> EnumerateTraversalRoots(G4Rule rule)
        {
            yield return new HookTraversalRoot(rule.Content, HookTraversalPosition.Unspecified);
        }

        /// <inheritdoc />
        public IReadOnlyList<G4Alternative> OrderAlternatives(G4Alternation alternation) => alternation.Alternatives;

        /// <inheritdoc />
        public HookTraversalPosition EnterAlternative(HookTraversalPosition current, int alternativeIndex) => new(alternativeIndex, -1);

        /// <inheritdoc />
        public IReadOnlyList<G4Content> GetAlternativeItems(G4Alternative alternative) => alternative.Items;

        /// <inheritdoc />
        public bool ShouldVisitSingleAlternativeItemDirectly(IReadOnlyList<G4Content> items) => false;

        /// <inheritdoc />
        public HookTraversalPosition EnterSingleAlternativeItem(HookTraversalPosition current) => current;

        /// <inheritdoc />
        public HookTraversalPosition EnterSequenceItem(HookTraversalPosition current, int itemIndex) => new(current.AlternativeIndex, itemIndex);

        /// <inheritdoc />
        public HookTraversalPosition EnterQuantifier(HookTraversalPosition current) => current;

        /// <inheritdoc />
        public HookTraversalPosition EnterNegation(HookTraversalPosition current) => current;

        /// <inheritdoc />
        public string BuildMethodName(string ruleName, EmbeddedCodeHookKind kind, HookTraversalPosition position, int hookIndex)
        {
            string prefix = kind == EmbeddedCodeHookKind.SemanticPredicate ? "__LexerPredicate" : "__LexerAction";
            return $"{prefix}_{Sanitize(ruleName)}_{NormalizeIndexForName(position.AlternativeIndex)}_{NormalizeIndexForName(position.ElementIndex)}_{hookIndex}";
        }

        /// <inheritdoc />
        public ParserEmbeddedCodeLocation GetLocation(EmbeddedCodeHookKind kind)
        {
            return kind == EmbeddedCodeHookKind.SemanticPredicate ? ParserEmbeddedCodeLocation.LexerSemanticPredicate : ParserEmbeddedCodeLocation.LexerInlineAction;
        }

        /// <inheritdoc />
        public G4Rule GetTransformationRule(G4Rule rule)
        {
            return new G4Rule { Name = rule.Name };
        }

        /// <inheritdoc />
        public EmbeddedCodeHook CreateHook(string ruleName, string code, EmbeddedCodeHookKind kind, HookTraversalPosition position, string methodName)
        {
            return EmbeddedCodeHook.CreateLexer(ruleName, code, kind, position.AlternativeIndex, position.ElementIndex, methodName);
        }
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
    /// Converts runtime indexes into stable identifier fragments.
    /// </summary>
    /// <param name="index">Runtime index value.</param>
    /// <returns>Identifier-safe index fragment.</returns>
    private static string NormalizeIndexForName(int index)
    {
        return index < 0 ? "m1" : index.ToString();
    }


    /// <summary>
    /// Identifies the generated embedded-code hook owner.
    /// </summary>
    private enum EmbeddedCodeHookOwner
    {
        /// <summary>The hook belongs to parser embedded code.</summary>
        Parser,

        /// <summary>The hook belongs to lexer embedded code.</summary>
        Lexer
    }

    /// <summary>
    /// Identifies the generated embedded-code hook executable category.
    /// </summary>
    private enum EmbeddedCodeHookKind
    {
        /// <summary>The hook evaluates a semantic predicate.</summary>
        SemanticPredicate,

        /// <summary>The hook executes an inline action.</summary>
        InlineAction
    }

    /// <summary>
    /// Metadata for one generated parser or lexer embedded-code hook.
    /// </summary>
    private sealed record EmbeddedCodeHook
    {
        /// <summary>
        /// Initializes generated embedded-code hook metadata after validating common invariants.
        /// </summary>
        /// <param name="owner">Parser or lexer owner for this hook.</param>
        /// <param name="kind">Predicate or action category for this hook.</param>
        /// <param name="ruleName">Owning grammar rule name.</param>
        /// <param name="code">Raw embedded source code without ANTLR braces.</param>
        /// <param name="alternativeIndex">Runtime alternative index used for dispatch.</param>
        /// <param name="elementIndex">Runtime element index used for dispatch.</param>
        /// <param name="methodName">Generated C# hook method name.</param>
        private EmbeddedCodeHook(EmbeddedCodeHookOwner owner, EmbeddedCodeHookKind kind, string ruleName, string code, int alternativeIndex, int elementIndex, string methodName)
        {
            if (!Enum.IsDefined(typeof(EmbeddedCodeHookOwner), owner))
            {
                throw new ArgumentOutOfRangeException(nameof(owner));
            }

            if (!Enum.IsDefined(typeof(EmbeddedCodeHookKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(ruleName))
            {
                throw new ArgumentException("Rule name is required.", nameof(ruleName));
            }

            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (alternativeIndex < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(alternativeIndex));
            }

            if (elementIndex < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }

            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("Generated hook method name is required.", nameof(methodName));
            }

            Owner = owner;
            Kind = kind;
            RuleName = ruleName;
            RawCode = new RawEmbeddedCode(code);
            AlternativeIndex = alternativeIndex;
            ElementIndex = elementIndex;
            MethodName = methodName;
        }

        /// <summary>Gets the parser or lexer owner for this hook.</summary>
        public EmbeddedCodeHookOwner Owner { get; }

        /// <summary>Gets the predicate or action category for this hook.</summary>
        public EmbeddedCodeHookKind Kind { get; }

        /// <summary>Gets the owning grammar rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets the raw embedded source code without ANTLR braces.</summary>
        public RawEmbeddedCode RawCode { get; }

        /// <summary>Gets the validated transformed C# source, or <c>null</c> before transformation.</summary>
        public TransformedEmbeddedCode? TransformedCode { get; init; }

        /// <summary>Gets a value indicating whether the hook is a semantic predicate.</summary>
        public bool IsPredicate => Kind == EmbeddedCodeHookKind.SemanticPredicate;

        /// <summary>Gets the runtime alternative index used for dispatch.</summary>
        public int AlternativeIndex { get; }

        /// <summary>Gets the runtime element index used for dispatch.</summary>
        public int ElementIndex { get; }

        /// <summary>Gets the generated C# hook method name.</summary>
        public string MethodName { get; }

        /// <summary>Creates a parser-owned embedded-code hook.</summary>
        public static EmbeddedCodeHook CreateParser(string ruleName, string code, EmbeddedCodeHookKind kind, int alternativeIndex, int elementIndex, string methodName)
        {
            return new EmbeddedCodeHook(EmbeddedCodeHookOwner.Parser, kind, ruleName, code, alternativeIndex, elementIndex, methodName);
        }

        /// <summary>Creates a lexer-owned embedded-code hook.</summary>
        public static EmbeddedCodeHook CreateLexer(string ruleName, string code, EmbeddedCodeHookKind kind, int alternativeIndex, int elementIndex, string methodName)
        {
            return new EmbeddedCodeHook(EmbeddedCodeHookOwner.Lexer, kind, ruleName, code, alternativeIndex, elementIndex, methodName);
        }
    }


    /// <summary>
    /// Validates that a generated embedded-code hook is consumed by the parser or lexer path it belongs to.
    /// </summary>
    /// <param name="hook">Hook consumed by a specialized emitter path.</param>
    /// <param name="expectedOwner">Owner required by the specialized emitter path.</param>
    /// <param name="expectedKind">Executable category required by the specialized emitter path.</param>
    private static void ValidateEmbeddedCodeHook(EmbeddedCodeHook hook, EmbeddedCodeHookOwner expectedOwner, EmbeddedCodeHookKind expectedKind)
    {
        if (hook is null)
        {
            throw new ArgumentNullException(nameof(hook));
        }

        if (hook.Owner != expectedOwner)
        {
            throw new InvalidOperationException($"Embedded code hook '{hook.MethodName}' belongs to {hook.Owner} but was consumed by the {expectedOwner} emission path.");
        }

        if (hook.Kind != expectedKind)
        {
            throw new InvalidOperationException($"Embedded code hook '{hook.MethodName}' is a {hook.Kind} hook but was consumed by a {expectedKind} emission path.");
        }
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
        /// <param name="code">Transformed lifecycle action body ready for emission.</param>
        /// <param name="isInit"><see langword="true"/> for <c>@init</c>; <see langword="false"/> for <c>@after</c>.</param>
        /// <param name="methodName">Generated C# hook method name.</param>
        public LifecycleHook(string ruleName, TransformedEmbeddedCode code, bool isInit, string methodName)
        {
            RuleName = ruleName;
            Code = code;
            IsInit = isInit;
            MethodName = methodName;
        }

        /// <summary>Gets the owning parser rule name.</summary>
        public string RuleName { get; }

        /// <summary>Gets the transformed lifecycle action body ready for emission.</summary>
        public TransformedEmbeddedCode Code { get; }

        /// <summary>Gets a value indicating whether this is an <c>@init</c> hook (<see langword="false"/> means <c>@after</c>).</summary>
        public bool IsInit { get; }

        /// <summary>Gets the generated C# hook method name.</summary>
        public string MethodName { get; }
    }
}
