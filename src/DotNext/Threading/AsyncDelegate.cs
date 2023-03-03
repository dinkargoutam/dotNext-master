using Debug = System.Diagnostics.Debug;
using Missing = System.Reflection.Missing;

namespace DotNext.Threading;

using static Tasks.Continuation;
using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;

/// <summary>
/// Provides set of methods for asynchronous invocation of various delegates.
/// </summary>
/// <remarks>
/// BeginInvoke and EndInvoke methods of delegate type are supported only in .NET Framework.
/// This class provides alternative approach which allows to invoke delegate asynchronously
/// with full support of async/await feature.
/// </remarks>
/// <seealso href="https://github.com/dotnet/runtime/issues/16312">BeginInvoke throws NotSupportedException</seealso>
public static partial class AsyncDelegate
{
    private static unsafe Task InvokeAsync<TDelegate, TContext>(TDelegate @delegate, TContext context, delegate*<TDelegate, in TContext, void> invoker, CancellationToken token)
        where TDelegate : MulticastDelegate
    {
        Debug.Assert(@delegate is not null);
        Debug.Assert(invoker is not null);

        return Task.Factory.StartNew(
            InvokeCore,
            token,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Current);

        void InvokeCore()
        {
            var errors = new ExceptionAggregator();
            foreach (TDelegate target in @delegate.GetInvocationList())
            {
                if (token.IsCancellationRequested)
                {
                    errors.Add(new OperationCanceledException(token));
                    break;
                }

                try
                {
                    invoker(target, in context);
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            // aggregate all exceptions
            errors.ThrowIfNeeded();
        }
    }

    /// <summary>
    /// Invokes a delegate of arbitrary type asynchronously.
    /// </summary>
    /// <param name="delegate">A delegate to be invoked asynchronously.</param>
    /// <param name="invoker">Synchronous invoker of the delegate from invocation list.</param>
    /// <param name="token">Cancellation token.</param>
    /// <typeparam name="TDelegate">Type of delegate to invoke.</typeparam>
    /// <returns>A task allows to control asynchronous invocation of methods attached to the multicast delegate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delegate"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<TDelegate>(this TDelegate @delegate, Action<TDelegate> invoker, CancellationToken token = default)
        where TDelegate : MulticastDelegate
    {
        ArgumentNullException.ThrowIfNull(@delegate);

        return InvokeAsync(@delegate, invoker, &Invoke, token);

        static void Invoke(TDelegate target, in Action<TDelegate> invoker) => invoker(target);
    }

    /// <summary>
    /// Invokes event handlers asynchronously.
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event object.</typeparam>
    /// <param name="handler">A set event handlers combined as single delegate.</param>
    /// <param name="sender">Event sender.</param>
    /// <param name="args">Event arguments.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>An object representing state of the asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<TEventArgs>(this EventHandler<TEventArgs> handler, object sender, TEventArgs args, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return InvokeAsync(handler, (sender, args), &Invoke, token);

        static void Invoke(EventHandler<TEventArgs> handler, in (object, TEventArgs) args)
            => handler(args.Item1, args.Item2);
    }

    /// <summary>
    /// Invokes event handlers asynchronously.
    /// </summary>
    /// <param name="handler">A set event handlers combined as single delegate.</param>
    /// <param name="sender">Event sender.</param>
    /// <param name="args">Event arguments.</param>
    /// <param name="token">Optional cancellation token.</param>
    /// <returns>An object representing state of the asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync(this EventHandler handler, object sender, EventArgs args, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return InvokeAsync(handler, (sender, args), &Invoke, token);

        static void Invoke(EventHandler handler, in (object, EventArgs) args)
            => handler(args.Item1, args.Item2);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync(this Action action, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, Missing.Value, &Invoke, token);

        static void Invoke(Action handler, in Missing args)
            => handler();
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of the action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg">The action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T>(this Action<T> action, T arg, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, arg, &Invoke, token);

        static void Invoke(Action<T> handler, in T arg)
            => handler(arg);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2), &Invoke, token);

        static void Invoke(Action<T1, T2> action, in (T1, T2) args)
            => action(args.Item1, args.Item2);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3>(this Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3), &Invoke, token);

        static void Invoke(Action<T1, T2, T3> action, in (T1, T2, T3) args)
            => action(args.Item1, args.Item2, args.Item3);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4> action, in (T1, T2, T3, T4) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5> action, in (T1, T2, T3, T4, T5) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5, arg6), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5, T6> action, in (T1, T2, T3, T4, T5, T6) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5, args.Item6);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5, arg6, arg7), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5, T6, T7> action, in (T1, T2, T3, T4, T5, T6, T7) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5, args.Item6, args.Item7);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The eighth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5, T6, T7, T8> action, in (T1, T2, T3, T4, T5, T6, T7, T8) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5, args.Item6, args.Item7, args.Item8);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
    /// <typeparam name="T9">Type of the ninth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The eighth action argument.</param>
    /// <param name="arg9">THe ninth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, in (T1, T2, T3, T4, T5, T6, T7, T8, T9) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5, args.Item6, args.Item7, args.Item8, args.Item9);
    }

    /// <summary>
    /// Invokes action asynchronously.
    /// </summary>
    /// <typeparam name="T1">Type of the first action argument.</typeparam>
    /// <typeparam name="T2">Type of the second action argument.</typeparam>
    /// <typeparam name="T3">Type of the third action argument.</typeparam>
    /// <typeparam name="T4">Type of the fourth action argument.</typeparam>
    /// <typeparam name="T5">Type of the fifth action argument.</typeparam>
    /// <typeparam name="T6">Type of the sixth action argument.</typeparam>
    /// <typeparam name="T7">Type of the seventh action argument.</typeparam>
    /// <typeparam name="T8">Type of the eighth action argument.</typeparam>
    /// <typeparam name="T9">Type of the ninth action argument.</typeparam>
    /// <typeparam name="T10">Type of the tenth action argument.</typeparam>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="arg1">The first action argument.</param>
    /// <param name="arg2">The second action argument.</param>
    /// <param name="arg3">The third action argument.</param>
    /// <param name="arg4">The fourth action argument.</param>
    /// <param name="arg5">The fifth action argument.</param>
    /// <param name="arg6">The sixth action argument.</param>
    /// <param name="arg7">The seventh action argument.</param>
    /// <param name="arg8">The eighth action argument.</param>
    /// <param name="arg9">The ninth action argument.</param>
    /// <param name="arg10">The tenth action argument.</param>
    /// <param name="token">Invocation cancellation token.</param>
    /// <returns>The task representing state of asynchronous invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static unsafe Task InvokeAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync(action, (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10), &Invoke, token);

        static void Invoke(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action, in (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) args)
            => action(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5, args.Item6, args.Item7, args.Item8, args.Item9, args.Item10);
    }

    /// <summary>
    /// Invokes synchronous delegate asynchronously.
    /// </summary>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="state">The state object to be passed to the action.</param>
    /// <param name="callback">The callback to be invoked on completion.</param>
    /// <param name="options">The task scheduling options.</param>
    /// <param name="scheduler">The task scheduler.</param>
    /// <returns>The task representing asynchronous execution of the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static Task BeginInvoke(this Action<object?> action, object? state, AsyncCallback? callback, TaskCreationOptions options = TaskCreationOptions.None, TaskScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var task = Task.Factory.StartNew(action, state, CancellationToken.None, options, scheduler ?? TaskScheduler.Default);
        if (callback is not null)
            task.OnCompleted(callback);

        return task;
    }

    /// <summary>
    /// Invokes synchronous delegate asynchronously.
    /// </summary>
    /// <param name="action">The action to invoke asynchronously.</param>
    /// <param name="state">The state object to be passed to the action.</param>
    /// <param name="callback">The callback to be invoked on completion.</param>
    /// <param name="options">The task scheduling options.</param>
    /// <param name="scheduler">The task scheduler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public static Task BeginInvoke(this Action<object?, CancellationToken> action, object? state, AsyncCallback? callback, TaskCreationOptions options = TaskCreationOptions.None, TaskScheduler? scheduler = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var task = Task.Factory.StartNew(s => action(s, token), state, token, options, scheduler ?? TaskScheduler.Default);
        if (callback is not null)
            task.OnCompleted(callback);

        return task;
    }

    /// <summary>
    /// Invokes synchronous delegate asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of result of asynchronous operation.</typeparam>
    /// <param name="function">The function to invoke asynchronously.</param>
    /// <param name="state">The state object to be passed to the action.</param>
    /// <param name="callback">The callback to be invoked on completion.</param>
    /// <param name="options">The task scheduling options.</param>
    /// <param name="scheduler">The task scheduler.</param>
    /// <returns>The task representing asynchronous execution of the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    public static Task<TResult> BeginInvoke<TResult>(this Func<object?, TResult> function, object? state, AsyncCallback? callback, TaskCreationOptions options = TaskCreationOptions.None, TaskScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var task = Task<TResult>.Factory.StartNew(function, state, CancellationToken.None, options, scheduler ?? TaskScheduler.Default);
        if (callback is not null)
            task.OnCompleted(callback);

        return task;
    }

    /// <summary>
    /// Invokes synchronous delegate asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of result of asynchronous operation.</typeparam>
    /// <param name="function">The function to invoke asynchronously.</param>
    /// <param name="state">The state object to be passed to the action.</param>
    /// <param name="callback">The callback to be invoked on completion.</param>
    /// <param name="options">The task scheduling options.</param>
    /// <param name="scheduler">The task scheduler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    public static Task<TResult> BeginInvoke<TResult>(this Func<object?, CancellationToken, TResult> function, object? state, AsyncCallback? callback, TaskCreationOptions options = TaskCreationOptions.None, TaskScheduler? scheduler = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(function);

        var task = Task<TResult>.Factory.StartNew(s => function(s, token), state, token, options, scheduler ?? TaskScheduler.Default);
        if (callback is not null)
            task.OnCompleted(callback);

        return task;
    }
}