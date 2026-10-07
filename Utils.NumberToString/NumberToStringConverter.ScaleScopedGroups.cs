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
        /// <summary>
        /// Number of scale indices whose effective group table is precompiled into
        /// <see cref="_groupTableByScale"/>; higher indices (values of 10^192 and above with three-digit
        /// groups) are resolved through the compiled ranges.
        /// </summary>
        private const int ScopedGroupLookupSize = 64;

        /// <summary>
        /// One compiled set of digit tables (all levels) with its own fusion plans. Built once at
        /// construction from private deep copies of the configured digits; never exposed nor mutated
        /// after construction.
        /// </summary>
        private sealed class GroupTable
        {
            /// <summary>Initializes a table from its digit tables.</summary>
            /// <param name="digits">The digit tables keyed by level, then by digit.</param>
            public GroupTable(ImmutableDictionary<int, ImmutableDictionary<long, DigitType>> digits) => Digits = digits;

            /// <summary>Gets the digit tables keyed by level, then by digit.</summary>
            public ImmutableDictionary<int, ImmutableDictionary<long, DigitType>> Digits { get; }

            /// <summary>
            /// Gets or sets the compiled fusion plans, indexed by level, digit and lower sub-group value
            /// (<see langword="null"/> when the table declares no fusion). Set only during construction.
            /// </summary>
            public FusionPlan?[]?[]?[]? FusionPlans { get; set; }
        }

        /// <summary>A compiled scale-scoped table with the range of scale indices it covers.</summary>
        /// <param name="Range">The covered scale indices.</param>
        /// <param name="Table">The table rendering the multipliers of those scales.</param>
        private sealed record ScopedGroupTable(IntRange<long> Range, GroupTable Table);

        /// <summary>The default tables, used for the units chunk and every scale without a scoped table.</summary>
        private GroupTable _defaultGroupTable = null!;

        /// <summary>The scoped tables in declaration order (no two ranges intersect).</summary>
        private ImmutableArray<ScopedGroupTable> _scopedGroupTables = [];

        /// <summary>
        /// Effective table of each scale index below <see cref="ScopedGroupLookupSize"/>, or
        /// <see langword="null"/> when no scoped table is configured (the default table is then used directly).
        /// </summary>
        private GroupTable[]? _groupTableByScale;

        /// <summary>The exposed copies of the scoped tables (see <see cref="ScaleScopedGroups"/>).</summary>
        private ImmutableArray<ScaleScopedGroups> _scaleScopedGroupsPublic = [];

        /// <summary>
        /// Gets the digit tables rendering the multiplier of specific scales instead of <see cref="Groups"/>,
        /// in declaration order. The entries are copies: changing them never affects this converter.
        /// </summary>
        public IReadOnlyList<ScaleScopedGroups> ScaleScopedGroups => _scaleScopedGroupsPublic;

        /// <summary>Gets the position of a group multiplier relative to its scale noun.</summary>
        public ScaleMultiplierPosition ScaleMultiplierPosition { get; private set; }

        /// <summary>Copies a digit definition, including its fusion rules.</summary>
        /// <param name="digit">The digit to copy.</param>
        /// <returns>An independent copy.</returns>
        internal static DigitType CopyDigit(DigitType digit) => new()
        {
            Digit = digit.Digit,
            StringValue = digit.StringValue,
            BuildString = digit.BuildString,
            Fusions = digit.Fusions?.Select(f => f is null ? null! : new FusionType
            {
                For = f.For,
                RemoveLeft = f.RemoveLeft,
                RemoveRight = f.RemoveRight,
                Left = f.Left,
                Right = f.Right,
            }).ToList(),
        };

        /// <summary>Copies a digit list and every digit it contains.</summary>
        /// <param name="list">The list to copy.</param>
        /// <returns>An independent copy.</returns>
        internal static DigitListType CopyDigitList(DigitListType list)
            => new() { Digits = list.Digits?.Select(d => d is null ? null! : CopyDigit(d)).ToList()! };

        /// <summary>Builds a table (without fusion plans) from already validated source tables.</summary>
        /// <param name="source">The validated source tables.</param>
        /// <returns>The table holding private copies of the digits.</returns>
        private static GroupTable BuildGroupTable(IReadOnlyDictionary<int, DigitListType> source)
            => new(source.ToImmutableDictionary(
                kv => kv.Key,
                kv => kv.Value.Digits.ToImmutableDictionary(d => d.Digit, CopyDigit)));

        /// <summary>Builds the exposed view of a table: independent copies keyed like the table.</summary>
        /// <param name="table">The table.</param>
        /// <returns>The exposed view.</returns>
        private static ImmutableDictionary<int, DigitListType> ToPublicGroups(GroupTable table)
            => table.Digits.ToImmutableDictionary(
                kv => kv.Key,
                kv => new DigitListType { Digits = [.. kv.Value.Values.OrderBy(d => d.Digit).Select(CopyDigit)] });

        /// <summary>
        /// Validates and compiles the default and scale-scoped digit tables and the multiplier position.
        /// Fusion plans are compiled later by <see cref="CompileAllFusions"/>, once the exceptions and the
        /// intra-group connector they depend on are known.
        /// </summary>
        /// <param name="options">The converter options.</param>
        /// <exception cref="ArgumentException">A scoped set is invalid or overlaps another one.</exception>
        private void CompileGroupTables(NumberToStringConverterOptions options)
        {
            _defaultGroupTable = BuildGroupTable(options.Groups!);
            ScaleMultiplierPosition = options.ScaleMultiplierPosition;
            if (!Enum.IsDefined(ScaleMultiplierPosition))
                throw new ArgumentOutOfRangeException(nameof(options.ScaleMultiplierPosition),
                    $"Unknown scale multiplier position {(int)ScaleMultiplierPosition}.");

            const string parameterName = nameof(NumberToStringConverterOptions.ScaleScopedGroups);
            ScaleScopedGroups[] snapshot = options.ScaleScopedGroups is null ? [] : [.. options.ScaleScopedGroups];
            if (snapshot.Length == 0) return;

            var defaultLevels = options.Groups!.Keys.OrderBy(k => k).ToArray();
            var compiled = ImmutableArray.CreateBuilder<ScopedGroupTable>(snapshot.Length);
            var exposed = ImmutableArray.CreateBuilder<ScaleScopedGroups>(snapshot.Length);
            for (int i = 0; i < snapshot.Length; i++)
            {
                var scoped = snapshot[i];
                if (scoped is null)
                    throw new ArgumentException($"ScaleScopedGroups[{i}] must not be null.", parameterName);
                string where = $"ScaleScopedGroups[{i}] onScale=\"{scoped.OnScale}\"";
                var range = ParseRuleRange(scoped.OnScale, where, parameterName);
                if (IsEmptyRange(range))
                    throw new ArgumentException($"{where}: the range covers no scale index.", parameterName);
                if (scoped.Groups is null)
                    throw new ArgumentException($"{where}: Groups must not be null.", parameterName);
                ValidateGroupsSource(scoped.Groups, parameterName);
                if (!scoped.Groups.Keys.OrderBy(k => k).SequenceEqual(defaultLevels))
                    throw new ArgumentException(
                        $"{where}: must declare the same group levels as the default Groups ({string.Join(", ", defaultLevels)}).", parameterName);

                string canonical = range.ToString(null, CultureInfo.InvariantCulture)!;
                foreach (var other in compiled)
                {
                    if (IsEmptyRange(other.Range & range)) continue;
                    string otherCanonical = other.Range.ToString(null, CultureInfo.InvariantCulture)!;
                    throw new ArgumentException(otherCanonical == canonical
                        ? $"{where} has the same range as another scoped Groups: a scale must be covered by one set at most."
                        : $"{where} overlaps another scoped Groups (onScale covering {otherCanonical}): a scale must be covered by one set at most.",
                        parameterName);
                }

                var table = BuildGroupTable(scoped.Groups);
                compiled.Add(new ScopedGroupTable(range, table));
                exposed.Add(new ScaleScopedGroups(scoped.OnScale, ToPublicGroups(table)));
            }
            _scopedGroupTables = compiled.MoveToImmutable();
            _scaleScopedGroupsPublic = exposed.MoveToImmutable();

            var byScale = new GroupTable[ScopedGroupLookupSize];
            for (int index = 0; index < byScale.Length; index++)
                byScale[index] = FindScopedTable(index) ?? _defaultGroupTable;
            _groupTableByScale = byScale;
        }

        /// <summary>Returns the scoped table covering <paramref name="scaleIndex"/>, if any.</summary>
        /// <param name="scaleIndex">The scale index.</param>
        /// <returns>The scoped table, or <see langword="null"/>.</returns>
        private GroupTable? FindScopedTable(int scaleIndex)
        {
            foreach (var scoped in _scopedGroupTables)
                if (scoped.Range.Contains(scaleIndex))
                    return scoped.Table;
            return null;
        }

        /// <summary>Returns the table rendering the multiplier of the group at <paramref name="scaleIndex"/>.</summary>
        /// <param name="scaleIndex">The scale index (0 = units).</param>
        /// <returns>The effective table.</returns>
        private GroupTable GetGroupTable(int scaleIndex)
        {
            var byScale = _groupTableByScale;
            if (byScale is null) return _defaultGroupTable;
            return scaleIndex < byScale.Length ? byScale[scaleIndex] : FindScopedTable(scaleIndex) ?? _defaultGroupTable;
        }

        /// <summary>Compiles the fusion plans of the default table and of every scoped table.</summary>
        private void CompileAllFusions()
        {
            CompileFusions(_defaultGroupTable, "Groups", nameof(NumberToStringConverterOptions.Groups));
            foreach (var scoped in _scopedGroupTables)
            {
                string label = $"ScaleScopedGroups[onScale={scoped.Range.ToString(null, CultureInfo.InvariantCulture)}]";
                CompileFusions(scoped.Table, label, nameof(NumberToStringConverterOptions.ScaleScopedGroups));
            }
        }

        /// <summary>
        /// Rejects the <see cref="OrdinalScaleRules"/> that would render a round scale value differently from
        /// its cardinal: <c>OrdinalScale</c> assembles "multiplier + separator + noun" itself, with the
        /// standalone cardinal of the multiplier, so it supports neither a multiplier placed after the noun
        /// nor a scale whose multiplier has its own digit tables.
        /// </summary>
        /// <param name="rule">The rule being compiled.</param>
        /// <param name="index">A scale index the rule covers.</param>
        /// <param name="parameterName">The options property name reported in exceptions.</param>
        /// <exception cref="ArgumentException">The rule cannot be combined with the configuration.</exception>
        private void RejectOrdinalScaleConflicts(OrdinalScaleRule rule, int index, string parameterName)
        {
            if (ScaleMultiplierPosition == ScaleMultiplierPosition.AfterScale)
                throw new ArgumentException(
                    $"OrdinalScale scales=\"{rule.Scales}\" cannot be combined with ScaleMultiplierPosition.AfterScale: it places the multiplier before the scale noun.", parameterName);
            if (FindScopedTable(index) is not null)
                throw new ArgumentException(
                    $"OrdinalScale scales=\"{rule.Scales}\" covers scale {index}, whose multiplier is rendered by a scoped Groups: OrdinalScale renders the standalone cardinal.", parameterName);
        }
    }
}
