using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using Utils.String;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>Empty variant query handed to scale selectors when a caller supplies none.</summary>
        private static readonly IReadOnlyDictionary<string, string> s_emptyVariantQuery =
            ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Immutable, resolved lexical configuration of one scale noun: its effective forms and the
        /// selector choosing which form a group multiplier governs.
        /// </summary>
        /// <param name="Forms">The effective forms (synthesized singular/plural merged with the configured overrides).</param>
        /// <param name="Selector">The selector, <see cref="DefaultLexicalFormSelector"/> unless configured.</param>
        private sealed record ScaleFormDefinition(LexicalFormSet Forms, ILexicalFormSelector Selector);

        /// <summary>
        /// Validates and snapshots the configured scale forms and selectors. Every configured index
        /// must be a scale the <see cref="Scale"/> can name (≥ 1). The effective forms of a scale are
        /// its "singular"/"plural" scale name (as <see cref="StringUtils.ToPlural"/> renders it for one
        /// and for two) overridden by the configured forms; its selector defaults to
        /// <see cref="DefaultLexicalFormSelector"/>. Called once from the constructor, so no selector
        /// is resolved and no collection is built on the conversion path.
        /// </summary>
        /// <param name="forms">The configured forms by scale index, or <see langword="null"/>.</param>
        /// <param name="selectors">The configured selectors by scale index, or <see langword="null"/>.</param>
        /// <returns>The resolved definitions by scale index.</returns>
        /// <exception cref="ArgumentException">An index cannot be named by the scale, or an entry is null.</exception>
        private ImmutableDictionary<int, ScaleFormDefinition> CompileScaleForms(
            IReadOnlyDictionary<int, LexicalFormSet>? forms,
            IReadOnlyDictionary<int, ILexicalFormSelector>? selectors)
        {
            // Copy both sources first: validation and resolution only ever read these snapshots.
            var formSnapshot = forms?.ToArray() ?? [];
            var selectorSnapshot = selectors?.ToArray() ?? [];
            if (formSnapshot.Length == 0 && selectorSnapshot.Length == 0)
                return ImmutableDictionary<int, ScaleFormDefinition>.Empty;

            foreach (var (index, value) in formSnapshot)
                if (value is null)
                    throw new ArgumentException($"ScaleForms[{index}] must not be null.", nameof(NumberToStringConverterOptions.ScaleForms));
            foreach (var (index, value) in selectorSnapshot)
                if (value is null)
                    throw new ArgumentException($"ScaleFormSelectors[{index}] must not be null.", nameof(NumberToStringConverterOptions.ScaleFormSelectors));

            var formByIndex = formSnapshot.ToDictionary(kv => kv.Key, kv => kv.Value);
            var selectorByIndex = selectorSnapshot.ToDictionary(kv => kv.Key, kv => kv.Value);
            var builder = ImmutableDictionary.CreateBuilder<int, ScaleFormDefinition>();
            foreach (int index in formByIndex.Keys.Union(selectorByIndex.Keys).OrderBy(i => i))
            {
                if (index < 1 || !Scale.CanNameGroup(index))
                    throw new ArgumentException(
                        $"Scale forms are configured for scale {index}, which the NumberScale cannot name; " +
                        "only scale indices from 1 (thousands) that the scale can name are allowed.",
                        nameof(NumberToStringConverterOptions.ScaleForms));

                string name = Scale.GetScaleName(index);
                var baseForms = LexicalFormSet.Create(("singular", name.ToPlural(1)), ("plural", name.ToPlural(2)));
                var effectiveForms = formByIndex.TryGetValue(index, out var overrides) ? baseForms.MergeOverriddenBy(overrides) : baseForms;
                var selector = selectorByIndex.TryGetValue(index, out var configured) ? configured : DefaultLexicalFormSelector.Instance;
                builder.Add(index, new ScaleFormDefinition(effectiveForms, selector));
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Validates and snapshots the per-scale multiplier/scale junctions. Every index must be a scale the
        /// <see cref="Scale"/> can name (≥ 1) and every value must be non-null; values are kept verbatim (an empty
        /// string is a valid junction, whitespace is never trimmed). Called once from the constructor.
        /// </summary>
        /// <param name="separators">The configured junctions by scale index, or <see langword="null"/>.</param>
        /// <returns>The validated junctions by scale index.</returns>
        /// <exception cref="ArgumentException">An index cannot be named by the scale, or a value is null.</exception>
        private ImmutableDictionary<int, string> CompileScaleFormMultiplierSeparators(IReadOnlyDictionary<int, string>? separators)
        {
            var snapshot = separators?.ToArray() ?? [];
            if (snapshot.Length == 0) return ImmutableDictionary<int, string>.Empty;

            foreach (var (index, value) in snapshot)
            {
                if (value is null)
                    throw new ArgumentException($"ScaleFormMultiplierSeparators[{index}] must not be null.",
                        nameof(NumberToStringConverterOptions.ScaleFormMultiplierSeparators));
                if (index < 1 || !Scale.CanNameGroup(index))
                    throw new ArgumentException(
                        $"A multiplier separator is configured for scale {index}, which the NumberScale cannot name; " +
                        "only scale indices from 1 (thousands) that the scale can name are allowed.",
                        nameof(NumberToStringConverterOptions.ScaleFormMultiplierSeparators));
            }
            return snapshot.ToImmutableDictionary(kv => kv.Key, kv => kv.Value);
        }

        /// <summary>
        /// Returns the junction between the multiplier and the noun of scale <paramref name="groupNumber"/>: the exact
        /// <see cref="ScaleFormMultiplierSeparators"/> entry of the scale, else <see cref="NumberScale.MultiplierSeparator"/>,
        /// else the historical <see cref="Separator"/>. Generated scales have no entry of their own (their index may
        /// still carry an exact one).
        /// </summary>
        /// <param name="groupNumber">The scale index.</param>
        /// <returns>The junction, possibly empty.</returns>
        private string GetMultiplierSeparator(int groupNumber)
            => _scaleFormMultiplierSeparators.Count != 0 && _scaleFormMultiplierSeparators.TryGetValue(groupNumber, out var exact)
                ? exact
                : Scale.MultiplierSeparator ?? Separator;

        /// <summary>The largest multiplier the selector of a generated form is probed with at construction.</summary>
        private const int GeneratedScaleFormProbeLimit = 9999;

        /// <summary>
        /// Validates and snapshots the generated scale forms. Every key must be a suffix index of
        /// <see cref="Scale"/> whose generated names the scale can produce; every forced variant must be
        /// declared by the language (and is canonicalized); and the selector, probed once with every
        /// multiplier a group can hold (up to <see cref="GeneratedScaleFormProbeLimit"/>) under the
        /// language defaults, must only return configured keys. Called once from the constructor.
        /// </summary>
        /// <param name="forms">The configured forms by suffix index, or <see langword="null"/>.</param>
        /// <returns>The validated entries by suffix index.</returns>
        /// <exception cref="ArgumentException">An index is invalid, an entry is null, or the scale cannot generate names with that suffix.</exception>
        /// <exception cref="NumberToStringConfigurationException">A forced variant is unknown (<c>UNTS006</c>) or the selector returns an unknown key (<c>UNTS007</c>).</exception>
        private ImmutableDictionary<int, GeneratedScaleForm> CompileGeneratedScaleForms(IReadOnlyDictionary<int, GeneratedScaleForm>? forms)
        {
            var snapshot = forms?.ToArray() ?? [];
            if (snapshot.Length == 0) return ImmutableDictionary<int, GeneratedScaleForm>.Empty;

            int probeLimit = (int)BigInteger.Min(_groupDivisors.Aggregate(BigInteger.Max) - 1, GeneratedScaleFormProbeLimit);
            var builder = ImmutableDictionary.CreateBuilder<int, GeneratedScaleForm>();
            foreach (var (suffix, form) in snapshot.OrderBy(kv => kv.Key))
            {
                string path = $"GeneratedScaleForms[{suffix}]";
                if (form is null)
                    throw new ArgumentException($"{path} must not be null.", nameof(NumberToStringConverterOptions.GeneratedScaleForms));
                if (suffix < 0 || suffix >= Scale.ScaleSuffixes.Count)
                    throw new ArgumentException(
                        $"Generated scale forms are configured for suffix {suffix}, but the NumberScale declares " +
                        $"{Scale.ScaleSuffixes.Count} suffix(es); only suffix indices from 0 to {Scale.ScaleSuffixes.Count - 1} are allowed.",
                        nameof(NumberToStringConverterOptions.GeneratedScaleForms));
                if (!CanGenerateWithSuffix(suffix))
                    throw new ArgumentException(
                        $"Generated scale forms are configured for suffix {suffix}, but the NumberScale cannot generate a scale name with it.",
                        nameof(NumberToStringConverterOptions.GeneratedScaleForms));

                var canonical = form;
                if (form.MultiplierForcedVariants.Count > 0)
                {
                    bool changed = false;
                    var canonicalForced = new Dictionary<string, ForcedVariantSet>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, forced) in form.MultiplierForcedVariants)
                    {
                        var canonicalSet = CanonicalizeForcedVariants(forced, $"{path}.{key}");
                        changed |= !canonicalSet.Constraints.Values.SequenceEqual(forced.Constraints.Values, OrdinalPairComparer.Instance);
                        canonicalForced[key] = canonicalSet;
                    }
                    if (changed) canonical = form.WithForcedVariants(canonicalForced);
                }

                for (int value = 1; value <= probeLimit; value++)
                {
                    string key = canonical.Selector.SelectForm(new LexicalFormContext(value, _defaultVariantQuery));
                    if (!canonical.Forms.TryGetForm(key, out _))
                        throw UnknownGeneratedFormKey(path, key, canonical.Forms);
                }
                builder.Add(suffix, canonical);
            }
            return builder.ToImmutable();
        }

        /// <summary>Determines whether the scale names at least one generated scale with the suffix <paramref name="suffix"/>.</summary>
        /// <param name="suffix">The suffix index.</param>
        /// <returns><see langword="true"/> when the first generated scale using that suffix can be named.</returns>
        private bool CanGenerateWithSuffix(int suffix)
        {
            // Each suffix occurs exactly once among the first ScaleSuffixes.Count generated scales.
            int first = Scale.StaticValues.Count;
            for (int scale = first; scale < first + Scale.ScaleSuffixes.Count; scale++)
                if (Scale.TryGetDynamicScaleInfo(scale, out _, out int index) && index == suffix)
                    return Scale.CanNameGroup(scale);
            return false;
        }

        /// <summary>Creates the <c>UNTS007</c> error of a generated form selector returning an undeclared key.</summary>
        /// <param name="path">The configuration path.</param>
        /// <param name="key">The returned key.</param>
        /// <param name="forms">The declared forms.</param>
        /// <returns>The exception.</returns>
        private NumberToStringConfigurationException UnknownGeneratedFormKey(string path, string key, LexicalFormSet forms)
            => new("UNTS007", LanguageIdentifier, path,
                $"Language '{LanguageIdentifier}', {path}: lexical form selector returned unknown form key '{key}'. " +
                $"Available forms: {string.Join(", ", forms.Keys)}.");

        /// <summary>Compares dimension/value pairs ordinally (canonicalization changes the case of an alias only).</summary>
        private sealed class OrdinalPairComparer : IEqualityComparer<KeyValuePair<string, string>>
        {
            /// <summary>The shared instance.</summary>
            public static readonly OrdinalPairComparer Instance = new();

            /// <inheritdoc />
            public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
                => string.Equals(x.Key, y.Key, StringComparison.Ordinal) && string.Equals(x.Value, y.Value, StringComparison.Ordinal);

            /// <inheritdoc />
            public int GetHashCode(KeyValuePair<string, string> pair) => HashCode.Combine(pair.Key, pair.Value);
        }

        /// <summary>First character of a shield token; never produced by a configuration.</summary>
        private const char ShieldStart = '';

        /// <summary>Last character of a shield token.</summary>
        private const char ShieldEnd = '';

        /// <summary>First of the sixteen private-use characters spelling the index inside a shield token.</summary>
        private const char ShieldDigitBase = '';

        /// <summary>
        /// Returns the list collecting the shielded groups of one conversion (see <see cref="ConvertRaw"/>),
        /// or <see langword="null"/> when no generated scale form is configured, so that languages without
        /// one allocate nothing.
        /// </summary>
        /// <returns>A new empty list, or <see langword="null"/>.</returns>
        private List<string>? NewShieldedGroups() => _generatedScaleFormBySuffix.Length == 0 ? null : [];

        /// <summary>
        /// Runs the whole-number passes made with the caller's query (the global variant rules and the end
        /// triggers) without letting them touch a group whose multiplier was rendered under forced variants:
        /// such a group already went through its complete local pipeline with the forced values, which take
        /// precedence over the caller's. Each shielded group is replaced by an opaque private-use token for
        /// the passes, then restored. A group a preceding replacement or adjustment rewrote can no longer be
        /// located and is processed as before.
        /// </summary>
        /// <param name="text">The assembled cardinal.</param>
        /// <param name="shieldedGroups">The shielded group texts in assembly order, or <see langword="null"/>.</param>
        /// <param name="passes">The whole-number passes.</param>
        /// <returns>The text after the passes, the shielded groups unchanged.</returns>
        /// <exception cref="InvalidOperationException">A pass removed or duplicated a shield token.</exception>
        private string ApplyShielded(string text, List<string>? shieldedGroups, Func<string, string> passes)
        {
            if (shieldedGroups is not { Count: > 0 }) return passes(text);

            var masked = new StringBuilder(text.Length);
            var tokens = new List<(string Token, string Group)>(shieldedGroups.Count);
            int cursor = 0;
            foreach (string group in shieldedGroups)
            {
                int at = text.IndexOf(group, cursor, StringComparison.Ordinal);
                if (at < 0) continue;
                string token = ShieldToken(tokens.Count);
                masked.Append(text, cursor, at - cursor).Append(token);
                tokens.Add((token, group));
                cursor = at + group.Length;
            }
            masked.Append(text, cursor, text.Length - cursor);

            string result = passes(masked.ToString());
            foreach (var (token, group) in tokens)
            {
                int at = result.IndexOf(token, StringComparison.Ordinal);
                if (at < 0 || result.IndexOf(token, at + token.Length, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}': a whole-number variant rule or end trigger rewrote the scale group " +
                        $"'{group}', whose multiplier carries forced variants.");
                result = string.Concat(result.AsSpan(0, at), group, result.AsSpan(at + token.Length));
            }
            return result;
        }

        /// <summary>Spells the shield token of index <paramref name="index"/> with private-use characters only.</summary>
        /// <param name="index">The token index.</param>
        /// <returns>The token.</returns>
        private static string ShieldToken(int index)
        {
            var token = new StringBuilder().Append(ShieldStart);
            do
            {
                token.Append((char)(ShieldDigitBase + (index & 0xF)));
                index >>= 4;
            }
            while (index > 0);
            return token.Append(ShieldEnd).ToString();
        }

        /// <summary>
        /// Returns the scale noun governed by the multiplier <paramref name="group"/> at scale
        /// <paramref name="groupNumber"/> (see <see cref="ResolveScaleWord"/>), without its multiplier variants.
        /// </summary>
        /// <param name="groupNumber">The scale index (0 = units).</param>
        /// <param name="group">The group multiplier.</param>
        /// <param name="variantQuery">The effective variant query, or <see langword="null"/>.</param>
        /// <returns>The scale word (empty for the units chunk).</returns>
        private string GetScaleWord(int groupNumber, long group, IReadOnlyDictionary<string, string>? variantQuery)
            => ResolveScaleWord(groupNumber, group, variantQuery).Word;

        /// <summary>
        /// Resolves the scale noun governed by the multiplier <paramref name="group"/> at scale
        /// <paramref name="groupNumber"/>, in this order: the exact <see cref="ScaleForms"/> entry of the
        /// scale; for a generated scale, the <see cref="GeneratedScaleForms"/> entry of its suffix, whose
        /// selected suffix fragment is joined to the Conway prefix of the scale and whose forced variants
        /// apply to the multiplier; otherwise the historical singular/plural scale name. The selector runs
        /// once, on the caller's effective variants.
        /// </summary>
        /// <param name="groupNumber">The scale index (0 = units).</param>
        /// <param name="group">The group multiplier.</param>
        /// <param name="variantQuery">The effective variant query, or <see langword="null"/>.</param>
        /// <returns>The scale word (empty for the units chunk) and the variants it forces on the multiplier.</returns>
        /// <exception cref="NumberToStringConfigurationException">The selector returned a key with no configured form (<c>UNTS007</c>).</exception>
        private (string Word, ForcedVariantSet MultiplierVariants) ResolveScaleWord(int groupNumber, long group, IReadOnlyDictionary<string, string>? variantQuery)
        {
            if (_scaleForms.Count != 0 && _scaleForms.TryGetValue(groupNumber, out var definition))
            {
                string formKey = definition.Selector.SelectForm(new LexicalFormContext(group, variantQuery ?? s_emptyVariantQuery));
                if (!definition.Forms.TryGetForm(formKey, out string word))
                    throw new NumberToStringConfigurationException("UNTS007", LanguageIdentifier, $"ScaleForms[{groupNumber}]",
                        $"Language '{LanguageIdentifier}', ScaleForms[{groupNumber}]: lexical form selector returned " +
                        $"unknown form key '{formKey}'. Available forms: {string.Join(", ", definition.Forms.Keys)}.");
                return (word, ForcedVariantSet.Empty);
            }

            if (_generatedScaleFormBySuffix.Length != 0
                && Scale.TryGetDynamicScaleInfo(groupNumber, out long prefix, out int suffix)
                && _generatedScaleFormBySuffix[suffix] is { } generated)
            {
                string formKey = generated.Selector.SelectForm(new LexicalFormContext(group, variantQuery ?? _defaultVariantQuery));
                if (!generated.Forms.TryGetForm(formKey, out string suffixForm))
                    throw UnknownGeneratedFormKey($"GeneratedScaleForms[{suffix}]", formKey, generated.Forms);
                var forced = generated.MultiplierForcedVariants.TryGetValue(formKey, out var set) ? set : ForcedVariantSet.Empty;
                return (Scale.BuildDynamicName(prefix, suffixForm), forced);
            }

            return (Scale.GetScaleName(groupNumber).ToPlural(group), ForcedVariantSet.Empty);
        }
    }
}
