namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static IO.Pipelines.PipeExtensions;

internal partial class ServerExchange
{
    private async ValueTask<bool> BeginReceiveConfiguration(ReadOnlyMemory<byte> input, bool completed, CancellationToken token)
    {
        var count = ConfigurationExchange.ParseAnnouncement(input.Span, out var fingerprint, out var length);
        input = input.Slice(count);
        var result = await Writer.WriteAsync(input, token).ConfigureAwait(false);
        task = server.ProposeConfigurationAsync(Reader.ReadBlockAsync, length, fingerprint, token).AsTask();
        if (result.IsCompleted || completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            state = State.ReceivingConfigurationFinished;
        }

        return true;
    }

    private async ValueTask<bool> ReceivingConfiguration(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
    {
        if (content.IsEmpty)
        {
            completed = true;
        }
        else
        {
            var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
            completed |= result.IsCompleted;
        }

        if (completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            state = State.ReceivingConfigurationFinished;
        }

        return true;
    }

    private static ValueTask<(PacketHeaders, int, bool)> RequestConfigurationChunk()
        => new((new PacketHeaders(MessageType.Continue, FlowControl.Ack), 0, true));

    private async ValueTask<(PacketHeaders, int, bool)> EndReceiveConfiguration()
    {
        await (Interlocked.Exchange(ref task, null) ?? Task.CompletedTask).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.None, FlowControl.Ack), 0, false);
    }
}