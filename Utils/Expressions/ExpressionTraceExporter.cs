using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Utils.Mathematics.Expressions;

namespace Utils.Expressions;

/// <summary>Options of <see cref="ExpressionTraceExporter"/>. This type only holds data.</summary>
public sealed record ExpressionTraceExportOptions
{
    /// <summary>Gets the default options.</summary>
    public static ExpressionTraceExportOptions Default { get; } = new();

    /// <summary>Gets the maximum length of a rendered expression (see <see cref="ExpressionTraceFormatter"/>).</summary>
    public int MaxExpressionLength { get; init; } = 200;

    /// <summary>Gets the maximum depth of a rendered expression (see <see cref="ExpressionTraceFormatter"/>).</summary>
    public int MaxExpressionDepth { get; init; } = 24;

    /// <summary>
    /// Gets whether the treatments of constants and parameters that end unchanged are exported. Defaults to
    /// <see langword="false"/> for the text export: they are numerous and carry no rewriting. The JSON export always keeps them.
    /// </summary>
    public bool IncludeUnchangedLeaves { get; init; }

    /// <summary>
    /// Gets whether the messages of the exceptions are exported. Defaults to <see langword="false"/>:
    /// <see cref="Exception.Message"/> is virtual and may run user code, so only the exception types are exported by default.
    /// </summary>
    public bool IncludeExceptionMessages { get; init; }
}

/// <summary>
/// Exports trace events (see <see cref="ExpressionTraceCollector"/>) as an indented text, one line per event nested by
/// treatment depth, or as JSON. Exporting is an explicit step, outside the engine: nothing is rendered while tracing.
/// </summary>
/// <remarks>
/// Expressions are rendered by <see cref="ExpressionTraceFormatter"/> (bounded, blind to extension nodes); exceptions by their
/// type names, and their messages only on request (<see cref="ExpressionTraceExportOptions.IncludeExceptionMessages"/>).
/// </remarks>
public static class ExpressionTraceExporter
{
    /// <summary>Exports <paramref name="events"/> as indented text.</summary>
    /// <param name="events">The events, in the order they were received.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="ExpressionTraceExportOptions.Default"/>.</param>
    /// <returns>The text.</returns>
    public static string ToText(IEnumerable<ExpressionTransformEvent> events, ExpressionTraceExportOptions? options = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteText(writer, events, options);
        return writer.ToString();
    }

    /// <summary>Writes <paramref name="events"/> as indented text.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="events">The events, in the order they were received.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="ExpressionTraceExportOptions.Default"/>.</param>
    public static void WriteText(TextWriter writer, IEnumerable<ExpressionTransformEvent> events, ExpressionTraceExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(events);
        options ??= ExpressionTraceExportOptions.Default;

        IReadOnlyList<ExpressionTransformEvent> list = events as IReadOnlyList<ExpressionTransformEvent> ?? events.ToArray();
        HashSet<(long Session, long Operation)> hidden = options.IncludeUnchangedLeaves ? [] : FindUnchangedLeaves(list);
        Dictionary<long, int> baseDepths = MinimumDepths(list);
        long session = -1;
        int baseDepth = 0;
        foreach (ExpressionTransformEvent traceEvent in list)
        {
            if (traceEvent.SessionId != session)
            {
                // Sessions traced concurrently by one collector interleave: each switch repeats the session header.
                session = traceEvent.SessionId;
                baseDepth = baseDepths[session];
                writer.WriteLine("Session " + session.ToString(CultureInfo.InvariantCulture));
            }

            if (IsLeafEvent(traceEvent) && hidden.Contains((traceEvent.SessionId, traceEvent.OperationId))) continue;

            string indent = new(' ', 2 * (1 + int.Max(0, traceEvent.Depth - baseDepth)));
            writer.WriteLine(indent + Describe(traceEvent, options));
            foreach (string detail in Details(traceEvent, options))
            {
                writer.WriteLine(indent + "   " + detail);
            }
        }
    }

