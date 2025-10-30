using System;
using System.Linq;

namespace Utils.Fonts.TTF;

/// <summary>
/// Indicates the TrueType table associated with a class and specifies its dependencies.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class TTFTableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TTFTableAttribute"/> class.
    /// </summary>
    /// <param name="tableTag">The table tag associated with the class.</param>
    /// <param name="dependsOn">
    /// Zero or more table tags that this table depends on.
    /// </param>
    public TTFTableAttribute(TableTypes.Tags tableTag, params TableTypes.Tags[] dependsOn)
    {
        TableTag = new Tag((int)tableTag);
        DependsOn = dependsOn.Select(d => new Tag((int)d)).ToArray() ?? [];
    }

    /// <summary>
    /// Gets the table tag.
    /// </summary>
    public Tag TableTag { get; }

    /// <summary>
    /// Gets the array of table tags this table depends on.
    /// </summary>
    public Tag[] DependsOn { get; }
}
