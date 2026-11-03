using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Mathematics.Expressions;

namespace Utils.Expressions;

/// <summary>The shape of a periodic rule sequence found by <see cref="ExpressionTraceCycleAnalyzer"/>.</summary>
public enum ExpressionTraceLoopKind
{
    /// <summary>The same form (strict structural identity) comes back once per period: a cycle between forms.</summary>
    StrictCycle,

    /// <summary>The forms grow at every period: an endless expansion.</summary>
    Expansion,

    /// <summary>The forms shrink at every period.</summary>
    Contraction,

    /// <summary>The forms keep their size but never come back: the rewriting drifts without reducing the complexity.</summary>
    Drift,

    /// <summary>The rule sequence repeats, but the sizes of the forms follow no monotonic trend.</summary>
    Irregular,
}

/// <summary>Options of <see cref="ExpressionTraceCycleAnalyzer"/>. This type only holds data.</summary>
public sealed record ExpressionTraceCycleOptions
{
    /// <summary>Gets the default options.</summary>
    public static ExpressionTraceCycleOptions Default { get; } = new();

    /// <summary>Gets the minimum number of consecutive repetitions for a rule sequence to be reported as periodic.</summary>
    public int MinimumRepetitions { get; init; } = 3;

    /// <summary>Gets the longest period (in treatments) searched for.</summary>
    public int MaximumPeriod { get; init; } = 64;

    /// <summary>Gets the maximum number of recurrences reported per session (the first ones, by depth).</summary>
    public int MaximumRecurrences { get; init; } = 16;
}

/// <summary>
/// A form transformed again while it is already being transformed by an enclosing treatment: the recursion re-entered the
/// same problem. This type only holds data.
/// </summary>
/// <param name="SessionId">The traced operation.</param>
/// <param name="Ancestor">The <see cref="ExpressionTransformEventKind.TransformEnter"/> event of the enclosing treatment.</param>
/// <param name="Descendant">The <see cref="ExpressionTransformEventKind.TransformEnter"/> event of the nested treatment of the same form.</param>
/// <param name="Path">The enter events from the treatment after <paramref name="Ancestor"/> down to <paramref name="Descendant"/>, outermost first.</param>
/// <param name="FormSize">The number of nodes of the form.</param>
/// <param name="Confirmed">
/// Whether the raw structural comparison of the engine confirmed the identity found by the fingerprints (it never simplifies
/// and compares extension nodes and non-primitive constants by reference).
/// </param>
public sealed record ExpressionTraceRecurrence(
    long SessionId,
    ExpressionTransformEvent Ancestor,
    ExpressionTransformEvent Descendant,
    IReadOnlyList<ExpressionTransformEvent> Path,
    int FormSize,
    bool Confirmed);

/// <summary>A rule sequence that repeats along the deepest chain of nested treatments. This type only holds data.</summary>
/// <param name="SessionId">The traced operation.</param>
/// <param name="Kind">How the forms evolve from one period to the next.</param>
/// <param name="Steps">One period: for each nested treatment, the rule that requested it, or <c>child Type</c> for a child prepared by its parent.</param>
/// <param name="Repetitions">The number of consecutive repetitions at the deep end of the chain.</param>
/// <param name="StartSizes">The size of the form at the start of each repeated period, outermost first.</param>
/// <param name="LastPeriod">The enter events of the deepest complete period, outermost first.</param>
public sealed record ExpressionTracePeriod(
    long SessionId,
    ExpressionTraceLoopKind Kind,
    IReadOnlyList<string> Steps,
    int Repetitions,
    IReadOnlyList<int> StartSizes,
    IReadOnlyList<ExpressionTransformEvent> LastPeriod);

/// <summary>How the applications of one rule changed the size of the forms. This type only holds data.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Applications">The number of completed applications.</param>
/// <param name="Grew">The applications whose result has more nodes than the rule's input.</param>
/// <param name="KeptSize">The applications whose result has as many nodes as the rule's input.</param>
/// <param name="Shrank">The applications whose result has fewer nodes than the rule's input.</param>
/// <param name="NetSizeChange">The total change of size over all applications.</param>
public sealed record ExpressionTraceRuleBalance(
    ExpressionTransformRuleInfo Rule,
    int Applications,
    int Grew,
    int KeptSize,
    int Shrank,
    long NetSizeChange);

