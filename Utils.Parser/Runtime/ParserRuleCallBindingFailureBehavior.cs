namespace Utils.Parser.Runtime;

/// <summary>
/// Defines how positional literal rule-call binding responds to invalid call metadata.
/// </summary>
public enum ParserRuleCallBindingFailureBehavior
{
    /// <summary>
    /// Leaves the call unbound and allows parsing to continue.
    /// </summary>
    IgnoreCall = 0,

    /// <summary>
    /// Throws a <see cref="ParserRuleCallBindingException"/> before the child rule is invoked.
    /// </summary>
    Throw = 1
}