    /// <summary>Exports <paramref name="events"/> as a JSON array, one object per event.</summary>
    /// <param name="events">The events, in the order they were received.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="ExpressionTraceExportOptions.Default"/>.</param>
    /// <returns>The JSON text.</returns>
    public static string ToJson(IEnumerable<ExpressionTransformEvent> events, ExpressionTraceExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        options ??= ExpressionTraceExportOptions.Default;

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartArray();
            foreach (ExpressionTransformEvent traceEvent in events)
            {
                WriteJson(json, traceEvent, options);
            }

            json.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes one event as a JSON object.</summary>
    /// <param name="json">The writer.</param>
    /// <param name="traceEvent">The event.</param>
    /// <param name="options">The options.</param>
    private static void WriteJson(Utf8JsonWriter json, in ExpressionTransformEvent traceEvent, ExpressionTraceExportOptions options)
    {
        json.WriteStartObject();
        json.WriteNumber("session", traceEvent.SessionId);
        json.WriteNumber("sequence", traceEvent.Sequence);
        json.WriteString("kind", traceEvent.Kind.ToString());
        json.WriteNumber("operation", traceEvent.OperationId);
        json.WriteNumber("parent", traceEvent.ParentOperationId);
        json.WriteNumber("depth", traceEvent.Depth);
        WriteOptional(json, "nodeType", traceEvent.NodeType?.ToString());
        WriteOptional(json, "transformer", traceEvent.TransformerType?.FullName);
        WriteOptional(json, "rule", traceEvent.Rule?.Id);
        WriteOptional(json, "ruleName", traceEvent.Rule?.DisplayName);
        if (traceEvent.Input is not null) json.WriteString("input", Render(traceEvent.Input, options));
        if (traceEvent.Output is not null) json.WriteString("output", Render(traceEvent.Output, options));
        if (traceEvent.Outcome != ExpressionTransformOutcome.None) json.WriteString("outcome", traceEvent.Outcome.ToString());
        if (traceEvent.Rejection != ExpressionRuleRejection.None) json.WriteString("rejection", traceEvent.Rejection.ToString());
        if (traceEvent.ScopeKind != ExpressionTraceScopeKind.None) json.WriteString("scope", traceEvent.ScopeKind.ToString());
        if (traceEvent.Exception is not null) json.WriteString("exception", DescribeException(traceEvent.Exception, options));
        WriteOptional(json, "resourceLimit", traceEvent.ResourceLimit?.ToString());
        json.WriteEndObject();
    }

    /// <summary>Writes a string property when the value is present.</summary>
    /// <param name="json">The writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    private static void WriteOptional(Utf8JsonWriter json, string name, string? value)
    {
        if (value is not null) json.WriteString(name, value);
    }

    /// <summary>Describes an event on one line (without its before/after details).</summary>
    /// <param name="e">The event.</param>
    /// <param name="options">The options.</param>
    /// <returns>The line.</returns>
    private static string Describe(in ExpressionTransformEvent e, ExpressionTraceExportOptions options)
    {
        string head = "#" + e.Sequence.ToString(CultureInfo.InvariantCulture) + " ";
        string op = " op " + e.OperationId.ToString(CultureInfo.InvariantCulture);
        string node = e.NodeType?.ToString() ?? (e.Input is null ? "-" : "Extension");
        return e.Kind switch
        {
            ExpressionTransformEventKind.TransformEnter => head + "Enter " + node + op + (e.Rule is null ? "" : " via " + e.Rule.DisplayName) + ": " + Render(e.Input, options),
            ExpressionTransformEventKind.TransformExit => head + "Exit" + op + " " + OutcomeText(e) + (e.Rule is null ? "" : " by " + e.Rule.DisplayName)
                + (ReferenceEquals(e.Output, e.Input) ? "" : ": " + Render(e.Output, options)),
            ExpressionTransformEventKind.RuleApplied => head + "RuleApplied " + e.Rule?.DisplayName + op,
            ExpressionTransformEventKind.Finalize => head + "Finalize" + op + " " + OutcomeText(e),
            ExpressionTransformEventKind.ResourceLimitReached => head + "ResourceLimitReached " + e.ResourceLimit + op + (e.Rule is null ? "" : " during " + e.Rule.DisplayName),
            ExpressionTransformEventKind.TransformFailed => head + "TransformFailed" + op + " " + DescribeException(e.Exception, options) + (e.Rule is null ? "" : " during " + e.Rule.DisplayName),
            ExpressionTransformEventKind.RuleCandidate => head + "RuleCandidate " + e.Rule?.DisplayName + op,
            ExpressionTransformEventKind.RuleRejected => head + "RuleRejected " + e.Rule?.DisplayName + op + " (" + e.Rejection + ")",
            ExpressionTransformEventKind.ScopeEnter or ExpressionTransformEventKind.ScopeExit => head + e.Kind + " " + e.ScopeKind + op,
            _ => head + e.Kind + op,
        };
    }

    /// <summary>Gets the before/after lines of a rule application or of a finalization that changed the node.</summary>
    /// <param name="e">The event.</param>
    /// <param name="options">The options.</param>
    /// <returns>The detail lines.</returns>
    private static IEnumerable<string> Details(ExpressionTransformEvent e, ExpressionTraceExportOptions options)
    {
        bool changed = e.Kind == ExpressionTransformEventKind.RuleApplied
            || (e.Kind == ExpressionTransformEventKind.Finalize && e.Outcome == ExpressionTransformOutcome.Finalized);
        if (!changed) yield break;

        yield return "Before: " + Render(e.Input, options);
        yield return "After:  " + Render(e.Output, options);
    }

    /// <summary>Gets the outcome label; a simplifier's finalization that changes the node is its canonicalization.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The label.</returns>
    private static string OutcomeText(in ExpressionTransformEvent e)
        => e.Outcome == ExpressionTransformOutcome.Finalized && e.TransformerType is not null && typeof(ExpressionSimplifier).IsAssignableFrom(e.TransformerType)
            ? "Finalized (canonicalization)"
            : e.Outcome.ToString();

    /// <summary>Renders an expression with the options' bounds.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="options">The options.</param>
    /// <returns>The rendering.</returns>
    private static string Render(Expression? expression, ExpressionTraceExportOptions options)
        => ExpressionTraceFormatter.Format(expression, options.MaxExpressionDepth, options.MaxExpressionLength);

    /// <summary>
    /// Describes an exception by its type names, through the <see cref="TargetInvocationException"/> layers of rule invocation
    /// (and its message only on request).
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="options">The options.</param>
    /// <returns>The description.</returns>
    private static string DescribeException(Exception? exception, ExpressionTraceExportOptions options)
    {
        if (exception is null) return "-";

        var text = new StringBuilder(exception.GetType().Name);
        Exception current = exception;
        while (current is TargetInvocationException { InnerException: { } inner })
        {
            current = inner;
            text.Append(" > ").Append(current.GetType().Name);
        }

        if (current is ExpressionResourceLimitException resource) text.Append(" (").Append(resource.Kind).Append(')');
        if (options.IncludeExceptionMessages) text.Append(": ").Append(current.Message);
        return text.ToString();
    }

    /// <summary>Whether an event is one of the three events of a treatment (enter, finalization, exit).</summary>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> for an enter, finalize or exit event.</returns>
    private static bool IsLeafEvent(in ExpressionTransformEvent e)
        => e.Kind is ExpressionTransformEventKind.TransformEnter or ExpressionTransformEventKind.Finalize or ExpressionTransformEventKind.TransformExit;

    /// <summary>Finds the treatments of constants and parameters that ended unchanged.</summary>
    /// <param name="events">The events.</param>
    /// <returns>Their (session, treatment) pairs.</returns>
    private static HashSet<(long Session, long Operation)> FindUnchangedLeaves(IReadOnlyList<ExpressionTransformEvent> events)
    {
        var leaves = new HashSet<(long, long)>();
        foreach (ExpressionTransformEvent e in events)
        {
            if (e.Kind == ExpressionTransformEventKind.TransformExit
                && e.Outcome is ExpressionTransformOutcome.Unchanged or ExpressionTransformOutcome.Rebuilt
                && e.NodeType is ExpressionType.Constant or ExpressionType.Parameter)
            {
                leaves.Add((e.SessionId, e.OperationId));
            }
        }

        return leaves;
    }

    /// <summary>Gets the smallest depth of the events of each session (a circular collector may have dropped its root events).</summary>
    /// <param name="events">The events.</param>
    /// <returns>The smallest depth, by session.</returns>
    private static Dictionary<long, int> MinimumDepths(IReadOnlyList<ExpressionTransformEvent> events)
    {
        var minimums = new Dictionary<long, int>();
        foreach (ExpressionTransformEvent e in events)
        {
            minimums[e.SessionId] = minimums.TryGetValue(e.SessionId, out int minimum) ? int.Min(minimum, e.Depth) : e.Depth;
        }

        return minimums;
    }
}
