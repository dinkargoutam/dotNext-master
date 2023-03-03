﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNext.Threading.Channels;

using IO;

internal sealed class PersistentChannelWriter<T> : ChannelWriter<T>, IChannelInfo, IDisposable
    where T : notnull
{
    private const string StateFileName = "writer.state";
    private readonly IChannelWriter<T> writer;
    private readonly FileStreamFactory fileFactory;
    private AsyncLock writeLock;
    private Partition? writeTopic;
    private ChannelCursor cursor;

    internal PersistentChannelWriter(IChannelWriter<T> writer, bool singleWriter, long initialSize)
    {
        writeLock = singleWriter ? default : AsyncLock.Exclusive();
        this.writer = writer;
        fileFactory = new()
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.Read,
            Optimization = FileOptions.Asynchronous | FileOptions.WriteThrough,
            InitialSize = initialSize,
        };

        cursor = new(writer.Location, StateFileName);
    }

    public long Position => cursor.Position;

    public override bool TryComplete(Exception? error = null) => writer.TryComplete(error);

    public override bool TryWrite(T item) => false;

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken token = default)
        => token.IsCancellationRequested ? ValueTask.FromCanceled<bool>(token) : ValueTask.FromResult(true);

    [MemberNotNull(nameof(writeTopic))]
    private void GetOrCreatePartition() => writer.GetOrCreatePartition(ref cursor, ref writeTopic, fileFactory, false);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public override async ValueTask WriteAsync(T item, CancellationToken token)
    {
        using (await writeLock.AcquireAsync(token).ConfigureAwait(false))
        {
            if (writer.Completion.IsCompleted)
                throw new ChannelClosedException();

            GetOrCreatePartition();
            await writer.SerializeAsync(item, writeTopic, token).ConfigureAwait(false);
            await writeTopic.Stream.FlushAsync(token).ConfigureAwait(false);
            await cursor.AdvanceAsync(writeTopic.Stream.Position, token).ConfigureAwait(false);
        }

        writer.MessageReady();
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            writeTopic?.Dispose();
            writeTopic = null;
            cursor.Dispose();
        }

        writeLock.Dispose();
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PersistentChannelWriter() => Dispose(false);
}