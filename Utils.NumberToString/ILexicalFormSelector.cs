using System.Collections.Generic;
using System.Numerics;
using System.Xml.Linq;

namespace Utils.NumberToString;

/// <summary>
/// Chooses which named lexical form of a configured constituent (e.g. a time unit) applies to
/// a given numeric value and grammatical context.
/// </summary>
/// <remarks>
/// <para>
/// A selector returns a FORM KEY only (e.g. <c>"singular"</c>, <c>"plural"</c>,
/// <c>"attributive"</c>, <c>"one"</c>, <c>"few"</c>, <c>"many"</c>) — never the localized word.
/// The mapping from a form key to its localized word is owned entirely by configuration (XML
/// <c>&lt;Forms&gt;</c> entries or the programmatic equivalent), not by the selector. A selector
/// implementation must not contain any language-specific word.
/// </para>
/// <para>
/// This is a distinct concern from <see cref="ForcedVariantSet"/>: a lexical form selector
/// chooses which form of the UNIT word applies, while <see cref="ForcedVariantSet"/> constrains
/// the grammatical morphology of the NUMBER associated with that unit. The two mechanisms are
/// applied independently and compose through <see cref="LexicalFormContext.Variants"/>, which
/// carries the same effective variant query used to render the numeral.
/// </para>
/// <para>
/// A selector instance may be shared across every conversion performed by a converter (and
/// across every converter that references it, when registered by type name) and may be invoked
/// concurrently. Implementations must therefore be stateless or internally thread-safe after
/// construction; a selector must not mutate its own state per call. Custom selector types are
/// resolved at most once, during configuration loading / converter construction — never from a
/// conversion method — so <see cref="SelectForm"/> itself is the only member ever invoked on the
/// conversion hot path and must remain a simple, side-effect-free computation.
/// </para>
/// </remarks>
public interface ILexicalFormSelector
{
    /// <summary>
    /// Returns the form key that applies to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The numeric value and active grammatical variants to select a form for.</param>
    /// <returns>
    /// A form key identifying which configured lexical form to use. The caller looks this key up
    /// in the owning constituent's configured forms; a key with no matching configured form is a
    /// deterministic configuration/runtime error, not a silently ignored condition.
    /// </returns>
    string SelectForm(LexicalFormContext context);
}

/// <summary>
/// Carries the numeric value and active grammatical variant query an <see cref="ILexicalFormSelector"/>
/// uses to choose a form key.
/// </summary>
/// <param name="Value">The signed numeric value governing the constituent (e.g. the time-unit count).</param>
/// <param name="Variants">
/// The effective, already-resolved dimension → value variant query for the governed numeral
/// (language defaults, caller-supplied variants, and any <see cref="ForcedVariantSet"/> already
/// merged). Read-only; never mutated by a selector.
/// </param>
public readonly record struct LexicalFormContext(BigInteger Value, IReadOnlyDictionary<string, string> Variants)
{
    /// <summary>Gets the absolute value of <see cref="Value"/>.</summary>
    public BigInteger AbsoluteValue => BigInteger.Abs(Value);
}

/// <summary>
/// The built-in <see cref="ILexicalFormSelector"/> that reproduces the number-to-string
/// converter's historical singular/plural selection: <c>"singular"</c> when the absolute value
/// equals one, <c>"plural"</c> otherwise. Used automatically for any constituent that does not
/// configure a different selector, so legacy Singular/Plural-only configurations are unaffected
/// by the lexical-form mechanism.
/// </summary>
public sealed class DefaultLexicalFormSelector : ILexicalFormSelector
{
    /// <summary>Gets the shared, stateless default selector instance.</summary>
    internal static readonly DefaultLexicalFormSelector Instance = new();

    /// <summary>Gets <c>"singular"</c> when <see cref="LexicalFormContext.AbsoluteValue"/> equals one, otherwise <c>"plural"</c>.</summary>
    /// <param name="context">The value/variant context to select a form for.</param>
    public string SelectForm(LexicalFormContext context) =>
        context.AbsoluteValue == BigInteger.One ? "singular" : "plural";
}

/// <summary>
/// Identifies a custom <see cref="ILexicalFormSelector"/> being resolved from configuration, and
/// carries the selector's own, selector-owned configuration subtree, so a selector's constructor
/// can access the type name, owning language, and its configuration for diagnostics and setup.
/// </summary>
/// <remarks>
/// <para>
/// A selector type may optionally declare a public constructor accepting a single
/// <see cref="LexicalFormSelectorConfiguration"/> parameter; when present, it is used instead of
/// a parameterless constructor.
/// </para>
/// <para>
/// The core library owns type resolution and activation lifecycle; it does not — and must not —
/// interpret <see cref="Configuration"/> itself. A selector interprets its own configuration
/// subtree in whatever shape it chooses (attributes, child elements, …); the core library never
/// invents a universal expression language for it. <see cref="Configuration"/> is handed to the
/// selector as-is and must be treated as read-only: the core library does not defend against a
/// selector mutating the <see cref="XElement"/> it receives, but doing so has no effect on the
/// XML configuration the converter itself was built from.
/// </para>
/// </remarks>
/// <param name="typeName">The configured type name or built-in alias being resolved.</param>
/// <param name="languageIdentifier">The language identifier the selector is being resolved for, when known.</param>
/// <param name="configuration">
/// The selector-owned <c>&lt;Configuration&gt;</c> subtree from a <c>&lt;LexicalFormSelector&gt;</c>
/// XML element, or <see langword="null"/> when no selector-specific configuration was supplied
/// (including every programmatic use that does not construct one explicitly).
/// </param>
public sealed class LexicalFormSelectorConfiguration(string typeName, string? languageIdentifier, XElement? configuration = null)
{
    /// <summary>Gets the configured type name or built-in alias (e.g. <c>"default"</c>) being resolved.</summary>
    public string TypeName { get; } = typeName;

    /// <summary>Gets the language identifier the selector is being resolved for, or <see langword="null"/> when resolved programmatically outside a language context.</summary>
    public string? LanguageIdentifier { get; } = languageIdentifier;

    /// <summary>
    /// Gets the selector-owned configuration subtree, or <see langword="null"/> when none was
    /// supplied. Read-only by convention; the core library never reads or validates its content.
    /// </summary>
    public XElement? Configuration { get; } = configuration;
}
