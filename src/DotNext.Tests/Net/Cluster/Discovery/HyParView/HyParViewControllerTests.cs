using Microsoft.AspNetCore.Connections;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using HttpPeerConfiguration = Http.HttpPeerConfiguration;

    [ExcludeFromCodeCoverage]
    public sealed class HyParViewControllerTests : Test
    {
        private abstract class TestPeerControllerBase : PeerController
        {
            internal readonly UriEndPoint Address;

            protected TestPeerControllerBase(HttpPeerConfiguration configuration)
                : base(configuration)
                => Address = new(configuration.LocalNode);

            protected sealed override bool IsLocalNode(EndPoint peer) => PeerComparer.Equals(peer, Address);

            internal new ValueTask EnqueueJoinAsync(EndPoint joinedPeer, CancellationToken token)
                => base.EnqueueJoinAsync(joinedPeer, token);

            internal new ValueTask EnqueueNeighborAsync(EndPoint sender, bool highPriority, CancellationToken token)
                => base.EnqueueNeighborAsync(sender, highPriority, token);

            internal new ValueTask EnqueueForwardJoinAsync(EndPoint sender, EndPoint joinedPeer, int timeToLive, CancellationToken token)
                => base.EnqueueForwardJoinAsync(sender, joinedPeer, timeToLive, token);

            internal new ValueTask EnqueueDisconnectAsync(EndPoint sender, bool isAlive, CancellationToken token)
                => base.EnqueueDisconnectAsync(sender, isAlive, token);

            internal new ValueTask EnqueueShuffleAsync(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token)
                => base.EnqueueShuffleAsync(sender, origin, peers, timeToLive, token);

            internal new ValueTask EnqueueShuffleReplyAsync(IReadOnlyCollection<EndPoint> peers, CancellationToken token)
                => base.EnqueueShuffleReplyAsync(peers, token);
        }

        private sealed class TransportLayer : Dictionary<EndPoint, TestPeerControllerBase>
        {
            internal TransportLayer()
                : base(EndPointFormatter.UriEndPointComparer)
            {
            }
        }

        private sealed class TestPeerController : TestPeerControllerBase
        {
            private readonly UriEndPoint contactNode;
            private readonly TransportLayer transport;

            internal TestPeerController(HttpPeerConfiguration configuration, TransportLayer transport)
                : base(configuration)
            {
                this.transport = transport;
                contactNode = configuration.ContactNode is not null ? new(configuration.ContactNode) : null;
            }

            public Task StartAsync(CancellationToken token = default)
            {
                transport.Add(Address, this);
                return StartAsync(contactNode, token);
            }

            public override async Task StopAsync(CancellationToken token = default)
            {
                await base.StopAsync(token);
                transport.Remove(Address);
            }

            protected override Task JoinAsync(EndPoint contactNode, CancellationToken token)
                => transport[contactNode].EnqueueJoinAsync(Address, token).AsTask();

            protected override Task NeighborAsync(EndPoint neighbor, bool highPriority, CancellationToken token)
                => transport[neighbor].EnqueueNeighborAsync(Address, highPriority, token).AsTask();

            protected override Task ForwardJoinAsync(EndPoint receiver, EndPoint joinedPeer, int timeToLive, CancellationToken token)
                => transport[receiver].EnqueueForwardJoinAsync(Address, joinedPeer, timeToLive, token).AsTask();

            protected override Task DisconnectAsync(EndPoint peer, bool isAlive, CancellationToken token)
                => transport.TryGetValue(peer, out var client) ? client.EnqueueDisconnectAsync(Address, isAlive, token).AsTask() : Task.CompletedTask;

            protected override Task ShuffleAsync(EndPoint receiver, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token)
                => transport[receiver].EnqueueShuffleAsync(Address, origin, peers, timeToLive, token).AsTask();

            protected override Task ShuffleReplyAsync(EndPoint receiver, IReadOnlyCollection<EndPoint> peers, CancellationToken token)
                => transport[receiver].EnqueueShuffleReplyAsync(peers, token).AsTask();
        }

        private static async Task WaitForPeer(PeerController controller, UriEndPoint peer)
        {
            var listener = new TaskCompletionSource();
            Action<PeerController, PeerEventArgs> handler = (c, a) =>
            {
                if (EndPointFormatter.UriEndPointComparer.Equals(peer, a.PeerAddress))
                    listener.SetResult();
            };
            controller.PeerDiscovered += handler;
            await listener.Task.WaitAsync(DefaultTimeout);
            controller.PeerDiscovered -= handler;
        }

        [Fact]
        public static async Task ActiveViewOverflow()
        {
            var transport = new TransportLayer();
            using var peer1 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                LocalNode = new("http://localhost:3262", UriKind.Absolute)
            },
            transport);

            await peer1.StartAsync();

            using var peer2 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new("http://localhost:3262"),
                LocalNode = new("http://localhost:3263")
            },
            transport);

            var task = WaitForPeer(peer2, new UriEndPoint(new("http://localhost:3262")));
            await peer2.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer3 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new("http://localhost:3262"),
                LocalNode = new("http://localhost:3264")
            },
            transport);

            task = WaitForPeer(peer3, new UriEndPoint(new("http://localhost:3262")));
            await peer3.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer4 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new("http://localhost:3262"),
                LocalNode = new("http://localhost:3265")
            },
            transport);

            task = WaitForPeer(peer4, new UriEndPoint(new("http://localhost:3262")));
            await peer4.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer5 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new("http://localhost:3262"),
                LocalNode = new("http://localhost:3266")
            },
            transport);

            task = WaitForPeer(peer5, new UriEndPoint(new("http://localhost:3262")));
            await peer5.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            await peer5.StopAsync();
            await peer4.StopAsync();
            await peer3.StopAsync();
            await peer2.StopAsync();
            await peer1.StopAsync();
        }
    }
}