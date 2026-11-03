using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionTracingBench;

/// <summary>
/// Measures one engine operation on one scenario and prints one CSV line:
/// <c>label,scenario,mode,medianNs,p25Ns,p75Ns,bytesPerOp,events</c>.
/// </summary>
/// <remarks>
/// Usage: <c>ExpressionTracingBench &lt;scenario&gt; [label] [off|minimal|collector]</c>, or <c>ExpressionTracingBench list</c>.
/// Each run measures a single scenario in a fresh process, so the variants compared by <c>run-campaign.ps1</c> never share a
/// JIT state. Only the public API is used, so the harness builds against any checkout that has the tracing API.
/// </remarks>
public static class Program
{
    /// <summary>The parameters of the scenarios.</summary>
    private static readonly ParameterExpression[] Xs = Enumerable.Range(0, 64).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();

    /// <summary>
    /// The scenarios: <c>ext-root</c> isolates the fixed cost of a public operation (an opaque root, a single visit); the others
    /// grow the number of visited nodes, so a per-node cost shows as a drift proportional to their size.
    /// </summary>
    private static readonly Dictionary<string, Func<Scenario>> Scenarios = new()
    {
        ["ext-root"] = () => Simplify(new BenchExtension()),
        ["scalar"] = () => Simplify(Zero(One(Xs[0]))),
        ["binary"] = () => Simplify(Expression.Subtract(Expression.Add(Expression.Multiply(Xs[0], Expression.Constant(2.0)), Expression.Multiply(Xs[1], Expression.Constant(3.0))), Expression.Multiply(Xs[0], Expression.Constant(2.0)))),
        ["lambda"] = () => Simplify(Expression.Lambda<Func<double, double, double>>(Zero(One(Expression.Add(Xs[0], Xs[1]))), Xs[0], Xs[1])),
        ["leftsum20"] = () => Simplify(Xs.Take(20).Aggregate<Expression>(Expression.Add)),
        ["p9family16"] = () => Simplify(Enumerable.Range(0, 16).Select(i => (Expression)Expression.Multiply(Expression.Constant(i + 2.0), Xs[i % 4])).Aggregate(Expression.Add)),
        ["dag10"] = () => Simplify(Dag(10)),
        ["block16"] = () => Simplify(BlockOf(16)),
        ["li1024"] = () => Simplify(Expression.ListInit(Expression.New(typeof(List<double>)), Enumerable.Range(0, 1024).Select(i => One(Xs[i % 64])))),
        ["cmp-pos"] = () => Compare(Chain(8), Chain(8)),
        ["hash"] = () => Hash(Chain(8)),
    };

