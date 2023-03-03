﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents <see cref="Task"/> or <see cref="ValueTask"/>
/// completed synchronously.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct CompletedTask
{
    private readonly Exception? failure;

    /// <summary>
    ///  Creates task that has completed with a specified exception.
    /// </summary>
    /// <param name="failure">The exception with which to complete the task.</param>
    public CompletedTask(Exception failure) => this.failure = failure;

    /// <summary>
    /// Obtains <see cref="Task"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator Task(CompletedTask task)
        => task.failure is null ? Task.CompletedTask : Task.FromException(task.failure);

    /// <summary>
    /// Obtains <see cref="ValueTask"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator ValueTask(CompletedTask task)
        => task.failure is null ? ValueTask.CompletedTask : ValueTask.FromException(task.failure);
}

/// <summary>
/// Represents <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>
/// completed synchronously.
/// </summary>
/// <typeparam name="T">The type of the result produced by the task.</typeparam>
[StructLayout(LayoutKind.Auto)]
internal readonly struct CompletedTask<T>
{
    private readonly Exception? failure;
    [AllowNull]
    private readonly T result;

    /// <summary>
    /// Creates task that has completed with a specified exception.
    /// </summary>
    /// <param name="failure">The exception with which to complete the task.</param>
    public CompletedTask(Exception failure)
    {
        this.failure = failure;
        result = default;
    }

    /// <summary>
    /// Creates task that has completed successfully with a specified result.
    /// </summary>
    /// <param name="result">The task result.</param>
    public CompletedTask(T result)
    {
        failure = null;
        this.result = result;
    }

    /// <summary>
    /// Obtains <see cref="Task{TResult}"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator Task<T>(CompletedTask<T> task)
        => task.failure is null ? Task.FromResult(task.result) : Task.FromException<T>(task.failure);

    /// <summary>
    /// Obtains <see cref="ValueTask{TResult}"/> completed synchronously.
    /// </summary>
    /// <param name="task">Completed task.</param>
    public static implicit operator ValueTask<T>(CompletedTask<T> task)
    {
        var builder = AsyncValueTaskMethodBuilder<T>.Create();
        if (task.failure is null)
            builder.SetResult(task.result);
        else
            builder.SetException(task.failure);
        return builder.Task;
    }
}