/// <summary>The result of <see cref="ExpressionTraceCycleAnalyzer.Analyze"/>. This type only holds data.</summary>
/// <param name="Recurrences">The re-entries of a form inside its own treatment.</param>
/// <param name="Periods">The periodic rule sequences of the deepest chains.</param>
/// <param name="RuleBalances">The size balance of every applied rule, the most growing first.</param>
/// <param name="MaximumDepth">The deepest treatment seen.</param>
public sealed record ExpressionTraceCycleReport(
    IReadOnlyList<ExpressionTraceRecurrence> Recurrences,
    IReadOnlyList<ExpressionTracePeriod> Periods,
    IReadOnlyList<ExpressionTraceRuleBalance> RuleBalances,
    int MaximumDepth)
{
    /// <summary>Gets whether a form was transformed again inside its own treatment.</summary>
    public bool HasRecurrence => Recurrences.Count > 0;
}

/// <summary>
/// Looks for non-terminating rewriting in trace events (see <see cref="ExpressionTraceCollector"/>): forms re-entered inside
/// their own treatment, periodic rule sequences along the deepest recursion, and rules that never reduce the forms. A
/// diagnostic tool, separate from the engine: it never transforms, simplifies or canonicalizes anything, and never changes how
/// the engine behaves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Strict structural identity.</b> Forms are identified by a structural fingerprint that respects operand order, node types,
/// result types, methods and members, constant values (primitive types, decimal, string, char, Boolean and enumerations by
/// value, everything else by reference) and parameters by reference, so a scope or a lambda parameter is never confused with
/// another one; extension nodes are taken by reference without reading any of their members. Mathematical equivalence is never
/// taken for identity: <c>a + b</c> and <c>b + a</c> are different forms. A reported recurrence is confirmed with the engine's
/// raw structural comparison (<see cref="ExpressionTraceRecurrence.Confirmed"/>).
/// </para>
/// <para>
/// <b>Recursion levels.</b> Recurrences and periods are searched along the chain of nested treatments (parent and child
/// treatments, whether a child was prepared by its parent or requested by a rule), not only between consecutive events, so a
/// loop that goes through several rules and several levels of recursion is found.
/// </para>
/// <para>
/// <b>Resources.</b> Every fingerprint walk runs as an engine-internal structural walk: it counts structural visits and checks
/// the stack, inside the running operation or a default-limited one. Do not call the analyzer from a tracer callback, which
/// refuses engine operations.
/// </para>
/// </remarks>
public static class ExpressionTraceCycleAnalyzer
{
    /// <summary>Analyzes trace events.</summary>
    /// <param name="events">The events, in the order they were received (as returned by <see cref="ExpressionTraceCollector.GetEvents"/>).</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="ExpressionTraceCycleOptions.Default"/>.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ExpressionResourceLimitException">A form exceeded the structural budget or the stack.</exception>
    public static ExpressionTraceCycleReport Analyze(IReadOnlyList<ExpressionTransformEvent> events, ExpressionTraceCycleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        options ??= ExpressionTraceCycleOptions.Default;

        var fingerprints = new Fingerprints();
        var recurrences = new List<ExpressionTraceRecurrence>();
        var periods = new List<ExpressionTracePeriod>();
        int maximumDepth = 0;

        foreach (IGrouping<long, ExpressionTransformEvent> session in events.GroupBy(static e => e.SessionId))
        {
            SessionResult result = AnalyzeSession(session.Key, session.ToArray(), fingerprints, options);
            recurrences.AddRange(result.Recurrences);
            if (result.Period is not null) periods.Add(result.Period);
            maximumDepth = int.Max(maximumDepth, result.MaximumDepth);
        }

        return new ExpressionTraceCycleReport(recurrences, periods, BalanceRules(events, fingerprints), maximumDepth);
    }

