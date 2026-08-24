using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.IO;

/// <summary>
/// A writable-only stream that copies written data to multiple target streams simultaneously.
///
/// This class implements both <see cref="Stream"/> and <see cref="IList{Stream}"/> so that
/// the set of target streams can be dynamically modified at runtime. Note that no concurrency
/// control is provided; if multiple threads write concurrently, external locking may be needed.
///
/// Reading and seeking are not supported.
/// Calling <see cref="Write(byte[], int, int)"/> will broadcast the provided data to all
/// underlying streams in the targets collection.
///
/// <remarks>
/// <b>Target registration contract:</b> every target entering the collection — through a
/// constructor, <see cref="Add"/>, <see cref="Insert"/> or the indexer setter — must be
/// non-null, writable (<see cref="Stream.CanWrite"/>), distinct from this instance, and not
/// already registered. Registration is identified by reference (<see cref="ReferenceEquals(object?, object?)"/>),
/// not by <see cref="object.Equals(object?)"/>, so two distinct <see cref="Stream"/> instances
/// that happen to compare equal may both be registered, while the exact same instance cannot be
/// registered twice. Writability is checked only at registration time; a target that becomes
/// unwritable or is disposed externally afterwards does not affect this instance's own
/// <see cref="CanWrite"/> and is simply reported through the normal best-effort/aggregate
/// failure behavior on the next write.
/// <para>
/// <b>Best-effort fan-out:</b> <see cref="Write(byte[], int, int)"/>,
/// <see cref="Write(ReadOnlySpan{byte})"/>, and <see cref="Flush"/> attempt the operation
/// on every target stream even if earlier ones throw. All exceptions are collected and rethrown as
/// an <see cref="AggregateException"/> so no target is silently skipped.
/// This means targets that were reached before a failure already hold the written data while later
/// ones may not — callers that need strict all-or-nothing semantics must manage this externally.
/// </para>
/// <para>
/// By default, disposing this object does not dispose any of the contained streams. If the
/// parameter <see cref="T:closeAllTargetsOnDispose"/> is <see langword="true"/> when constructing
/// this class, all target streams will be disposed when <see cref="IDisposable.Dispose()"/>
/// is called.
/// </para>
/// <para>
/// <b>Post-dispose lifecycle:</b> disposing this instance freezes but does not erase the target
/// collection. <see cref="CanWrite"/> becomes <see langword="false"/> and <see cref="IsReadOnly"/>
/// becomes <see langword="true"/>; the collection remains inspectable (<see cref="Count"/>, the
/// indexer getter, <see cref="Contains"/>, <see cref="IndexOf"/>, <see cref="CopyTo"/> and
/// enumeration all keep working), but every mutating member throws
/// <see cref="ObjectDisposedException"/>. This holds regardless of <c>closeAllTargetsOnDispose</c>:
/// when ownership is enabled, the returned references remain registered even though the streams
/// they point to have themselves already been disposed.
/// </para>
/// </remarks>
/// </summary>
public class StreamCopier : Stream, IList<Stream>
{
    /// <summary>
    /// The collection of streams to which all written data will be copied.
    /// </summary>
    private readonly List<Stream> _targets;

    /// <summary>
    /// If set to <see langword="true"/>, disposing this <see cref="StreamCopier"/> will also
    /// dispose/close all target streams in <see cref="_targets"/>.
    /// </summary>
    private readonly bool closeAllTargetsOnDispose;

