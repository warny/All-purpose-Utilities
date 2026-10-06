using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Utils.Range;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>A compiled <see cref="OrdinalCompositionRule"/>: its parsed value range and the rule itself.</summary>
        /// <param name="Range">The parsed value range.</param>
        /// <param name="Rule">The rule.</param>
        private sealed record CompiledOrdinalComposition(IntRange<long> Range, OrdinalCompositionRule Rule);

        /// <summary>The configured ordinal scale rules, in declaration order (snapshot).</summary>
        private ImmutableArray<OrdinalScaleRule> _ordinalScaleRules = [];

        /// <summary>
        /// The ordinal scale rule covering each scale index reachable by a <see cref="long"/> value
        /// (index 0 unused), or an empty array when no rule is configured.
        /// </summary>
        private OrdinalScaleRule?[] _ordinalScaleByIndex = [];

        /// <summary>The configured ordinal composition rules, in declaration order (snapshot).</summary>
        private ImmutableArray<OrdinalCompositionRule> _ordinalCompositionRules = [];

        /// <summary>The configured ordinal composition rules with their parsed ranges (snapshot, pairwise disjoint).</summary>
        private ImmutableArray<CompiledOrdinalComposition> _ordinalCompositions = [];

        /// <summary>The number of digits of one scale group, as used by the cardinal decomposition.</summary>
        private int _scaleGroupDigits;

        /// <summary>The values below one, used to reject ranges that cover zero or negative values or scale indices.</summary>
        private static readonly IntRange<long> s_belowOne = ParseRangeExpression("..0");

        /// <summary>
        /// Gets the rules forming the ordinal of a round scale value from the scale noun (see
        /// <see cref="NumberToStringConverterOptions.OrdinalScaleRules"/>), in declaration order.
        /// </summary>
        public IReadOnlyList<OrdinalScaleRule> OrdinalScaleRules => _ordinalScaleRules;

        /// <summary>
        /// Gets the rules composing an analytic ordinal from the ordinals of the numeric head and tail
        /// (see <see cref="NumberToStringConverterOptions.OrdinalCompositionRules"/>), in declaration order.
        /// </summary>
        public IReadOnlyList<OrdinalCompositionRule> OrdinalCompositionRules => _ordinalCompositionRules;

        /// <summary>
        /// Validates and snapshots the ordinal scale and composition rules. Requires <see cref="Groups"/>
        /// and <see cref="Scale"/>. Ranges are parsed and indexed here once; the conversion path only
        /// reads the resulting arrays.
        /// </summary>
        /// <param name="scaleRules">The configured scale rules, or <see langword="null"/>.</param>
        /// <param name="compositionRules">The configured composition rules, or <see langword="null"/>.</param>
        /// <exception cref="ArgumentException">A rule is invalid or overlaps another one.</exception>
        private void CompileOrdinalCompositionRules(IReadOnlyList<OrdinalScaleRule>? scaleRules, IReadOnlyList<OrdinalCompositionRule>? compositionRules)
        {
            _scaleGroupDigits = Groups.Keys.Max();
            CompileOrdinalScaleRules(scaleRules);

            OrdinalCompositionRule[] snapshot = compositionRules is null ? [] : [.. compositionRules];
            var compiled = ImmutableArray.CreateBuilder<CompiledOrdinalComposition>(snapshot.Length);
            const string parameterName = nameof(NumberToStringConverterOptions.OrdinalCompositionRules);
            foreach (var rule in snapshot)
            {
                if (rule is null)
                    throw new ArgumentException("OrdinalComposition rules must not be null.", parameterName);
                if (rule.Divisor < 2)
                    throw new ArgumentException($"OrdinalComposition for=\"{rule.For}\": the divisor must be at least 2; got {rule.Divisor}.", parameterName);
                if (rule.Separator is null)
                    throw new ArgumentException($"OrdinalComposition for=\"{rule.For}\" must declare a separator (an empty string solders the two ordinals).", parameterName);
                var range = ParseRuleRange(rule.For, $"OrdinalComposition for=\"{rule.For}\"", parameterName);
                if (IsEmptyRange(range))
                    throw new ArgumentException($"OrdinalComposition for=\"{rule.For}\": the range covers no value.", parameterName);
                foreach (var other in compiled)
                {
                    if (!IsEmptyRange(other.Range & range))
                        throw new ArgumentException(
                            $"OrdinalComposition for=\"{rule.For}\" overlaps for=\"{other.Rule.For}\": a value must be covered by one rule at most.", parameterName);
                }
                compiled.Add(new CompiledOrdinalComposition(range, rule));
            }
            _ordinalCompositions = compiled.MoveToImmutable();
            _ordinalCompositionRules = ImmutableArray.Create(snapshot);
        }

        /// <summary>Validates the ordinal scale rules and indexes them by scale.</summary>
        /// <param name="rules">The configured rules, or <see langword="null"/>.</param>
        /// <exception cref="ArgumentException">A rule is invalid, covers an index the scale cannot name, or overlaps another rule.</exception>
        private void CompileOrdinalScaleRules(IReadOnlyList<OrdinalScaleRule>? rules)
        {
            OrdinalScaleRule[] snapshot = rules is null ? [] : [.. rules];
            _ordinalScaleRules = ImmutableArray.Create(snapshot);
            if (snapshot.Length == 0) return;

            const string parameterName = nameof(NumberToStringConverterOptions.OrdinalScaleRules);
            // Above this index a scale unit exceeds 10^18, so no long value can be round there: its
            // highest group is always at a lower index (the next unit would exceed long.MaxValue).
            int maxIndex = (_decimalPowersOfTen.Length - 1) / _scaleGroupDigits;
            var byIndex = new OrdinalScaleRule?[maxIndex + 1];
            foreach (var rule in snapshot)
            {
                if (rule is null)
                    throw new ArgumentException("OrdinalScale rules must not be null.", parameterName);
                if (rule.MultiplierSeparator is null)
                    throw new ArgumentException($"OrdinalScale scales=\"{rule.Scales}\" must declare a multiplierSeparator (an empty string solders the multiplier).", parameterName);
                var range = ParseRuleRange(rule.Scales, $"OrdinalScale scales=\"{rule.Scales}\"", parameterName);
                bool coversReachableScale = false;
                for (int index = 1; index <= maxIndex; index++)
                {
                    if (!range.Contains(index)) continue;
                    if (!Scale.CanNameGroup(index))
                        throw new ArgumentException($"OrdinalScale scales=\"{rule.Scales}\" covers scale {index}, which the NumberScale cannot name.", parameterName);
                    if (byIndex[index] is { } other)
                        throw new ArgumentException(
                            $"OrdinalScale scales=\"{rule.Scales}\" and scales=\"{other.Scales}\" both cover scale {index}: a scale must be covered by one rule at most.", parameterName);
                    byIndex[index] = rule;
                    coversReachableScale = true;
                }
                if (!coversReachableScale)
                    throw new ArgumentException($"OrdinalScale scales=\"{rule.Scales}\" covers no scale index reachable by a 64-bit value (1..{maxIndex}).", parameterName);
            }
            _ordinalScaleByIndex = byIndex;
        }

        /// <summary>Parses a rule range and requires every covered value to be at least one.</summary>
        /// <param name="expression">The range expression.</param>
        /// <param name="where">Diagnostic location prefix.</param>
        /// <param name="parameterName">The options property name reported in exceptions.</param>
        /// <returns>The parsed range.</returns>
        /// <exception cref="ArgumentException">The expression is missing, malformed, or covers a value below one.</exception>
        private static IntRange<long> ParseRuleRange(string? expression, string where, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException($"{where}: the range is required.", parameterName);
            IntRange<long> range;
            try
            {
                range = ParseRangeExpression(expression);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException or InvalidOperationException)
            {
                throw new ArgumentException($"{where}: the range cannot be parsed ({exception.Message}).", parameterName, exception);
            }
            if (!IsEmptyRange(range & s_belowOne))
                throw new ArgumentException($"{where}: every covered value must be at least 1.", parameterName);
            return range;
        }

        /// <summary>
        /// Determines whether <paramref name="range"/> covers no value, without enumerating it (an
        /// open bound cannot be enumerated): an empty range has no interval in its canonical form.
        /// </summary>
        /// <param name="range">The range to test.</param>
        /// <returns><see langword="true"/> when the range is empty.</returns>
        private static bool IsEmptyRange(IntRange<long> range)
            => string.IsNullOrEmpty(range.ToString(null, CultureInfo.InvariantCulture));

        /// <summary>
        /// Composes the ordinal of <paramref name="number"/> from the ordinals of its head and tail when a
        /// composition rule covers it and both parts are non-zero. Each part is built by
        /// <see cref="BuildOrdinalCore"/> (plugin, exceptions, compositions, scale ordinals, cardinal
        /// transformation) without raw adjustment, end triggers or finalization, which the caller applies
        /// once to the whole result.
        /// </summary>
        /// <param name="number">The non-negative ordinal value.</param>
        /// <param name="activeVariants">The resolved variant query, passed to both parts.</param>
        /// <param name="explicitVariantIntent">Whether a caller or constituent explicitly selected a variant.</param>
        /// <param name="ordinal">Receives the composed ordinal.</param>
        /// <returns><see langword="true"/> when the value was composed.</returns>
        /// <remarks>
        /// Termination: head and tail are both non-zero, so each is strictly smaller than
        /// <paramref name="number"/>; the recursion depth is therefore bounded without any runtime
        /// bookkeeping (in practice by the number of digits, since a rule only splits off the
        /// remainder of its divisor).
        /// </remarks>
        private bool TryComposeOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, bool explicitVariantIntent, out string ordinal)
        {
            ordinal = string.Empty;
            foreach (var composition in _ordinalCompositions)
            {
                if (!composition.Range.Contains(number)) continue;
                long tail = number % composition.Rule.Divisor;
                long head = number - tail;
                if (head == 0 || tail == 0) return false;
                ordinal = string.Concat(
                    BuildOrdinalCore(head, activeVariants, explicitVariantIntent),
                    composition.Rule.Separator,
                    BuildOrdinalCore(tail, activeVariants, explicitVariantIntent));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Builds the text whose ordinal transformation gives the ordinal of a round scale value covered
        /// by an <see cref="OrdinalScaleRule"/>: the singular scale noun alone for a multiplier of one,
        /// otherwise the multiplier's cardinal, the rule's separator and the singular scale noun.
        /// </summary>
        /// <param name="number">The non-negative ordinal value.</param>
        /// <param name="activeVariants">The resolved variant query (multiplier cardinal and scale noun).</param>
        /// <param name="text">Receives the text to transform.</param>
        /// <returns><see langword="true"/> when <paramref name="number"/> is round at a covered scale.</returns>
        private bool TryBuildRoundScaleText(long number, IReadOnlyDictionary<string, string> activeVariants, out string text)
        {
            text = string.Empty;
            // From the highest index down, the first unit not above the value is its highest scale.
            for (int index = _ordinalScaleByIndex.Length - 1; index >= 1; index--)
            {
                long unit = _decimalPowersOfTen[index * _scaleGroupDigits];
                if (number < unit) continue;
                if (_ordinalScaleByIndex[index] is not { } rule || number % unit != 0) return false;
                long multiplier = number / unit;
                string noun = GetScaleWord(index, 1, activeVariants);
                text = multiplier == 1
                    ? noun
                    : string.Concat(ConvertRaw(multiplier, activeVariants), rule.MultiplierSeparator, noun);
                return true;
            }
            return false;
        }
    }
}
