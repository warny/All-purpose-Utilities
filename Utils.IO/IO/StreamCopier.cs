using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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
/// By default, disposing this object does not dispose any of the contained streams. If the
/// parameter <see cref="T:closeAllTargetsOnDispose"/> is <see langword="true"/> when constructing
/// this class, all target streams will be disposed when <see cref="IDisposable.Dispose()"/>
/// is called.
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
    /// Flushes all target streams. This does not close or dispose them
    /// (unless <see cref="closeAllTargetsOnDispose"/> is true and <see cref="IDisposable.Dispose()"/> is called).
    /// </summary>
    public override void Flush()
    {
        // We call Flush on each target to ensure any buffered data is written.
        foreach (Stream s in _targets)
        {
            s.Flush();
        }
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
    /// Writes the specified buffer range to all target streams.
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the target streams.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Forward the write to all underlying target streams
        foreach (Stream s in _targets)
        {
            s.Write(buffer, offset, count);
        }
    }

    /// <summary>
    /// Disposes the current <see cref="StreamCopier"/>. If <see cref="closeAllTargetsOnDispose"/>
    /// is <see langword="true"/>, all target streams will also be disposed.
    /// </summary>
    /// <param name="disposing">Whether this method is being called from a managed context.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && closeAllTargetsOnDispose)
        {
            // Close/Dispose each stream in the list
            foreach (Stream s in _targets)
            {
                s?.Dispose();
            }
            _targets.Clear();
        }
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
    /// <param name="item">The stream to insert.</param>
    public void Insert(int index, Stream item) => _targets.Insert(index, item);

    /// <summary>
    /// Removes the stream at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the stream to remove.</param>
    public void RemoveAt(int index) => _targets.RemoveAt(index);

    /// <summary>
    /// Adds a stream to the end of the list of targets.
    /// </summary>
    /// <param name="item">The stream to add.</param>
    public void Add(Stream item) => _targets.Add(item);

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
