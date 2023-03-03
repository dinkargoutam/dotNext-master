﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DotNext.Reflection;

using ReaderWriterSpinLock = Threading.ReaderWriterSpinLock;

internal abstract class Cache<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /*
     * Can't use ConcurrentDictionary here because GetOrAdd method can call factory multiple times for the same key
     */
    private readonly IDictionary<TKey, TValue> elements;
    private ReaderWriterSpinLock syncObject;

    private protected Cache(IEqualityComparer<TKey> comparer) => elements = new Dictionary<TKey, TValue>(comparer);

    private protected Cache()
        : this(EqualityComparer<TKey>.Default)
    {
    }

    private protected abstract TValue? Create(TKey cacheKey);

    internal TValue? GetOrCreate(TKey cacheKey)
    {
        syncObject.EnterReadLock();
        var exists = elements.TryGetValue(cacheKey, out TValue? item);
        syncObject.ExitReadLock();
        if (exists)
            goto exit;

        // non-fast path, discover item
        syncObject.EnterWriteLock();
        if (elements.TryGetValue(cacheKey, out item))
        {
            syncObject.ExitWriteLock();
        }
        else
        {
            try
            {
                item = Create(cacheKey);
                if (item is not null)
                    elements.Add(cacheKey, item);
            }
            finally
            {
                syncObject.ExitWriteLock();
            }
        }

    exit:
        return item;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct MemberKey : IEquatable<MemberKey>
{
    private const StringComparison NameComparison = StringComparison.Ordinal;
    internal readonly bool NonPublic;
    internal readonly string Name;

    internal MemberKey(string name, bool nonPublic)
    {
        NonPublic = nonPublic;
        Name = name;
    }

    public bool Equals(MemberKey other) => NonPublic == other.NonPublic && string.Equals(Name, other.Name, NameComparison);

    public override bool Equals([NotNullWhen(true)] object? other) => other is MemberKey key && Equals(key);

    public override int GetHashCode()
    {
        var result = new HashCode();
        result.Add(NonPublic);
        result.Add(Name, StringComparer.FromComparison(NameComparison));
        return result.ToHashCode();
    }
}

internal abstract class MemberCache<TMember, TDescriptor> : Cache<MemberKey, TDescriptor>
    where TMember : MemberInfo
    where TDescriptor : class, IMember<TMember>
{
    [SuppressMessage("Performance", "CA1805", Justification = "https://github.com/dotnet/roslyn-analyzers/issues/5750")]
    private static readonly UserDataSlot<MemberCache<TMember, TDescriptor>> Slot = new();

    internal TDescriptor? GetOrCreate(string memberName, bool nonPublic) => GetOrCreate(new MemberKey(memberName, nonPublic));

    private protected abstract TDescriptor? Create(string memberName, bool nonPublic);

    private protected sealed override TDescriptor? Create(MemberKey key) => Create(key.Name, key.NonPublic);

    internal static MemberCache<TMember, TDescriptor> Of<TCache>(MemberInfo member)
        where TCache : MemberCache<TMember, TDescriptor>, new()
        => member.GetUserData().GetOrSet<MemberCache<TMember, TDescriptor>, TCache>(Slot);
}