    /// <summary>Renders a report as text.</summary>
    /// <param name="report">The report.</param>
    /// <param name="options">The export options for the expressions, or <see langword="null"/> for the defaults.</param>
    /// <returns>The text.</returns>
    public static string ToText(ExpressionTraceCycleReport report, ExpressionTraceExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        options ??= ExpressionTraceExportOptions.Default;
        var text = new StringBuilder();
        string Render(Expression? e) => ExpressionTraceFormatter.Format(e, options.MaxExpressionDepth, options.MaxExpressionLength);
        string Step(ExpressionTransformEvent e) => StepLabel(e) + " -> " + Render(e.Input);

        text.Append("Maximum depth: ").Append(report.MaximumDepth.ToString(CultureInfo.InvariantCulture)).AppendLine();
        text.Append("Recurrences: ").Append(report.Recurrences.Count.ToString(CultureInfo.InvariantCulture)).AppendLine();
        foreach (ExpressionTraceRecurrence recurrence in report.Recurrences)
        {
            text.Append("  Session ").Append(recurrence.SessionId).Append(": #").Append(recurrence.Ancestor.Sequence)
                .Append(" (depth ").Append(recurrence.Ancestor.Depth).Append(") re-entered at #").Append(recurrence.Descendant.Sequence)
                .Append(" (depth ").Append(recurrence.Descendant.Depth).Append("), ").Append(recurrence.FormSize).Append(" nodes, ")
                .Append(recurrence.Confirmed ? "confirmed" : "fingerprint only").AppendLine();
            text.Append("    Form: ").AppendLine(Render(recurrence.Ancestor.Input));
            foreach (ExpressionTransformEvent step in recurrence.Path)
            {
                text.Append("    ").AppendLine(Step(step));
            }
        }

        text.Append("Periods: ").Append(report.Periods.Count.ToString(CultureInfo.InvariantCulture)).AppendLine();
        foreach (ExpressionTracePeriod period in report.Periods)
        {
            text.Append("  Session ").Append(period.SessionId).Append(": ").Append(period.Kind).Append(", period ")
                .Append(period.Steps.Count).Append(", ").Append(period.Repetitions).Append(" repetitions, start sizes ")
                .AppendLine(string.Join(" ", period.StartSizes));
            foreach (ExpressionTransformEvent step in period.LastPeriod)
            {
                text.Append("    ").AppendLine(Step(step));
            }
        }

        text.AppendLine("Rule balances (applications: grew/kept/shrank, net size):");
        foreach (ExpressionTraceRuleBalance balance in report.RuleBalances)
        {
            text.Append("  ").Append(balance.Rule.DisplayName).Append(": ").Append(balance.Applications).Append(": ")
                .Append(balance.Grew).Append('/').Append(balance.KeptSize).Append('/').Append(balance.Shrank)
                .Append(", ").Append(balance.NetSizeChange.ToString("+0;-0;0", CultureInfo.InvariantCulture)).AppendLine();
        }

        return text.ToString();
    }

    /// <summary>The findings of one session.</summary>
    /// <param name="Recurrences">The recurrences.</param>
    /// <param name="Period">The periodic sequence of the deepest chain, if any.</param>
    /// <param name="MaximumDepth">The deepest treatment.</param>
    private sealed record SessionResult(List<ExpressionTraceRecurrence> Recurrences, ExpressionTracePeriod? Period, int MaximumDepth);

