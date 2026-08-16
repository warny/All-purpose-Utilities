using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// <b>Best-effort fan-out:</b> <see cref="Write(byte[], int, int)"/>,
/// <see cref="Write(ReadOnlySpan{byte})"/>, and <see cref="Flush"/> attempt the operation
/// on every target stream even if earlier ones throw. All exceptions are collected and rethrown as
/// an <see cref="AggregateException"/> so no target is silently skipped.
/// This means targets that were reached before a failure already hold the written data while later
/// ones may not — callers that need strict all-or-nothing semantics must manage this externally.
/// <para>
/// By default, disposing this object does not dispose any of the contained streams. If the
/// parameter <see cref="T:closeAllTargetsOnDispose"/> is <see langword="true"/> when constructing
/// this class, all target streams will be disposed when <see cref="IDisposable.Dispose()"/>
/// is called.
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
    /// and <see cref="DisposeAsync"/> are idempotent.
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="streams"/> is <see langword="null"/>.</exception>
    public StreamCopier(bool closeAllTargetsOnDispose, params Stream[] streams)
    {
        this.closeAllTargetsOnDispose = closeAllTargetsOnDispose;
        _targets = streams is null
            ? throw new ArgumentNullException(nameof(streams))
            : [.. streams];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamCopier"/> class and
    /// adds the specified array of streams to its targets (with <c>closeAllTargetsOnDispose</c> = false).
    /// </summary>
    /// <param name="streams">An array of streams to which data should be written.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="streams"/> is <see langword="null"/>.</exception>
    public StreamCopier(params Stream[] streams)
            : this(false, streams) { }

    #endregion

    #region Stream overrides

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
    /// is <see langword="true"/>, all target streams will also be disposed. The method is idempotent.
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
            // Attempt every target even if one fails; aggregate the errors.
            List<Exception>? errors = null;
            foreach (Stream s in _targets)
            {
                try { s?.Dispose(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
            _targets.Clear();
            if (errors is not null)
                throw new AggregateException("One or more target streams failed to dispose.", errors);
        }
    }

    /// <summary>
    /// Asynchronously disposes the current <see cref="StreamCopier"/>. If <see cref="closeAllTargetsOnDispose"/>
    /// is <see langword="true"/>, all target streams are asynchronously disposed; every target is attempted
    /// even if one fails and errors are aggregated. The method is idempotent.
    /// After disposal all write and flush operations throw <see cref="ObjectDisposedException"/>.
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
                try
                {
                    if (s is not null)
                        await s.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
            _targets.Clear();
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
    /// Gets the number of target streams in the list.
    /// </summary>
    public int Count => _targets.Count;

    /// <summary>
    /// Gets a value indicating whether the list of streams is read-only. This is always <see langword="false"/>.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the <see cref="Stream"/> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The stream at the specified index.</returns>
    public Stream this[int index]
    {
        get => _targets[index];
        set => _targets[index] = value;
    }

    /// <summary>
    /// Determines the index of a specific stream in the list.
    /// </summary>
    /// <param name="item">The stream to locate in the list.</param>
    /// <returns>The index of the stream if found; otherwise, -1.</returns>
    public int IndexOf(Stream item) => _targets.IndexOf(item);

    /// <summary>
    /// Inserts a stream at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
    /// <param name="item">The stream to insert. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    public void Insert(int index, Stream item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _targets.Insert(index, item);
    }

    /// <summary>
    /// Removes the stream at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the stream to remove.</param>
    public void RemoveAt(int index) => _targets.RemoveAt(index);

    /// <summary>
    /// Adds a stream to the end of the list of targets.
    /// </summary>
    /// <param name="item">The stream to add. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    public void Add(Stream item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _targets.Add(item);
    }

    /// <summary>
    /// Removes all streams from the targets list.
    /// </summary>
    public void Clear() => _targets.Clear();

    /// <summary>
    /// Determines whether the targets list contains a specific stream.
    /// </summary>
    /// <param name="item">The stream to locate in the list.</param>
    /// <returns><see langword="true"/> if the stream is found in the list; otherwise, <see langword="false"/>.</returns>
    public bool Contains(Stream item) => _targets.Contains(item);

    /// <summary>
    /// Copies the entire list of streams to a compatible one-dimensional array, 
    /// starting at the specified array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the list.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    public void CopyTo(Stream[] array, int arrayIndex) => _targets.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes the first occurrence of a specific stream from the list.
    /// </summary>
    /// <param name="item">The stream to remove.</param>
    /// <returns><see langword="true"/> if the stream was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(Stream item) => _targets.Remove(item);

    /// <summary>
    /// Returns an enumerator that iterates through the list of target streams.
    /// </summary>
    /// <returns>An enumerator for the underlying list of streams.</returns>
    public IEnumerator<Stream> GetEnumerator() => _targets.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the list of target streams.
    /// </summary>
    /// <returns>An enumerator for the underlying list of streams.</returns>
    IEnumerator IEnumerable.GetEnumerator() => _targets.GetEnumerator();

    #endregion
}
