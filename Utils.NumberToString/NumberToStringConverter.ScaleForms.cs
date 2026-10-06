using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        /// Returns the scale noun governed by the multiplier <paramref name="group"/> at scale
        /// <paramref name="groupNumber"/>: the historical singular/plural scale name when the scale
        /// has no configured forms, otherwise the form whose key the scale's selector returns.
        /// </summary>
        /// <param name="groupNumber">The scale index (0 = units).</param>
        /// <param name="group">The group multiplier.</param>
        /// <param name="variantQuery">The effective variant query, or <see langword="null"/>.</param>
        /// <returns>The scale word (empty for the units chunk).</returns>
        /// <exception cref="NumberToStringConfigurationException">The selector returned a key with no configured form (<c>UNTS007</c>).</exception>
        private string GetScaleWord(int groupNumber, long group, IReadOnlyDictionary<string, string>? variantQuery)
        {
            if (_scaleForms.Count == 0 || !_scaleForms.TryGetValue(groupNumber, out var definition))
                return Scale.GetScaleName(groupNumber).ToPlural(group);

            string formKey = definition.Selector.SelectForm(new LexicalFormContext(group, variantQuery ?? s_emptyVariantQuery));
            if (!definition.Forms.TryGetForm(formKey, out string word))
                throw new NumberToStringConfigurationException("UNTS007", LanguageIdentifier, $"ScaleForms[{groupNumber}]",
                    $"Language '{LanguageIdentifier}', ScaleForms[{groupNumber}]: lexical form selector returned " +
                    $"unknown form key '{formKey}'. Available forms: {string.Join(", ", definition.Forms.Keys)}.");
            return word;
        }
    }
}
