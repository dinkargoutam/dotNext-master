using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using static System.Globalization.CultureInfo;

namespace DotNext.IO
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class TextStreamTests : Test
    {
        public sealed class XmlSerializableType
        {
            private byte[] byteArray;
            private string[] stringArray;

            [XmlElement("Value")]
            public string Value { get; set; }

            [XmlArray("ByteItem")]
            public byte[] ByteArray
            {
                get => byteArray ?? Array.Empty<byte>();
                set => byteArray = value;
            }

            [XmlArray("StringItem")]
            public string[] StringArray
            {
                get => stringArray ?? Array.Empty<string>();
                set => stringArray = value;
            }
        }

        [Fact]
        public static void WriteTextToCharBuffer()
        {
            using var writer = new PooledArrayBufferWriter<char>();
            using var actual = writer.AsTextWriter();

            using TextWriter expected = new StringWriter(InvariantCulture);

            actual.Write("Hello, world!");
            expected.Write("Hello, world!");

            actual.Write("123".AsSpan());
            expected.Write("123".AsSpan());

            actual.Write(TimeSpan.Zero);
            expected.Write(TimeSpan.Zero);

            actual.Write(true);
            expected.Write(true);

            actual.Write('a');
            expected.Write('a');

            actual.Write(20);
            expected.Write(20);

            actual.Write(20U);
            expected.Write(20U);

            actual.Write(42L);
            expected.Write(42L);

            actual.Write(46UL);
            expected.Write(46UL);

            actual.Write(89M);
            expected.Write(89M);

            actual.Write(78.8F);
            expected.Write(78.8F);

            actual.Write(90.9D);
            expected.Write(90.9D);

            actual.WriteLine();
            expected.WriteLine();

            actual.Write(default(StringBuilder));
            expected.Write(default(StringBuilder));

            actual.Flush();
            Equal(expected.ToString(), writer.ToString());
            Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public static void EmptyLines()
        {
            var line = string.Concat(Environment.NewLine, "a", Environment.NewLine).AsMemory();
            using var reader = new ReadOnlySequence<char>(line).AsTextReader();
            Equal(string.Empty, reader.ReadLine());
            Equal("a", reader.ReadLine());
            Null(reader.ReadLine());
        }

        [Fact]
        public static void InvalidLineTermination()
        {
            var newLine = Environment.NewLine;
            var str = string.Concat("a", newLine[0].ToString());
            if (newLine.Length > 1)
            {
                using var reader = new ReadOnlySequence<char>(str.AsMemory()).AsTextReader();
                Equal(str, reader.ReadLine());
            }
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void InvalidLineTermination2(string encodingName, int bufferSize)
        {
            var newLine = Environment.NewLine;
            var str = string.Concat("a", newLine[0].ToString());
            if (newLine.Length > 1)
            {
                var enc = Encoding.GetEncoding(encodingName);
                using var reader = new ReadOnlySequence<byte>(enc.GetBytes(str).AsMemory()).AsTextReader(enc, bufferSize);
                Equal(str, reader.ReadLine());
            }
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void DecodingSparseBuffer(string encodingName, int bufferSize)
        {
            var enc = Encoding.GetEncoding(encodingName);
            var block = ToReadOnlySequence<byte>(enc.GetBytes("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!").AsMemory(), 1);
            using var reader = block.AsTextReader(enc, bufferSize);
            Equal("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!", reader.ReadToEnd());
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void DecodingSingleSegment(string encodingName, int bufferSize)
        {
            var enc = Encoding.GetEncoding(encodingName);
            var bytes = enc.GetBytes("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!").AsMemory();
            using var reader = new ReadOnlySequence<byte>(bytes).AsTextReader(enc, bufferSize);
            Equal("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!", reader.ReadLine());
        }

        [Theory]
        [InlineData("UTF-8", 16)]
        [InlineData("UTF-16BE", 16)]
        [InlineData("UTF-16LE", 16)]
        [InlineData("UTF-32BE", 16)]
        [InlineData("UTF-32LE", 16)]
        public static void EncodingWriterDecodingReader(string encodingName, int bufferSize)
        {
            using var buffer = new SparseBufferWriter<byte>(32, SparseBufferGrowth.Linear);
            var enc = Encoding.GetEncoding(encodingName);

            // write data
            using (var writer = buffer.AsTextWriter(enc, InvariantCulture))
            {
                writer.WriteLine("Привет, мир!");
                writer.WriteLine("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!");
                writer.WriteLine('c');
                writer.WriteLine(decimal.MaxValue);
                writer.WriteLine(0D);
                writer.WriteLine(1F);
                writer.WriteLine(42);
                writer.WriteLine(43U);
                writer.WriteLine(44L);
                writer.WriteLine(45UL);
                writer.WriteLine(true);
            }

            // decode data
            using (var reader = buffer.ToReadOnlySequence().AsTextReader(enc, bufferSize))
            {
                Equal('П', reader.Peek());
                Equal("Привет, мир!", reader.ReadLine());
                Equal("Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!", reader.ReadLine());
                Equal('c', reader.Read());
                var newLine = Environment.NewLine;
                for (var i = 0; i < newLine.Length; i++)
                    Equal(newLine[i], reader.Read());
                Equal(decimal.MaxValue.ToString(InvariantCulture), reader.ReadLine());
                Equal(0D.ToString(InvariantCulture), reader.ReadLine());
                Equal(1F.ToString(InvariantCulture), reader.ReadLine());
                Equal(42.ToString(InvariantCulture), reader.ReadLine());
                Equal(43U.ToString(InvariantCulture), reader.ReadLine());
                Equal(44L.ToString(InvariantCulture), reader.ReadLine());
                Equal(45UL.ToString(InvariantCulture), reader.ReadLine());
                Equal(bool.TrueString, reader.ReadLine());
                Null(reader.ReadLine());
                Equal(string.Empty, reader.ReadToEnd());
                Equal(-1, reader.Peek());
                Equal(-1, reader.Read());
            }
        }

        [Fact]
        public static async Task WriteTextAsync()
        {
            var writer = new ArrayBufferWriter<char>();
            using var actual = writer.AsTextWriter();

            using TextWriter expected = new StringWriter(InvariantCulture);

            await actual.WriteAsync("Hello, world!");
            await expected.WriteAsync("Hello, world!");

            await actual.WriteAsync("123".AsMemory());
            await expected.WriteAsync("123".AsMemory());

            await actual.WriteAsync('a');
            await expected.WriteAsync('a');

            await actual.WriteLineAsync();
            await expected.WriteLineAsync();

            await actual.FlushAsync();
            Equal(expected.ToString(), writer.BuildString());
            Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public static async Task WriteSequence()
        {
            var sequence = new[] { "abc".AsMemory(), "def".AsMemory(), "g".AsMemory() }.ToReadOnlySequence();
            await using var writer = new StringWriter();
            await writer.WriteAsync(sequence);
            Equal("abcdefg", writer.ToString());
        }

        [Theory]
        [InlineData("UTF-8", 128)]
        [InlineData("UTF-16BE", 128)]
        [InlineData("UTF-16LE", 128)]
        [InlineData("UTF-32BE", 128)]
        [InlineData("UTF-32LE", 128)]
        public static void StressTest(string encodingName, int bufferSize)
        {
            var enc = Encoding.GetEncoding(encodingName);
            var expected = new XmlSerializableType
            {
                Value = "Привет, мир!",
                StringArray = new[]
                {
                    "String1",
                    "Strin2"
                },
                ByteArray = RandomBytes(128),
            };

            using var buffer = new SparseBufferWriter<byte>(1024, SparseBufferGrowth.Linear);
            var serializer = new XmlSerializer(typeof(XmlSerializableType));

            using (var writer = buffer.AsTextWriter(enc, InvariantCulture))
            {
                serializer.Serialize(writer, expected);
            }

            XmlSerializableType actual;
            using (var reader = buffer.ToReadOnlySequence().AsTextReader(enc, bufferSize))
            {
                actual = (XmlSerializableType)serializer.Deserialize(reader);
            }

            Equal(expected.Value, actual.Value);
            Equal(expected.StringArray, actual.StringArray);
            Equal(expected.ByteArray, actual.ByteArray);
        }

        [Fact]
        public static async Task WriteInterpolatedString1Async()
        {
            using var writer = new StringWriter();
            int x = 10, y = 20;
            await writer.WriteAsync(null, $"{x} + {y} = {x + y}");
            Equal($"{x} + {y} = {x + y}", writer.ToString());
        }

        [Fact]
        public static async Task WriteInterpolatedString2Async()
        {
            using var writer = new StringWriter();
            int x = 10, y = 20;
            await writer.WriteLineAsync(null, $"{x} + {y} = {x + y}");
            Equal($"{x} + {y} = {x + y}{Environment.NewLine}", writer.ToString());
        }

        [Fact]
        public static void WriteInterpolatedString1()
        {
            using var writer = new StringWriter();
            int x = 10, y = 20;
            writer.Write(default(MemoryAllocator<char>), $"{x} + {y} = {x + y}");
            Equal($"{x} + {y} = {x + y}", writer.ToString());
        }

        [Fact]
        public static void WriteInterpolatedString2()
        {
            using var writer = new StringWriter();
            int x = 10, y = 20;
            writer.WriteLine(default(MemoryAllocator<char>), $"{x} + {y} = {x + y}");
            Equal($"{x} + {y} = {x + y}{Environment.NewLine}", writer.ToString());
        }
    }
}