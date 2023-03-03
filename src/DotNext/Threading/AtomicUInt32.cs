using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Various atomic operations for <see cref="uint"/> data type
/// accessible as extension methods.
/// </summary>
/// <remarks>
/// Methods exposed by this class provide volatile read/write
/// of the field even if it is not declared as volatile field.
/// </remarks>
/// <seealso cref="Interlocked"/>
[CLSCompliant(false)]
public static class AtomicUInt32
{
    /// <summary>
    /// Reads the value of the specified field. On systems that require it, inserts a
    /// memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears after this method in the code, the processor
    /// cannot move it before this method.
    /// </summary>
    /// <param name="value">The field to read.</param>
    /// <returns>
    /// The value that was read. This value is the latest written by any processor in
    /// the computer, regardless of the number of processors or the state of processor
    /// cache.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint VolatileRead(in this uint value) => Volatile.Read(ref Unsafe.AsRef(in value));

    /// <summary>
    /// Writes the specified value to the specified field. On systems that require it,
    /// inserts a memory barrier that prevents the processor from reordering memory operations
    /// as follows: If a read or write appears before this method in the code, the processor
    /// cannot move it after this method.
    /// </summary>
    /// <param name="value">The field where the value is written.</param>
    /// <param name="newValue">
    /// The value to write. The value is written immediately so that it is visible to
    /// all processors in the computer.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite(ref this uint value, uint newValue) => Volatile.Write(ref value, newValue);

    /// <summary>
    /// Atomically increments the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint IncrementAndGet(ref this uint value)
        => Interlocked.Increment(ref value);

    /// <summary>
    /// Atomically decrements the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint DecrementAndGet(ref this uint value)
        => Interlocked.Decrement(ref value);

    /// <summary>
    /// Atomically sets the referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet(ref this uint value, uint expected, uint update)
        => Interlocked.CompareExchange(ref value, update, expected) == expected;

    /// <summary>
    /// Adds two 32-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AddAndGet(ref this uint value, uint operand)
        => Interlocked.Add(ref value, operand);

    /// <summary>
    /// Adds two 32-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndAdd(ref this uint value, uint operand)
        => Accumulate(ref value, operand, new Adder()).OldValue;

    /// <summary>
    /// Bitwise "ands" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndBitwiseAnd(ref this uint value, uint operand)
        => Interlocked.And(ref value, operand);

    /// <summary>
    /// Bitwise "ands" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint BitwiseAndAndGet(ref this uint value, uint operand)
        => Interlocked.And(ref value, operand) & operand;

    /// <summary>
    /// Bitwise "ors" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndBitwiseOr(ref this uint value, uint operand)
        => Interlocked.Or(ref value, operand);

    /// <summary>
    /// Bitwise "ors" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint BitwiseOrAndGet(ref this uint value, uint operand)
        => Interlocked.Or(ref value, operand) | operand;

    /// <summary>
    /// Bitwise "xors" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndBitwiseXor(ref this uint value, uint operand)
        => Accumulate(ref value, operand, new BitwiseXor()).OldValue;

    /// <summary>
    /// Bitwise "xors" two 32-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint BitwiseXorAndGet(ref this uint value, uint operand)
        => Accumulate(ref value, operand, new BitwiseXor()).NewValue;

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndSet(ref this uint value, uint update)
        => Interlocked.Exchange(ref value, update);

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SetAndGet(ref this uint value, uint update)
    {
        VolatileWrite(ref value, update);
        return update;
    }

    private static (uint OldValue, uint NewValue) Update<TUpdater>(ref uint value, TUpdater updater)
        where TUpdater : struct, ISupplier<uint, uint>
    {
        uint oldValue, newValue, tmp = Volatile.Read(ref value);
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        }
        while ((tmp = Interlocked.CompareExchange(ref value, newValue, oldValue)) != oldValue);

        return (oldValue, newValue);
    }

    private static (uint OldValue, uint NewValue) Accumulate<TAccumulator>(ref uint value, uint x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<uint, uint, uint>
    {
        uint oldValue, newValue, tmp = Volatile.Read(ref value);
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        }
        while ((tmp = Interlocked.CompareExchange(ref value, newValue, oldValue)) != oldValue);

        return (oldValue, newValue);
    }

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AccumulateAndGet(ref this uint value, uint x, Func<uint, uint, uint> accumulator)
        => Accumulate<DelegatingSupplier<uint, uint, uint>>(ref value, x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint AccumulateAndGet(ref this uint value, uint x, delegate*<uint, uint, uint> accumulator)
        => Accumulate<Supplier<uint, uint, uint>>(ref value, x, accumulator).NewValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndAccumulate(ref this uint value, uint x, Func<uint, uint, uint> accumulator)
        => Accumulate<DelegatingSupplier<uint, uint, uint>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the current value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint GetAndAccumulate(ref this uint value, uint x, delegate*<uint, uint, uint> accumulator)
        => Accumulate<Supplier<uint, uint, uint>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint UpdateAndGet(ref this uint value, Func<uint, uint> updater)
        => Update<DelegatingSupplier<uint, uint>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint UpdateAndGet(ref this uint value, delegate*<uint, uint> updater)
        => Update<Supplier<uint, uint>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndUpdate(ref this uint value, Func<uint, uint> updater)
        => Update<DelegatingSupplier<uint, uint>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint GetAndUpdate(ref this uint value, delegate*<uint, uint> updater)
        => Update<Supplier<uint, uint>>(ref value, updater).OldValue;
}