    /// <summary>
    /// Replays the treatments of one session as a stack (enter pushes, exit or failure pops), looking up every entered form
    /// among the open treatments of the same transformer, and keeps the deepest chain for the period search.
    /// </summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="events">Its events, in order.</param>
    /// <param name="fingerprints">The fingerprint cache.</param>
    /// <param name="options">The options.</param>
    /// <returns>The findings.</returns>
    private static SessionResult AnalyzeSession(long sessionId, ExpressionTransformEvent[] events, Fingerprints fingerprints, ExpressionTraceCycleOptions options)
    {
        var path = new List<(ExpressionTransformEvent Enter, Fingerprint Form)>();
        var open = new Dictionary<(Fingerprint, Type?), List<int>>();
        var recurrences = new List<ExpressionTraceRecurrence>();

        // A loop re-enters the same forms at every period: each form is reported once, at its first (shallowest) recurrence.
        var reported = new HashSet<(Fingerprint, Type?)>();
        List<(ExpressionTransformEvent Enter, Fingerprint Form)> deepest = [];
        int maximumDepth = 0;

        void Pop()
        {
            if (path.Count > deepest.Count) deepest = [.. path];
            (ExpressionTransformEvent enter, Fingerprint form) = path[^1];
            List<int> positions = open[(form, enter.TransformerType)];
            positions.RemoveAt(positions.Count - 1);
            path.RemoveAt(path.Count - 1);
        }

        foreach (ExpressionTransformEvent e in events)
        {
            maximumDepth = int.Max(maximumDepth, e.Depth);
            switch (e.Kind)
            {
                case ExpressionTransformEventKind.TransformEnter when e.Input is not null:
                    // A circular collector may have dropped the beginning: realign the stack on the parent.
                    while (path.Count > 0 && path[^1].Enter.OperationId != e.ParentOperationId) Pop();

                    Fingerprint form = fingerprints.Of(e.Input);
                    if (!open.TryGetValue((form, e.TransformerType), out List<int>? positions))
                    {
                        positions = [];
                        open.Add((form, e.TransformerType), positions);
                    }

                    if (positions.Count > 0 && recurrences.Count < options.MaximumRecurrences && reported.Add((form, e.TransformerType)))
                    {
                        int ancestor = positions[^1];
                        ExpressionTransformEvent[] between = [.. path.Skip(ancestor + 1).Select(static step => step.Enter), e];
                        bool confirmed = ExpressionComparer.StructuralEqualsRaw(path[ancestor].Enter.Input, e.Input);
                        recurrences.Add(new ExpressionTraceRecurrence(sessionId, path[ancestor].Enter, e, between, form.Size, confirmed));
                    }

                    positions.Add(path.Count);
                    path.Add((e, form));
                    break;

                case ExpressionTransformEventKind.TransformExit or ExpressionTransformEventKind.TransformFailed:
                    int index = path.FindLastIndex(step => step.Enter.OperationId == e.OperationId);
                    while (index >= 0 && path.Count > index) Pop();
                    break;
            }
        }

        if (path.Count > deepest.Count) deepest = [.. path];
        return new SessionResult(recurrences, FindPeriod(sessionId, deepest, options), maximumDepth);
    }