    /// <summary>Runs one measurement.</summary>
    /// <param name="args">The scenario, the label and the tracing mode.</param>
    /// <returns>0 on success, 2 on an unknown argument.</returns>
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "list")
        {
            Console.WriteLine(string.Join(Environment.NewLine, Scenarios.Keys));
            return args.Length == 0 ? 2 : 0;
        }

        if (!Scenarios.TryGetValue(args[0], out Func<Scenario>? factory))
        {
            Console.Error.WriteLine("Unknown scenario: " + args[0]);
            return 2;
        }

        string label = args.Length > 1 ? args[1] : "run";
        string mode = args.Length > 2 ? args[2] : "off";
        var counter = new CountingTracer();
        IDisposable? scope = mode switch
        {
            "off" => null,
            "minimal" => ExpressionTransformTracing.Begin(counter),
            "collector" => ExpressionTransformTracing.Begin(new CountingCollector(counter, 65_536)),
            _ => null,
        };
        if (scope is null && mode != "off")
        {
            Console.Error.WriteLine("Unknown mode: " + mode);
            return 2;
        }

        Func<object?> operation = factory().Operation;
        Measurement measurement = Measure(operation);
        scope?.Dispose();

        long eventsPerOp = measurement.Operations == 0 ? 0 : counter.Count / (measurement.Operations + measurement.WarmupOperations);
        Console.WriteLine(string.Join(
            ",",
            label,
            args[0],
            mode,
            measurement.Median.ToString("F1", CultureInfo.InvariantCulture),
            measurement.P25.ToString("F1", CultureInfo.InvariantCulture),
            measurement.P75.ToString("F1", CultureInfo.InvariantCulture),
            measurement.BytesPerOperation.ToString(CultureInfo.InvariantCulture),
            eventsPerOp.ToString(CultureInfo.InvariantCulture)));
        return 0;
    }

    /// <summary>Warms the operation up (at least 10 calls and 1.5 s), then times 25 batches of about 20 ms each.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The per-operation quartiles and allocations.</returns>
    private static Measurement Measure(Func<object?> operation)
    {
        var warm = Stopwatch.StartNew();
        long warmup = 0;
        while (warmup < 10 || warm.ElapsedMilliseconds < 1500)
        {
            operation();
            warmup++;
        }

        var calibration = Stopwatch.StartNew();
        int batch = 0;
        while (calibration.ElapsedMilliseconds < 20)
        {
            operation();
            batch++;
        }

        warmup += batch;
        batch = Math.Max(1, batch);
        var samples = new List<double>();
        long bytes = 0, operations = 0;
        for (int b = 0; b < 25; b++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < batch; i++)
            {
                operation();
            }

            watch.Stop();
            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
            operations += batch;
            samples.Add(watch.Elapsed.TotalNanoseconds / batch);
        }

        samples.Sort();
        return new Measurement(samples[samples.Count / 4], samples[samples.Count / 2], samples[samples.Count * 3 / 4], bytes / operations, operations, warmup);
    }

    /// <summary>A simplification scenario.</summary>
    /// <param name="tree">The tree to simplify.</param>
    /// <returns>The scenario.</returns>
    private static Scenario Simplify(Expression tree)
    {
        var simplifier = new ExpressionSimplifier();
        return new Scenario(() => simplifier.Simplify(tree));
    }

    /// <summary>A structural comparison scenario.</summary>
    /// <param name="left">The left tree.</param>
    /// <param name="right">The right tree (a distinct instance, so the reference fast path does not apply).</param>
    /// <returns>The scenario.</returns>
    private static Scenario Compare(Expression left, Expression right) => new(() => ExpressionComparer.Default.Equals(left, right));

    /// <summary>A structural hash scenario.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The scenario.</returns>
    private static Scenario Hash(Expression tree) => new(() => ExpressionComparer.Default.GetHashCode(tree));

    /// <summary>Wraps <paramref name="e"/> as <c>e * 1</c>.</summary>
    /// <param name="e">The operand.</param>
    /// <returns>The product.</returns>
    private static Expression One(Expression e) => Expression.Multiply(e, Expression.Constant(1.0));

    /// <summary>Wraps <paramref name="e"/> as <c>e + 0</c>.</summary>
    /// <param name="e">The operand.</param>
    /// <returns>The sum.</returns>
    private static Expression Zero(Expression e) => Expression.Add(e, Expression.Constant(0.0));

    /// <summary>Builds a DAG where every level reuses the previous one twice.</summary>
    /// <param name="levels">The number of levels.</param>
    /// <returns>The DAG.</returns>
    private static Expression Dag(int levels)
    {
        Expression e = One(Xs[0]);
        for (int i = 0; i < levels; i++)
        {
            e = Expression.Add(e, e);
        }

        return e;
    }

    /// <summary>Builds a block of <paramref name="n"/> chained assignments.</summary>
    /// <param name="n">The number of variables.</param>
    /// <returns>The block.</returns>
    private static Expression BlockOf(int n)
    {
        ParameterExpression[] variables = Enumerable.Range(0, n).Select(i => Expression.Variable(typeof(double), "a" + i)).ToArray();
        var body = new List<Expression>();
        for (int i = 0; i < n; i++)
        {
            body.Add(Expression.Assign(variables[i], One(i == 0 ? Xs[0] : variables[i - 1])));
        }

        body.Add(Zero(variables[n - 1]));
        return Expression.Block(variables, body);
    }

    /// <summary>Builds a fresh subtraction chain of products of <paramref name="n"/> terms.</summary>
    /// <param name="n">The number of terms.</param>
    /// <returns>The chain.</returns>
    private static Expression Chain(int n)
        => Enumerable.Range(0, n)
            .Select(i => (Expression)Expression.Multiply(Expression.Constant(i + 2.0), Xs[i]))
            .Aggregate(Expression.Subtract);

    /// <summary>An operation to measure.</summary>
    /// <param name="Operation">The operation.</param>
    private sealed record Scenario(Func<object?> Operation);

    /// <summary>The result of a measurement, in nanoseconds per operation.</summary>
    /// <param name="P25">The first quartile.</param>
    /// <param name="Median">The median.</param>
    /// <param name="P75">The third quartile.</param>
    /// <param name="BytesPerOperation">The bytes allocated per operation.</param>
    /// <param name="Operations">The number of timed operations.</param>
    /// <param name="WarmupOperations">The number of operations run before timing.</param>
    private sealed record Measurement(double P25, double Median, double P75, long BytesPerOperation, long Operations, long WarmupOperations);
}

/// <summary>The minimal tracer: counts the events.</summary>
internal sealed class CountingTracer : IExpressionTransformTracer
{
    /// <summary>Gets the number of events received.</summary>
    public long Count { get; private set; }

    /// <inheritdoc/>
    public void OnEvent(in ExpressionTransformEvent traceEvent) => Count++;
}

/// <summary>A bounded collector that also counts the events (the realistic diagnostic configuration).</summary>
/// <param name="counter">The counter.</param>
/// <param name="capacity">The capacity of the collector.</param>
internal sealed class CountingCollector(CountingTracer counter, int capacity) : IExpressionTransformTracer
{
    /// <summary>The collector.</summary>
    private readonly ExpressionTraceCollector _collector = new(capacity);

    /// <inheritdoc/>
    public void OnEvent(in ExpressionTransformEvent traceEvent)
    {
        counter.OnEvent(traceEvent);
        _collector.OnEvent(traceEvent);
    }
}

/// <summary>A benign extension node: the simplifier treats it as an opaque root, which isolates the fixed cost of an operation.</summary>
internal sealed class BenchExtension : Expression
{
    /// <inheritdoc/>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc/>
    public override Type Type => typeof(double);

    /// <inheritdoc/>
    public override bool CanReduce => true;

    /// <inheritdoc/>
    public override Expression Reduce() => Constant(1.0);
}
