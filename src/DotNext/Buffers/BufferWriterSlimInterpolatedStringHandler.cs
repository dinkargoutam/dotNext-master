using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Buffers;

/// <summary>
/// Represents handler of the interpolated string
/// that can be written to <see cref="IBufferWriter{T}"/> without temporary allocations.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public ref struct BufferWriterSlimInterpolatedStringHandler
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const char Whitespace = ' ';

    private readonly IFormatProvider? provider;
    private readonly BufferWriterSlim<char>.Ref buffer; // TODO: Replace with ref field in the next version of C#
    private int count;

    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="provider">Optional formatting provider.</param>
    public BufferWriterSlimInterpolatedStringHandler(int literalLength, int formattedCount, ref BufferWriterSlim<char> buffer, IFormatProvider? provider = null)
    {
        this.buffer = new(ref buffer);
        this.provider = provider;

        buffer.GetSpan(literalLength + formattedCount);
        count = 0;
    }

    /// <summary>
    /// Gets number of written characters.
    /// </summary>
    public readonly int WrittenCount => count;

    /// <summary>
    /// Writes the specified string to the handler.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public void AppendLiteral(string? value)
        => AppendFormatted(value.AsSpan());

    internal static int AppendFormatted<T>(scoped ref BufferWriterSlim<char> buffer, T value, string? format, IFormatProvider? provider)
    {
        int charsWritten;

        switch (value)
        {
            case IFormattable:
                if (value is ISpanFormattable)
                {
                    for (int bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                    {
                        var span = buffer.GetSpan(bufferSize);

                        // constrained call avoiding boxing for value types
                        if (((ISpanFormattable)value).TryFormat(span, out charsWritten, format, provider))
                        {
                            buffer.Advance(charsWritten);
                            break;
                        }
                    }
                }
                else
                {
                    // constrained call avoiding boxing for value types
                    charsWritten = Write(ref buffer, ((IFormattable)value).ToString(format, provider));
                }

                break;
            case not null:
                charsWritten = Write(ref buffer, value.ToString());
                break;
            default:
                charsWritten = 0;
                break;
        }

        return charsWritten;

        static int Write(scoped ref BufferWriterSlim<char> buffer, scoped ReadOnlySpan<char> chars)
        {
            buffer.Write(chars);
            return chars.Length;
        }
    }

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format = null)
        => count += AppendFormatted(ref buffer.Value, value, format, provider);

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
    {
        buffer.Value.Write(value);
        count += value.Length;
    }

    private void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment, bool leftAlign)
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            AppendFormatted(value);
            return;
        }

        var span = buffer.Value.GetSpan(alignment);
        if (leftAlign)
        {
            span.Slice(value.Length, padding).Fill(Whitespace);
            value.CopyTo(span);
        }
        else
        {
            span.Slice(0, padding).Fill(Whitespace);
            value.CopyTo(span.Slice(padding));
        }

        buffer.Value.Advance(alignment);
        count += alignment;
    }

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        AppendFormatted(value, alignment, leftAlign);
    }

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format = null)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        switch (value)
        {
            case IFormattable:
                if (value is ISpanFormattable)
                {
                    for (int bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                    {
                        var span = buffer.Value.GetSpan(bufferSize);
                        if (((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider))
                        {
                            var padding = alignment - charsWritten;

                            if (padding <= 0)
                            {
                                alignment = charsWritten;
                            }
                            else if (leftAlign)
                            {
                                span.Slice(charsWritten, padding).Fill(Whitespace);
                            }
                            else
                            {
                                span.Slice(0, charsWritten).CopyTo(span.Slice(padding));
                                span.Slice(0, padding).Fill(Whitespace);
                            }

                            buffer.Value.Advance(alignment);
                            count += alignment;
                            break;
                        }
                    }
                }
                else
                {
                    AppendFormatted(((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign);
                }

                break;
            case not null:
                AppendFormatted(value.ToString().AsSpan(), alignment, leftAlign);
                break;
        }
    }

    /// <summary>
    /// Renders interpolated string.
    /// </summary>
    /// <returns>The rendered string.</returns>
    public readonly override string ToString() => buffer.Value.ToString();
}