    /// <summary>
    /// Tracks whether this instance has already been disposed so that <see cref="Dispose(bool)"/>
    /// and <see cref="DisposeAsync"/> are idempotent, and so that <see cref="CanWrite"/>,
    /// <see cref="IsReadOnly"/> and collection mutation all observe a single, consistent
    /// disposed lifetime.
    /// </summary>
    private bool _disposed;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamCopier"/> class with an empty
    /// list of target streams.
    /// </summary>
    /// <param name="closeAllTargetsOnDispose">
    /// If <see langword="true"/>, disposing this <see cref="StreamCopier"/> will also dispose/close
    /// all streams in <see cref="_targets"/>.
    /// </param>
    public StreamCopier(bool closeAllTargetsOnDispose = false)
    {
        this.closeAllTargetsOnDispose = closeAllTargetsOnDispose;
        _targets = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamCopier"/> class and
    /// adds the specified array of streams to its targets.
    /// </summary>
    /// <param name="closeAllTargetsOnDispose">
    /// If <see langword="true"/>, disposing this <see cref="StreamCopier"/> will also dispose/close
    /// all streams in <see cref="_targets"/>.
    /// </param>
    /// <param name="streams">An array of streams to which data should be written.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="streams"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an element is not writable, is this <see cref="StreamCopier"/> itself, or is
    /// already present elsewhere in <paramref name="streams"/>.
    /// </exception>
    public StreamCopier(bool closeAllTargetsOnDispose, params Stream[] streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        this.closeAllTargetsOnDispose = closeAllTargetsOnDispose;
        _targets = new List<Stream>(streams.Length);
        foreach (Stream stream in streams)
        {
            ValidateTarget(stream, nameof(streams));
            _targets.Add(stream);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamCopier"/> class and
    /// adds the specified array of streams to its targets (with <c>closeAllTargetsOnDispose</c> = false).
    /// </summary>
    /// <param name="streams">An array of streams to which data should be written.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="streams"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an element is not writable, is this <see cref="StreamCopier"/> itself, or is
    /// already present elsewhere in <paramref name="streams"/>.
    /// </exception>
    public StreamCopier(params Stream[] streams)
            : this(false, streams) { }

    #endregion

    #region Stream overrides

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value indicating whether this instance can still be written to. This reflects the
    /// lifetime of the <see cref="StreamCopier"/> itself — it is <see langword="true"/> while the
    /// instance is active, including when it has zero targets, and becomes <see langword="false"/>
    /// once disposed. It is not a live health check of the registered target streams.
    /// </summary>
    public override bool CanWrite => !_disposed;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Flushes all target streams, collecting any exceptions. If any flush fails, an
    /// <see cref="AggregateException"/> is thrown after all streams have been attempted.
    /// </summary>
    public override void Flush()
    {
        ThrowIfDisposed();
        List<Exception>? errors = null;
        foreach (Stream s in _targets)
        {
            try { s.Flush(); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (errors is not null)
            throw new AggregateException("One or more target streams failed to flush.", errors);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Reading is not supported by StreamCopier.");
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seeking is not supported by StreamCopier.");
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException("Setting length is not supported by StreamCopier.");
    }

    /// <summary>
    /// Writes the specified buffer range to all target streams. Every stream is attempted even if
    /// earlier ones throw; all exceptions are collected and rethrown as an <see cref="AggregateException"/>.
    /// </summary>
    /// <param name="buffer">An array of bytes.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        List<Exception>? errors = null;
        // Snapshot to guard against concurrent modification
        Stream[] snapshot = [.. _targets];
        foreach (Stream s in snapshot)
        {
            try { s.Write(buffer, offset, count); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (errors is not null)
            throw new AggregateException("One or more target streams failed to write.", errors);
    }

    /// <summary>
    /// Writes a span of bytes to all target streams. Every target receives the same span without any
    /// intermediate allocation. Errors are aggregated in the same way as <see cref="Write(byte[], int, int)"/>.
    /// </summary>
    /// <param name="buffer">The span of bytes to broadcast to every target.</param>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();
        List<Exception>? errors = null;
        Stream[] snapshot = [.. _targets];
        foreach (Stream s in snapshot)
        {
            try { s.Write(buffer); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (errors is not null)
            throw new AggregateException("One or more target streams failed to write.", errors);
    }

    /// <summary>
    /// Asynchronously writes the specified buffer range to every target stream. A snapshot of the targets
    /// is taken before the loop; all targets are attempted even if earlier ones fail, and errors are
    /// aggregated. Cancellation requested before the loop takes priority over target failures.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The offset of the first byte to write.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <param name="cancellationToken">A token used to observe cancellation before the operation starts.</param>
    /// <returns>A task that completes when every target has been attempted.</returns>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Asynchronously writes the specified memory buffer to every target stream. A snapshot of the targets
    /// is taken before the loop; all targets are attempted even if earlier ones fail, and errors are
    /// aggregated. Cancellation requested before the loop takes priority over target failures.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="cancellationToken">A token used to observe cancellation before the operation starts.</param>
    /// <returns>A task that completes when every target has been attempted.</returns>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // Snapshot first so concurrent list mutations do not affect this operation.
        Stream[] snapshot = [.. _targets];
        // Cancellation observed before any target is touched aborts the whole operation.
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        List<Exception>? errors = null;
        foreach (Stream s in snapshot)
        {
            try { await s.WriteAsync(buffer, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        ThrowAggregatedOrCancellation(errors, cancellationToken, "One or more target streams failed to write.");
    }

    /// <summary>
    /// Asynchronously flushes every target stream. A snapshot is taken before the loop; all targets are
    /// attempted even if earlier ones fail, and errors are aggregated. Cancellation requested before the
    /// loop takes priority over target failures.
    /// </summary>
    /// <param name="cancellationToken">A token used to observe cancellation before the operation starts.</param>
    /// <returns>A task that completes when every target has been attempted.</returns>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        Stream[] snapshot = [.. _targets];
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        List<Exception>? errors = null;
        foreach (Stream s in snapshot)
        {
            try { await s.FlushAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        ThrowAggregatedOrCancellation(errors, cancellationToken, "One or more target streams failed to flush.");
    }

    /// <summary>
    /// Throws an aggregated error, an <see cref="OperationCanceledException"/>, or nothing depending on the
    /// collected errors and cancellation state. When cancellation was requested it takes priority over any
    /// aggregated target failures.
    /// </summary>
    /// <param name="errors">The collected target errors, or <see langword="null"/> when none occurred.</param>
    /// <param name="cancellationToken">The token whose cancellation state is checked.</param>
    /// <param name="message">The message used for the aggregated exception.</param>
    private static void ThrowAggregatedOrCancellation(List<Exception>? errors, CancellationToken cancellationToken, string message)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);
        if (errors is not null)
            throw new AggregateException(message, errors);
    }

    /// <summary>
    /// Disposes the current <see cref="StreamCopier"/>. If <see cref="closeAllTargetsOnDispose"/>
    /// is <see langword="true"/>, all target streams will also be disposed, but their references
    /// remain in the target collection. The method is idempotent and shares its disposed state with
    /// <see cref="DisposeAsync"/>: whichever path runs first attempts target disposal, the other
    /// becomes a no-op.
    /// </summary>
    /// <param name="disposing">Whether this method is being called from a managed context.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }
        _disposed = true;

        base.Dispose(disposing);

        if (disposing && closeAllTargetsOnDispose)
        {
            // Attempt every target even if one fails; aggregate the errors. Targets cannot be null
            // (registration forbids it), so every reference is disposed unconditionally.
            List<Exception>? errors = null;
            foreach (Stream s in _targets)
            {
                try { s.Dispose(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
            if (errors is not null)
                throw new AggregateException("One or more target streams failed to dispose.", errors);
        }
    }

    /// <summary>
    /// Asynchronously disposes the current <see cref="StreamCopier"/>. If <see cref="closeAllTargetsOnDispose"/>
    /// is <see langword="true"/>, all target streams are asynchronously disposed, but their references
    /// remain in the target collection; every target is attempted even if one fails and errors are
    /// aggregated. The method is idempotent and shares its disposed state with <see cref="Dispose(bool)"/>.
    /// After disposal all write and flush operations throw <see cref="ObjectDisposedException"/>, and
    /// <see cref="CanWrite"/> becomes <see langword="false"/> while the target collection remains
    /// inspectable.
    /// </summary>
    /// <returns>A task that completes once disposal has been attempted for every target.</returns>
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        GC.SuppressFinalize(this);

        List<Exception>? errors = null;

        if (closeAllTargetsOnDispose)
        {
            foreach (Stream s in _targets)
            {
                try { await s.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
        }

        // Signal to the base Stream that this instance is disposed so that the runtime and
        // callers using the base-class abstraction observe consistent disposed semantics.
        try { await base.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { (errors ??= []).Add(ex); }

        if (errors is not null)
            throw new AggregateException("One or more streams failed during asynchronous disposal.", errors);
    }

    #endregion

    #region IList<Stream> implementation

    /// <summary>
    /// Gets the number of target streams in the list. Remains accurate after disposal.
    /// </summary>
    public int Count => _targets.Count;

    /// <summary>
    /// Gets a value indicating whether the target list is read-only. This is <see langword="false"/>
    /// while the instance is active and becomes <see langword="true"/> once disposed, at which point
    /// every mutating member throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    public bool IsReadOnly => _disposed;

    /// <summary>
    /// Gets or sets the <see cref="Stream"/> at the specified index. The getter remains available
    /// after disposal. The setter validates the replacement target using the same registration
    /// contract as <see cref="Add"/> and <see cref="Insert"/>, except that replacing an index with
    /// the exact reference already occupying it is allowed rather than treated as a duplicate.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The stream at the specified index.</returns>
    /// <exception cref="ObjectDisposedException">Thrown by the setter once this instance has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the new value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown by the setter when the new value is not writable, is this <see cref="StreamCopier"/>
    /// itself, or is already registered at a different index.
    /// </exception>
    public Stream this[int index]
    {
        get => _targets[index];
        set
        {
            ThrowIfDisposed();
            ValidateTarget(value, nameof(value), excludedIndex: index);
            _targets[index] = value;
        }
    }

    /// <summary>
    /// Determines the index of a specific stream in the list, using reference identity rather than
    /// <see cref="object.Equals(object?)"/>. Remains available after disposal.
    /// </summary>
    /// <param name="item">The stream to locate in the list.</param>
    /// <returns>The index of the stream if found; otherwise, -1.</returns>
    public int IndexOf(Stream item) => IndexOfReference(item);

    /// <summary>
    /// Inserts a stream at the specified index, applying the same registration contract as
    /// <see cref="Add"/>.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
    /// <param name="item">The stream to insert.</param>
    /// <exception cref="ObjectDisposedException">Thrown once this instance has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="item"/> is not writable, is this <see cref="StreamCopier"/> itself,
    /// or is already registered.
    /// </exception>
    public void Insert(int index, Stream item)
    {
        ThrowIfDisposed();
        ValidateTarget(item, nameof(item));
        _targets.Insert(index, item);
    }

    /// <summary>
    /// Removes the stream at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the stream to remove.</param>
    /// <exception cref="ObjectDisposedException">Thrown once this instance has been disposed.</exception>
    public void RemoveAt(int index)
    {
        ThrowIfDisposed();
        _targets.RemoveAt(index);
    }

    /// <summary>
    /// Adds a stream to the end of the list of targets. The target must be non-null, writable, not
    /// this <see cref="StreamCopier"/> itself, and not already registered (by reference identity).
    /// </summary>
    /// <param name="item">The stream to add.</param>
    /// <exception cref="ObjectDisposedException">Thrown once this instance has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="item"/> is not writable, is this <see cref="StreamCopier"/> itself,
    /// or is already registered.
    /// </exception>
    public void Add(Stream item)
    {
        ThrowIfDisposed();
        ValidateTarget(item, nameof(item));
        _targets.Add(item);
    }

    /// <summary>
    /// Removes all streams from the targets list.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown once this instance has been disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();
        _targets.Clear();
    }

    /// <summary>
    /// Determines whether the targets list contains a specific stream, using reference identity
    /// rather than <see cref="object.Equals(object?)"/>. Remains available after disposal.
    /// </summary>
    /// <param name="item">The stream to locate in the list.</param>
    /// <returns><see langword="true"/> if the stream is found in the list; otherwise, <see langword="false"/>.</returns>
    public bool Contains(Stream item) => IndexOfReference(item) >= 0;

    /// <summary>
    /// Copies the entire list of streams to a compatible one-dimensional array,
    /// starting at the specified array index. Remains available after disposal.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the list.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    public void CopyTo(Stream[] array, int arrayIndex) => _targets.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes the first occurrence of a specific stream from the list, using reference identity
    /// rather than <see cref="object.Equals(object?)"/>.
    /// </summary>
    /// <param name="item">The stream to remove.</param>
    /// <returns><see langword="true"/> if the stream was successfully removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown once this instance has been disposed.</exception>
    public bool Remove(Stream item)
    {
        ThrowIfDisposed();
        int index = IndexOfReference(item);
        if (index < 0) return false;
        _targets.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the list of target streams. Remains available
    /// after disposal.
    /// </summary>
    /// <returns>An enumerator for the underlying list of streams.</returns>
    public IEnumerator<Stream> GetEnumerator() => _targets.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the list of target streams. Remains available
    /// after disposal.
    /// </summary>
    /// <returns>An enumerator for the underlying list of streams.</returns>
    IEnumerator IEnumerable.GetEnumerator() => _targets.GetEnumerator();

    /// <summary>
    /// Finds the index of a target by reference identity (<see cref="ReferenceEquals(object?, object?)"/>),
    /// deliberately ignoring any custom <see cref="object.Equals(object?)"/> override a target stream
    /// might define.
    /// </summary>
    /// <param name="item">The stream instance to locate.</param>
    /// <returns>The index of the exact instance if found; otherwise, -1.</returns>
    private int IndexOfReference(Stream item)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            if (ReferenceEquals(_targets[i], item)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Validates a candidate target against the single registration contract shared by every
    /// insertion path: it must be non-null, writable, distinct from this <see cref="StreamCopier"/>,
    /// and not already registered elsewhere in the collection.
    /// </summary>
    /// <param name="target">The candidate target stream.</param>
    /// <param name="paramName">The parameter name to report in thrown exceptions.</param>
    /// <param name="excludedIndex">
    /// An index to exclude from the duplicate check, used by the indexer setter so that replacing a
    /// slot with the exact reference already occupying it is allowed.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="target"/> is not writable, is this <see cref="StreamCopier"/> itself,
    /// or is already registered at an index other than <paramref name="excludedIndex"/>.
    /// </exception>
    private void ValidateTarget(Stream target, string paramName, int excludedIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(target, paramName);
        if (!target.CanWrite)
            throw new ArgumentException("The target stream must be writable.", paramName);
        if (ReferenceEquals(target, this))
            throw new ArgumentException("A StreamCopier cannot target itself.", paramName);

        int existingIndex = IndexOfReference(target);
        if (existingIndex >= 0 && existingIndex != excludedIndex)
            throw new ArgumentException("The target stream is already registered.", paramName);
    }

    /// <summary>
    /// Rejects the call once this instance has been disposed. Applied to every collection mutation
    /// and to the write/flush operations so lifetime checks stay in a single place.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    #endregion
}
