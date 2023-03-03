using System.IO.Pipelines;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

internal sealed partial class ServerExchange : PipeExchange, IReusableExchange
{
    private enum State : byte
    {
        Ready = 0,
        VoteRequestReceived,
        PreVoteRequestReceived,
        MetadataRequestReceived,
        SendingMetadata,
        ResignRequestReceived,
        HeartbeatRequestReceived,
        AppendEntriesReceived,
        ReadyToReceiveEntry,    // ready to receive next entry
        ReceivingEntry,   // log entry header is obtained, content is available
        EntryReceived,  // similar to ReceivingEntry but its content completely received
        ReceivingEntriesFinished,
        SnapshotReceived,
        ReceivingSnapshotFinished,
        ConfigurationReceived,
        ReceivingConfigurationFinished,
        SynchronizeReceived,
    }

    private readonly ILocalMember server;
    private volatile State state;
    private Task? task;

    internal ServerExchange(ILocalMember server, PipeOptions? options = null)
        : base(options)
    {
        this.server = server;
        state = State.Ready;
    }

    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        var result = new ValueTask<bool>(true);
        switch (headers.Type)
        {
            default:
                result = new ValueTask<bool>(false);
                break;
            case MessageType.Vote:
                state = State.VoteRequestReceived;
                BeginVote(payload, token);
                break;
            case MessageType.PreVote:
                state = State.PreVoteRequestReceived;
                BeginPreVote(payload, token);
                break;
            case MessageType.Metadata:
                if (headers.Control == FlowControl.None)
                {
                    state = State.MetadataRequestReceived;
                    BeginSendMetadata(token);
                }
                else
                {
                    state = State.SendingMetadata;
                }

                break;
            case MessageType.Resign:
                state = State.ResignRequestReceived;
                BeginResign(token);
                break;
            case MessageType.Heartbeat:
                state = State.HeartbeatRequestReceived;
                BeginProcessHeartbeat(payload, token);
                break;
            case MessageType.AppendEntries:
                switch (state)
                {
                    case State.Ready:
                        BeginReceiveEntries(payload.Span, token);
                        break;
                    case State.ReadyToReceiveEntry:
                        result = BeginReceiveEntry(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    case State.ReceivingEntry:
                        result = ReceivingEntry(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    default:
                        result = default;
                        break;
                }

                break;
            case MessageType.InstallSnapshot:
                switch (state)
                {
                    case State.Ready:
                        state = State.SnapshotReceived;
                        result = BeginReceiveSnapshot(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    case State.SnapshotReceived:
                        result = ReceivingSnapshot(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    default:
                        result = default;
                        break;
                }

                break;
            case MessageType.Configuration:
                switch (state)
                {
                    case State.Ready:
                        state = State.ConfigurationReceived;
                        result = BeginReceiveConfiguration(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    case State.ConfigurationReceived:
                        result = ReceivingConfiguration(payload, headers.Control == FlowControl.StreamEnd, token);
                        break;
                    default:
                        result = default;
                        break;
                }

                break;
            case MessageType.Synchronize:
                state = State.SynchronizeReceived;
                BeginSynchronize(payload, token);
                break;
        }

        return result;
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> output, CancellationToken token) => state switch
    {
        State.VoteRequestReceived => EndVote(output),
        State.PreVoteRequestReceived => EndPreVote(output),
        State.MetadataRequestReceived => SendMetadataPortionAsync(true, output, token),
        State.SendingMetadata => SendMetadataPortionAsync(false, output, token),
        State.ResignRequestReceived => EndResign(output),
        State.HeartbeatRequestReceived => EndProcessHearbeat(output),
        State.AppendEntriesReceived or State.ReadyToReceiveEntry or State.ReceivingEntry or State.ReceivingEntriesFinished or State.EntryReceived => TransmissionControl(output, token),
        State.SnapshotReceived => RequestSnapshotChunk(),
        State.ReceivingSnapshotFinished => EndReceiveSnapshot(output),
        State.ConfigurationReceived => RequestConfigurationChunk(),
        State.ReceivingConfigurationFinished => EndReceiveConfiguration(),
        State.SynchronizeReceived => EndSynchronize(output),
        _ => default,
    };

    public void Reset()
    {
        ReusePipe();
        task = null;
        state = State.Ready;
        entriesExchangeCoordinator?.CancelSuspendedCallers(new(true));
    }
}