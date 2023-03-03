using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DotNext.Collections.Specialized;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class TypeMapBenchmark
{
    private sealed class DictionaryBasedLookup<TValue> : Dictionary<Type, TValue>
    {
        public void Set<TKey>(TValue value) => this[typeof(TKey)] = value;

        public bool TryGetValue<TKey>(out TValue value) => TryGetValue(typeof(TKey), out value);
    }

    private sealed class ConcurrentDictionaryBasedLookup<TValue> : ConcurrentDictionary<Type, TValue>
    {
        public void Set<TKey>(TValue value) => this[typeof(TKey)] = value;

        public bool TryGetValue<TKey>(out TValue value) => TryGetValue(typeof(TKey), out value);

        public TValue GetOrAdd<TKey>(TValue value) => GetOrAdd(typeof(TKey), value);
    }

    private readonly TypeMap<int> threadUnsafeMap = new();
    private readonly ConcurrentTypeMap<int> threadSafeMap = new();
    private readonly DictionaryBasedLookup<int> dictionaryLookup = new();
    private readonly ConcurrentDictionaryBasedLookup<int> concurrentLookup = new();

    [Benchmark(Description = "TypeMap, Set + TryGetValue")]
    public int TypeMapLookup()
    {
        threadUnsafeMap.Set<string>(42);
        threadUnsafeMap.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark(Description = "Dictionary, Set + TryGetValue")]
    public int DictionaryLookup()
    {
        dictionaryLookup.Set<string>(42);
        dictionaryLookup.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark(Description = "ConcurrentTypeMap, Set + TryGetValue")]
    public int ConcurrentTypeMapLookup()
    {
        threadSafeMap.Set<string>(42);
        threadSafeMap.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark(Description = "ConcurrentDictionary, Set + TryGetValue")]
    public int ConcurrentDictionaryLookup()
    {
        concurrentLookup.Set<string>(42);
        concurrentLookup.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark(Description = "ConcurrentTypeMap, GetOrAdd")]
    public int ConcurrentTypeMapAtomicLookup() => threadSafeMap.GetOrAdd<object>(42, out _);

    [Benchmark(Description = "ConcurrentDictionary, GetOrAdd")]
    public int ConcurrentDictionaryAtomicLookup() => concurrentLookup.GetOrAdd<object>(42);
}