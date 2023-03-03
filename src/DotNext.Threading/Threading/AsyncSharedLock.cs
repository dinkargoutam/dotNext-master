﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Tasks.Pooling;

/// <summary>
/// Represents a lock that can be acquired in exclusive or weak mode.
/// </summary>
/// <remarks>
/// This lock represents the combination of semaphore and reader-writer
/// lock. The caller can acquire weak locks simultaneously which count
/// is limited by the concurrency level passed into the constructor. However, the
/// only one caller can acquire the lock exclusively.
/// </remarks>
[DebuggerDisplay($"AvailableLocks = {{{nameof(RemainingCount)}}}, StrongLockHeld = {{{nameof(IsStrongLockHeld)}}}")]
public class AsyncSharedLock : QueuedSynchronizer, IAsyncDisposable
{
    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>
    {
        internal bool IsStrongLock;

        protected override void AfterConsumed() => AfterConsumed(this);

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed { get; set; }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        private const long ExclusiveMode = -1L;

        internal readonly long ConcurrencyLevel;
        private long remainingLocks;   // -1 means that the lock is acquired in exclusive mode

        internal State(long concurrencyLevel) => ConcurrencyLevel = remainingLocks = concurrencyLevel;

        internal long RemainingLocks
        {
            readonly get => remainingLocks.VolatileRead();
            private set => remainingLocks.VolatileWrite(value);
        }

        internal readonly bool IsWeakLockAllowed => RemainingLocks > 0L;

        internal void AcquireWeakLock() => remainingLocks.DecrementAndGet();

        internal void ExitLock()
        {
            if (RemainingLocks < 0L)
            {
                RemainingLocks = ConcurrencyLevel;
            }
            else
            {
                remainingLocks.IncrementAndGet();
            }
        }

        internal readonly bool IsStrongLockHeld => RemainingLocks < 0L;

        internal readonly bool IsStrongLockAllowed => RemainingLocks == ConcurrencyLevel;

        internal void AcquireStrongLock() => RemainingLocks = ExclusiveMode;

        internal void Downgrade() => RemainingLocks = ConcurrencyLevel - 1L;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct WeakLockManager : ILockManager<WaitNode>
    {
        bool ILockManager.IsLockAllowed
            => Unsafe.As<WeakLockManager, State>(ref Unsafe.AsRef(this)).IsWeakLockAllowed;

        void ILockManager.AcquireLock()
            => Unsafe.As<WeakLockManager, State>(ref Unsafe.AsRef(this)).AcquireWeakLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.IsStrongLock = false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct StrongLockManager : ILockManager<WaitNode>
    {
        bool ILockManager.IsLockAllowed
            => Unsafe.As<StrongLockManager, State>(ref Unsafe.AsRef(this)).IsStrongLockAllowed;

        void ILockManager.AcquireLock()
            => Unsafe.As<StrongLockManager, State>(ref Unsafe.AsRef(this)).AcquireStrongLock();

        void ILockManager<WaitNode>.InitializeNode(WaitNode node)
            => node.IsStrongLock = true;
    }

    private State state;
    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;

    /// <summary>
    /// Initializes a new shared lock.
    /// </summary>
    /// <param name="concurrencyLevel">The number of unique callers that can obtain shared lock simultaneously.</param>
    /// <param name="limitedConcurrency">
    /// <see langword="true"/> if the potential number of concurrent flows will not be greater than <paramref name="concurrencyLevel"/>;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
    public AsyncSharedLock(long concurrencyLevel, bool limitedConcurrency = true)
    {
        if (concurrencyLevel < 1L)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

        state = new(concurrencyLevel);
        pool = new(OnCompleted, limitedConcurrency ? concurrencyLevel : null);
    }

    [MethodImpl(MethodImplOptions.Synchronized | MethodImplOptions.NoInlining)]
    private void OnCompleted(WaitNode node)
    {
        if (node.NeedsRemoval && RemoveNode(node))
            DrainWaitQueue();

        pool.Return(node);
    }

    /// <summary>
    /// Gets the number of shared locks that can be acquired.
    /// </summary>
    public long RemainingCount => Math.Max(state.RemainingLocks, 0L);

    /// <summary>
    /// Gets the maximum number of locks that can be obtained simultaneously.
    /// </summary>
    public long ConcurrencyLevel => state.ConcurrencyLevel;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive or shared mode.
    /// </summary>
    public bool IsLockHeld => state.RemainingLocks < state.ConcurrencyLevel;

    /// <summary>
    /// Indicates that the lock is acquired in exclusive mode.
    /// </summary>
    public bool IsStrongLockHeld => state.IsStrongLockHeld;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private bool TryAcquire<TManager>()
        where TManager : struct, ILockManager<WaitNode>
    {
        ThrowIfDisposed();
        return TryAcquire(ref Unsafe.As<State, TManager>(ref state));
    }

    /// <summary>
    /// Attempts to obtain lock synchronously without blocking caller thread.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public bool TryAcquire(bool strongLock)
        => strongLock ? TryAcquire<StrongLockManager>() : TryAcquire<WeakLockManager>();

    [MethodImpl(MethodImplOptions.Synchronized)]
    private ValueTaskFactory Acquire<TManager>(bool throwOnTimeout, bool zeroTimeout)
        where TManager : struct, ILockManager<WaitNode>
        => Wait(ref Unsafe.As<State, TManager>(ref state), ref pool, throwOnTimeout, zeroTimeout);

    /// <summary>
    /// Attempts to enter the lock asynchronously, with an optional time-out.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns><see langword="true"/> if the caller entered the lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> TryAcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
    {
        if (ValidateTimeoutAndToken(timeout, token, out ValueTask<bool> task))
        {
            task = (strongLock
                ? Acquire<StrongLockManager>(throwOnTimeout: false, timeout == TimeSpan.Zero)
                : Acquire<WeakLockManager>(throwOnTimeout: false, timeout == TimeSpan.Zero)).CreateTask(timeout, token);
        }

        return task;
    }

    /// <summary>
    /// Entres the lock asynchronously.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time-out value is negative.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="TimeoutException">The lock cannot be acquired during the specified amount of time.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask AcquireAsync(bool strongLock, TimeSpan timeout, CancellationToken token = default)
    {
        if (ValidateTimeoutAndToken(timeout, token, out ValueTask task))
        {
            task = (strongLock
                ? Acquire<StrongLockManager>(throwOnTimeout: true, timeout == TimeSpan.Zero)
                : Acquire<WeakLockManager>(throwOnTimeout: true, timeout == TimeSpan.Zero)).CreateVoidTask(timeout, token);
        }

        return task;
    }

    /// <summary>
    /// Entres the lock asynchronously.
    /// </summary>
    /// <param name="strongLock"><see langword="true"/> to acquire strong(exclusive) lock; <see langword="false"/> to acquire weak lock.</param>
    /// <param name="token">The token that can be used to abort lock acquisition.</param>
    /// <returns>The task representing lock acquisition operation.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask AcquireAsync(bool strongLock, CancellationToken token = default)
    {
        return token.IsCancellationRequested
            ? ValueTask.FromCanceled(token)
            : (strongLock ? Acquire<StrongLockManager>(throwOnTimeout: false, zeroTimeout: false) : Acquire<WeakLockManager>(throwOnTimeout: false, zeroTimeout: false))
            .CreateVoidTask(token);
    }

    private void DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(first is null or WaitNode);

        for (WaitNode? current = Unsafe.As<WaitNode>(first), next; current is not null; current = next)
        {
            Debug.Assert(current.Next is null or WaitNode);

            next = Unsafe.As<WaitNode>(current.Next);

            switch ((current.IsStrongLock, state.IsStrongLockAllowed))
            {
                case (true, true):
                    if (RemoveAndSignal(current))
                    {
                        state.AcquireStrongLock();
                        return;
                    }

                    continue;
                case (true, false):
                    return;
                default:
                    // no more locks to acquire
                    if (!state.IsWeakLockAllowed)
                        return;

                    if (RemoveAndSignal(current))
                        state.AcquireWeakLock();

                    continue;
            }
        }
    }

    /// <summary>
    /// Releases the acquired weak lock or downgrade exclusive lock to the weak lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Downgrade()
    {
        ThrowIfDisposed();

        if (state.IsStrongLockAllowed) // nothing to release
            throw new SynchronizationLockException(ExceptionMessages.NotInLock);

        state.Downgrade();
        DrainWaitQueue();
    }

    /// <summary>
    /// Release the acquired lock.
    /// </summary>
    /// <exception cref="SynchronizationLockException">The caller has not entered the lock.</exception>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Release()
    {
        ThrowIfDisposed();

        if (state.IsStrongLockAllowed) // nothing to release
            throw new SynchronizationLockException(ExceptionMessages.NotInLock);

        state.ExitLock();
        DrainWaitQueue();

        if (IsDisposing && IsReadyToDispose)
            Dispose(true);
    }

    private protected sealed override bool IsReadyToDispose => state.IsStrongLockAllowed && first is null;
}