﻿using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents a list with one element.
/// </summary>
/// <typeparam name="T">The type of the element in the list.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct SingletonList<T> : IReadOnlyList<T>, IList<T>
{
    /// <summary>
    /// Represents an enumerator over the collection containing a single element.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<T>
    {
        private const byte NotRequestedState = 1;
        private const byte RequestedState = 2;

        private byte state;

        internal Enumerator(T item)
        {
            Current = item;
            state = NotRequestedState;
        }

        /// <summary>
        /// Gets the current element.
        /// </summary>
        public readonly T Current { get; }

        /// <inheritdoc />
        readonly object? IEnumerator.Current => Current;

        /// <inheritdoc />
        void IDisposable.Dispose() => this = default;

        /// <summary>
        /// Advances the position of the enumerator to the next element.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator advanced successfully; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (state == NotRequestedState)
            {
                state = RequestedState;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets state of this enumerator.
        /// </summary>
        public void Reset()
        {
            if (state == RequestedState)
                state = NotRequestedState;
        }
    }

    /// <summary>
    /// Initializes a new list with one element.
    /// </summary>
    /// <param name="item">The element in the list.</param>
    public SingletonList(T item) => Item = item;

    /// <summary>
    /// Gets or sets the item in this list.
    /// </summary>
    public T Item
    {
        readonly get;
        set;
    }

    /// <summary>
    /// Gets or sets the item in this list.
    /// </summary>
    /// <param name="index">The index of the item. Must be zero.</param>
    /// <returns>The item.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is not equal to zero.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [IndexerName("Element")]
    public T this[int index]
    {
        readonly get => index == 0 ? Item : throw new IndexOutOfRangeException(ExceptionMessages.IndexShouldBeZero);
        set => Item = index == 0 ? value : throw new IndexOutOfRangeException(ExceptionMessages.IndexShouldBeZero);
    }

    /// <inheritdoc />
    readonly bool ICollection<T>.IsReadOnly => true;

    /// <inheritdoc />
    readonly int IReadOnlyCollection<T>.Count => 1;

    /// <inheritdoc />
    readonly int ICollection<T>.Count => 1;

    /// <inheritdoc />
    readonly void ICollection<T>.Clear() => throw new NotSupportedException();

    /// <inheritdoc />
    readonly bool ICollection<T>.Contains(T item) => EqualityComparer<T>.Default.Equals(Item, item);

    /// <inheritdoc />
    readonly void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        => array.AsSpan(arrayIndex)[0] = Item;

    /// <inheritdoc />
    readonly bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly void ICollection<T>.Add(T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly int IList<T>.IndexOf(T item) => EqualityComparer<T>.Default.Equals(Item, item) ? 0 : -1;

    /// <inheritdoc />
    readonly void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

    /// <summary>
    /// Gets enumerator for the single element in the list.
    /// </summary>
    /// <returns>The enumerator over single element.</returns>
    public readonly Enumerator GetEnumerator() => new(Item);

    /// <inheritdoc />
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Converts a value to the read-only list.
    /// </summary>
    /// <param name="item">The item in the list.</param>
    /// <returns>The collection containing the list.</returns>
    public static implicit operator SingletonList<T>(T item) => new(item);
}