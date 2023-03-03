using System.Collections;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using Collections.Generic;

/// <summary>
/// Represents immutable list of delegates.
/// </summary>
/// <remarks>
/// This type can be used to store a list of event handlers in situations
/// when the delegate type allows variance. In this case, <see cref="Delegate.Combine(Delegate[])"/>
/// may not be applicable due to lack of variance support.
/// </remarks>
/// <typeparam name="TDelegate">The type of delegates in the list.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct InvocationList<TDelegate> : IReadOnlyCollection<TDelegate> // TODO: Workaround for https://github.com/dotnet/runtime/issues/4556
    where TDelegate : MulticastDelegate
{
    /// <summary>
    /// Represents enumerator over the list of delegates.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private object? list;
        private int index;

        internal Enumerator(object? list)
        {
            this.list = list;
            index = -1;
            Current = default!;
        }

        /// <summary>
        /// Gets the current delegate.
        /// </summary>
        public TDelegate Current
        {
            get;
            private set;
        }

        /// <summary>
        /// Moves to the next delegate in the list.
        /// </summary>
        /// <returns><see langword="false"/> if the enumerator reaches the end of the list; otherwise, <see langword="true"/>.</returns>
        public bool MoveNext()
        {
            if (list is null)
                goto fail;

            if (list is TDelegate)
            {
                Current = Unsafe.As<TDelegate>(list);
                list = null;
                goto success;
            }

            var array = Unsafe.As<TDelegate[]>(list);
            index += 1;
            if ((uint)index >= (uint)array.Length)
                goto fail;

            Current = array[index];

        success:
            return true;
        fail:
            return false;
        }
    }

    /// <summary>
    /// Gets an empty list.
    /// </summary>
    public static InvocationList<TDelegate> Empty => default;

    // null, TDelegate or TDelegate[]
    private readonly object? list;

    /// <summary>
    /// Creates a new list containing a single element.
    /// </summary>
    /// <param name="d">The delegate to add.</param>
    public InvocationList(TDelegate d) => list = d;

    private InvocationList(TDelegate[] array, TDelegate d)
    {
        Array.Resize(ref array, array.Length + 1);
        array[array.Length - 1] = d;
        list = array;
    }

    private InvocationList(TDelegate d1, TDelegate d2)
        => list = new TDelegate[] { d1, d2 };

    private InvocationList(TDelegate[] array)
        => list = array;

    /// <summary>
    /// Indicates that this list is empty.
    /// </summary>
    public bool IsEmpty => list is null;

    /// <summary>
    /// Adds a delegate to the list and return a new list.
    /// </summary>
    /// <param name="d">The delegate to add.</param>
    /// <returns>The modified list of delegates.</returns>
    public InvocationList<TDelegate> Add(TDelegate? d) => d is null ? this : list switch
    {
        null => new(d),
        TDelegate => new(Unsafe.As<TDelegate>(list), d),
        _ => new(Unsafe.As<TDelegate[]>(list), d),
    };

    /// <summary>
    /// Removes the delegate from the list.
    /// </summary>
    /// <param name="d">The delegate to remove.</param>
    /// <returns>The modified list of delegates.</returns>
    public InvocationList<TDelegate> Remove(TDelegate? d)
    {
        InvocationList<TDelegate> result;

        if (d is null || list is null)
        {
            result = this;
        }
        else if (Equals(list, d))
        {
            result = default;
        }
        else
        {
            var array = Unsafe.As<TDelegate[]>(list);
            long index = Array.IndexOf(array, d);

            if (index >= 0L)
                array = OneDimensionalArray.RemoveAt(array, index);

            result = new(array);
        }

        return result;
    }

    /// <summary>
    /// Gets the number of delegates in this list.
    /// </summary>
    public int Count => list switch
    {
        null => 0,
        TDelegate => 1,
        _ => Unsafe.As<TDelegate[]>(list).Length,
    };

    /// <summary>
    /// Combines the delegates in the list to a single delegate.
    /// </summary>
    /// <returns>A list of delegates combined as a single delegate.</returns>
    public TDelegate? Combine() => list switch
    {
        null => null,
        TDelegate => Unsafe.As<TDelegate>(list),
        _ => Delegate.Combine(Unsafe.As<TDelegate[]>(list)) as TDelegate,
    };

    /// <summary>
    /// Addes the delegate to the list and returns modified list.
    /// </summary>
    /// <param name="list">The list of delegates.</param>
    /// <param name="d">The delegate to add.</param>
    /// <returns>The modified list of delegates.</returns>
    public static InvocationList<TDelegate> operator +(InvocationList<TDelegate> list, TDelegate? d)
        => list.Add(d);

    /// <summary>
    /// Removes the delegate from the list and returns modified list.
    /// </summary>
    /// <param name="list">The list of delegates.</param>
    /// <param name="d">The delegate to remove.</param>
    /// <returns>The modified list of delegates.</returns>
    public static InvocationList<TDelegate> operator -(InvocationList<TDelegate> list, TDelegate? d)
        => list.Remove(d);

    /// <summary>
    /// Gets enumerator over all delegates in this list.
    /// </summary>
    /// <returns>The enumerator over delegates.</returns>
    public Enumerator GetEnumerator() => new(list);

    private IEnumerator<TDelegate> GetEnumeratorCore() => list switch
    {
        null => Sequence.GetEmptyEnumerator<TDelegate>(),
        TDelegate d => new SingletonList<TDelegate>.Enumerator(d),
        _ => Unsafe.As<IEnumerable<TDelegate>>(list).GetEnumerator(),
    };

    /// <inheritdoc />
    IEnumerator<TDelegate> IEnumerable<TDelegate>.GetEnumerator() => GetEnumeratorCore();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumeratorCore();

    private static ReadOnlySpan<TDelegate> GetSpan(in object? list) => list switch
    {
        null => ReadOnlySpan<TDelegate>.Empty,
        TDelegate => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, TDelegate>(ref Unsafe.AsRef(in list)), 1),
        _ => Unsafe.As<TDelegate[]>(list),
    };

    internal static ReadOnlySpan<TDelegate> GetList(in InvocationList<TDelegate> list) => GetSpan(in list.list);
}

