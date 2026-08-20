using System;

namespace Utils.Collections;

/// <summary>
/// Provides shared BCL-compatible validation for generic collection copy operations.
/// </summary>
internal static class CollectionCopyValidation
{
    /// <summary>
    /// Validates a destination array, starting index, and required capacity before copying begins.
    /// </summary>
    /// <typeparam name="T">The destination element type.</typeparam>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination index.</param>
    /// <param name="count">The number of elements that will be copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException">The destination does not have enough available space.</exception>
    internal static void Validate<T>(T[] array, int arrayIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (arrayIndex > array.Length || count > array.Length - arrayIndex)
        {
            throw new ArgumentException("The destination array has insufficient space for the collection.", nameof(array));
        }
    }
}
