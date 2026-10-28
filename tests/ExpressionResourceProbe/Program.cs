using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionResourceProbe;

/// <summary>
/// ETC R0 resource probe. Runs exactly one scenario and one operation per process and prints one <c>RESULT</c> line, so a
/// stack overflow (which terminates the process) is observed by the caller (<c>run-campaign.ps1</c>) as an exit code.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// ExpressionResourceProbe list
/// ExpressionResourceProbe run &lt;scenario&gt; &lt;size&gt; &lt;operation&gt; [--thread main|pool|stack:&lt;KiB&gt;] [--limits default|unlimited|budgets] [--counters]
/// ExpressionResourceProbe ess [--thread ...]
/// </code>
/// Exit codes: 0 success, 2 usage error, 10 <see cref="InsufficientExecutionStackException"/>, 11
/// <see cref="OutOfMemoryException"/>, 12 any other exception, 13 <see cref="ExpressionResourceLimitException"/> (a
/// controlled refusal of the ETC R1a-Security limits; the outcome names its kind). A stack overflow ends the process with the operating
/// system's code (Windows <c>0xC00000FD</c>, Linux <c>SIGABRT</c> 134 with "Stack overflow." on stderr).
/// </remarks>
internal static class Program
{
    /// <summary>Exit code of a successful run.</summary>
    private const int ExitSuccess = 0;

    /// <summary>Exit code of a command-line error.</summary>
    private const int ExitUsage = 2;

    /// <summary>Exit code when <see cref="InsufficientExecutionStackException"/> was caught.</summary>
    private const int ExitInsufficientStack = 10;

    /// <summary>Exit code when <see cref="OutOfMemoryException"/> was caught.</summary>
    private const int ExitOutOfMemory = 11;

    /// <summary>Exit code for any other exception.</summary>
    private const int ExitOtherException = 12;

    /// <summary>Exit code when an <see cref="ExpressionResourceLimitException"/> was caught (controlled refusal).</summary>
    private const int ExitResourceLimit = 13;

    /// <summary>
    /// The resource limits of the run (<c>--limits</c>): <c>default</c> (<see cref="ExpressionResourceLimits.Default"/>),
    /// <c>unlimited</c> (only the stack guard stays active) or <c>budgets</c> (default visit budgets, no depth or node
    /// limit, so the recursion itself meets the stack guard while the work stays bounded).
    /// </summary>
    private static ExpressionResourceLimits _limits = ExpressionResourceLimits.Default;

    /// <summary>Entry point.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code.</returns>
    private static int Main(string[] args)
    {
        if (args.Length == 0) return Usage();
        string thread = Option(args, "--thread") ?? "main";
        bool counters = Array.IndexOf(args, "--counters") >= 0;
        _limits = (Option(args, "--limits") ?? "default") switch
        {
            "default" => ExpressionResourceLimits.Default,
            "unlimited" => ExpressionResourceLimits.Unlimited,
            "budgets" => ExpressionResourceLimits.Default with { MaxDepth = int.MaxValue, MaxNodes = long.MaxValue },
            var other => throw new ArgumentException($"Unknown limits '{other}'."),
        };

        return args[0] switch
        {
            "list" => List(),
            "run" when args.Length >= 4 => OnThread(thread, () => Run(args[1], int.Parse(args[2], CultureInfo.InvariantCulture), args[3], thread, counters)),
            "ess" => OnThread(thread, () => MeasureStackGuard(thread)),
            _ => Usage(),
        };
    }

    /// <summary>Prints the usage text.</summary>
    /// <returns><see cref="ExitUsage"/>.</returns>
    private static int Usage()
    {
        Console.Error.WriteLine("usage: list | run <scenario> <size> <operation> [--thread main|pool|stack:<KiB>] [--limits default|unlimited|budgets] [--counters] | ess [--thread ...]");
        Console.Error.WriteLine("operations: build shape simplify transform substitute hash equals rawhash rawequals canonical tokenize parse compile");
        return ExitUsage;
    }

