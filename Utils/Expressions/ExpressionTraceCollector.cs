namespace Utils.Expressions;

/// <summary>Which events an <see cref="ExpressionTraceCollector"/> keeps once it is full.</summary>
public enum ExpressionTraceRetention
{
    /// <summary>
    /// Keeps the most recent events (a circular buffer): the events that precede a failure or a resource refusal are kept, the
    /// oldest ones are dropped.
    /// </summary>
    KeepLast,

    /// <summary>Keeps the first events and drops the following ones.</summary>
    KeepFirst,
}

/// <summary>
/// A tracer that stores the events of the operations it traces, up to a fixed number (see
/// <see cref="ExpressionTransformTracing.Begin"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bounds.</b> At most <see cref="Capacity"/> events are kept; <see cref="Retention"/> decides which ones. Reaching the
/// capacity never fails the traced operation: the extra events are counted in <see cref="DroppedEvents"/>. Every event received
/// is counted in <see cref="TotalEvents"/>.
/// </para>
/// <para>
/// <b>References.</b> The stored events hold references to the traced trees; <see cref="Clear"/> releases them, and so does
/// dropping the collector. Nothing is rendered while collecting: export the events afterwards with
/// <see cref="ExpressionTraceExporter"/>.
/// </para>
/// <para>
/// <b>Threads.</b> The collector is thread-safe (it locks its own state), so one instance can trace operations running on
/// several threads; their events are then interleaved, and <see cref="ExpressionTransformEvent.SessionId"/> tells them apart.
/// </para>
/// </remarks>
public sealed class ExpressionTraceCollector : IExpressionTransformTracer
{
    /// <summary>The default capacity.</summary>
    public const int DefaultCapacity = 100_000;

    /// <summary>Guards the buffer and the counters.</summary>
    private readonly object _gate = new();

    /// <summary>The stored events (a circular buffer for <see cref="ExpressionTraceRetention.KeepLast"/>).</summary>
    private ExpressionTransformEvent[] _buffer;

    /// <summary>The index of the oldest stored event in <see cref="_buffer"/>.</summary>
    private int _start;

    /// <summary>The number of stored events.</summary>
    private int _count;

    /// <summary>The number of events received.</summary>
    private long _total;

    /// <summary>The number of events dropped.</summary>
    private long _dropped;

    /// <summary>Initializes a collector.</summary>
    /// <param name="capacity">The maximum number of events kept.</param>
    /// <param name="retention">Which events are kept once the collector is full.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public ExpressionTraceCollector(int capacity = DefaultCapacity, ExpressionTraceRetention retention = ExpressionTraceRetention.KeepLast)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        Retention = retention;
        _buffer = new ExpressionTransformEvent[int.Min(capacity, 1024)];
    }

    /// <summary>Gets the maximum number of events kept.</summary>
    public int Capacity { get; }

    /// <summary>Gets which events are kept once the collector is full.</summary>
    public ExpressionTraceRetention Retention { get; }

    /// <summary>Gets the number of events received since the creation or the last <see cref="Clear"/>.</summary>
    public long TotalEvents
    {
        get
        {
            lock (_gate) return _total;
        }
    }

    /// <summary>Gets the number of events received but not kept (the oldest or the newest, see <see cref="Retention"/>).</summary>
    public long DroppedEvents
    {
        get
        {
            lock (_gate) return _dropped;
        }
    }

    /// <summary>Gets the number of events kept.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _count;
        }
    }

    /// <summary>Stores an event, or counts it as dropped when the collector is full.</summary>
    /// <param name="traceEvent">The event.</param>
    public void OnEvent(in ExpressionTransformEvent traceEvent)
    {
        lock (_gate)
        {
            _total++;
            if (_count < Capacity)
            {
                if (_count == _buffer.Length) Grow();
                _buffer[(_start + _count) % _buffer.Length] = traceEvent;
                _count++;
                return;
            }

            _dropped++;
            if (Retention == ExpressionTraceRetention.KeepLast)
            {
                // Full circular buffer (its length is the capacity): overwrite the oldest event.
                _buffer[_start] = traceEvent;
                _start = (_start + 1) % _buffer.Length;
            }
        }
    }

    /// <summary>Returns a copy of the kept events, oldest first.</summary>
    /// <returns>The events, in the order they were received.</returns>
    public ExpressionTransformEvent[] GetEvents()
    {
        lock (_gate)
        {
            var events = new ExpressionTransformEvent[_count];
            for (int i = 0; i < _count; i++)
            {
                events[i] = _buffer[(_start + i) % _buffer.Length];
            }

            return events;
        }
    }

    /// <summary>Drops every kept event (releasing the trees they reference) and resets the counters.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _buffer = new ExpressionTransformEvent[int.Min(Capacity, 1024)];
            _start = 0;
            _count = 0;
            _total = 0;
            _dropped = 0;
        }
    }

    /// <summary>Doubles the buffer, up to <see cref="Capacity"/>, keeping the events in order from index 0.</summary>
    private void Grow()
    {
        var grown = new ExpressionTransformEvent[(int)long.Min((long)_buffer.Length * 2, Capacity)];
        for (int i = 0; i < _count; i++)
        {
            grown[i] = _buffer[(_start + i) % _buffer.Length];
        }

        _buffer = grown;
        _start = 0;
    }
}
