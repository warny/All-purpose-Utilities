using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

namespace ExpressionResourceProbe;

/// <summary>
/// Samples managed allocations by type through the runtime's <c>GCAllocationTick</c> events (one event roughly every
/// 100 KB allocated, attributed to the type whose allocation crossed the threshold). The distribution is statistical:
/// it ranks allocation sites by volume, it does not count objects.
/// </summary>
internal sealed class AllocationSampler : EventListener
{
    /// <summary>The GC keyword of the runtime event source.</summary>
    private const EventKeywords GcKeyword = (EventKeywords)0x1;

    /// <summary>Sampled bytes per allocated type name.</summary>
    private readonly Dictionary<string, long> _bytesByType = new(StringComparer.Ordinal);

    /// <summary>Synchronizes the event callbacks, which may run on any thread.</summary>
    private readonly object _gate = new();

    /// <summary>Whether sampling is active.</summary>
    private volatile bool _active;

    /// <summary>Starts sampling.</summary>
    public void Start() => _active = true;

    /// <summary>Stops sampling.</summary>
    public void Stop() => _active = false;

    /// <summary>Formats the largest sampled types.</summary>
    /// <param name="count">The number of types.</param>
    /// <returns>One <c>type=bytes</c> entry per line, largest first, with the share of the sampled total.</returns>
    public string Format(int count)
    {
        lock (_gate)
        {
            long total = _bytesByType.Values.Sum();
            return string.Join(Environment.NewLine, _bytesByType
                .OrderByDescending(pair => pair.Value)
                .Take(count)
                .Select(pair => $"ALLOC {pair.Value.ToString(CultureInfo.InvariantCulture),14} {(100.0 * pair.Value / Math.Max(1, total)).ToString("0.0", CultureInfo.InvariantCulture),5}% {pair.Key}")
                .Prepend($"ALLOC total-sampled={total.ToString(CultureInfo.InvariantCulture)}"));
        }
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            EnableEvents(eventSource, EventLevel.Verbose, GcKeyword);
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!_active || eventData.EventName is null || !eventData.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || eventData.PayloadNames is null)
        {
            return;
        }

        int typeIndex = eventData.PayloadNames.IndexOf("TypeName");
        int amountIndex = eventData.PayloadNames.IndexOf("AllocationAmount64");
        if (typeIndex < 0 || amountIndex < 0)
        {
            return;
        }

        string type = eventData.Payload![typeIndex] as string ?? "?";
        long amount = Convert.ToInt64(eventData.Payload[amountIndex], CultureInfo.InvariantCulture);
        lock (_gate)
        {
            _bytesByType[type] = _bytesByType.TryGetValue(type, out long current) ? current + amount : amount;
        }
    }
}