    /// <summary>Prints the scenario catalog.</summary>
    /// <returns><see cref="ExitSuccess"/>.</returns>
    private static int List()
    {
        foreach (Scenario scenario in Scenarios.All) Console.WriteLine($"{scenario.Name}\t{scenario.Family}\t{scenario.Description}");
        return ExitSuccess;
    }

    /// <summary>Reads the value following <paramref name="name"/>.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="name">The option name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Runs <paramref name="body"/> on the requested kind of thread and returns its exit code.</summary>
    /// <param name="thread"><c>main</c>, <c>pool</c> (a thread-pool thread) or <c>stack:&lt;KiB&gt;</c> (a dedicated thread with that stack size).</param>
    /// <param name="body">The work.</param>
    /// <returns>The exit code.</returns>
    private static int OnThread(string thread, Func<int> body)
    {
        if (thread == "main") return body();
        if (thread == "pool") return Task.Run(body).GetAwaiter().GetResult();
        if (thread.StartsWith("stack:", StringComparison.Ordinal))
        {
            int stackBytes = int.Parse(thread["stack:".Length..], CultureInfo.InvariantCulture) * 1024;
            int result = ExitOtherException;
            var worker = new Thread(() => result = body(), stackBytes);
            worker.Start();
            worker.Join();
            return result;
        }

        return Usage();
    }

