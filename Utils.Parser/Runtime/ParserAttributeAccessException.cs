using System;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a deterministic failure to read a limited generated parser return attribute.
/// </summary>
public sealed class ParserAttributeAccessException : InvalidOperationException
{
    /// <summary>
    /// Initializes an exception for a current-rule return that is unavailable.
    /// </summary>
    /// <param name="attributeText">Canonical attribute text.</param>
    /// <param name="returnName">Requested return name.</param>
    /// <param name="message">Deterministic failure message.</param>
    public ParserAttributeAccessException(string attributeText, string returnName, string message)
        : this(attributeText, null, returnName, message)
    {
    }

    /// <summary>
    /// Initializes an exception for an assignment-labeled child return that is unavailable.
    /// </summary>
    /// <param name="attributeText">Canonical attribute text.</param>
    /// <param name="labelName">Assignment label name, or <see langword="null"/> for current-rule access.</param>
    /// <param name="returnName">Requested return name.</param>
    /// <param name="message">Deterministic failure message.</param>
    public ParserAttributeAccessException(string attributeText, string? labelName, string returnName, string message)
        : base(message)
    {
        AttributeText = attributeText ?? throw new ArgumentNullException(nameof(attributeText));
        LabelName = labelName;
        ReturnName = returnName ?? throw new ArgumentNullException(nameof(returnName));
    }

    /// <summary>Gets the canonical attribute expression that failed.</summary>
    public string AttributeText { get; }

    /// <summary>Gets the assignment label name, or <see langword="null"/> for current-rule access.</summary>
    public string? LabelName { get; }

    /// <summary>Gets the requested return name.</summary>
    public string ReturnName { get; }
}
