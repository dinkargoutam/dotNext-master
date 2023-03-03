using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

public partial class TaskCompletionPipe<T>
{
    private class LinkedTaskNode
    {
        internal readonly T Task;
        private object? nextOrOwner;

        internal LinkedTaskNode(T task) => Task = task;

        private protected LinkedTaskNode(T task, TaskCompletionPipe<T> owner)
        {
            Debug.Assert(task is not null);
            Debug.Assert(owner is not null);

            Task = task;
            nextOrOwner = owner;
        }

        internal LinkedTaskNode? Next
        {
            get
            {
                Debug.Assert(nextOrOwner is not TaskCompletionPipe<T>);

                return Unsafe.As<LinkedTaskNode>(nextOrOwner);
            }

            set => nextOrOwner = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected TaskCompletionPipe<T>? GetOwnerAndClear()
        {
            Debug.Assert(nextOrOwner is not LinkedTaskNode);

            var result = Unsafe.As<TaskCompletionPipe<T>>(nextOrOwner);
            nextOrOwner = null;
            return result;
        }
    }

    private sealed class LazyLinkedTaskNode : LinkedTaskNode
    {
        private readonly uint expectedVersion;

        internal LazyLinkedTaskNode(T task, TaskCompletionPipe<T> owner, uint version)
            : base(task, owner)
            => expectedVersion = version;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Invoke()
        {
            var owner = GetOwnerAndClear();

            (owner?.version.VolatileRead() == expectedVersion
                ? owner.EnqueueCompletedTask(this, expectedVersion)
                : null)?.
                TrySetResultAndSentinelToAll(result: true);
        }
    }

    private LinkedTaskNode? firstTask, lastTask;

    private LinkedValueTaskCompletionSource<bool>? EnqueueCompletedTask(LinkedTaskNode node)
    {
        Debug.Assert(Monitor.IsEntered(this));
        Debug.Assert(node is { Task: { IsCompleted: true } });

        if (firstTask is null || lastTask is null)
        {
            firstTask = lastTask = node;
        }
        else
        {
            lastTask = lastTask.Next = node;
        }

        scheduledTasksCount--;
        return DetachWaitQueue();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private LinkedValueTaskCompletionSource<bool>? EnqueueCompletedTask(LinkedTaskNode node, uint expectedVersion)
        => version == expectedVersion ? EnqueueCompletedTask(node) : null;

    private bool TryDequeueCompletedTask([NotNullWhen(true)] out T? task)
    {
        Debug.Assert(Monitor.IsEntered(this));

        if (firstTask is not null)
        {
            task = firstTask.Task;
            var next = firstTask.Next;
            firstTask.Next = null; // help GC
            if ((firstTask = next) is null)
                lastTask = null;

            return true;
        }

        task = null;
        return false;
    }

    private void ClearTaskQueue()
    {
        Debug.Assert(Monitor.IsEntered(this));

        firstTask = lastTask = null;
    }
}