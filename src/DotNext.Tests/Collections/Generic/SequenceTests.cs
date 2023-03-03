﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Generic
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class SequenceTests : Test
    {
        private sealed class Counter<T>
        {
            public int value;

            public void Accept(T item) => value += 1;
        }

        [Fact]
        public static void ForEachTest()
        {
            IList<int> list = new List<int> { 1, 10, 20 };
            var counter = new Counter<int>();
            list.ForEach(counter.Accept);
            Equal(3, counter.value);
            counter.value = 0;
            var array2 = new int[] { 1, 2, 10, 11, 15 };
            array2.ForEach(counter.Accept);
            Equal(5, counter.value);
        }

        [Fact]
        public static async Task ForEachTestAsync()
        {
            var list = new List<int> { 1, 10, 20 }.ToAsyncEnumerable();
            var counter = new Counter<int>();
            await list.ForEachAsync(counter.Accept);
            Equal(3, counter.value);
            counter.value = 0;

            list = new int[] { 1, 2, 10, 11, 15 }.ToAsyncEnumerable();
            await list.ForEachAsync(counter.Accept);
            Equal(5, counter.value);
        }

        [Fact]
        public static void FirstOrNullTest()
        {
            var array = new long[0];
            var element = array.FirstOrNull();
            Null(element);
            array = new long[] { 10, 20 };
            element = array.FirstOrNull();
            Equal(10, element);
        }

        [Fact]
        public static async Task FirstOrNullTestAsync()
        {
            var array = new long[0].ToAsyncEnumerable();
            var element = await array.FirstOrNullAsync();
            Null(element);
            array = new long[] { 10, 20 }.ToAsyncEnumerable();
            element = await array.FirstOrNullAsync();
            Equal(10, element);
        }

        [Fact]
        public static void ElementAtIndex()
        {
            var list = new LinkedList<long>();
            list.AddLast(10);
            list.AddLast(40);
            list.AddLast(100);
            list.ElementAt(2, out var element);
            Equal(100, element);
            list.ElementAt(0, out element);
            Equal(10, element);
        }

        [Fact]
        public static async Task ElementAtIndexAsync()
        {
            var list = new LinkedList<long>();
            list.AddLast(10);
            list.AddLast(40);
            list.AddLast(100);

            var asyncList = list.ToAsyncEnumerable();
            Equal(100, await asyncList.ElementAtAsync(2));
            Equal(10, await asyncList.ElementAtAsync(0));
        }

        [Fact]
        public static void SkipNullsTest()
        {
            var list = new LinkedList<string>();
            list.AddLast("a");
            list.AddLast(default(string));
            list.AddLast("b");
            list.AddLast(default(string));
            Equal(4, list.Count);
            var array = list.SkipNulls().ToArray();
            Equal(2, array.Length);
            True(Array.Exists(array, "a".Equals));
            True(Array.Exists(array, "b".Equals));
        }

        [Fact]
        public static async Task SkipNullsTestAsync()
        {
            var list = new LinkedList<string>();
            list.AddLast("a");
            list.AddLast(default(string));
            list.AddLast("b");
            list.AddLast(default(string));
            Equal(4, list.Count);

            var array = await list.ToAsyncEnumerable().SkipNulls().ToArrayAsync();
            Equal(2, array.Length);
            True(Array.Exists(array, "a".Equals));
            True(Array.Exists(array, "b".Equals));
        }

        [Fact]
        public static void ToStringTest()
        {
            var array = new int[] { 10, 20, 30 };
            var str = array.ToString(":");
            Equal("10:20:30", str);
        }

        [Fact]
        public static void Prepend()
        {
            IEnumerable<string> items = new[] { "One", "Two" };
            items = items.Prepend("Zero");
            NotEmpty(items);
            Equal(3, items.Count());
            Equal("Zero", items.First());
            Equal("Two", items.Last());
        }

        [Fact]
        public static void Append()
        {
            IEnumerable<string> items = new[] { "One", "Two" };
            items = items.Append("Three", "Four");
            NotEmpty(items);
            Equal(4, items.Count());
            Equal("One", items.First());
            Equal("Four", items.Last());
        }

        [Fact]
        public static void Skip()
        {
            var range = Enumerable.Range(0, 10);
            using var enumerator = range.GetEnumerator();
            True(enumerator.Skip(8));
            True(enumerator.MoveNext());
            Equal(8, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(9, enumerator.Current);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static async Task SkipAsync()
        {
            var range = Enumerable.Range(0, 10);
            await using var enumerator = range.GetAsyncEnumerator();
            True(await enumerator.SkipAsync(8));
            True(await enumerator.MoveNextAsync());
            Equal(8, enumerator.Current);
            True(await enumerator.MoveNextAsync());
            Equal(9, enumerator.Current);
            False(await enumerator.MoveNextAsync());
        }

        [Fact]
        public static void SkipValueEnumerator()
        {
            var list = new List<long> { 10L, 20L, 30L };
            var enumerator = list.GetEnumerator();
            True(enumerator.Skip<List<long>.Enumerator, long>(2));
            True(enumerator.MoveNext());
            Equal(30L, enumerator.Current);
            enumerator.Dispose();
        }

        [Fact]
        public static void LimitedSequence()
        {
            var range = Enumerable.Range(0, 10);
            using var enumerator = range.GetEnumerator().Limit(3);
            True(enumerator.MoveNext());
            Equal(0, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(1, enumerator.Current);
            True(enumerator.MoveNext());
            Equal(2, enumerator.Current);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static void Iteration()
        {
            IEnumerable<int> collection = Array.Empty<int>();
            Null(collection.FirstOrNull());
            Equal(Optional<int>.None, collection.FirstOrNone());
            Equal(Optional<int>.None, collection.FirstOrNone(Predicate.True<int>()));

            collection = new int[] { 42 };
            Equal(42, collection.FirstOrNull());
            Equal(42, collection.FirstOrNone());
            Equal(42, collection.FirstOrNone(Predicate.True<int>()));

            Equal('a', "ab".FirstOrNone());
            False(string.Empty.FirstOrNone().HasValue);
            Equal('a', "ab".FirstOrNone(Predicate.True<char>()));
            False(string.Empty.FirstOrNone(Predicate.True<char>()).HasValue);
        }

        [Fact]
        public static void Iteration2()
        {
            IEnumerable<int> collection = Array.Empty<int>();
            Null(collection.LastOrNull());
            Equal(Optional<int>.None, collection.LastOrNone());

            collection = new int[] { 42 };
            Equal(42, collection.LastOrNull());
            Equal(42, collection.LastOrNone());

            Equal('b', "ab".LastOrNone());
            False(string.Empty.LastOrNone().HasValue);
        }

        [Fact]
        public static async Task IterationAsync()
        {
            var collection = Array.Empty<int>().ToAsyncEnumerable();
            Null(await collection.FirstOrNullAsync());
            Equal(Optional<int>.None, await collection.FirstOrNoneAsync());
            Equal(Optional<int>.None, await collection.FirstOrNoneAsync(Predicate.True<int>()));
            collection = new int[] { 42 }.ToAsyncEnumerable();
            Equal(42, await collection.FirstOrNullAsync());
            Equal(42, await collection.FirstOrNoneAsync());
            Equal(42, await collection.FirstOrNoneAsync(Predicate.True<int>()));
        }

        [Fact]
        public static async Task Iteration2Async()
        {
            var collection = Array.Empty<int>().ToAsyncEnumerable();
            Null(await collection.LastOrNullAsync());
            Equal(Optional<int>.None, await collection.LastOrNoneAsync());
            collection = new int[] { 42 }.ToAsyncEnumerable();
            Equal(42, await collection.LastOrNullAsync());
            Equal(42, await collection.LastOrNoneAsync());
        }

        [Fact]
        public static async Task ConversionToAsyncEnumerable()
        {
            int index = 0;
            await foreach (var item in new int[] { 10, 20, 30 }.ToAsyncEnumerable())
            {
                switch (index++)
                {
                    case 0:
                        Equal(10, item);
                        break;
                    case 1:
                        Equal(20, item);
                        break;
                    case 2:
                        Equal(30, item);
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException();
                }
            }
        }

        [Fact]
        public static async Task ConversionToAsyncEnumerator()
        {
            await using var enumerator = new int[] { 10, 20, 30 }.GetAsyncEnumerator();
            for (int index = 0; await enumerator.MoveNextAsync(); index++)
            {
                switch (index)
                {
                    case 0:
                        Equal(10, enumerator.Current);
                        break;
                    case 1:
                        Equal(20, enumerator.Current);
                        break;
                    case 2:
                        Equal(30, enumerator.Current);
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException();
                }
            }
        }

        [Fact]
        public static async Task CanceledAsyncEnumerator()
        {
            await using var enumerator = new int[] { 10, 20, 30 }.GetAsyncEnumerator(new CancellationToken(true));
            await ThrowsAsync<TaskCanceledException>(enumerator.MoveNextAsync().AsTask);
        }

        [Fact]
        public static void GeneratorMethod()
        {
            int i = 0;
            Func<Optional<int>> generator = () => i < 3 ? i++ : Optional<int>.None;
            var list = new List<int>();
            foreach (var item in generator.ToEnumerable())
                list.Add(item);

            NotEmpty(list);
            Equal(3, list.Count);
            Equal(0, list[0]);
            Equal(1, list[1]);
            Equal(2, list[2]);

            list.Clear();
            foreach (var item in new Sequence.Generator<int>())
                list.Add(item);

            Empty(list);
        }

        [Fact]
        public static async Task AsyncGeneratorMethod()
        {
            int i = 0;
            Func<CancellationToken, ValueTask<Optional<int>>> generator = token => new ValueTask<Optional<int>>(i < 3 ? i++ : Optional<int>.None);
            var list = new List<int>();
            await foreach (var item in generator.ToAsyncEnumerable())
                list.Add(item);

            NotEmpty(list);
            Equal(3, list.Count);
            Equal(0, list[0]);
            Equal(1, list[1]);
            Equal(2, list[2]);

            list.Clear();
            await foreach (var item in new Sequence.AsyncGenerator<int>())
                list.Add(item);

            Empty(list);
        }

        [Fact]
        public static void EmptyConsumingEnumerable()
        {
            var enumerable = new Sequence.ConsumingEnumerable<int>();
            Empty(enumerable);
        }

        [Fact]
        public static void ConsumeQueue()
        {
            var queue = new ConcurrentQueue<int>();
            queue.Enqueue(42);
            queue.Enqueue(52);

            var consumer = queue.GetConsumer();
            Collection(consumer, static i => Equal(42, i), static i => Equal(52, i));
        }

        [Fact]
        public static void ConsumeStack()
        {
            var queue = new ConcurrentStack<int>();
            queue.Push(42);
            queue.Push(52);

            var consumer = queue.GetConsumer();
            Collection(consumer, static i => Equal(52, i), static i => Equal(42, i));
        }

        [Fact]
        public static void CopyArray()
        {
            using var copy = new int[] { 10, 20, 30 }.Copy();
            Equal(3, copy.Length);
            Equal(10, copy[0]);
            Equal(20, copy[1]);
            Equal(30, copy[2]);
        }

        [Fact]
        public static void CopyList()
        {
            using var copy = new List<int> { 10, 20, 30 }.Copy();
            Equal(3, copy.Length);
            Equal(10, copy[0]);
            Equal(20, copy[1]);
            Equal(30, copy[2]);
        }

        [Fact]
        public static void CopyLinkedList()
        {
            using var copy = new LinkedList<int>(new int[] { 10, 20, 30 }).Copy();
            Equal(3, copy.Length);
            Equal(10, copy[0]);
            Equal(20, copy[1]);
            Equal(30, copy[2]);
        }

        [Fact]
        public static void CopyEnumerable()
        {
            using var copy = GetElements().Copy();
            Equal(3, copy.Length);
            Equal(10, copy[0]);
            Equal(20, copy[1]);
            Equal(30, copy[2]);

            static IEnumerable<int> GetElements()
            {
                yield return 10;
                yield return 20;
                yield return 30;
            }
        }

        [Fact]
        public static void CopyEmptyCollection()
        {
            using var copy = Enumerable.Empty<int>().Copy();
            True(copy.IsEmpty);
        }

        [Fact]
        public static async Task CopyEmptCollectionAsync()
        {
            using var copy = await Enumerable.Empty<int>().ToAsyncEnumerable().CopyAsync();
            True(copy.IsEmpty);
        }

        [Fact]
        public static async Task CopyListAsync()
        {
            using var copy = await new List<int> { 10, 20, 30 }.ToAsyncEnumerable().CopyAsync(sizeHint: 4);
            Equal(3, copy.Length);
            Equal(10, copy[0]);
            Equal(20, copy[1]);
            Equal(30, copy[2]);
        }

        [Fact]
        public static void CopyString()
        {
            using var copy = "abcd".Copy();
            Equal("abcd", copy.Memory.ToString());
        }

        [Fact]
        public static void SingletonCollection()
        {
            var collection = Sequence.Singleton(42);
            NotEmpty(collection);
            Equal(42, collection.First());
        }

        [Fact]
        public static void EmptyMemoryEnumerator()
        {
            using var enumerator = Sequence.ToEnumerator(ReadOnlyMemory<int>.Empty);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static void ArrayMemoryEnumerator()
        {
            using var enumerator = Sequence.ToEnumerator(new ReadOnlyMemory<int>(new int[] { 1, 2, 3 }));

            True(enumerator.MoveNext());
            Equal(1, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(2, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(3, enumerator.Current);

            False(enumerator.MoveNext());
        }

        [Fact]
        public static void NativeMemoryEnumerator()
        {
            using var owner = UnmanagedMemoryAllocator.Allocate<int>(3);
            owner[(nint)0] = 10;
            owner[(nint)1] = 20;
            owner[(nint)2] = 30;

            using var enumerator = Sequence.ToEnumerator<int>(owner.Memory);

            True(enumerator.MoveNext());
            Equal(10, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(20, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(30, enumerator.Current);

            False(enumerator.MoveNext());
        }

        [Fact]
        public static void EmptySequenceEnumerator()
        {
            using var enumerator = Sequence.ToEnumerator(ReadOnlySequence<int>.Empty);
            False(enumerator.MoveNext());
        }

        [Fact]
        public static void ArraySequenceEnumerator()
        {
            using var enumerator = Sequence.ToEnumerator(new ReadOnlySequence<int>(new ReadOnlyMemory<int>(new int[] { 1, 2, 3 })));
            True(enumerator.MoveNext());
            Equal(1, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(2, enumerator.Current);

            True(enumerator.MoveNext());
            Equal(3, enumerator.Current);

            False(enumerator.MoveNext());
        }

        [Fact]
        public static void SequenceEnumerator()
        {
            var bytes = RandomBytes(64);
            using var enumerator = Sequence.ToEnumerator(ToReadOnlySequence<byte>(bytes, 32));

            var i = 0;
            while (enumerator.MoveNext())
            {
                Equal(bytes[i++], enumerator.Current);
            }
        }

        [Fact]
        public static async Task EmptyAsyncEnumerable()
        {
            var count = 0;

            await foreach (var item in Sequence.GetEmptyAsyncEnumerable<int>())
                count++;

            Equal(0, count);
        }
    }
}