using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Prints an expression as a strict structural signature: node class, node type, static type, operator method, lifting,
/// exact constant bits, free parameters by reference and bound parameters by binding position. Two trees with the same
/// signature are structurally identical; nothing is simplified and no user code runs (an opaque constant is printed by
/// its type and identity, an extension node by its identity, without reading any of its members).
/// </summary>
internal sealed class StrictForm
{
    /// <summary>The identifiers of the free parameters met so far, by reference (shared by every call on this instance).</summary>
    private readonly Dictionary<ParameterExpression, int> _free = new(ReferenceEqualityComparer.Instance);

    /// <summary>Returns the strict signature of <paramref name="expression"/>.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The signature.</returns>
    public string Of(Expression? expression)
    {
        var builder = new StringBuilder();
        Write(expression, builder, new List<IReadOnlyList<ParameterExpression>>());
        return builder.ToString();
    }

    /// <summary>Writes one node and its children.</summary>
    /// <param name="e">The node.</param>
    /// <param name="b">The output.</param>
    /// <param name="scopes">The open binding scopes, outermost first.</param>
    private void Write(Expression? e, StringBuilder b, List<IReadOnlyList<ParameterExpression>> scopes)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (e is null)
        {
            b.Append("_");
            return;
        }

        if (AuditText.IsExtension(e))
        {
            b.Append("<ext#").Append(RuntimeHelpers.GetHashCode(e)).Append('>');
            return;
        }

        switch (e)
        {
            case ConstantExpression c:
                b.Append("C[").Append(TypeName(c.Type)).Append(':').Append(ConstantText(c.Value)).Append(']');
                return;
            case ParameterExpression p:
                WriteParameter(p, b, scopes);
                return;
            case UnaryExpression u:
                b.Append("U[").Append(u.NodeType).Append(':').Append(TypeName(u.Type)).Append(MethodText(u.Method)).Append(u.IsLifted ? ":L" : "").Append(u.IsLiftedToNull ? ":LN" : "").Append('(');
                Write(u.Operand, b, scopes);
                b.Append(")]");
                return;
            case BinaryExpression bin:
                b.Append("B[").Append(bin.NodeType).Append(':').Append(TypeName(bin.Type)).Append(MethodText(bin.Method)).Append(bin.IsLifted ? ":L" : "").Append(bin.IsLiftedToNull ? ":LN" : "").Append('(');
                Write(bin.Left, b, scopes);
                b.Append(',');
                Write(bin.Right, b, scopes);
                if (bin.Conversion is not null)
                {
                    b.Append(",conv:");
                    Write(bin.Conversion, b, scopes);
                }

                b.Append(")]");
                return;
            case MethodCallExpression call:
                b.Append("M[").Append(MethodText(call.Method)).Append('(');
                Write(call.Object, b, scopes);
                foreach (Expression argument in call.Arguments)
                {
                    b.Append(',');
                    Write(argument, b, scopes);
                }

                b.Append(")]");
                return;
            case LambdaExpression lambda:
                b.Append("L[").Append(TypeName(lambda.Type)).Append(':').Append(lambda.Parameters.Count).Append('(');
                scopes.Add(lambda.Parameters);
                try
                {
                    Write(lambda.Body, b, scopes);
                }
                finally
                {
                    scopes.RemoveAt(scopes.Count - 1);
                }

                b.Append(")]");
                return;
            case BlockExpression block:
                b.Append("K[").Append(TypeName(block.Type)).Append(':').Append(block.Variables.Count).Append('(');
                scopes.Add(block.Variables);
                try
                {
                    foreach (Expression item in block.Expressions)
                    {
                        Write(item, b, scopes);
                        b.Append(';');
                    }
                }
                finally
                {
                    scopes.RemoveAt(scopes.Count - 1);
                }

                b.Append(")]");
                return;
            case ConditionalExpression conditional:
                b.Append("Q[").Append(TypeName(conditional.Type)).Append('(');
                Write(conditional.Test, b, scopes);
                b.Append(',');
                Write(conditional.IfTrue, b, scopes);
                b.Append(',');
                Write(conditional.IfFalse, b, scopes);
                b.Append(")]");
                return;
            case MemberExpression member:
                b.Append("F[").Append(member.Member.DeclaringType?.FullName).Append('.').Append(member.Member.Name).Append('(');
                Write(member.Expression, b, scopes);
                b.Append(")]");
                return;
            case InvocationExpression invocation:
                b.Append("I[(");
                Write(invocation.Expression, b, scopes);
                foreach (Expression argument in invocation.Arguments)
                {
                    b.Append(',');
                    Write(argument, b, scopes);
                }

                b.Append(")]");
                return;
            default:
                b.Append("<").Append(e.GetType().Name).Append('#').Append(RuntimeHelpers.GetHashCode(e)).Append('>');
                return;
        }
    }

    /// <summary>Writes a parameter: bound ones by binding position, free ones by a reference identifier.</summary>
    /// <param name="p">The parameter.</param>
    /// <param name="b">The output.</param>
    /// <param name="scopes">The open binding scopes.</param>
    private void WriteParameter(ParameterExpression p, StringBuilder b, List<IReadOnlyList<ParameterExpression>> scopes)
    {
        for (int depth = scopes.Count - 1; depth >= 0; depth--)
        {
            IReadOnlyList<ParameterExpression> scope = scopes[depth];
            for (int position = 0; position < scope.Count; position++)
            {
                if (ReferenceEquals(scope[position], p))
                {
                    b.Append("P[b").Append(depth).Append('.').Append(position).Append(':').Append(TypeName(p.Type)).Append(']');
                    return;
                }
            }
        }

        if (!_free.TryGetValue(p, out int id))
        {
            id = _free.Count;
            _free.Add(p, id);
        }

        b.Append("P[f").Append(id).Append(':').Append(TypeName(p.Type)).Append(']');
    }

    /// <summary>Returns the exact text of a constant value without calling user code.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    internal static string ConstantText(object? value) => value switch
    {
        null => "null",
        double d => "d" + BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture),
        float f => "f" + BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture),
        decimal m => "m" + string.Join(",", decimal.GetBits(m)),
        string s => "s'" + s + "'",
        bool or char or byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        Enum en => en.GetType().FullName + "." + Convert.ToInt64(en, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        _ => "obj#" + RuntimeHelpers.GetHashCode(value),
    };

    /// <summary>Returns a short type name.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    private static string TypeName(Type t) => Nullable.GetUnderlyingType(t) is Type u ? u.Name + "?" : t.Name;

    /// <summary>Returns the text of an operator method (empty when none).</summary>
    /// <param name="m">The method.</param>
    /// <returns>The text.</returns>
    private static string MethodText(MethodInfo? m)
        => m is null ? "" : ":" + m.DeclaringType?.Name + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + ")";
}

