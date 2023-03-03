using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Numerics;
using System.Text;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO
{
    using Buffers;
    using Text;

    [ExcludeFromCodeCoverage]
    public sealed class AsyncBinaryReaderWriterTests : Test
    {
        public interface IAsyncBinaryReaderWriterSource : IAsyncDisposable
        {
            IAsyncBinaryWriter CreateWriter();

            IAsyncBinaryReader CreateReader();
        }

        private sealed class DefaultSource : IAsyncBinaryReaderWriterSource
        {
            private sealed class DefaultAsyncBinaryReader : IAsyncBinaryReader
            {
                private readonly IAsyncBinaryReader reader;

                internal DefaultAsyncBinaryReader(Stream stream, Memory<byte> buffer)
                    => reader = IAsyncBinaryReader.Create(stream, buffer);

                ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
                    => reader.ReadAsync(output, token);

                ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte> allocator, CancellationToken token)
                    => reader.ReadAsync(lengthFormat, allocator, token);

                Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
                    => reader.CopyToAsync(output, token);

                Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
                    => reader.CopyToAsync(output, token);

                Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
                    => reader.CopyToAsync(consumer, arg, token);

                Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
                    => reader.CopyToAsync(consumer, token);
            }

            private sealed class DefaultAsyncBinaryWriter : IAsyncBinaryWriter
            {
                private readonly IAsyncBinaryWriter writer;

                internal DefaultAsyncBinaryWriter(Stream stream, Memory<byte> buffer)
                    => writer = IAsyncBinaryWriter.Create(stream, buffer);

                ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
                    => writer.WriteAsync(input, lengthFormat, token);
            }

            private readonly MemoryStream stream = new();
            private readonly byte[] buffer = new byte[512];

            public IAsyncBinaryReader CreateReader()
            {
                stream.Position = 0;
                return new DefaultAsyncBinaryReader(stream, buffer);
            }

            public IAsyncBinaryWriter CreateWriter() => new DefaultAsyncBinaryWriter(stream, buffer);

            public ValueTask DisposeAsync() => stream.DisposeAsync();
        }

        private sealed class BufferSource : IAsyncBinaryReaderWriterSource
        {
            private readonly PooledBufferWriter<byte> buffer = new();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(buffer);

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(buffer.WrittenMemory);

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                buffer.Dispose();
                return new ValueTask();
            }
        }

        private sealed class StreamSource : IAsyncBinaryReaderWriterSource
        {
            private readonly MemoryStream stream = new(1024);
            private readonly byte[] buffer = new byte[512];

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

            public IAsyncBinaryReader CreateReader()
            {
                stream.Position = 0L;
                return IAsyncBinaryReader.Create(stream, buffer);
            }

            public ValueTask DisposeAsync() => stream.DisposeAsync();
        }

        private sealed class PipeSource : IAsyncBinaryReaderWriterSource
        {
            private readonly Pipe pipe = new();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer);

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

            internal ValueTask CompleteWriterAsync() => pipe.Writer.CompleteAsync();

            public async ValueTask DisposeAsync()
            {
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
                pipe.Reset();
            }
        }

        private sealed class PipeSourceWithSettings : IAsyncBinaryReaderWriterSource
        {
            private readonly Pipe pipe = new();

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(pipe.Writer, 1024, 128);

            public IAsyncBinaryReader CreateReader() => IAsyncBinaryReader.Create(pipe.Reader);

            public async ValueTask DisposeAsync()
            {
                await pipe.Writer.CompleteAsync();
                await pipe.Reader.CompleteAsync();
                pipe.Reset();
            }
        }

        private sealed class ReadOnlySequenceSource : IAsyncBinaryReaderWriterSource
        {
            private readonly MemoryStream stream = new(1024);
            private readonly byte[] buffer = new byte[512];

            public IAsyncBinaryWriter CreateWriter() => IAsyncBinaryWriter.Create(stream, buffer);

            public IAsyncBinaryReader CreateReader()
            {
                stream.Position = 0L;
                var sequence = ToReadOnlySequence<byte>(stream.ToArray(), 3);
                return IAsyncBinaryReader.Create(sequence);
            }

            public ValueTask DisposeAsync() => stream.DisposeAsync();
        }

        private sealed class FileSource : Disposable, IAsyncBinaryReaderWriterSource, IFlushable
        {
            private readonly SafeFileHandle handle;
            private readonly FileWriter writer;
            private readonly FileReader reader;

            public FileSource(int bufferSize)
            {
                var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous);
                writer = new(handle, bufferSize: bufferSize);
                reader = new(handle, bufferSize: bufferSize);
            }

            public IAsyncBinaryWriter CreateWriter() => writer;

            public IAsyncBinaryReader CreateReader() => reader;

            public Task FlushAsync(CancellationToken token) => writer.WriteAsync(token).AsTask();

            void IFlushable.Flush()
            {
                using (var task = FlushAsync(CancellationToken.None))
                    task.Wait(DefaultTimeout);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    writer.Dispose();
                    reader.Dispose();
                    handle.Dispose();
                }

                base.Dispose(disposing);
            }

            public new ValueTask DisposeAsync() => base.DisposeAsync();
        }

        public static IEnumerable<object[]> GetDataForPrimitives()
        {
            yield return new object[] { new FileSource(128), true, Encoding.UTF8 };
            yield return new object[] { new FileSource(128), false, Encoding.UTF8 };
            yield return new object[] { new FileSource(1024), true, Encoding.UTF8 };
            yield return new object[] { new FileSource(1024), false, Encoding.UTF8 };
            yield return new object[] { new StreamSource(), true, Encoding.UTF8 };
            yield return new object[] { new StreamSource(), false, Encoding.UTF8 };
            yield return new object[] { new PipeSource(), true, Encoding.UTF8 };
            yield return new object[] { new PipeSource(), false, Encoding.UTF8 };
            yield return new object[] { new BufferSource(), true, Encoding.UTF8 };
            yield return new object[] { new BufferSource(), false, Encoding.UTF8 };
            yield return new object[] { new PipeSourceWithSettings(), true, Encoding.UTF8 };
            yield return new object[] { new PipeSourceWithSettings(), false, Encoding.UTF8 };
            yield return new object[] { new ReadOnlySequenceSource(), true, Encoding.UTF8 };
            yield return new object[] { new ReadOnlySequenceSource(), false, Encoding.UTF8 };
            yield return new object[] { new DefaultSource(), true, Encoding.UTF8 };
            yield return new object[] { new DefaultSource(), false, Encoding.UTF8 };

            yield return new object[] { new FileSource(128), true, Encoding.Unicode };
            yield return new object[] { new FileSource(128), false, Encoding.Unicode };
            yield return new object[] { new FileSource(1024), true, Encoding.Unicode };
            yield return new object[] { new FileSource(1024), false, Encoding.Unicode };
            yield return new object[] { new StreamSource(), true, Encoding.Unicode };
            yield return new object[] { new StreamSource(), false, Encoding.Unicode };
            yield return new object[] { new PipeSource(), true, Encoding.Unicode };
            yield return new object[] { new PipeSource(), false, Encoding.Unicode };
            yield return new object[] { new BufferSource(), true, Encoding.Unicode };
            yield return new object[] { new BufferSource(), false, Encoding.Unicode };
            yield return new object[] { new PipeSourceWithSettings(), true, Encoding.Unicode };
            yield return new object[] { new PipeSourceWithSettings(), false, Encoding.Unicode };
            yield return new object[] { new ReadOnlySequenceSource(), true, Encoding.Unicode };
            yield return new object[] { new ReadOnlySequenceSource(), false, Encoding.Unicode };
            yield return new object[] { new DefaultSource(), true, Encoding.Unicode };
            yield return new object[] { new DefaultSource(), false, Encoding.Unicode };
        }

        [Theory]
        [MemberData(nameof(GetDataForPrimitives))]
        public static async Task WriteReadPrimitivesAsync(IAsyncBinaryReaderWriterSource source, bool littleEndian, Encoding encoding)
        {
            await using (source)
            {
                const byte value8 = 254;
                const short value16 = 42;
                const int value32 = int.MaxValue;
                const long value64 = long.MaxValue;
                const decimal valueM = 42M;
                const float valueF = 56.6F;
                const double valueD = 67.7D;
                var valueG = Guid.NewGuid();
                var valueDT = DateTime.Now;
                var valueDTO = DateTimeOffset.Now;
                var valueT = TimeSpan.FromMilliseconds(1024);
                var blob = RandomBytes(128);
                var bi = new BigInteger(RandomBytes(64));
                var memberId = new Net.Cluster.ClusterMemberId(Random.Shared);

                var writer = source.CreateWriter();
                await writer.WriteInt16Async(value16, littleEndian);
                await writer.WriteInt32Async(value32, littleEndian);
                await writer.WriteInt64Async(value64, littleEndian);
                await writer.WriteAsync(valueM);
                var encodingContext = new EncodingContext(encoding, true);
                await writer.WriteFormattableAsync(value8, LengthFormat.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(value16, LengthFormat.Compressed, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(value32, LengthFormat.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(value64, LengthFormat.PlainBigEndian, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueM, LengthFormat.PlainLittleEndian, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueF, LengthFormat.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueD, LengthFormat.Plain, encodingContext, provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueG, LengthFormat.Plain, encodingContext);
                await writer.WriteFormattableAsync(valueG, LengthFormat.Plain, encodingContext, "X");
                await writer.WriteFormattableAsync(valueDT, LengthFormat.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueDTO, LengthFormat.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueDT, LengthFormat.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueDTO, LengthFormat.Plain, encodingContext, format: "O", provider: InvariantCulture);
                await writer.WriteFormattableAsync(valueT, LengthFormat.Plain, encodingContext);
                await writer.WriteFormattableAsync(valueT, LengthFormat.Plain, encodingContext, "G", InvariantCulture);
                await writer.WriteAsync(blob, LengthFormat.Compressed);
                await writer.WriteFormattableAsync(bi, LengthFormat.Compressed, encodingContext, provider: InvariantCulture);
                await writer.WriteBigIntegerAsync(bi, littleEndian, LengthFormat.Compressed);
                await writer.WriteFormattableAsync(memberId);

                if (source is IFlushable flushable)
                    await flushable.FlushAsync();

                var reader = source.CreateReader();
                Equal(value16, await reader.ReadInt16Async(littleEndian));
                Equal(value32, await reader.ReadInt32Async(littleEndian));
                Equal(value64, await reader.ReadInt64Async(littleEndian));
                Equal(valueM, await reader.ReadAsync<decimal>());
                var decodingContext = new DecodingContext(encoding, true);
                Equal(value8, await reader.ParseAsync<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(value16, await reader.ParseAsync<short>(static (c, p) => short.Parse(c, provider: p), LengthFormat.Compressed, decodingContext, provider: InvariantCulture));
                Equal(value32, await reader.ParseAsync<int>(static (c, p) => int.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(value64, await reader.ParseAsync<long>(static (c, p) => long.Parse(c, provider: p), LengthFormat.PlainBigEndian, decodingContext, provider: InvariantCulture));
                Equal(valueM, await reader.ParseAsync<decimal>(static (c, p) => decimal.Parse(c, provider: p), LengthFormat.PlainLittleEndian, decodingContext, provider: InvariantCulture));
                Equal(valueF, await reader.ParseAsync<float>(static (c, p) => float.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueD, await reader.ParseAsync<double>(static (c, p) => double.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueG, await reader.ParseAsync<Guid>(static (c, p) => Guid.Parse(c), LengthFormat.Plain, decodingContext));
                Equal(valueG, await reader.ParseAsync<Guid>(static (c, p) => Guid.ParseExact(c, "X"), LengthFormat.Plain, decodingContext));
                Equal(valueDT, await reader.ParseAsync<DateTime>(static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueDTO, await reader.ParseAsync<DateTimeOffset>(static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueDT, await reader.ParseAsync<DateTime>(static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueDTO, await reader.ParseAsync<DateTimeOffset>(static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
                Equal(valueT, await reader.ParseAsync<TimeSpan>(TimeSpan.Parse, LengthFormat.Plain, decodingContext, InvariantCulture));
                Equal(valueT, await reader.ParseAsync<TimeSpan>(static (c, p) => TimeSpan.ParseExact(c, "G", p), LengthFormat.Plain, decodingContext, InvariantCulture));
                using var decodedBlob = await reader.ReadAsync(LengthFormat.Compressed);
                Equal(blob, decodedBlob.Memory.ToArray());
                Equal(bi, await reader.ParseAsync<BigInteger>(static (c, p) => BigInteger.Parse(c, provider: p), LengthFormat.Compressed, decodingContext, provider: InvariantCulture));
                Equal(bi, await reader.ReadBigIntegerAsync(LengthFormat.Compressed, littleEndian));
                Equal(memberId, await reader.ParseAsync<Net.Cluster.ClusterMemberId>());
            }
        }

        public static IEnumerable<object[]> GetDataForStringEncoding()
        {
            yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.Compressed };
            yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.Plain };
            yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.PlainBigEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF8, LengthFormat.PlainLittleEndian };
            yield return new object[] { new StreamSource(), Encoding.UTF8, null };

            yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.Compressed };
            yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.Plain };
            yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.PlainBigEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF8, LengthFormat.PlainLittleEndian };
            yield return new object[] { new BufferSource(), Encoding.UTF8, null };

            yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.Compressed };
            yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.Plain };
            yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.PlainBigEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF8, LengthFormat.PlainLittleEndian };
            yield return new object[] { new PipeSource(), Encoding.UTF8, null };

            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, LengthFormat.Compressed };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, LengthFormat.Plain };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, LengthFormat.PlainBigEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, LengthFormat.PlainLittleEndian };
            yield return new object[] { new PipeSourceWithSettings(), Encoding.UTF8, null };

            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.Compressed };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.Plain };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.PlainBigEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, LengthFormat.PlainLittleEndian };
            yield return new object[] { new ReadOnlySequenceSource(), Encoding.UTF8, null };
        }

        [Theory]
        [MemberData(nameof(GetDataForStringEncoding))]
        public static async Task WriteReadStringAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding, LengthFormat? lengthFormat)
        {
            await using (source)
            {
                const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
                var writer = source.CreateWriter();
                await writer.WriteStringAsync(value.AsMemory(), encoding, lengthFormat);

                var reader = source.CreateReader();
                var result = await (lengthFormat is null ?
                    reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                    reader.ReadStringAsync(lengthFormat.GetValueOrDefault(), encoding));
                Equal(value, result);
            }
        }

        [Theory]
        [MemberData(nameof(GetDataForStringEncoding))]
        public static async Task WriteReadStringBufferAsync(IAsyncBinaryReaderWriterSource source, Encoding encoding, LengthFormat? lengthFormat)
        {
            await using (source)
            {
                const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
                var writer = source.CreateWriter();
                await writer.WriteStringAsync(value.AsMemory(), encoding, lengthFormat);

                var reader = source.CreateReader();
                using var result = await (lengthFormat is null ?
                    reader.ReadStringAsync(encoding.GetByteCount(value), encoding, null) :
                    reader.ReadStringAsync(lengthFormat.GetValueOrDefault(), encoding, null));
                Equal(value, new string(result.Span));
            }
        }

        public static IEnumerable<object[]> GetSources()
        {
            yield return new object[] { new StreamSource() };
            yield return new object[] { new PipeSource() };
            yield return new object[] { new BufferSource() };
            yield return new object[] { new DefaultSource() };
        }

        [Theory]
        [MemberData(nameof(GetSources))]
        public static async Task CopyFromStreamToStream(IAsyncBinaryReaderWriterSource source)
        {
            await using (source)
            {
                var content = new byte[] { 1, 2, 3 };
                using var sourceStream = new MemoryStream(content, false);
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(sourceStream);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                using var destStream = new MemoryStream(256);
                await reader.CopyToAsync(destStream);
                Equal(content, destStream.ToArray());
            }
        }

        private sealed class MemorySource
        {
            internal readonly byte[] Content;
            private bool state;

            internal MemorySource(byte[] content) => Content = content;

            internal ReadOnlyMemory<byte> ReadContent()
            {
                if (state)
                    return default;
                state = true;
                return Content;
            }

            internal static ValueTask<ReadOnlyMemory<byte>> SupplyContent(MemorySource supplier, CancellationToken token)
                => new(supplier.ReadContent());
        }

        [Theory]
        [MemberData(nameof(GetSources))]
        public static async Task CopyUsingSpanAction(IAsyncBinaryReaderWriterSource source)
        {
            await using (source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3 });
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync((span, writer) => writer.Write(span), consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }
        }

        [Theory]
        [MemberData(nameof(GetSources))]
        public static async Task CopyToBuffer(IAsyncBinaryReaderWriterSource source)
        {
            await using (source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3 });
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync(consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }
        }

        [Theory]
        [MemberData(nameof(GetSources))]
        public static async Task CopyUsingAsyncFunc(IAsyncBinaryReaderWriterSource source)
        {
            await using (source)
            {
                var supplier = new MemorySource(new byte[] { 1, 2, 3 });
                var writer = source.CreateWriter();
                await writer.CopyFromAsync(MemorySource.SupplyContent, supplier);
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                var consumer = new ArrayBufferWriter<byte>();
                await reader.CopyToAsync(ConsumeMemory, consumer);
                Equal(supplier.Content, consumer.WrittenMemory.ToArray());
            }

            static ValueTask ConsumeMemory(IBufferWriter<byte> writer, ReadOnlyMemory<byte> block, CancellationToken token)
            {
                writer.Write(block.Span);
                return new ValueTask();
            }
        }

        [Fact]
        public static async Task ReadFromEmptyReader()
        {
            await using var ms = new MemoryStream();
            var reader = IAsyncBinaryReader.Empty;
            await reader.CopyToAsync(ms);
            Equal(0, ms.Length);

            var writer = new ArrayBufferWriter<byte>();
            await reader.CopyToAsync(writer);
            Equal(0, writer.WrittenCount);

            var context = new DecodingContext();
            await ThrowsAsync<EndOfStreamException>(reader.ParseAsync<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadStringAsync(LengthFormat.Plain, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadBigIntegerAsync(10, true).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadStringAsync(10, context).AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadAsync<decimal>().AsTask);
            await ThrowsAsync<EndOfStreamException>(reader.ReadAsync(new byte[1]).AsTask);
        }

        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        public static async Task WriteViaBufferWriter(int length)
        {
            var bytes = RandomBytes(length);
            using var stream = new MemoryStream();
            var writer = IAsyncBinaryWriter.Create(stream, new byte[64]);
            await writer.WriteAsync((array, output) => output.Write(array), bytes);
            Equal(bytes, stream.ToArray());
        }

        [Theory]
        [MemberData(nameof(GetSources))]
        public static async Task SkipContent(IAsyncBinaryReaderWriterSource source)
        {
            await using (source)
            {
                var writer = source.CreateWriter();
                await writer.WriteAsync(new byte[] { 1, 2, 3 });
                await writer.WriteAsync(new byte[] { 4, 5, 6 });
                if (source is PipeSource pipe)
                    await pipe.CompleteWriterAsync();

                var reader = source.CreateReader();
                Memory<byte> buffer = new byte[3];
                await reader.SkipAsync(3);
                await reader.ReadAsync(buffer);
                Equal(4, buffer.Span[0]);
                Equal(5, buffer.Span[1]);
                Equal(6, buffer.Span[2]);
                await ThrowsAsync<EndOfStreamException>(() => reader.SkipAsync(3).AsTask());
            }
        }

        [Fact]
        public static void EmptyReader()
        {
            var reader = IAsyncBinaryReader.Empty;
            True(reader.TryGetSequence(out var sequence));
            True(sequence.IsEmpty);
            True(reader.TryGetRemainingBytesCount(out var remainingCount));
            Equal(0L, remainingCount);
            True(reader.SkipAsync(0).IsCompletedSuccessfully);
            True(reader.CopyToAsync(Stream.Null).IsCompletedSuccessfully);
            True(reader.CopyToAsync(new ArrayBufferWriter<byte>()).IsCompletedSuccessfully);
            True(reader.CopyToAsync(static (sp, arg) => { }, 10).IsCompletedSuccessfully);
            True(reader.CopyToAsync(new StreamConsumer(Stream.Null)).IsCompletedSuccessfully);
            Throws<EndOfStreamException>(() => reader.ReadAsync<int>().Result);
            Throws<EndOfStreamException>(() => reader.ReadAsync(new byte[2].AsMemory()).GetAwaiter().GetResult());
            Throws<EndOfStreamException>(() => reader.SkipAsync(10).GetAwaiter().GetResult());
            True(reader.ReadAsync(Memory<byte>.Empty).IsCompletedSuccessfully);
            Throws<EndOfStreamException>(() => reader.ReadInt16Async(true).Result);
            Throws<EndOfStreamException>(() => reader.ReadInt32Async(true).Result);
            Throws<EndOfStreamException>(() => reader.ReadInt64Async(true).Result);

            Throws<EndOfStreamException>(() => reader.ReadStringAsync(10, default).Result);
            Empty(reader.ReadStringAsync(0, default).Result);
            Throws<EndOfStreamException>(() => reader.ReadStringAsync(LengthFormat.Plain, default).Result);

            Throws<EndOfStreamException>(() => reader.ParseAsync<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, default).Result);
        }
    }
}