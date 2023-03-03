namespace DotNext.Runtime
{
    public sealed class ReferenceTests : Test
    {
        private static long staticField;

        [Fact]
        public static void AllocateStorage()
        {
            var handle = Reference.Allocate<string>(string.Empty);
            True(handle.IsValid);
            Empty(handle.Target);

            handle.Target = "Hello, world!";
            Equal("Hello, world!", handle.Target);
        }

        [Fact]
        public static void ArrayElementAccess()
        {
            int[] array = { 10, 20, 30 };
            var handle = Reference.ArrayElement(array, 1);
            True(handle.IsValid);

            Equal(array[1], handle.Target);

            array[1] = 42;
            Equal(42, handle.Target);

            handle.Target = 43;
            Equal(43, array[1]);
        }

        [Fact]
        public static unsafe void StaticFieldAccess()
        {
            var handle = Reference.Create<long>(&GetStaticFieldRef);
            True(handle.IsValid);

            handle.Target = 42;
            Equal(42, staticField);
            Equal(42, handle.Target);

            static ref long GetStaticFieldRef() => ref staticField;
        }

        [Fact]
        public static void BoxedValueAccess()
        {
            var handle = Reference.Unbox<int>(42);
            Equal(42, handle.Target);
        }

        [Fact]
        public static unsafe void PointerAccess()
        {
            var value = 42;
            var handle = Reference.FromPointer<int>(&value);
            True(handle.IsValid);

            Equal(42, handle.Target);

            handle.Target = 43;
            Equal(43, value);
        }

        [Fact]
        public static void SpanAccess()
        {
            var handle = Reference.Allocate<int>(42);
            Equal(42, handle.Span[0]);
        }

        [Fact]
        public static void InvalidHandle()
        {
            var handle = default(Reference<int>);
            False(handle.IsValid);
            Null(handle.ToString());
            Throws<NullReferenceException>(() => handle.Target);
        }

        [Fact]
        public static unsafe void InvalidArrayElementRef()
        {
            Throws<ArgumentException>(static () => Reference.Create<string[], string>(Array.Empty<string>(), &GetElementRef));

            static ref string GetElementRef(string[] array) => ref array[0];
        }
    }
}