    /// <summary>Runs one scenario operation and prints the <c>RESULT</c> line.</summary>
    /// <param name="scenarioName">The scenario.</param>
    /// <param name="size">The size.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="thread">The thread kind, echoed in the result.</param>
    /// <param name="counters">Whether to collect comparer counters (changes the code path: diagnostics are active).</param>
    /// <returns>The exit code.</returns>
    private static int Run(string scenarioName, int size, string operation, string thread, bool counters)
    {
        Scenario scenario = Scenarios.Get(scenarioName);
        string prefix = $"RESULT scenario={scenario.Name} family={scenario.Family} size={size} op={operation} thread={thread} limits={DescribeLimits()} os={(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux")} runtime={RuntimeInformation.FrameworkDescription.Replace(' ', '_')} arch={RuntimeInformation.ProcessArchitecture}";

        // Warm-up on a tiny instance so JIT time is not measured; it runs the same code paths.
        Execute(scenario.Create(Math.Min(size, 4)), operation);

        ScenarioInstance instance = scenario.Create(size);
        Expression? tree = operation is "tokenize" or "parse" or "compile" ? null : instance.Build();
        string shapeText = tree is null ? "nodes=- distinct=- depth=- parameters=-" : FormatShape(ExpressionShapeMeter.Measure(tree));
        string textLength = instance.Text is null ? "-" : instance.Text.Length.ToString(CultureInfo.InvariantCulture);

        long visitsBefore = InternalDiagnostics.TransformCoreVisits();
        long userCodeBefore = InternalDiagnostics.UserCodeInvocations();
        bool capturing = counters && InternalDiagnostics.BeginComparerCapture();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        string outcome;
        int exitCode;
        try
        {
            string detail = Execute(instance, operation, tree);
            outcome = "ok" + (detail.Length > 0 ? ":" + detail : string.Empty);
            exitCode = ExitSuccess;
        }
        catch (ExpressionResourceLimitException exception)
        {
            outcome = "resource-limit:" + exception.Kind + (exception.InnerException is null ? string.Empty : "+" + exception.InnerException.GetType().Name);
            exitCode = ExitResourceLimit;
        }
        catch (InsufficientExecutionStackException)
        {
            outcome = "insufficient-stack";
            exitCode = ExitInsufficientStack;
        }
        catch (OutOfMemoryException)
        {
            outcome = "out-of-memory";
            exitCode = ExitOutOfMemory;
        }
        catch (Exception exception)
        {
            outcome = "exception:" + exception.GetType().Name;
            exitCode = ExitOtherException;
        }

        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        (int events, int maxDepth) = capturing ? InternalDiagnostics.EndComparerCapture() : (-1, -1);
        long visits = visitsBefore < 0 ? -1 : InternalDiagnostics.TransformCoreVisits() - visitsBefore;
        long userCode = userCodeBefore < 0 ? -1 : InternalDiagnostics.UserCodeInvocations() - userCodeBefore;

        Console.WriteLine(string.Join(' ',
            prefix,
            shapeText,
            "text=" + textLength,
            "ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
            "allocated=" + allocated.ToString(CultureInfo.InvariantCulture),
            "visits=" + (visits < 0 ? "n/a" : visits.ToString(CultureInfo.InvariantCulture)),
            "comparerSimplifications=" + (events < 0 ? "n/a" : events.ToString(CultureInfo.InvariantCulture)),
            "comparerMaxDepth=" + (maxDepth < 0 ? "n/a" : maxDepth.ToString(CultureInfo.InvariantCulture)),
            "userCode=" + (userCode < 0 ? "n/a" : userCode.ToString(CultureInfo.InvariantCulture)),
            "outcome=" + outcome));
        return exitCode;
    }

    /// <summary>Names the active <c>--limits</c> choice.</summary>
    /// <returns><c>default</c>, <c>unlimited</c> or <c>budgets</c>.</returns>
    private static string DescribeLimits()
        => _limits == ExpressionResourceLimits.Default ? "default" : _limits == ExpressionResourceLimits.Unlimited ? "unlimited" : "budgets";

    /// <summary>Formats a measured shape.</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The formatted fields.</returns>
    private static string FormatShape(ExpressionShape shape)
        => $"nodes={shape.LogicalNodes} distinct={shape.DistinctNodes} depth={shape.Depth} parameters={shape.FreeOrDeclaredParameters}";

    /// <summary>Executes one operation.</summary>
    /// <param name="instance">The scenario instance.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="tree">The pre-built tree, or <see langword="null"/> to build one.</param>
    /// <returns>A short detail of the result.</returns>
    private static string Execute(ScenarioInstance instance, string operation, Expression? tree = null)
    {
        switch (operation)
        {
            case "tokenize":
                return RequireDialect(instance, operation).Tokenize(instance.Text!).ToString(CultureInfo.InvariantCulture);
            case "parse":
                RequireDialect(instance, operation).Parse(instance.Text!);
                return string.Empty;
            case "compile":
                RequireDialect(instance, operation);
                return "depth" + ExpressionShapeMeter.Measure(instance.Build()).Depth.ToString(CultureInfo.InvariantCulture);
        }

        tree ??= instance.Build();
        if (operation.StartsWith("recover-", StringComparison.Ordinal))
        {
            return Recover(instance, operation["recover-".Length..], tree);
        }

        switch (operation)
        {
            case "build":
                return string.Empty;
            case "shape":
                return ExpressionShapeMeter.Measure(tree).DistinctNodes.ToString(CultureInfo.InvariantCulture);
            case "simplify":
                return "resultDepth" + ExpressionShapeMeter.Measure(new ExpressionSimplifier(_limits).Simplify(tree)).Depth.ToString(CultureInfo.InvariantCulture);
            case "transform":
                new IdentityRecursiveTransformer(_limits).Transform(tree);
                return string.Empty;
            case "substitute":
                new IdentityRecursiveTransformer(_limits).Substitute(tree, instance.Parameters[0], Expression.Constant(5.0));
                return string.Empty;
            case "hash":
                ExpressionComparer.Create(_limits).GetHashCode(tree);
                return string.Empty;
            case "equals":
                return ExpressionComparer.Create(_limits).Equals(tree, instance.Build()) ? "equal" : "unequal";
            case "rawhash":
                InternalDiagnostics.StructuralHashRaw(tree);
                return string.Empty;
            case "rawequals":
                return InternalDiagnostics.StructuralEqualsRaw(tree, instance.Build()) ? "equal" : "unequal";
            case "canonical":
                return InternalDiagnostics.CanonicalCompare(tree, instance.Build()).ToString(CultureInfo.InvariantCulture);
            default:
                throw new ArgumentException($"Unknown operation '{operation}'.", nameof(operation));
        }
    }

    /// <summary>
    /// Runs <paramref name="operation"/>, swallows a controlled <see cref="InsufficientExecutionStackException"/>, then
    /// reports the ambient thread-static state and checks that an ordinary simplification still produces the expected
    /// result on the same thread (session, cache and lexical-scope restoration after a failure).
    /// </summary>
    /// <param name="instance">The scenario instance.</param>
    /// <param name="operation">The operation expected to fail.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>A detail string: the failure, the ambient state and the sanity check.</returns>
    private static string Recover(ScenarioInstance instance, string operation, Expression tree)
    {
        string failure = "none";
        try
        {
            Execute(instance, operation, tree);
        }
        catch (InsufficientExecutionStackException)
        {
            failure = "insufficient-stack";
        }
        catch (ExpressionResourceLimitException exception)
        {
            failure = "resource-limit:" + exception.Kind;
        }

        string state = InternalDiagnostics.DescribeAmbientState();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression sample = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(x, Expression.Constant(1.0)), Expression.Multiply(Expression.Constant(2.0), x)), x);
        Expression simplified = new ExpressionSimplifier().Simplify(sample);
        bool equal = ExpressionComparer.Default.Equals(simplified, new ExpressionSimplifier().Simplify(sample));
        return $"failure={failure};state={state};sanity={simplified};sanityEqual={equal}";
    }

    /// <summary>Requires a parser scenario (one with a source text and a dialect).</summary>
    /// <param name="instance">The scenario instance.</param>
    /// <param name="operation">The operation needing it.</param>
    /// <returns>The dialect.</returns>
    private static ParserDialect RequireDialect(ScenarioInstance instance, string operation)
        => instance.Dialect is not null && instance.Text is not null
            ? instance.Dialect
            : throw new ArgumentException($"Operation '{operation}' needs a parser (CSyntax or VBSyntax) scenario.");

    /// <summary>
    /// Calibrates <see cref="RuntimeHelpers.EnsureSufficientExecutionStack"/> on the current thread: the cost of one call,
    /// and how many frames of a fixed-size recursive method run before it throws (instead of overflowing).
    /// </summary>
    /// <param name="thread">The thread kind, echoed in the result.</param>
    /// <returns>The exit code.</returns>
    private static int MeasureStackGuard(string thread)
    {
        const int calls = 10_000_000;
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < calls; i++) RuntimeHelpers.EnsureSufficientExecutionStack();
        stopwatch.Stop();
        double nanoseconds = stopwatch.Elapsed.TotalMilliseconds * 1_000_000 / calls;

        int frames = 0;
        string outcome;
        try
        {
            GuardedRecursion(ref frames);
            outcome = "no-limit";
        }
        catch (InsufficientExecutionStackException)
        {
            outcome = "insufficient-stack";
        }

        // The exception unwound every frame: a second guarded descent must reach the same depth.
        int framesAgain = 0;
        try
        {
            GuardedRecursion(ref framesAgain);
        }
        catch (InsufficientExecutionStackException)
        {
        }

        Console.WriteLine($"RESULT op=ess thread={thread} os={(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux")} runtime={RuntimeInformation.FrameworkDescription.Replace(' ', '_')} nsPerCall={nanoseconds.ToString("0.##", CultureInfo.InvariantCulture)} guardedFrames={frames} guardedFramesAgain={framesAgain} outcome={outcome}");
        return ExitSuccess;
    }

    /// <summary>A recursive method with a fixed 256-byte local buffer, guarded on every call.</summary>
    /// <param name="frames">Counts the frames entered.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GuardedRecursion(ref int frames)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        Span<byte> buffer = stackalloc byte[256];
        buffer[frames & 255] = 1;
        frames++;
        GuardedRecursion(ref frames);
        buffer[0] += buffer[frames & 255];
    }
}
