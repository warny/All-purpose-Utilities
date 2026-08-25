using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.NumberToString;

/// <summary>
/// Represents an immutable mapping from lexical form keys (e.g. <c>"singular"</c>,
/// <c>"plural"</c>, <c>"few"</c>) to the localized word a configured constituent uses for that
/// form.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="LexicalFormSet"/> is the word-side counterpart of <see cref="ForcedVariantSet"/>:
/// an <see cref="ILexicalFormSelector"/> chooses a form KEY, and a <see cref="LexicalFormSet"/>
/// resolves that key to the actual localized word for one constituent (e.g. one time unit).
/// Selectors never see the words; <see cref="LexicalFormSet"/> never chooses which key applies.
/// </para>
/// <para>Instances are immutable; key comparison is case-insensitive.</para>
/// </remarks>
public sealed class LexicalFormSet
{
    /// <summary>Gets a form set with no configured forms.</summary>
    public static LexicalFormSet Empty { get; } = new(ImmutableDictionary<string, string>.Empty);

    private readonly ImmutableDictionary<string, string> _forms;

    private LexicalFormSet(ImmutableDictionary<string, string> forms) => _forms = forms;

    /// <summary>Gets whether this set declares no forms.</summary>
    public bool IsEmpty => _forms.Count == 0;

    /// <summary>Gets the configured form keys.</summary>
    internal IReadOnlyCollection<string> Keys => _forms.Keys.ToArray();

    /// <summary>
    /// Creates a lexical form set from explicit key/value pairs.
    /// </summary>
    /// <param name="forms">
    /// The form key/word pairs, enumerated once. A <see langword="null"/> or empty sequence
    /// returns <see cref="Empty"/>.
    /// </param>
    /// <exception cref="NumberToStringConfigurationException">
    /// A key or value is null/empty, or the same key (case-insensitive) is supplied more than
    /// once — error code <c>"UNTS007"</c>.
    /// </exception>
    public static LexicalFormSet Create(params IEnumerable<(string Key, string Value)> forms)
    {
        if (forms is null) return Empty;

        var builder = ImmutableDictionary.CreateBuilder<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in forms)
        {
            if (string.IsNullOrEmpty(key))
                throw new NumberToStringConfigurationException("UNTS007", null, "LexicalFormSet",
                    "A lexical form key must not be null or empty.");
            if (string.IsNullOrEmpty(value))
                throw new NumberToStringConfigurationException("UNTS007", null, "LexicalFormSet",
                    $"Lexical form key '{key}' must have a non-empty value.");
            if (!builder.TryAdd(key, value))
                throw new NumberToStringConfigurationException("UNTS007", null, "LexicalFormSet",
                    $"Duplicate lexical form key '{key}'. A form set may declare a key only once.");
        }

        return builder.Count == 0 ? Empty : new LexicalFormSet(builder.ToImmutable());
    }

    /// <summary>
    /// Returns a new form set overlaying <paramref name="overrides"/> on top of this set: keys
    /// present in <paramref name="overrides"/> replace this set's value for the same key, and
    /// keys unique to either set are preserved. Neither operand is mutated.
    /// </summary>
    /// <param name="overrides">The form set whose entries take precedence.</param>
    internal LexicalFormSet MergeOverriddenBy(LexicalFormSet overrides)
    {
        if (overrides.IsEmpty) return this;
        var builder = _forms.ToBuilder();
        foreach (var pair in overrides._forms)
            builder[pair.Key] = pair.Value;
        return new LexicalFormSet(builder.ToImmutable());
    }

    /// <summary>Attempts to resolve the localized word configured for <paramref name="key"/>.</summary>
    /// <param name="key">The form key to look up.</param>
    /// <param name="value">The configured word, when found.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> has a configured word.</returns>
    internal bool TryGetForm(string key, out string value) => _forms.TryGetValue(key, out value!);
}
