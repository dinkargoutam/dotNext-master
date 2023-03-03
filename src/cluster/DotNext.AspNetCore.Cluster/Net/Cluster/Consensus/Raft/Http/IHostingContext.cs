﻿using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal interface IHostingContext
{
    HttpMessageHandler CreateHttpHandler();

    bool IsLeader(IRaftClusterMember member);

    bool UseEfficientTransferOfLogEntries { get; }

    ILogger Logger { get; }

    ref readonly ClusterMemberId LocalMember { get; }

    IReadOnlyDictionary<string, string> Metadata { get; }

    // allows to override default HTTP timeout for specific kind of messages
    bool TryGetTimeout<TMessage>(out TimeSpan timeout)
        where TMessage : class, IHttpMessage;
}