/// <summary>
/// Provides various extensions for <see cref="InvocationList{TDelegate}"/> type.
/// </summary>
public static class InvocationList
{
    /// <summary>
    /// Gets a span over the delegates in the list.
    /// </summary>
    /// <typeparam name="TDelegate">The type of the delegates.</typeparam>
    /// <param name="delegates">The list of delegates.</param>
    /// <returns>A span over the delegates in the list.</returns>
    public static ReadOnlySpan<TDelegate> AsSpan<TDelegate>(this ref InvocationList<TDelegate> delegates)
        where TDelegate : MulticastDelegate
        => InvocationList<TDelegate>.GetList(in delegates);

    /// <summary>
    /// Invokes all actions in the list.
    /// </summary>
    /// <typeparam name="T">The type of the action argument.</typeparam>
    /// <param name="actions">The list of actions.</param>
    /// <param name="arg">The argument of the action.</param>
    public static void Invoke<T>(this InvocationList<Action<T>> actions, T arg)
    {
        foreach (var action in actions.AsSpan())
            action(arg);
    }

    /// <summary>
    /// Invokes all actions in the list.
    /// </summary>
    /// <typeparam name="T1">The type of the first action argument.</typeparam>
    /// <typeparam name="T2">The type of the second action argument.</typeparam>
    /// <param name="actions">The list of actions.</param>
    /// <param name="arg1">The first argument of the action.</param>
    /// <param name="arg2">The second argument of the action.</param>
    public static void Invoke<T1, T2>(this InvocationList<Action<T1, T2>> actions, T1 arg1, T2 arg2)
    {
        foreach (var action in actions.AsSpan())
            action(arg1, arg2);
    }
}