/// <summary>Safe text helpers (never call user code nor read an extension node's members).</summary>
internal static class AuditText
{
    /// <summary>Whether <paramref name="e"/> is a node whose class is outside the framework expression model (an extension node).</summary>
    /// <param name="e">The node.</param>
    /// <returns><see langword="true"/> for an extension node.</returns>
    public static bool IsExtension(Expression e) => e.GetType().Assembly != typeof(Expression).Assembly;

    /// <summary>Formats an expression for a report with the engine's safe formatter.</summary>
    /// <param name="e">The expression.</param>
    /// <returns>The text.</returns>
    public static string Format(Expression? e) => ExpressionTraceFormatter.Format(e, 64, 2000);

    /// <summary>Counts the nodes of a tree (a shared node is counted once per path).</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The node count.</returns>
    public static int Count(Expression? e)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (e is null || IsExtension(e)) return e is null ? 0 : 1;
        return e switch
        {
            UnaryExpression u => 1 + Count(u.Operand),
            BinaryExpression b => 1 + Count(b.Left) + Count(b.Right),
            MethodCallExpression c => 1 + Count(c.Object) + c.Arguments.Sum(Count),
            LambdaExpression l => 1 + Count(l.Body),
            ConditionalExpression q => 1 + Count(q.Test) + Count(q.IfTrue) + Count(q.IfFalse),
            BlockExpression k => 1 + k.Expressions.Sum(Count),
            MemberExpression m => 1 + Count(m.Expression),
            InvocationExpression i => 1 + Count(i.Expression) + i.Arguments.Sum(Count),
            _ => 1,
        };
    }

    /// <summary>Returns the innermost exception of a chain of <see cref="TargetInvocationException"/>.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The innermost exception.</returns>
    public static Exception Innermost(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
        return exception;
    }

    /// <summary>Returns a short description of an exception: its type and, for a resource refusal, its kind.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The description.</returns>
    public static string Describe(Exception exception)
    {
        Exception inner = Innermost(exception);
        return inner is ExpressionResourceLimitException limit ? "ResourceLimit:" + limit.Kind : inner.GetType().Name;
    }
}

