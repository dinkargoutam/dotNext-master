namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

/// <summary>
/// Represents server-side interface of the network transport.
/// </summary>
internal interface IServer : INetworkTransport, IAsyncDisposable
{
    TimeSpan ReceiveTimeout { get; init; }

    ValueTask StartAsync(CancellationToken token);
}