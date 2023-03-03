﻿using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents leader election timeout.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ElectionTimeout
{
    private readonly int lowerValue, upperValue;

    /// <summary>
    /// Gets recommended election timeout.
    /// </summary>
    public static ElectionTimeout Recommended => new() { LowerValue = 150, UpperValue = 300 };

    /// <summary>
    /// Generates random election timeout.
    /// </summary>
    /// <param name="random">The source of random values.</param>
    /// <returns>The randomized election timeout.</returns>
    public int RandomTimeout(Random random) => random.Next(LowerValue, UpperValue + 1);

    /// <summary>
    /// Gets lower possible value of leader election timeout, in milliseconds.
    /// </summary>
    public int LowerValue
    {
        get => lowerValue;
        init => lowerValue = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets upper possible value of leader election timeout, in milliseconds.
    /// </summary>
    public int UpperValue
    {
        get => upperValue;
        init => upperValue = value is > 0 and < int.MaxValue ? value : throw new ArgumentOutOfRangeException(nameof(upperValue));
    }

    /// <summary>
    /// Deconstructs the election timeout.
    /// </summary>
    /// <param name="lowerValue">The lower possible value of leader election timeout, in milliseconds.</param>
    /// <param name="upperValue">The upper possible value of leader election timeout, in milliseconds.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out int lowerValue, out int upperValue)
    {
        lowerValue = this.lowerValue;
        upperValue = this.upperValue;
    }
}