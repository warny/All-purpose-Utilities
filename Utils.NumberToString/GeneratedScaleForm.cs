using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Utils.NumberToString;

/// <summary>
/// The lexical forms of one family of generated scale nouns: every scale that <see cref="NumberScale"/>
/// names with the same suffix (<see cref="NumberScale.ScaleSuffixes"/> index) shares it, whatever its
/// Conway prefix. Configured by <c>&lt;GeneratedScaleForm suffix="…"&gt;</c> or
/// <see cref="NumberToStringConverterOptions.GeneratedScaleForms"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="NumberToStringConverterOptions.ScaleForms"/>, which holds whole words for one exact
/// scale index, the forms here are <em>suffix</em> fragments (Russian <c>он</c> / <c>она</c> / <c>онов</c>):
/// the converter selects a key with <see cref="Selector"/> from the group multiplier, then builds the
/// noun with the Conway prefix machinery of the scale and the selected fragment (три + лли + она). An
/// exact scale form, when one is configured for the same scale, keeps precedence.
/// </para>
/// <para>
/// A form may also carry <see cref="MultiplierForcedVariants"/>: the variant values the noun imposes on
/// the numeral of its multiplier when that form is selected (Czech <c>dvě biliardy</c>: the feminine
/// few form forces gender on <c>dva</c>). They are overlaid on the caller's variants, forced values winning
/// dimension by dimension, and that query drives the whole local pipeline of the group (group triggers,
/// variant rules of the multiplier, <c>onScale</c> variant rules, group-with-scale triggers); the group is
/// then left untouched by the whole-number passes run with the caller's query. The selector runs once,
/// before they are applied. Instances are immutable.
/// </para>
/// </remarks>
public sealed class GeneratedScaleForm
{
    /// <summary>
    /// Initializes the forms of a generated scale family.
    /// </summary>
    /// <param name="forms">The suffix fragment of each form key; must not be empty.</param>
    /// <param name="selector">
    /// The selector choosing the form key from the group multiplier, or <see langword="null"/> for
    /// <see cref="DefaultLexicalFormSelector"/> (keys "singular" and "plural").
    /// </param>
    /// <param name="multiplierForcedVariants">
    /// The variants each form key forces on the multiplier, or <see langword="null"/> for none. Every key
    /// must be a key of <paramref name="forms"/>. Validated against the language when the converter is built.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="forms"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="forms"/> is empty, or a forced-variant entry is null or names a key absent from <paramref name="forms"/>.
    /// </exception>
    public GeneratedScaleForm(
        LexicalFormSet forms,
        ILexicalFormSelector? selector = null,
        IReadOnlyDictionary<string, ForcedVariantSet>? multiplierForcedVariants = null)
    {
        ArgumentNullException.ThrowIfNull(forms);
        if (forms.IsEmpty)
            throw new ArgumentException("A generated scale form needs at least one suffix form.", nameof(forms));

        Forms = forms;
        Selector = selector ?? DefaultLexicalFormSelector.Instance;
        MultiplierForcedVariants = SnapshotMultiplierForcedVariants(forms, multiplierForcedVariants, "MultiplierForcedVariants", nameof(multiplierForcedVariants));
    }

    /// <summary>
    /// Validates and snapshots the variants that the forms of a scale noun force on its multiplier, for a generated
    /// family (<see cref="GeneratedScaleForm"/>) as for an exact scale
    /// (<see cref="NumberToStringConverterOptions.ScaleFormMultiplierForcedVariants"/>): every key must be a key of
    /// <paramref name="forms"/>, case-insensitively and once; an empty set forces nothing and is dropped. The values
    /// are validated against the language later, by the converter.
    /// </summary>
    /// <param name="forms">The forms the keys refer to.</param>
    /// <param name="forced">The forced variants by form key, or <see langword="null"/> for none.</param>
    /// <param name="description">The name of the configured collection, used in the messages (e.g. <c>MultiplierForcedVariants</c>).</param>
    /// <param name="parameterName">The parameter name reported by the exceptions.</param>
    /// <returns>The non-empty forced variants by form key (case-insensitive keys).</returns>
    /// <exception cref="ArgumentException">An entry is null, names a key absent from <paramref name="forms"/>, or is declared twice.</exception>
    internal static ImmutableDictionary<string, ForcedVariantSet> SnapshotMultiplierForcedVariants(
        LexicalFormSet forms,
        IReadOnlyDictionary<string, ForcedVariantSet>? forced,
        string description,
        string parameterName)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ForcedVariantSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in forced ?? ImmutableDictionary<string, ForcedVariantSet>.Empty)
        {
            if (value is null)
                throw new ArgumentException($"{description}[{key}] must not be null.", parameterName);
            if (!forms.TryGetForm(key, out _))
                throw new ArgumentException(
                    $"{description} key '{key}' has no matching form. Available forms: {string.Join(", ", forms.Keys)}.",
                    parameterName);
            if (!value.IsEmpty && !builder.TryAdd(key, value))
                throw new ArgumentException($"{description} declares form '{key}' more than once.", parameterName);
        }
        return builder.ToImmutable();
    }

    /// <summary>Gets the suffix fragment of each form key.</summary>
    public LexicalFormSet Forms { get; }

    /// <summary>Gets the selector choosing the form key from the group multiplier.</summary>
    public ILexicalFormSelector Selector { get; }

    /// <summary>Gets the variants each form key forces on the multiplier; forms absent from it force nothing.</summary>
    public IReadOnlyDictionary<string, ForcedVariantSet> MultiplierForcedVariants { get; }

    /// <summary>
    /// Returns a copy whose forced variants are replaced by <paramref name="canonical"/> (the converter's
    /// canonicalized sets), keeping the forms and the selector.
    /// </summary>
    /// <param name="canonical">The canonicalized forced variants, with the same keys.</param>
    /// <returns>The copy, or this instance when nothing is forced.</returns>
    internal GeneratedScaleForm WithForcedVariants(IReadOnlyDictionary<string, ForcedVariantSet> canonical)
        => MultiplierForcedVariants.Count == 0 ? this : new GeneratedScaleForm(Forms, Selector, canonical);
}
