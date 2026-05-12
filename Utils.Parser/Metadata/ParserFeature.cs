namespace Utils.Parser.Metadata;

/// <summary>
/// Lists parser feature capabilities tracked by the parser capability model.
/// </summary>
public enum ParserFeature
{
    /// <summary>Right-associativity metadata via &lt;assoc=right&gt;.</summary>
    AssocRight,
    /// <summary>Grammar import declarations and project-level resolution.</summary>
    GrammarImports,
    /// <summary>Semantic predicate parsing and optional runtime evaluation policy.</summary>
    SemanticPredicates,
    /// <summary>Inline action parsing and optional runtime execution policy.</summary>
    InlineActions,
    /// <summary>Rule action blocks such as @init and @after.</summary>
    RuleActions,
    /// <summary>Rule parameter declaration metadata.</summary>
    RuleParameters,
    /// <summary>Rule returns declaration metadata.</summary>
    RuleReturns,
    /// <summary>Shared-prefix analysis metadata.</summary>
    SharedPrefixMetadata,
    /// <summary>Shared-prefix runtime execution optimization.</summary>
    SharedPrefixExecution,
    /// <summary>Continuation replay execution strategy.</summary>
    ContinuationReplay,
    /// <summary>Parser graph runtime execution strategy.</summary>
    ParserGraphExecution,
    /// <summary>Adaptive LL parsing strategy.</summary>
    AdaptiveLl,
    /// <summary>GLL parsing strategy.</summary>
    Gll,
    /// <summary>Asynchronous parsing execution.</summary>
    AsyncParsing,
    /// <summary>Parallel parsing execution.</summary>
    ParallelParsing
}
