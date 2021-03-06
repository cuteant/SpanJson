﻿namespace SpanJson
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using SpanJson.Internal;

    partial struct JsonWriter<TSymbol>
    {
        /// <summary>The value should already be properly escaped.</summary>
        public void WriteUtf8Name(in JsonEncodedText value)
        {
            ref var pos = ref _pos;
            var utf8Text = value.EncodedUtf8Bytes;
            EnsureUnsafe(pos, utf8Text.Length + 3);

            ref byte pinnableAddr = ref Utf8PinnableAddress;

            WriteUtf8DoubleQuote(ref pinnableAddr, ref pos);
            UnsafeMemory.WriteRaw(ref pinnableAddr, ref MemoryMarshal.GetReference(utf8Text), utf8Text.Length, ref _pos);
            WriteUtf8DoubleQuote(ref pinnableAddr, ref pos);

            Unsafe.AddByteOffset(ref pinnableAddr, (IntPtr)pos++) = JsonUtf8Constant.NameSeparator;
        }

        public void WriteUtf8Name(string value)
        {
            WriteUtf8StringEscapeValue(value.AsSpan(), true);
        }

        public void WriteUtf8Name(in ReadOnlySpan<char> value)
        {
            WriteUtf8StringEscapeValue(value, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8Name(string value, JsonEscapeHandling escapeHandling)
        {
            WriteUtf8Name(value.AsSpan(), escapeHandling);
        }

        public void WriteUtf8Name(in ReadOnlySpan<char> value, JsonEscapeHandling escapeHandling)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
                    WriteUtf8StringEscapeNonAsciiValue(value, true);
                    break;

                case JsonEscapeHandling.EscapeHtml:
                    WriteUtf8StringEscapeHtmlValue(value, true);
                    break;

                case JsonEscapeHandling.Default:
                default:
                    WriteUtf8StringEscapeValue(value, true);
                    break;
            }
        }

        /// <summary>The value should already be properly escaped.</summary>
        public void WriteUtf8VerbatimNameSpan(in ReadOnlySpan<byte> value)
        {
            ref var pos = ref _pos;
            EnsureUnsafe(pos, value.Length + 3);

            ref byte pinnableAddr = ref Utf8PinnableAddress;

            WriteUtf8DoubleQuote(ref pinnableAddr, ref pos);
            UnsafeMemory.WriteRaw(ref pinnableAddr, ref MemoryMarshal.GetReference(value), value.Length, ref pos);
            WriteUtf8DoubleQuote(ref pinnableAddr, ref pos);
            Unsafe.AddByteOffset(ref pinnableAddr, (IntPtr)pos++) = JsonUtf8Constant.NameSeparator;
        }
    }
}
