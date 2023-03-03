﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using FastMember;
using System;
using System.Reflection;

namespace DotNext.Reflection;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class PropertyGetterReflectionBenchmark
{
    private sealed class IndexOfCalculator
    {
        private readonly char character;
        private readonly int startIndex;
        private readonly string source;

        internal IndexOfCalculator(string source, char ch, int startIndex)
        {
            this.source = source;
            character = ch;
            this.startIndex = startIndex;
        }

        public int IndexOf => source.IndexOf(character, startIndex);
    }

    private static readonly IndexOfCalculator IndexOfCalc = new("Hello, world!", 'd', 0);
    private static readonly ObjectAccessor Accessor = ObjectAccessor.Create(IndexOfCalc);
    private static readonly MethodInfo ReflectedGetter = IndexOfCalc.GetType().GetProperty(nameof(IndexOfCalculator.IndexOf)).GetMethod;
    private static readonly MemberGetter<IndexOfCalculator, int> StaticallyReflected = Type<IndexOfCalculator>.Property<int>.RequireGetter(nameof(IndexOfCalculator.IndexOf));

    private static readonly Function<object, ValueTuple, object> UntypedReflected = ReflectedGetter.Unreflect<Function<object, ValueTuple, object>>();

    private static readonly DynamicInvoker DynamicAccessor = ReflectedGetter.Unreflect();

    [Benchmark(Description = "Direct call", Baseline = true)]
    public int NoReflection() => IndexOfCalc.IndexOf;

    [Benchmark(Description = "Reflection with DotNext using delegate type MemberGetter<IndexOfCalculator, int>")]
    public int UseFastTypedReflection() => StaticallyReflected(IndexOfCalc);

    [Benchmark(Description = "Reflection with DotNext using DynamicInvoker")]
    public object UseDynamicInvoker() => DynamicAccessor(IndexOfCalc, Array.Empty<object>());

    [Benchmark(Description = "Reflection with DotNext using delegate type Function<object, ValueTuple, object>")]
    public object UseFastUntypedReflection() => UntypedReflected(IndexOfCalc, new ValueTuple());

    [Benchmark(Description = "ObjectAccess class from FastMember library")]
    public object UseObjectAccessor() => Accessor[nameof(IndexOfCalculator.IndexOf)];

    [Benchmark(Description = ".NET reflection")]
    public object UseReflection() => ReflectedGetter.Invoke(IndexOfCalc, Array.Empty<object>());
}