    /// <summary>
    /// Finds the shortest rule sequence that repeats at least <see cref="ExpressionTraceCycleOptions.MinimumRepetitions"/> times
    /// near the deep end of a chain, and classifies how the forms evolve from one period to the next.
    /// </summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="chain">The chain of nested treatments, outermost first.</param>
    /// <param name="options">The options.</param>
    /// <returns>The period, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    /// The chain is cut after its last treatment requested by a rule: what follows is the preparation of the children of the
    /// deepest form (a refusal or a full collector interrupts the recursion anywhere), not part of the loop. The cut chain may
    /// still stop inside a period, so up to <see cref="ExpressionTraceCycleOptions.MaximumPeriod"/> trailing treatments may be
    /// left out of the repetition; the shortest period wins, then the shortest left-out tail. A period made of child
    /// preparations only is the traversal of a deep input and is ignored.
    /// </para>
    /// <para>
    /// The labels may repeat faster than the forms (one rule alternating between two forms): the period reported is the
    /// shortest multiple of the label period over which the forms repeat too, when there is one (a strict cycle).
    /// </para>
    /// </remarks>
    private static ExpressionTracePeriod? FindPeriod(long sessionId, List<(ExpressionTransformEvent Enter, Fingerprint Form)> chain, ExpressionTraceCycleOptions options)
    {
        int end = chain.FindLastIndex(static step => step.Enter.Rule is not null) + 1;
        string[] labels = [.. chain.Take(end).Select(static step => StepLabel(step.Enter))];
        for (int period = 1; period <= options.MaximumPeriod; period++)
        {
            for (int tail = 0; tail <= options.MaximumPeriod; tail++)
            {
                int n = end - tail;
                if (period * options.MinimumRepetitions > n) break;

                int matched = 0;
                while (matched + period < n && labels[n - 1 - matched] == labels[n - 1 - matched - period]) matched++;

                int repetitions = 1 + matched / period;
                if (repetitions < options.MinimumRepetitions) continue;
                if (!chain.Skip(n - period).Take(period).Any(static step => step.Enter.Rule is not null)) continue;

                // The repeated region ends 'tail' treatments before the cut; the forms may repeat over a multiple of the period.
                int first = n - repetitions * period;
                int formPeriod = FormPeriod(chain, first, n, period, options.MaximumPeriod);
                bool strict = formPeriod > 0;
                int reported = strict ? formPeriod : period;
                int count = (n - first) / reported;
                int start = n - count * reported;
                Fingerprint[] starts = [.. Enumerable.Range(0, count).Select(r => chain[start + r * reported].Form)];
                ExpressionTransformEvent[] lastPeriod = [.. chain.Skip(n - reported).Take(reported).Select(static step => step.Enter)];
                ExpressionTraceLoopKind kind = strict ? ExpressionTraceLoopKind.StrictCycle : Classify(starts);
                return new ExpressionTracePeriod(sessionId, kind, labels[(n - reported)..n], count, [.. starts.Select(static s => s.Size)], lastPeriod);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the shortest multiple of <paramref name="period"/> over which every form of the region repeats exactly, at least
    /// twice.
    /// </summary>
    /// <param name="chain">The chain.</param>
    /// <param name="first">The first treatment of the repeated region.</param>
    /// <param name="end">The end of the region (exclusive).</param>
    /// <param name="period">The period of the labels.</param>
    /// <param name="maximumPeriod">The longest period searched.</param>
    /// <returns>The form period, or 0 when the forms never repeat.</returns>
    private static int FormPeriod(List<(ExpressionTransformEvent Enter, Fingerprint Form)> chain, int first, int end, int period, int maximumPeriod)
    {
        for (int candidate = period; candidate <= maximumPeriod && 2 * candidate <= end - first; candidate += period)
        {
            bool repeats = true;
            for (int i = first; i + candidate < end && repeats; i++)
            {
                repeats = chain[i].Form.Equals(chain[i + candidate].Form);
            }

            if (repeats) return candidate;
        }

        return 0;
    }

    /// <summary>Classifies how the forms found at the start of consecutive periods evolve, when they never repeat exactly.</summary>
    /// <param name="starts">The forms, outermost first.</param>
    /// <returns>The kind of loop.</returns>
    private static ExpressionTraceLoopKind Classify(Fingerprint[] starts)
    {
        bool growing = true, shrinking = true, sameSize = true;
        for (int i = 1; i < starts.Length; i++)
        {
            growing &= starts[i].Size > starts[i - 1].Size;
            shrinking &= starts[i].Size < starts[i - 1].Size;
            sameSize &= starts[i].Size == starts[i - 1].Size;
        }

        return growing ? ExpressionTraceLoopKind.Expansion
            : shrinking ? ExpressionTraceLoopKind.Contraction
            : sameSize ? ExpressionTraceLoopKind.Drift
            : ExpressionTraceLoopKind.Irregular;
    }

    /// <summary>Computes the size balance of every rule that completed.</summary>
    /// <param name="events">The events.</param>
    /// <param name="fingerprints">The fingerprint cache.</param>
    /// <returns>The balances, the most growing rule first.</returns>
    private static List<ExpressionTraceRuleBalance> BalanceRules(IReadOnlyList<ExpressionTransformEvent> events, Fingerprints fingerprints)
    {
        var balances = new Dictionary<ExpressionTransformRuleInfo, (int Applications, int Grew, int Kept, int Shrank, long Net)>(ReferenceEqualityComparer.Instance);
        foreach (ExpressionTransformEvent e in events)
        {
            if (e.Kind != ExpressionTransformEventKind.RuleApplied || e.Rule is null || e.Input is null || e.Output is null) continue;

            int delta = fingerprints.Of(e.Output).Size - fingerprints.Of(e.Input).Size;
            balances.TryGetValue(e.Rule, out var balance);
            balances[e.Rule] = (balance.Applications + 1, balance.Grew + (delta > 0 ? 1 : 0), balance.Kept + (delta == 0 ? 1 : 0), balance.Shrank + (delta < 0 ? 1 : 0), balance.Net + delta);
        }

        return [.. balances
            .Select(static pair => new ExpressionTraceRuleBalance((ExpressionTransformRuleInfo)pair.Key, pair.Value.Applications, pair.Value.Grew, pair.Value.Kept, pair.Value.Shrank, pair.Value.Net))
            .OrderByDescending(static balance => balance.Grew + balance.KeptSize)
            .ThenBy(static balance => balance.Rule.Id, StringComparer.Ordinal)];
    }

    /// <summary>Labels a treatment by what requested it: the rule of its parent, or its own node type for a prepared child.</summary>
    /// <param name="enter">The enter event of the treatment.</param>
    /// <returns>The label.</returns>
    private static string StepLabel(ExpressionTransformEvent enter)
        => enter.Rule is not null ? enter.Rule.DisplayName : "child " + (enter.NodeType?.ToString() ?? "Extension");

    /// <summary>A strict structural fingerprint: two 64-bit hashes and the node count.</summary>
    /// <param name="Hash1">The first hash.</param>
    /// <param name="Hash2">The second, independent hash.</param>
    /// <param name="Size">The number of nodes.</param>
    private readonly record struct Fingerprint(ulong Hash1, ulong Hash2, int Size);

    /// <summary>Computes and caches fingerprints by node reference.</summary>
    private sealed class Fingerprints
    {
        /// <summary>The fingerprints already computed, by node reference.</summary>
        private readonly Dictionary<Expression, Fingerprint> _cache = new(ReferenceEqualityComparer.Instance);

        /// <summary>The identity numbers of the objects hashed by reference (parameters, labels, opaque nodes and constants, binders).</summary>
        private readonly Dictionary<object, long> _identities = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// The identity numbers of types and members, by reflection equality: the same method reached through two
        /// <see cref="System.Reflection.MemberInfo"/> objects is one member.
        /// </summary>
        private readonly Dictionary<System.Reflection.MemberInfo, long> _members = [];

        /// <summary>Gets the fingerprint of <paramref name="expression"/>.</summary>
        /// <param name="expression">The form.</param>
        /// <returns>Its fingerprint.</returns>
        public Fingerprint Of(Expression expression)
        {
            if (_cache.TryGetValue(expression, out Fingerprint fingerprint)) return fingerprint;

            fingerprint = ExpressionResourceBudget.RunInternal((Owner: this, Expression: expression), static state =>
            {
                var walker = new FingerprintWalker(state.Owner);
                walker.Visit(state.Expression);
                return walker.Result;
            });
            _cache.Add(expression, fingerprint);
            return fingerprint;
        }

        /// <summary>Gets the identity number of an object compared by reference.</summary>
        /// <param name="value">The object.</param>
        /// <returns>Its number, stable for the analysis.</returns>
        public long Identity(object value)
        {
            if (value is System.Reflection.MemberInfo member)
            {
                if (!_members.TryGetValue(member, out long memberIdentity))
                {
                    memberIdentity = -(_members.Count + 1);
                    _members.Add(member, memberIdentity);
                }

                return memberIdentity;
            }

            if (!_identities.TryGetValue(value, out long identity))
            {
                identity = _identities.Count + 1;
                _identities.Add(value, identity);
            }

            return identity;
        }
    }

    /// <summary>
    /// A read-only walk that hashes a tree in pre-order with explicit end markers (so the shape is exact), counting one
    /// structural visit per node (stack check included). Extension nodes are hashed by reference, before any member is read.
    /// </summary>
    private sealed class FingerprintWalker : ExpressionVisitor
    {
        /// <summary>The cache that numbers the objects hashed by reference.</summary>
        private readonly Fingerprints _owner;

        /// <summary>The first hash accumulator.</summary>
        private ulong _hash1 = 0xcbf29ce484222325UL;

        /// <summary>The second hash accumulator.</summary>
        private ulong _hash2 = 0x84222325cbf29ce4UL;

        /// <summary>The number of nodes.</summary>
        private int _size;

        /// <summary>Initializes a walk.</summary>
        /// <param name="owner">The fingerprint cache.</param>
        public FingerprintWalker(Fingerprints owner) => _owner = owner;

        /// <summary>Gets the fingerprint of the visited tree.</summary>
        public Fingerprint Result => new(_hash1, _hash2, _size);

        /// <summary>Mixes one token into both hashes.</summary>
        /// <param name="token">The token.</param>
        private void Mix(long token)
        {
            ulong value = unchecked((ulong)token);
            _hash1 = BitOperations.RotateLeft((_hash1 ^ value) * 0x100000001b3UL, 23);
            _hash2 = BitOperations.RotateLeft(_hash2 + value * 0x9e3779b97f4a7c15UL, 31) * 0xc2b2ae3d27d4eb4fUL;
        }

        /// <summary>Mixes the identity of an object compared by reference.</summary>
        /// <param name="value">The object, or <see langword="null"/>.</param>
        private void MixIdentity(object? value) => Mix(value is null ? 0 : _owner.Identity(value));

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                Mix(-1);
                return null;
            }

            ExpressionResourceBudget.ConsumeStructuralVisit();
            _size++;
            if (ExpressionOpacity.IsOpaque(node))
            {
                Mix(-2);
                MixIdentity(node);
                return node;
            }

            Mix((long)node.NodeType);
            MixIdentity(node.Type);
            if (node is DynamicExpression dynamic)
            {
                // A plain ExpressionVisitor would route a dynamic operation through its extension path (AUD-02).
                MixIdentity(dynamic.Binder);
                MixIdentity(dynamic.DelegateType);
                foreach (Expression argument in dynamic.Arguments) Visit(argument);
            }
            else
            {
                base.Visit(node);
            }

            Mix(-3);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            switch (node.Value)
            {
                case null: Mix(0); break;
                case double value: Mix(BitConverter.DoubleToInt64Bits(value)); break;
                case float value: Mix(BitConverter.SingleToInt32Bits(value)); break;
                case string or decimal or bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint:
                    // Framework value types and strings: their hash is the framework's, never user code.
                    Mix(node.Value.GetHashCode());
                    Mix(node.Value is string text ? text.Length : 0);
                    break;
                case Enum value when value.GetType().IsEnum: MixIdentity(value.GetType()); Mix(value.GetHashCode()); break;
                default: MixIdentity(node.Value); break;
            }

            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            MixIdentity(node);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            MixIdentity(node.Method);
            Mix(node.IsLiftedToNull ? 1 : 0);
            return base.VisitBinary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            MixIdentity(node.Method);
            return base.VisitUnary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            MixIdentity(node.Method);
            return base.VisitMethodCall(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitMember(MemberExpression node)
        {
            MixIdentity(node.Member);
            return base.VisitMember(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitNew(NewExpression node)
        {
            MixIdentity(node.Constructor);
            return base.VisitNew(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            MixIdentity(node.TypeOperand);
            return base.VisitTypeBinary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitIndex(IndexExpression node)
        {
            MixIdentity(node.Indexer);
            return base.VisitIndex(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitSwitch(SwitchExpression node)
        {
            MixIdentity(node.Comparison);
            return base.VisitSwitch(node);
        }

        /// <inheritdoc/>
        protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
        {
            MixIdentity(node);
            return node;
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            MixIdentity(node.Test);
            return base.VisitCatchBlock(node);
        }

        /// <inheritdoc/>
        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            Mix(node.TestValues.Count);
            return base.VisitSwitchCase(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            // Bindings nest without expression nodes: walk them here instead of letting the framework recurse (ETC R3).
            Visit(node.NewExpression);
            foreach (MemberBinding binding in node.Bindings) WalkBinding(binding);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitListInit(ListInitExpression node)
        {
            Visit(node.NewExpression);
            foreach (ElementInit initializer in node.Initializers) WalkInitializer(initializer);
            return node;
        }

        /// <summary>Hashes one member binding and its children.</summary>
        /// <param name="binding">The binding.</param>
        private void WalkBinding(MemberBinding binding)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            Mix((long)binding.BindingType);
            MixIdentity(binding.Member);
            switch (binding)
            {
                case MemberAssignment assignment: Visit(assignment.Expression); break;
                case MemberMemberBinding nested: foreach (MemberBinding child in nested.Bindings) WalkBinding(child); break;
                case MemberListBinding list: foreach (ElementInit initializer in list.Initializers) WalkInitializer(initializer); break;
            }

            Mix(-4);
        }

        /// <summary>Hashes one collection initializer and its arguments.</summary>
        /// <param name="initializer">The initializer.</param>
        private void WalkInitializer(ElementInit initializer)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            MixIdentity(initializer.AddMethod);
            foreach (Expression argument in initializer.Arguments) Visit(argument);
            Mix(-5);
        }
    }
}
