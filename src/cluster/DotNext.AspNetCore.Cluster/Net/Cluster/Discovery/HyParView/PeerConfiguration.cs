namespace DotNext.Net.Cluster.Discovery.HyParView;

/// <summary>
/// Represents configuration of the peer powered by HyParView membership protocol.
/// </summary>
public class PeerConfiguration : IPeerConfiguration
{
    private readonly Random random = new();
    private int activeViewCapacity = 5;
    private int passiveViewCapacity = 10;
    private int activeRandomWalkLength = 3;
    private int passiveRandomWalkLength = 2;
    private int? shuffleActiveViewCount, shufflePassiveViewCount, shuffleRandomWalkLength;
    private int? queueCapacity;
    private int periodLowerBound = 1000, periodUpperBound = 3000;

    /// <summary>
    /// Gets or sets the capacity of active view representing resolved peers.
    /// </summary>
    public int ActiveViewCapacity
    {
        get => activeViewCapacity;
        set => activeViewCapacity = value > 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the capacity of backlog for peers.
    /// </summary>
    public int PassiveViewCapacity
    {
        get => passiveViewCapacity;
        set => passiveViewCapacity = value > 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the maximum number of hops a ForwardJoin request is propagated.
    /// </summary>
    public int ActiveRandomWalkLength
    {
        get => activeRandomWalkLength;
        set => activeRandomWalkLength = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the value specifies at which point in the walk the peer is inserted into passive view.
    /// </summary>
    public int PassiveRandomWalkLength
    {
        get => passiveRandomWalkLength;
        set => passiveRandomWalkLength = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the number of peers from active view to be included into Shuffle message.
    /// </summary>
    public int ShuffleActiveViewCount
    {
        get => shuffleActiveViewCount ?? Math.Max(activeViewCapacity / 2, 2);
        set => shuffleActiveViewCount = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the number of peers from passive view to be included into Shuffle message.
    /// </summary>
    public int ShufflePassiveViewCount
    {
        get => shufflePassiveViewCount ?? Math.Max(passiveViewCapacity / 2, 2);
        set => shufflePassiveViewCount = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the maximum number of hops a Shuffle message is propagated.
    /// </summary>
    public int ShuffleRandomWalkLength
    {
        get => shuffleRandomWalkLength ?? passiveRandomWalkLength;
        set => shuffleRandomWalkLength = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the capacity of internal queue used to process messages.
    /// </summary>
    public int QueueCapacity
    {
        get => queueCapacity ?? activeViewCapacity + passiveViewCapacity;
        set => queueCapacity = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets lower bound of randomly selected shuffle period.
    /// </summary>
    public int LowerShufflePeriod
    {
        get => periodLowerBound;
        set => periodLowerBound = value.IsBetween(0, int.MaxValue) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets upper bound of randomly selected shuffle period.
    /// </summary>
    public int UpperShufflePeriod
    {
        get => periodUpperBound;
        set => periodUpperBound = value.IsBetween(0, int.MaxValue) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <inheritdoc/>
    TimeSpan? IPeerConfiguration.ShufflePeriod => periodLowerBound.CompareTo(periodUpperBound) switch
    {
        < 0 => TimeSpan.FromMilliseconds(random.Next(periodLowerBound, periodUpperBound)),
        0 => TimeSpan.FromMilliseconds(periodLowerBound),
        > 0 => null,
    };
}