/// <summary>
/// Reflection access to the engine's internal diagnostics (the harness is outside the assemblies <c>Utils</c> grants
/// internals to). Every member degrades to "unavailable" when a checkout does not have it.
/// </summary>
internal static class EngineInternals
{
    /// <summary>The internal <c>ExpressionComparer.BeginDiagnosticCapture</c>.</summary>
    private static readonly MethodInfo? BeginCapture = typeof(ExpressionComparer).GetMethod("BeginDiagnosticCapture", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionComparer.EndDiagnosticCapture</c>.</summary>
    private static readonly MethodInfo? EndCapture = typeof(ExpressionComparer).GetMethod("EndDiagnosticCapture", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionComparer.UserCodeInvocationCount</c>.</summary>
    private static readonly PropertyInfo? UserCode = typeof(ExpressionComparer).GetProperty("UserCodeInvocationCount", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionComparer.StructuralEqualsRaw</c>.</summary>
    private static readonly MethodInfo? RawEquals = typeof(ExpressionComparer).GetMethod("StructuralEqualsRaw", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionComparer.StructuralHashRaw</c>.</summary>
    private static readonly MethodInfo? RawHash = typeof(ExpressionComparer).GetMethod("StructuralHashRaw", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>The internal <c>ExpressionCanonicalOrder.Compare(Expression, Expression, IReadOnlyList&lt;ParameterExpression[]&gt;)</c>.</summary>
    private static readonly MethodInfo? CanonicalCompare = typeof(ExpressionComparer).Assembly
        .GetType("Utils.Expressions.ExpressionCanonicalOrder")?
        .GetMethod("Compare", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Expression), typeof(Expression), typeof(IReadOnlyList<ParameterExpression[]>)]);

    /// <summary>Gets whether the comparer diagnostics are available.</summary>
    public static bool HasCapture => BeginCapture is not null && EndCapture is not null;

    /// <summary>Gets whether the raw structural comparison and the canonical order are available.</summary>
    public static bool HasStructural => RawEquals is not null && RawHash is not null && CanonicalCompare is not null;

    /// <summary>Gets the comparer's user-code counter on this thread, or -1 when unavailable.</summary>
    public static long UserCodeCount => UserCode is null ? -1 : (long)UserCode.GetValue(null)!;

    /// <summary>Runs <paramref name="action"/> under the comparer's diagnostic capture.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The number of comparer simplifications, their maximum comparison depth and depth histogram (empty when unavailable).</returns>
    public static (int Count, int MaxDepth, SortedDictionary<int, int> Histogram) Capture(Action action)
    {
        var histogram = new SortedDictionary<int, int>();
        if (!HasCapture)
        {
            action();
            return (-1, -1, histogram);
        }

        BeginCapture!.Invoke(null, null);
        System.Collections.IEnumerable events;
        try
        {
            action();
        }
        finally
        {
            events = (System.Collections.IEnumerable)EndCapture!.Invoke(null, null)!;
        }

        int count = 0, max = 0;
        PropertyInfo? depthProperty = null;
        foreach (object item in events)
        {
            depthProperty ??= item.GetType().GetProperty("ComparisonDepth");
            int depth = depthProperty is null ? 0 : (int)depthProperty.GetValue(item)!;
            histogram[depth] = histogram.TryGetValue(depth, out int n) ? n + 1 : 1;
            max = Math.Max(max, depth);
            count++;
        }

        return (count, max, histogram);
    }

    /// <summary>The raw structural equality (no simplification, safe constants only).</summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <returns>The raw equality.</returns>
    public static bool StructuralEqualsRaw(Expression x, Expression y) => (bool)Invoke(RawEquals!, x, y)!;

    /// <summary>The raw structural hash.</summary>
    /// <param name="x">The tree.</param>
    /// <returns>The raw hash.</returns>
    public static int StructuralHashRaw(Expression x) => (int)Invoke(RawHash!, x)!;

    /// <summary>The S4 canonical order with no enclosing scope.</summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <returns>The sign of the order.</returns>
    public static int CanonicalOrder(Expression x, Expression y) => Math.Sign((int)Invoke(CanonicalCompare!, x, y, Array.Empty<ParameterExpression[]>())!);

    /// <summary>Invokes an internal method and unwraps its exception.</summary>
    /// <param name="method">The method.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result.</returns>
    private static object? Invoke(MethodInfo method, params object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }
    }
}

/// <summary>
/// A tracer that only counts: transformation visits, rule applications per rule, re-entries per requesting rule, failures
/// and the maximum treatment depth. It also derives the dynamic rule-interaction edges (see <see cref="Edges"/>).
/// </summary>
internal sealed class CountingTracer : IExpressionTransformTracer
{
    /// <summary>The requesting rule and parent of each open or finished treatment of the current operation.</summary>
    private readonly Dictionary<(long Session, long Op), (long Parent, string? Via)> _ops = new();

    /// <summary>The rules applied to each treatment, for the "consumed by the parent" edges.</summary>
    private readonly Dictionary<(long Session, long Op), List<string>> _appliedOnChildren = new();

    /// <summary>Gets the number of treatments entered (one per <c>TransformCore</c> visit).</summary>
    public long Visits { get; private set; }

    /// <summary>Gets the number of rule applications.</summary>
    public long RuleApplications { get; private set; }

    /// <summary>Gets the number of failed treatments.</summary>
    public long Failures { get; private set; }

    /// <summary>Gets the maximum treatment depth.</summary>
    public int MaxDepth { get; private set; }

    /// <summary>Gets the applications per rule identifier.</summary>
    public Dictionary<string, long> Applied { get; } = new();

    /// <summary>Gets the re-entries into the engine per requesting rule identifier.</summary>
    public Dictionary<string, long> Reentries { get; } = new();

    /// <summary>
    /// Gets the dynamic interaction edges: <c>via|A|B</c> when rule B was applied inside a treatment a rule A requested,
    /// and <c>consumed|A|B</c> when rule B was applied to the parent of a node rule A had rewritten.
    /// </summary>
    public Dictionary<string, long> Edges { get; } = new();

    /// <summary>Forgets the per-operation state (call between two public operations to bound memory) and the maximum depth.</summary>
    public void ResetOperationState()
    {
        _ops.Clear();
        _appliedOnChildren.Clear();
        MaxDepth = 0;
    }

    /// <inheritdoc/>
    public void OnEvent(in ExpressionTransformEvent e)
    {
        MaxDepth = Math.Max(MaxDepth, e.Depth);
        var key = (e.SessionId, e.OperationId);
        switch (e.Kind)
        {
            case ExpressionTransformEventKind.TransformEnter:
                Visits++;
                string? via = e.Rule?.DisplayName;
                if (via is not null) Reentries[via] = Reentries.TryGetValue(via, out long r) ? r + 1 : 1;
                _ops[key] = (e.ParentOperationId, via);
                break;
            case ExpressionTransformEventKind.RuleApplied when e.Rule is not null:
                RuleApplications++;
                string rule = e.Rule.DisplayName;
                Applied[rule] = Applied.TryGetValue(rule, out long a) ? a + 1 : 1;
                RecordViaEdge(key, rule);
                if (_appliedOnChildren.Remove(key, out List<string>? children))
                {
                    foreach (string child in children) Bump("consumed|" + child + "|" + rule);
                }

                if (_ops.TryGetValue(key, out var self))
                {
                    var parentKey = (e.SessionId, self.Parent);
                    if (!_appliedOnChildren.TryGetValue(parentKey, out List<string>? list)) _appliedOnChildren[parentKey] = list = new List<string>(2);
                    list.Add(rule);
                }

                break;
            case ExpressionTransformEventKind.TransformFailed:
                Failures++;
                break;
        }
    }

    /// <summary>Records the edge from the nearest requesting rule above a treatment to the rule applied on it.</summary>
    /// <param name="key">The treatment.</param>
    /// <param name="rule">The applied rule.</param>
    private void RecordViaEdge((long Session, long Op) key, string rule)
    {
        var current = key;
        for (int guard = 0; guard < 4096 && _ops.TryGetValue(current, out var info); guard++)
        {
            if (info.Via is not null)
            {
                Bump("via|" + info.Via + "|" + rule);
                return;
            }

            if (info.Parent == 0) return;
            current = (key.Session, info.Parent);
        }
    }

    /// <summary>Increments an edge counter.</summary>
    /// <param name="edge">The edge.</param>
    private void Bump(string edge) => Edges[edge] = Edges.TryGetValue(edge, out long n) ? n + 1 : 1;
}

/// <summary>Runs work on a dedicated thread with a large stack, under a per-case watchdog (time and memory).</summary>
internal static class Worker
{
    /// <summary>The stack size of the worker thread.</summary>
    public const int StackBytes = 64 * 1024 * 1024;

    /// <summary>The description of the case in progress, read by the watchdog.</summary>
    private static volatile string s_current = "";

    /// <summary>The start of the case in progress, in <see cref="Stopwatch"/> ticks (0 when idle).</summary>
    private static long s_caseStart;

    /// <summary>Marks the start of a case (the watchdog measures its duration).</summary>
    /// <param name="description">The case description written on a timeout.</param>
    public static void BeginCase(string description)
    {
        s_current = description;
        Interlocked.Exchange(ref s_caseStart, Stopwatch.GetTimestamp());
    }

    /// <summary>Marks the end of the case in progress.</summary>
    public static void EndCase() => Interlocked.Exchange(ref s_caseStart, 0);

    /// <summary>
    /// Runs <paramref name="body"/> on the worker thread. When one case exceeds <paramref name="caseTimeout"/> or the process
    /// exceeds <paramref name="memoryLimitBytes"/>, the watchdog writes the case to <paramref name="onAbort"/> and exits the
    /// process (code 5 for a timeout, 6 for memory): a runaway simplification cannot be stopped from inside the process.
    /// </summary>
    /// <param name="body">The campaign body.</param>
    /// <param name="caseTimeout">The maximum duration of one case.</param>
    /// <param name="memoryLimitBytes">The maximum managed heap size.</param>
    /// <param name="onAbort">Receives the reason and the case description before the process exits.</param>
    /// <returns>The body's exit code.</returns>
    public static int Run(Func<int> body, TimeSpan caseTimeout, long memoryLimitBytes, Action<string, string> onAbort)
    {
        int code = 3;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                code = body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, StackBytes) { IsBackground = true, Name = "audit-worker" };
        thread.Start();
        while (!thread.Join(200))
        {
            long start = Interlocked.Read(ref s_caseStart);
            if (start != 0 && Stopwatch.GetElapsedTime(start) > caseTimeout)
            {
                onAbort("timeout", s_current);
                Environment.Exit(5);
            }

            if (GC.GetTotalMemory(false) > memoryLimitBytes)
            {
                onAbort("memory", s_current);
                Environment.Exit(6);
            }
        }

        if (failure is not null)
        {
            Console.Error.WriteLine("Harness failure: " + failure);
            return 3;
        }

        return code;
    }
}

/// <summary>Writes the results of a campaign: one JSON object per line, plus a Markdown summary.</summary>
internal sealed class ResultWriter : IDisposable
{
    /// <summary>The JSON options (compact, invariant).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>The records file.</summary>
    private readonly StreamWriter _records;

