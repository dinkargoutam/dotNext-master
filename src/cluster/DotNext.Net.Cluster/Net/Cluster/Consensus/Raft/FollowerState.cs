﻿using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;

internal sealed class FollowerState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly AsyncAutoResetEvent refreshEvent;
    private readonly AsyncManualResetEvent suppressionEvent;
    private readonly CancellationTokenSource trackerCancellation;
    private Task? tracker;
    internal IFollowerStateMetrics? Metrics;
    private volatile bool timedOut;

    internal FollowerState(IRaftStateMachine<TMember> stateMachine)
        : base(stateMachine)
    {
        refreshEvent = new(initialState: false);
        suppressionEvent = new(initialState: true);
        trackerCancellation = new();
    }

    private void SuspendTracking()
    {
        suppressionEvent.Reset();
        refreshEvent.Set();
    }

    private void ResumeTracking() => suppressionEvent.Set();

    private async Task Track(TimeSpan timeout, IAsyncEvent refreshEvent, CancellationToken token)
    {
        Debug.Assert(token != trackerCancellation.Token);

        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, trackerCancellation.Token);

        // spin loop to wait for the timeout
        while (await refreshEvent.WaitAsync(timeout, tokenSource.Token).ConfigureAwait(false))
        {
            // Transition can be suppressed. If so, resume the loop and reset the timer.
            // If the event is in signaled state then the returned task is completed synchronously.
            await suppressionEvent.WaitAsync(tokenSource.Token).ConfigureAwait(false);
        }

        timedOut = true;

        // timeout happened, move to candidate state
        MoveToCandidateState();
    }

    internal void StartServing(TimeSpan timeout, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            trackerCancellation.Cancel(false);
            tracker = null;
        }
        else
        {
            timedOut = false;
            tracker = Track(timeout, refreshEvent, token);
        }
    }

    internal bool IsExpired => timedOut;

    internal void Refresh()
    {
        Logger.TimeoutReset();
        refreshEvent.Set();
        Metrics?.ReportHeartbeat();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            trackerCancellation.Cancel(false);
            await (tracker ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.FollowerStateExitedFailed(e);
        }
        finally
        {
            Dispose(disposing: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshEvent.Dispose();
            suppressionEvent.Dispose();
            trackerCancellation.Dispose();
            tracker = null;
            Metrics = null;
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct TransitionSuppressionScope : IDisposable
    {
        private readonly FollowerState<TMember>? state;

        internal TransitionSuppressionScope(FollowerState<TMember>? state)
        {
            state?.SuspendTracking();
            this.state = state;
        }

        public void Dispose() => state?.ResumeTracking();
    }
}