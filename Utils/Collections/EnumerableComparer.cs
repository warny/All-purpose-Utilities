using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Utils.Collections
{
    /// <summary>
    /// Compares two sequences of values of comparable types.
    /// Supports custom comparers and optimizations for <see cref="IReadOnlyList{T}"/>
    /// and <see cref="Span{T}"/> where applicable.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences. Must be comparable.</typeparam>
    public sealed class EnumerableComparer<T> : IComparer<IEnumerable<T>>
    {
        private readonly IComparer<T> elementComparer;

        /// <summary>
        /// A thread-safe, cached instance of <see cref="EnumerableComparer{T}"/>
        /// using the default comparison logic for elements.
        /// </summary>
        public static IComparer<IEnumerable<T>> Default { get; } = new EnumerableComparer<T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumerableComparer{T}"/> class
        /// using the default comparison logic.
        /// </summary>
        private EnumerableComparer()
        {
            elementComparer = CreateComparer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumerableComparer{T}"/> class
        /// using a provided element comparer.
        /// </summary>
        /// <param name="elementComparer">A comparer for elements of type <typeparamref name="T"/>.</param>
        public EnumerableComparer(IComparer<T> elementComparer)
        {
            ArgumentNullException.ThrowIfNull(elementComparer);
            this.elementComparer = elementComparer;
        }

        /// <inheritdoc/>
        public int Compare(IEnumerable<T> x, IEnumerable<T> y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            // Try to get spans for both
            if (GetSpan(x, out var spanX) && GetSpan(y, out var spanY))
                return CompareSpans(spanX, spanY);

            // If both are IReadOnlyList<T>, compare Count first
            if (x is IReadOnlyList<T> listX && y is IReadOnlyList<T> listY)
                return CompareLists(listX, listY);

            // Fall back to enumeration-based comparison
            using var enumX = x.GetEnumerator();
            using var enumY = y.GetEnumerator();

            while (true)
            {
                bool hasNextX = enumX.MoveNext();
                bool hasNextY = enumY.MoveNext();

                if (!hasNextX && !hasNextY) return 0;
                if (!hasNextX) return -1;
                if (!hasNextY) return 1;

                int comparison = elementComparer.Compare(enumX.Current, enumY.Current);
                if (comparison != 0) return comparison;
            }
        }

        /// <summary>
        /// Efficiently compares two spans using the element comparer.
        /// Compares elements sequentially and determines order by the first difference found.
        /// If all elements are equal, the shorter span is considered smaller.
        /// </summary>
        private int CompareSpans(ReadOnlySpan<T> spanX, ReadOnlySpan<T> spanY)
        {
            int minLength = Math.Min(spanX.Length, spanY.Length);

            for (int i = 0; i < minLength; i++)
            {
                int comparison = elementComparer.Compare(spanX[i], spanY[i]);
                if (comparison != 0) return comparison;
            }

            // If all compared elements are equal, the shorter sequence is "less than" the longer one.
            return spanX.Length.CompareTo(spanY.Length);
        }

        /// <summary>
        /// Compares two IReadOnlyLists element by element.
        /// </summary>
        private int CompareLists(IReadOnlyList<T> listX, IReadOnlyList<T> listY)
        {
            int minLength = Math.Min(listX.Count, listY.Count);

            for (int i = 0; i < minLength; i++)
            {
                int comparison = elementComparer.Compare(listX[i], listY[i]);
                if (comparison != 0) return comparison;
            }

            return listX.Count.CompareTo(listY.Count);
        }

        /// <summary>
        /// Tries to retrieve a <see cref="Span{T}"/> from an enumerable.
        /// Returns <see langword="true"/> if successful, along with the extracted span.
        /// Otherwise, returns <see langword="false"/>.
        /// </summary>
        private static bool GetSpan(IEnumerable<T> obj, out ReadOnlySpan<T> span)
        {
            switch (obj)
            {
                case T[] array:
                    span = array;
                    return true;
                case List<T> list:
                    span = CollectionsMarshal.AsSpan(list);
                    return true;
                case Memory<T> memory:
                    span = memory.Span;
                    return true;
                case ReadOnlyMemory<T> rom:
                    span = rom.Span;
                    return true;
                default:
                    span = default;
                    return false;
            }
        }

        private static IComparer<T> CreateComparer()
        {
            var typeOfElement = typeof(T);

            if (typeof(IComparer<T>).IsAssignableFrom(typeOfElement))
                return Comparer<T>.Default;

            if (typeOfElement.IsArray)
                return (IComparer<T>)Activator.CreateInstance(
                    typeof(EnumerableComparer<>).MakeGenericType(typeOfElement.GetElementType()));

            if (typeof(IComparable<T>).IsAssignableFrom(typeOfElement))
                return new QuickComparer<T>((x, y) => ((IComparable<T>)x).CompareTo(y));

            if (typeof(IComparable).IsAssignableFrom(typeOfElement))
                return new QuickComparer<T>((x, y) => ((IComparable)x).CompareTo(y));

            throw new NotSupportedException($"The type {typeof(T).Name} doesn't support comparison.");
        }


    }
}