    /// <summary>The anomalies file.</summary>
    private readonly StreamWriter _anomalies;

    /// <summary>Opens the files of <paramref name="campaign"/> in <paramref name="directory"/>, refusing to overwrite existing results unless resuming.</summary>
    /// <param name="directory">The output directory.</param>
    /// <param name="campaign">The campaign name.</param>
    /// <param name="resume">Whether to append to existing files (resumed campaign).</param>
    public ResultWriter(string directory, string campaign, bool resume)
    {
        System.IO.Directory.CreateDirectory(directory);
        Directory = directory;
        Campaign = campaign;
        string records = Path.Combine(directory, campaign + ".jsonl");
        if (File.Exists(records) && !resume) throw new IOException("Results already exist (use a new --out directory or --resume): " + records);
        _records = new StreamWriter(records, append: resume, new UTF8Encoding(false)) { AutoFlush = true };
        _anomalies = new StreamWriter(Path.Combine(directory, campaign + "-anomalies.jsonl"), append: resume, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// <summary>Gets the output directory.</summary>
    public string Directory { get; }

    /// <summary>Gets the campaign name.</summary>
    public string Campaign { get; }

    /// <summary>Gets the number of anomalies written by this instance.</summary>
    public int AnomalyCount { get; private set; }

    /// <summary>Writes one record.</summary>
    /// <param name="record">The record (serialized as JSON).</param>
    public void Record(object record) => _records.WriteLine(JsonSerializer.Serialize(record, record.GetType(), JsonOptions));

    /// <summary>Writes one anomaly.</summary>
    /// <param name="record">The anomaly (serialized as JSON).</param>
    public void Anomaly(object record)
    {
        AnomalyCount++;
        _anomalies.WriteLine(JsonSerializer.Serialize(record, record.GetType(), JsonOptions));
    }

    /// <summary>Writes the Markdown summary of the campaign.</summary>
    /// <param name="markdown">The summary.</param>
    public void Summary(string markdown) => File.WriteAllText(Path.Combine(Directory, Campaign + "-summary.md"), markdown, new UTF8Encoding(false));

    /// <inheritdoc/>
    public void Dispose()
    {
        _records.Dispose();
        _anomalies.Dispose();
    }
}

/// <summary>Simplifies with resource limits and an optional tracer, and classifies the outcome.</summary>
internal static class Simplification
{
    /// <summary>
    /// The simplification policy of the campaigns, read once from the <c>S5_AUDIT_POLICY</c> environment variable (S5 P18
    /// Architecture C): absent or <c>default</c> for <see cref="ExpressionSimplifierOptions.Default"/>, otherwise a comma-separated
    /// list of <c>trust</c>, <c>aggressive</c> and the exception categories still preserved, <c>arithmetic</c>, <c>runtime</c>,
    /// <c>usercode</c> (none listed: no category preserved). <c>permissive</c> stands for <c>trust,aggressive</c>.
    /// </summary>
    public static readonly ExpressionSimplifierOptions Policy = ParsePolicy(Environment.GetEnvironmentVariable("S5_AUDIT_POLICY"));

    /// <summary>Parses the value of <c>S5_AUDIT_POLICY</c>.</summary>
    /// <param name="text">The value, or <see langword="null"/>.</param>
    /// <returns>The policy.</returns>
    private static ExpressionSimplifierOptions ParsePolicy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("default", StringComparison.OrdinalIgnoreCase)) return ExpressionSimplifierOptions.Default;
        if (text.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase)) text = "trust,aggressive";
        var options = new ExpressionSimplifierOptions { ExceptionPreservation = ExpressionExceptionPreservation.None };
        foreach (string item in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            options = item.ToLowerInvariant() switch
            {
                "trust" => options with { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
                "aggressive" => options with { AggressiveSimplification = true },
                "arithmetic" => options with { ExceptionPreservation = options.ExceptionPreservation | ExpressionExceptionPreservation.Arithmetic },
                "runtime" => options with { ExceptionPreservation = options.ExceptionPreservation | ExpressionExceptionPreservation.Runtime },
                "usercode" => options with { ExceptionPreservation = options.ExceptionPreservation | ExpressionExceptionPreservation.UserCode },
                _ => throw new ArgumentException("Unknown S5_AUDIT_POLICY item: " + item),
            };
        }

        return options;
    }

    /// <summary>Simplifies <paramref name="source"/> with a fresh simplifier of <see cref="Policy"/>.</summary>
    /// <param name="source">The source tree.</param>
    /// <param name="limits">The resource limits (default limits when <see langword="null"/>).</param>
    /// <param name="tracer">An optional tracer.</param>
    /// <returns>The result or the exception.</returns>
    public static (Expression? Result, Exception? Error) Run(Expression source, ExpressionResourceLimits? limits = null, IExpressionTransformTracer? tracer = null)
    {
        var simplifier = limits is null ? new ExpressionSimplifier { Options = Policy } : new ExpressionSimplifier(limits) { Options = Policy };
        ExpressionTraceScope? scope = tracer is null ? null : ExpressionTransformTracing.Begin(tracer);
        try
        {
            return (simplifier.Simplify(source), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
        finally
        {
            scope?.Dispose();
            if (scope is not null && scope.UndeliveredEvents != 0) throw new InvalidOperationException("Trace events were dropped: " + scope.UndeliveredEvents);
        }
    }
}
