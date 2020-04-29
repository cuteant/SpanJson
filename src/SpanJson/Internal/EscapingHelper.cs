// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Largely based on https://github.com/dotnet/corefx/blob/8135319caa7e457ed61053ca1418313b88057b51/src/System.Text.Json/src/System/Text/Json/Writer/JsonWriterHelper.Transcoding.cs#L12

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;

namespace SpanJson.Internal
{
    public static partial class EscapingHelper
    {
        // A simple lookup table for converting numbers to hex.
        private const string HexTableLower = "0123456789abcdef";
        private const string HexFormatString = "x4";
        private static readonly StandardFormat s_hexStandardFormat = new StandardFormat('x', 4);
        public const int LastAsciiCharacter = 0x7F;
        private const uint nLastAsciiCharacter = 0x7F;

        public static bool IsAsciiValue(byte value) => (uint)value <= nLastAsciiCharacter ? true : false;

        public static bool IsAsciiValue(char value) => (uint)value <= nLastAsciiCharacter ? true : false;

        #region -- GetUnescapedTextFromUtf8WithCache --

        static readonly AsymmetricKeyHashTable<string> s_utf8StringCache = new AsymmetricKeyHashTable<string>(StringReadOnlySpanByteAscymmetricEqualityComparer.Instance);

        /// <summary> <see cref="JsonReader{TSymbol}.ReadUtf8VerbatimNameSpan(out int)"/> or <see cref="JsonReader{TSymbol}.ReadUtf8VerbatimStringSpan(out int)"/> </summary>
        public static string GetUnescapedTextFromUtf8WithCache(in ReadOnlySpan<byte> escapedUtf8Source, int idx)
        {
            if ((uint)idx > JsonSharedConstant.TooBigOrNegative)
            {
                return TextEncodings.Utf8.GetStringWithCache(escapedUtf8Source);
            }
            else
            {
                if (escapedUtf8Source.IsEmpty) { return string.Empty; }

                if (s_utf8StringCache.TryGetValue(escapedUtf8Source, out var value)) { return value; }

                return GetUnescapedTextFromUtf8Slow(escapedUtf8Source, idx);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetUnescapedTextFromUtf8Slow(in ReadOnlySpan<byte> escapedUtf8Source, int idx)
        {
            var buffer = escapedUtf8Source.ToArray();
            var value = JsonReaderHelper.GetUnescapedString(escapedUtf8Source, idx);
            s_utf8StringCache.TryAdd(buffer, value);
            return value;
        }

        #endregion

        #region -- GetUnescapedTextFromUtf16WithCache --

        static readonly AsymmetricKeyHashTable<string> s_utf16StringCache = new AsymmetricKeyHashTable<string>(StringReadOnlySpanByteAscymmetricEqualityComparer.Instance);

        /// <summary> <see cref="JsonReader{TSymbol}.ReadUtf8VerbatimNameSpan(out int)"/> or <see cref="JsonReader{TSymbol}.ReadUtf8VerbatimStringSpan(out int)"/> </summary>
        public static string GetUnescapedTextFromUtf16WithCache(in ReadOnlySpan<char> escapedUtf16Source, int escapedCharSize)
        {
            if (escapedUtf16Source.IsEmpty) { return string.Empty; }

            var utf16Source = MemoryMarshal.AsBytes(escapedUtf16Source);
            if (0u >= (uint)escapedCharSize)
            {
                return TextEncodings.Utf16.GetStringWithCache(utf16Source);
            }
            else
            {
                if (s_utf16StringCache.TryGetValue(utf16Source, out var value)) { return value; }

                return GetUnescapedTextFromUtf8Slow(escapedUtf16Source, utf16Source, escapedCharSize);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetUnescapedTextFromUtf8Slow(in ReadOnlySpan<char> escapedUtf16Source, in ReadOnlySpan<byte> utf16Source, int escapedCharSize)
        {
            var buffer = utf16Source.ToArray();
            var value = JsonReader<char>.UnescapeUtf16(escapedUtf16Source, escapedCharSize);
            s_utf16StringCache.TryAdd(buffer, value);
            return value;
        }

        #endregion

        #region -- GetEncodedText --

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonEncodedText GetEncodedText(string text, JsonEscapeHandling escapeHandling, JavaScriptEncoder encoder = null)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
#if !NET451
                    return NonAscii.GetEncodedText(text, encoder);
#else
                    return NonAscii.GetEncodedText(text);
#endif
                case JsonEscapeHandling.EscapeHtml:
                    return Html.GetEncodedText(text);
                case JsonEscapeHandling.Default:
                default:
                    return Default.GetEncodedText(text);
            }
        }

        #endregion

        #region -- GetMaxEscapedLength --

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEscapedLength(int textLength, int firstIndexToEscape)
        {
            Debug.Assert(textLength > 0);
            Debug.Assert(firstIndexToEscape >= 0 && firstIndexToEscape < textLength);
            return firstIndexToEscape + JsonSharedConstant.MaxExpansionFactorWhileEscaping * (textLength - firstIndexToEscape);
        }

        #endregion

        #region -- NeedsEscaping --

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NeedsEscaping(byte utf8Value, JsonEscapeHandling escapeHandling)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
                    return NonAscii.NeedsEscaping(utf8Value);
                case JsonEscapeHandling.EscapeHtml:
                    return Html.NeedsEscaping(utf8Value);
                case JsonEscapeHandling.Default:
                default:
                    return Default.NeedsEscaping(utf8Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NeedsEscaping(char value, JsonEscapeHandling escapeHandling)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
                    return NonAscii.NeedsEscaping(value);
                case JsonEscapeHandling.EscapeHtml:
                    return Html.NeedsEscaping(value);
                case JsonEscapeHandling.Default:
                default:
                    return Default.NeedsEscaping(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NeedsEscaping(in ReadOnlySpan<byte> utf8Source, JsonEscapeHandling escapeHandling, JavaScriptEncoder encoder = null)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
                    return NonAscii.NeedsEscaping(utf8Source, encoder);
                case JsonEscapeHandling.EscapeHtml:
                    return Html.NeedsEscaping(utf8Source);
                case JsonEscapeHandling.Default:
                default:
                    return Default.NeedsEscaping(utf8Source);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NeedsEscaping(in ReadOnlySpan<char> utf16Source, JsonEscapeHandling escapeHandling, JavaScriptEncoder encoder = null)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
                    return NonAscii.NeedsEscaping(utf16Source, encoder);
                case JsonEscapeHandling.EscapeHtml:
                    return Html.NeedsEscaping(utf16Source);
                case JsonEscapeHandling.Default:
                default:
                    return Default.NeedsEscaping(utf16Source);
            }
        }

        #endregion

        #region -- EscapeString --

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EscapeString(in ReadOnlySpan<byte> utf8Source, Span<byte> destination, JsonEscapeHandling escapeHandling, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
#if !NET451
                    NonAscii.EscapeString(utf8Source, destination, indexOfFirstByteToEscape, encoder, out written);
#else
                    NonAscii.EscapeString(utf8Source, destination, indexOfFirstByteToEscape, out written);
#endif
                    break;
                case JsonEscapeHandling.EscapeHtml:
                    Html.EscapeString(utf8Source, destination, indexOfFirstByteToEscape, out written);
                    break;
                case JsonEscapeHandling.Default:
                default:
                    Default.EscapeString(utf8Source, destination, indexOfFirstByteToEscape, out written);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EscapeString(in ReadOnlySpan<char> utf16Source, Span<char> destination, JsonEscapeHandling escapeHandling, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
        {
            switch (escapeHandling)
            {
                case JsonEscapeHandling.EscapeNonAscii:
#if !NET451
                    NonAscii.EscapeString(utf16Source, destination, indexOfFirstByteToEscape, encoder, out written);
#else
                    NonAscii.EscapeString(utf16Source, destination, indexOfFirstByteToEscape, out written);
#endif
                    break;
                case JsonEscapeHandling.EscapeHtml:
                    Html.EscapeString(utf16Source, destination, indexOfFirstByteToEscape, out written);
                    break;
                case JsonEscapeHandling.Default:
                default:
                    Default.EscapeString(utf16Source, destination, indexOfFirstByteToEscape, out written);
                    break;
            }
        }

        public static string EscapeString(string input, JsonEscapeHandling escapeHandling = JsonEscapeHandling.Default, JavaScriptEncoder encoder = null)
        {
            if (string.IsNullOrEmpty(input)) { return input; }

#if NETSTANDARD2_0 || NET471 || NET451
            ReadOnlySpan<char> source = input.AsSpan();
#else
            ReadOnlySpan<char> source = input;
#endif
            var firstEscapeIndex = NeedsEscaping(source, escapeHandling);
            if ((uint)firstEscapeIndex > JsonSharedConstant.TooBigOrNegative) // -1
            {
                return input;
            }
            else
            {
                char[] tempArray = null;
                var length = GetMaxEscapedLength(source.Length, firstEscapeIndex);
                try
                {
                    Span<char> escapedName = (uint)length <= JsonSharedConstant.StackallocThreshold ?
                        stackalloc char[length] :
                        (tempArray = ArrayPool<char>.Shared.Rent(length));
                    EscapeString(source, escapedName, escapeHandling, firstEscapeIndex, encoder, out int written);

                    return escapedName.Slice(0, written).ToString();
                }
                finally
                {
                    if (tempArray is object) { ArrayPool<char>.Shared.Return(tempArray); }
                }
            }
        }

        public static string EscapeString(ReadOnlySpan<char> input, JsonEscapeHandling escapeHandling = JsonEscapeHandling.Default, JavaScriptEncoder encoder = null)
        {
            if (input.IsEmpty) { return string.Empty; }

            var firstEscapeIndex = NeedsEscaping(input, escapeHandling);
            if ((uint)firstEscapeIndex > JsonSharedConstant.TooBigOrNegative) // -1
            {
                return input.ToString();
            }
            else
            {
                char[] tempArray = null;
                var length = GetMaxEscapedLength(input.Length, firstEscapeIndex);
                try
                {
                    Span<char> escapedName = (uint)length <= JsonSharedConstant.StackallocThreshold ?
                        stackalloc char[length] :
                        (tempArray = ArrayPool<char>.Shared.Rent(length));
                    EscapeString(input, escapedName, escapeHandling, firstEscapeIndex, encoder, out int written);

                    return escapedName.Slice(0, written).ToString();
                }
                finally
                {
                    if (tempArray is object) { ArrayPool<char>.Shared.Return(tempArray); }
                }
            }
        }

        #endregion

        #region == EscapeChar ==

        internal static void EscapeChar(JsonEscapeHandling escapeHandling, ref char destSpace, char value, ref int pos)
        {
            switch (value)
            {
                case JsonUtf16Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, JsonUtf16Constant.DoubleQuote, ref pos);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref pos);
                    }
                    break;
                case JsonUtf16Constant.ReverseSolidus:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref pos);
                    break;
                case '\n':
                    WriteSingleChar(ref destSpace, 'n', ref pos);
                    break;
                case '\r':
                    WriteSingleChar(ref destSpace, 'r', ref pos);
                    break;
                case '\t':
                    WriteSingleChar(ref destSpace, 't', ref pos);
                    break;
                case '\b':
                    WriteSingleChar(ref destSpace, 'b', ref pos);
                    break;
                case '\f':
                    WriteSingleChar(ref destSpace, 'f', ref pos);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref pos);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref pos);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref pos);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref pos);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref pos);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref pos);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref pos);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref pos);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref pos);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref pos);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref pos);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref pos);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref pos);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref pos);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref pos);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref pos);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref pos);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref pos);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref pos);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref pos);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref pos);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref pos);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref pos);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref pos);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref pos);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref pos);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref pos);
                    break;

                default:
                    WriteHexChar(ref destSpace, value, ref pos);
                    break;
            }
        }

        internal static void EscapeChar(JsonEscapeHandling escapeHandling, ref byte destSpace, char value, ref int pos)
        {
            switch (value)
            {
                case JsonUtf16Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, JsonUtf16Constant.DoubleQuote, ref pos);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref pos);
                    }
                    break;
                case JsonUtf16Constant.ReverseSolidus:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref pos);
                    break;
                case '\n':
                    WriteSingleChar(ref destSpace, 'n', ref pos);
                    break;
                case '\r':
                    WriteSingleChar(ref destSpace, 'r', ref pos);
                    break;
                case '\t':
                    WriteSingleChar(ref destSpace, 't', ref pos);
                    break;
                case '\b':
                    WriteSingleChar(ref destSpace, 'b', ref pos);
                    break;
                case '\f':
                    WriteSingleChar(ref destSpace, 'f', ref pos);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref pos);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref pos);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref pos);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref pos);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref pos);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref pos);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref pos);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref pos);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref pos);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref pos);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref pos);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref pos);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref pos);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref pos);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref pos);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref pos);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref pos);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref pos);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref pos);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref pos);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref pos);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref pos);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref pos);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref pos);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref pos);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref pos);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref pos);
                    break;

                default:
                    WriteHexChar(ref destSpace, value, ref pos);
                    break;
            }
        }

        #endregion

        #region == EscapeNextBytes ==

        private static void EscapeNextBytes(JsonEscapeHandling escapeHandling, byte value, Span<byte> destination, ref byte destSpace, ref int written)
        {
            switch ((uint)value)
            {
                case JsonUtf8Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, '"', ref written);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref written);
                    }
                    break;
                case JsonUtf8Constant.BackSlash:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref written);
                    break;
                case JsonUtf8Constant.LineFeed:
                    WriteSingleChar(ref destSpace, 'n', ref written);
                    break;
                case JsonUtf8Constant.CarriageReturn:
                    WriteSingleChar(ref destSpace, 'r', ref written);
                    break;
                case JsonUtf8Constant.Tab:
                    WriteSingleChar(ref destSpace, 't', ref written);
                    break;
                case JsonUtf8Constant.BackSpace:
                    WriteSingleChar(ref destSpace, 'b', ref written);
                    break;
                case JsonUtf8Constant.FormFeed:
                    WriteSingleChar(ref destSpace, 'f', ref written);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref written);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref written);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref written);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref written);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref written);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref written);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref written);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref written);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref written);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref written);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref written);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref written);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref written);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref written);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref written);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref written);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref written);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref written);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref written);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref written);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref written);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref written);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref written);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref written);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref written);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref written);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref written);
                    break;
                default:
                    IntPtr offset = (IntPtr)written;
                    Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.BackSlash;
                    Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
                    written += 2;

                    bool result = Utf8Formatter.TryFormat(value, destination.Slice(written), out int bytesWritten, format: s_hexStandardFormat);
                    Debug.Assert(result);
                    Debug.Assert(bytesWritten == 4);
                    written += bytesWritten;
                    break;
            }
        }

        private static bool EscapeNextBytes(JsonEscapeHandling escapeHandling, ref byte sourceSpace, ref int consumed, uint remaining,
            Span<byte> destination, ref byte destSpace, ref int written)
        {
            SequenceValidity status = PeekFirstSequence(ref sourceSpace, consumed, remaining, out int numBytesConsumed, out int scalar);
            if (status != SequenceValidity.WellFormed) { return false; }

            consumed += numBytesConsumed;

            switch (scalar)
            {
                case JsonUtf8Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, '"', ref written);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref written);
                    }
                    break;
                case JsonUtf8Constant.BackSlash:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref written);
                    break;
                case JsonUtf8Constant.LineFeed:
                    WriteSingleChar(ref destSpace, 'n', ref written);
                    break;
                case JsonUtf8Constant.CarriageReturn:
                    WriteSingleChar(ref destSpace, 'r', ref written);
                    break;
                case JsonUtf8Constant.Tab:
                    WriteSingleChar(ref destSpace, 't', ref written);
                    break;
                case JsonUtf8Constant.BackSpace:
                    WriteSingleChar(ref destSpace, 'b', ref written);
                    break;
                case JsonUtf8Constant.FormFeed:
                    WriteSingleChar(ref destSpace, 'f', ref written);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref written);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref written);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref written);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref written);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref written);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref written);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref written);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref written);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref written);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref written);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref written);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref written);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref written);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref written);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref written);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref written);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref written);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref written);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref written);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref written);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref written);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref written);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref written);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref written);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref written);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref written);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref written);
                    break;
                default:
                    IntPtr offset = (IntPtr)written;
                    Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.BackSlash;
                    Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
                    written += 2;
                    if (scalar < JsonSharedConstant.UnicodePlane01StartValue)
                    {
                        bool result = Utf8Formatter.TryFormat(scalar, destination.Slice(written), out int bytesWritten, format: s_hexStandardFormat);
                        Debug.Assert(result);
                        Debug.Assert(bytesWritten == 4);
                        written += bytesWritten;
                    }
                    else
                    {
                        // Divide by 0x400 to shift right by 10 in order to find the surrogate pairs from the scalar
                        // High surrogate = ((scalar -  0x10000) / 0x400) + D800
                        // Low surrogate = ((scalar -  0x10000) % 0x400) + DC00
                        int quotient = Math.DivRem(scalar - JsonSharedConstant.UnicodePlane01StartValue, JsonSharedConstant.BitShiftBy10, out int remainder);
                        int firstChar = quotient + JsonSharedConstant.HighSurrogateStartValue;
                        int nextChar = remainder + JsonSharedConstant.LowSurrogateStartValue;
                        bool result = Utf8Formatter.TryFormat(firstChar, destination.Slice(written), out int bytesWritten, format: s_hexStandardFormat);
                        Debug.Assert(result);
                        Debug.Assert(bytesWritten == 4);
                        written += bytesWritten;
                        offset = (IntPtr)written;
                        Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.BackSlash;
                        Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
                        written += 2;
                        result = Utf8Formatter.TryFormat(nextChar, destination.Slice(written), out bytesWritten, format: s_hexStandardFormat);
                        Debug.Assert(result);
                        Debug.Assert(bytesWritten == 4);
                        written += bytesWritten;
                    }
                    break;
            }
            return true;
        }

        /// <summary>Returns <see langword="true"/> if <paramref name="value"/> is a UTF-8 continuation byte.
        /// A UTF-8 continuation byte is a byte whose value is in the range 0x80-0xBF, inclusive.</summary>
        private static bool IsUtf8ContinuationByte(byte value) => (value & 0xC0) == 0x80 ? true : false;

        /// <summary>Returns <see langword="true"/> if the low word of <paramref name="char"/> is a UTF-16 surrogate.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLowWordSurrogate(uint @char) => (@char & 0xF800U) == 0xD800U ? true : false;

        /// <summary>A scalar that represents the Unicode replacement character U+FFFD.</summary>
        private const int ReplacementChar = 0xFFFD;

        // We can't use the type Rune since it is not available on netstandard2.0
        // To avoid extensive ifdefs and for simplicity, just using an int to reprepsent the scalar value, instead.
        private static SequenceValidity PeekFirstSequence(ref byte sourceSpace, int consumed, uint remaining, out int numBytesConsumed, out int rune)
        {
            // This method is implemented to match the behavior of System.Text.Encoding.UTF8 in terms of
            // how many bytes it consumes when reporting invalid sequences. The behavior is as follows:
            //
            // - Some bytes are *always* invalid (ranges [ C0..C1 ] and [ F5..FF ]), and when these
            //   are encountered it's an invalid sequence of length 1.
            //
            // - Multi-byte sequences which are overlong are reported as an invalid sequence of length 2,
            //   since per the Unicode Standard Table 3-7 it's always possible to tell these by the second byte.
            //   Exception: Sequences which begin with [ C0..C1 ] are covered by the above case, thus length 1.
            //
            // - Multi-byte sequences which are improperly terminated (no continuation byte when one is
            //   expected) are reported as invalid sequences up to and including the last seen continuation byte.

            Debug.Assert(JsonHelpers.IsValidUnicodeScalar(ReplacementChar));
            rune = ReplacementChar;

            IntPtr offset = (IntPtr)consumed;

            if (0u >= remaining)
            {
                // No data to peek at
                numBytesConsumed = 0;
                return SequenceValidity.Empty;
            }

            byte firstByte = Unsafe.AddByteOffset(ref sourceSpace, offset);

            if (IsAsciiValue(firstByte))
            {
                // ASCII byte = well-formed one-byte sequence.
                Debug.Assert(JsonHelpers.IsValidUnicodeScalar(firstByte));
                rune = firstByte;
                numBytesConsumed = 1;
                return SequenceValidity.WellFormed;
            }

            if (!JsonHelpers.IsInRangeInclusive(firstByte, (byte)0xC2U, (byte)0xF4U))
            {
                // Standalone continuation byte or "always invalid" byte = ill-formed one-byte sequence.
                goto InvalidOneByteSequence;
            }

            // At this point, we know we're working with a multi-byte sequence,
            // and we know that at least the first byte is potentially valid.

            if (remaining < 2u)
            {
                // One byte of an incomplete multi-byte sequence.
                goto OneByteOfIncompleteMultiByteSequence;
            }

            byte secondByte = Unsafe.AddByteOffset(ref sourceSpace, offset + 1);

            if (!IsUtf8ContinuationByte(secondByte))
            {
                // One byte of an improperly terminated multi-byte sequence.
                goto InvalidOneByteSequence;
            }

            if (firstByte < (byte)0xE0U)
            {
                // Well-formed two-byte sequence.
                uint scalar = (((uint)firstByte & 0x1FU) << 6) | ((uint)secondByte & 0x3FU);
                Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));
                rune = (int)scalar;
                numBytesConsumed = 2;
                return SequenceValidity.WellFormed;
            }

            if (firstByte < (byte)0xF0U)
            {
                // Start of a three-byte sequence.
                // Need to check for overlong or surrogate sequences.

                uint scalar = (((uint)firstByte & 0x0FU) << 12) | (((uint)secondByte & 0x3FU) << 6);
                if (scalar < 0x800U || IsLowWordSurrogate(scalar))
                {
                    goto OverlongOutOfRangeOrSurrogateSequence;
                }

                // At this point, we have a valid two-byte start of a three-byte sequence.

                if (remaining < 3u)
                {
                    // Two bytes of an incomplete three-byte sequence.
                    goto TwoBytesOfIncompleteMultiByteSequence;
                }
                else
                {
                    byte thirdByte = Unsafe.AddByteOffset(ref sourceSpace, offset + 2);
                    if (IsUtf8ContinuationByte(thirdByte))
                    {
                        // Well-formed three-byte sequence.
                        scalar |= (uint)thirdByte & 0x3FU;
                        Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));
                        rune = (int)scalar;
                        numBytesConsumed = 3;
                        return SequenceValidity.WellFormed;
                    }
                    else
                    {
                        // Two bytes of improperly terminated multi-byte sequence.
                        goto InvalidTwoByteSequence;
                    }
                }
            }

            {
                // Start of four-byte sequence.
                // Need to check for overlong or out-of-range sequences.

                uint scalar = (((uint)firstByte & 0x07U) << 18) | (((uint)secondByte & 0x3FU) << 12);
                Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));
                if (!JsonHelpers.IsInRangeInclusive(scalar, 0x10000U, 0x10FFFFU))
                {
                    goto OverlongOutOfRangeOrSurrogateSequence;
                }

                // At this point, we have a valid two-byte start of a four-byte sequence.

                if (remaining < 3u)
                {
                    // Two bytes of an incomplete four-byte sequence.
                    goto TwoBytesOfIncompleteMultiByteSequence;
                }
                else
                {
                    byte thirdByte = Unsafe.AddByteOffset(ref sourceSpace, offset + 2);
                    if (IsUtf8ContinuationByte(thirdByte))
                    {
                        // Valid three-byte start of a four-byte sequence.

                        if (remaining < 4u)
                        {
                            // Three bytes of an incomplete four-byte sequence.
                            goto ThreeBytesOfIncompleteMultiByteSequence;
                        }
                        else
                        {
                            byte fourthByte = Unsafe.AddByteOffset(ref sourceSpace, offset + 3);
                            if (IsUtf8ContinuationByte(fourthByte))
                            {
                                // Well-formed four-byte sequence.
                                scalar |= (((uint)thirdByte & 0x3FU) << 6) | ((uint)fourthByte & 0x3FU);
                                Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));
                                rune = (int)scalar;
                                numBytesConsumed = 4;
                                return SequenceValidity.WellFormed;
                            }
                            else
                            {
                                // Three bytes of an improperly terminated multi-byte sequence.
                                goto InvalidThreeByteSequence;
                            }
                        }
                    }
                    else
                    {
                        // Two bytes of improperly terminated multi-byte sequence.
                        goto InvalidTwoByteSequence;
                    }
                }
            }

        // Everything below here is error handling.

        InvalidOneByteSequence:
            numBytesConsumed = 1;
            return SequenceValidity.Invalid;

        InvalidTwoByteSequence:
        OverlongOutOfRangeOrSurrogateSequence:
            numBytesConsumed = 2;
            return SequenceValidity.Invalid;

        InvalidThreeByteSequence:
            numBytesConsumed = 3;
            return SequenceValidity.Invalid;

        OneByteOfIncompleteMultiByteSequence:
            numBytesConsumed = 1;
            return SequenceValidity.Incomplete;

        TwoBytesOfIncompleteMultiByteSequence:
            numBytesConsumed = 2;
            return SequenceValidity.Incomplete;

        ThreeBytesOfIncompleteMultiByteSequence:
            numBytesConsumed = 3;
            return SequenceValidity.Incomplete;
        }

        #endregion

        #region == EscapeNextChars ==

        private static void EscapeNextChars(JsonEscapeHandling escapeHandling, char value, ref char destSpace, ref int written)
        {
            Debug.Assert(IsAsciiValue(value));

            switch (value)
            {
                case JsonUtf16Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, JsonUtf16Constant.DoubleQuote, ref written);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref written);
                    }
                    break;
                case JsonUtf16Constant.BackSlash:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref written);
                    break;
                case JsonUtf16Constant.LineFeed:
                    WriteSingleChar(ref destSpace, 'n', ref written);
                    break;
                case JsonUtf16Constant.CarriageReturn:
                    WriteSingleChar(ref destSpace, 'r', ref written);
                    break;
                case JsonUtf16Constant.Tab:
                    WriteSingleChar(ref destSpace, 't', ref written);
                    break;
                case JsonUtf16Constant.BackSpace:
                    WriteSingleChar(ref destSpace, 'b', ref written);
                    break;
                case JsonUtf16Constant.FormFeed:
                    WriteSingleChar(ref destSpace, 'f', ref written);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref written);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref written);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref written);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref written);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref written);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref written);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref written);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref written);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref written);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref written);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref written);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref written);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref written);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref written);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref written);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref written);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref written);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref written);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref written);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref written);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref written);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref written);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref written);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref written);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref written);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref written);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref written);
                    break;
                default:
#if NETCOREAPP_2_X_GREATER1 // 屏蔽，与 System.Text.Json 不同
                    destination[written++] = '\\';
                    destination[written++] = 'u';
                    int intChar = value;
                    intChar.TryFormat(destination.Slice(written), out int charsWritten, HexFormatString);
                    Debug.Assert(charsWritten == 4);
                    written += charsWritten;
#else
                    WriteHexChar(ref destSpace, value, ref written);
#endif
                    break;
            }
        }

        internal static void EscapeNextChars(JsonEscapeHandling escapeHandling, ref char sourceSpace, uint srcLength, int firstChar, ref char destSpace, ref int consumed, ref int written)
        {
            switch (firstChar)
            {
                case JsonUtf16Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, JsonUtf16Constant.DoubleQuote, ref written);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref written);
                    }
                    break;
                case JsonUtf16Constant.BackSlash:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref written);
                    break;
                case JsonUtf16Constant.LineFeed:
                    WriteSingleChar(ref destSpace, 'n', ref written);
                    break;
                case JsonUtf16Constant.CarriageReturn:
                    WriteSingleChar(ref destSpace, 'r', ref written);
                    break;
                case JsonUtf16Constant.Tab:
                    WriteSingleChar(ref destSpace, 't', ref written);
                    break;
                case JsonUtf16Constant.BackSpace:
                    WriteSingleChar(ref destSpace, 'b', ref written);
                    break;
                case JsonUtf16Constant.FormFeed:
                    WriteSingleChar(ref destSpace, 'f', ref written);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref written);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref written);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref written);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref written);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref written);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref written);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref written);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref written);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref written);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref written);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref written);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref written);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref written);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref written);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref written);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref written);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref written);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref written);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref written);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref written);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref written);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref written);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref written);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref written);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref written);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref written);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref written);
                    break;

                default:
                    WriteHexChar(ref destSpace, firstChar, ref written);
                    int nextChar = -1;
                    if (JsonHelpers.IsInRangeInclusive(firstChar, JsonSharedConstant.HighSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                    {
                        consumed++;
                        if (srcLength <= (uint)consumed || firstChar >= JsonSharedConstant.LowSurrogateStartValue)
                        {
                            ThrowHelper.ThrowArgumentException_InvalidUTF16(firstChar);
                        }

                        nextChar = Unsafe.Add(ref sourceSpace, consumed);
                        if (!JsonHelpers.IsInRangeInclusive(nextChar, JsonSharedConstant.LowSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                        {
                            ThrowHelper.ThrowArgumentException_InvalidUTF16(nextChar);
                        }
                    }
                    if (nextChar != -1)
                    {
                        WriteHexChar(ref destSpace, nextChar, ref written);
                    }
                    break;
            }
        }

        internal static void EscapeNextChars(JsonEscapeHandling escapeHandling, ref char sourceSpace, uint srcLength, int firstChar, ref byte destSpace, ref int consumed, ref int written)
        {
            switch (firstChar)
            {
                case JsonUtf16Constant.DoubleQuote:
                    if (escapeHandling == JsonEscapeHandling.Default)
                    {
                        WriteSingleChar(ref destSpace, JsonUtf16Constant.DoubleQuote, ref written);
                    }
                    else
                    {
                        WriteDoubleQuote(ref destSpace, ref written);
                    }
                    break;
                case JsonUtf16Constant.BackSlash:
                    WriteSingleChar(ref destSpace, JsonUtf16Constant.ReverseSolidus, ref written);
                    break;
                case JsonUtf16Constant.LineFeed:
                    WriteSingleChar(ref destSpace, 'n', ref written);
                    break;
                case JsonUtf16Constant.CarriageReturn:
                    WriteSingleChar(ref destSpace, 'r', ref written);
                    break;
                case JsonUtf16Constant.Tab:
                    WriteSingleChar(ref destSpace, 't', ref written);
                    break;
                case JsonUtf16Constant.BackSpace:
                    WriteSingleChar(ref destSpace, 'b', ref written);
                    break;
                case JsonUtf16Constant.FormFeed:
                    WriteSingleChar(ref destSpace, 'f', ref written);
                    break;
                case '\x0':
                    WriteDoubleChar(ref destSpace, '0', '0', ref written);
                    break;
                case '\x1':
                    WriteDoubleChar(ref destSpace, '0', '1', ref written);
                    break;
                case '\x2':
                    WriteDoubleChar(ref destSpace, '0', '2', ref written);
                    break;
                case '\x3':
                    WriteDoubleChar(ref destSpace, '0', '3', ref written);
                    break;
                case '\x4':
                    WriteDoubleChar(ref destSpace, '0', '4', ref written);
                    break;
                case '\x5':
                    WriteDoubleChar(ref destSpace, '0', '5', ref written);
                    break;
                case '\x6':
                    WriteDoubleChar(ref destSpace, '0', '6', ref written);
                    break;
                case '\x7':
                    WriteDoubleChar(ref destSpace, '0', '7', ref written);
                    break;
                case '\xB':
                    WriteDoubleChar(ref destSpace, '0', 'b', ref written);
                    break;
                case '\xE':
                    WriteDoubleChar(ref destSpace, '0', 'e', ref written);
                    break;
                case '\xF':
                    WriteDoubleChar(ref destSpace, '0', 'f', ref written);
                    break;
                case '\x10':
                    WriteDoubleChar(ref destSpace, '1', '0', ref written);
                    break;
                case '\x11':
                    WriteDoubleChar(ref destSpace, '1', '1', ref written);
                    break;
                case '\x12':
                    WriteDoubleChar(ref destSpace, '1', '2', ref written);
                    break;
                case '\x13':
                    WriteDoubleChar(ref destSpace, '1', '3', ref written);
                    break;
                case '\x14':
                    WriteDoubleChar(ref destSpace, '1', '4', ref written);
                    break;
                case '\x15':
                    WriteDoubleChar(ref destSpace, '1', '5', ref written);
                    break;
                case '\x16':
                    WriteDoubleChar(ref destSpace, '1', '6', ref written);
                    break;
                case '\x17':
                    WriteDoubleChar(ref destSpace, '1', '7', ref written);
                    break;
                case '\x18':
                    WriteDoubleChar(ref destSpace, '1', '8', ref written);
                    break;
                case '\x19':
                    WriteDoubleChar(ref destSpace, '1', '9', ref written);
                    break;
                case '\x1A':
                    WriteDoubleChar(ref destSpace, '1', 'a', ref written);
                    break;
                case '\x1B':
                    WriteDoubleChar(ref destSpace, '1', 'b', ref written);
                    break;
                case '\x1C':
                    WriteDoubleChar(ref destSpace, '1', 'c', ref written);
                    break;
                case '\x1D':
                    WriteDoubleChar(ref destSpace, '1', 'd', ref written);
                    break;
                case '\x1E':
                    WriteDoubleChar(ref destSpace, '1', 'e', ref written);
                    break;
                case '\x1F':
                    WriteDoubleChar(ref destSpace, '1', 'f', ref written);
                    break;

                default:
                    WriteHexChar(ref destSpace, firstChar, ref written);
                    int nextChar = -1;
                    if (JsonHelpers.IsInRangeInclusive(firstChar, JsonSharedConstant.HighSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                    {
                        consumed++;
                        if (srcLength <= (uint)consumed || firstChar >= JsonSharedConstant.LowSurrogateStartValue)
                        {
                            ThrowHelper.ThrowArgumentException_InvalidUTF16(firstChar);
                        }

                        nextChar = Unsafe.Add(ref sourceSpace, consumed);
                        if (!JsonHelpers.IsInRangeInclusive(nextChar, JsonSharedConstant.LowSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                        {
                            ThrowHelper.ThrowArgumentException_InvalidUTF16(nextChar);
                        }
                    }
                    if (nextChar != -1)
                    {
                        WriteHexChar(ref destSpace, nextChar, ref written);
                    }
                    break;
            }
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteSingleChar(ref char destSpace, char toEscape, ref int pos)
        {
            Unsafe.Add(ref destSpace, pos++) = JsonUtf16Constant.ReverseSolidus;
            Unsafe.Add(ref destSpace, pos++) = toEscape;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteSingleChar(ref byte destSpace, char toEscape, ref int pos)
        {
            var offset = (IntPtr)pos;
            Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.ReverseSolidus;
            Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)toEscape;
            pos += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDoubleQuote(ref char destSpace, ref int pos)
        {
            Unsafe.Add(ref destSpace, pos++) = JsonUtf16Constant.ReverseSolidus;
            Unsafe.Add(ref destSpace, pos++) = 'u';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = '2';
            Unsafe.Add(ref destSpace, pos++) = '2';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDoubleQuote(ref byte destSpace, ref int pos)
        {
            var offset = (IntPtr)pos;
            Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.ReverseSolidus;
            Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
            Unsafe.AddByteOffset(ref destSpace, offset + 2) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 3) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 4) = (byte)'2';
            Unsafe.AddByteOffset(ref destSpace, offset + 5) = (byte)'2';
            pos += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteSlash(ref char destSpace, ref int pos)
        {
            Unsafe.Add(ref destSpace, pos++) = JsonUtf16Constant.ReverseSolidus;
            Unsafe.Add(ref destSpace, pos++) = 'u';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = '2';
            Unsafe.Add(ref destSpace, pos++) = 'f';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteSlash(ref byte destSpace, ref int pos)
        {
            var offset = (IntPtr)pos;
            Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.ReverseSolidus;
            Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
            Unsafe.AddByteOffset(ref destSpace, offset + 2) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 3) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 4) = (byte)'2';
            Unsafe.AddByteOffset(ref destSpace, offset + 5) = (byte)'f';
            pos += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDoubleChar(ref char destSpace, char firstToEscape, char secondToEscape, ref int pos)
        {
            Unsafe.Add(ref destSpace, pos++) = JsonUtf16Constant.ReverseSolidus;
            Unsafe.Add(ref destSpace, pos++) = 'u';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = '0';
            Unsafe.Add(ref destSpace, pos++) = firstToEscape;
            Unsafe.Add(ref destSpace, pos++) = secondToEscape;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDoubleChar(ref byte destSpace, char firstToEscape, char secondToEscape, ref int pos)
        {
            var offset = (IntPtr)pos;
            Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.ReverseSolidus;
            Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
            Unsafe.AddByteOffset(ref destSpace, offset + 2) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 3) = (byte)'0';
            Unsafe.AddByteOffset(ref destSpace, offset + 4) = (byte)firstToEscape;
            Unsafe.AddByteOffset(ref destSpace, offset + 5) = (byte)secondToEscape;
            pos += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteHexChar(ref char destSpace, int toEscape, ref int pos)
        {
            Unsafe.Add(ref destSpace, pos++) = JsonUtf16Constant.ReverseSolidus;
            Unsafe.Add(ref destSpace, pos++) = 'u';
            Unsafe.Add(ref destSpace, pos++) = (char)Int32LsbToHexDigit(toEscape >> 12);
            Unsafe.Add(ref destSpace, pos++) = (char)Int32LsbToHexDigit((int)((toEscape >> 8) & 0xFU));
            Unsafe.Add(ref destSpace, pos++) = (char)Int32LsbToHexDigit((int)((toEscape >> 4) & 0xFU));
            Unsafe.Add(ref destSpace, pos++) = (char)Int32LsbToHexDigit((int)(toEscape & 0xFU));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteHexChar(ref byte destSpace, int toEscape, ref int pos)
        {
            var offset = (IntPtr)pos;
            Unsafe.AddByteOffset(ref destSpace, offset) = JsonUtf8Constant.ReverseSolidus;
            Unsafe.AddByteOffset(ref destSpace, offset + 1) = (byte)'u';
            Unsafe.AddByteOffset(ref destSpace, offset + 2) = Int32LsbToHexDigit(toEscape >> 12);
            Unsafe.AddByteOffset(ref destSpace, offset + 3) = Int32LsbToHexDigit((int)((toEscape >> 8) & 0xFU));
            Unsafe.AddByteOffset(ref destSpace, offset + 4) = Int32LsbToHexDigit((int)((toEscape >> 4) & 0xFU));
            Unsafe.AddByteOffset(ref destSpace, offset + 5) = Int32LsbToHexDigit((int)(toEscape & 0xFU));
            pos += 6;
        }

        /// <summary>Converts a number 0 - 15 to its associated hex character '0' - 'f' as byte.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Int32LsbToHexDigit(int value)
        {
            Debug.Assert(value < 16);
            return (byte)((value < 10) ? ('0' + value) : ('a' + (value - 10)));
        }

        /// <summary>Converts a number 0 - 15 to its associated hex character '0' - 'F' as byte.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Int32LsbToUpperHexDigit(int value)
        {
            Debug.Assert(value < 16);
            return (byte)((value < 10) ? ('0' + value) : ('A' + (value - 10)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsUnicodeLinefeeds(char value)
        {
            switch (value)
            {
                case '\u0085': // Next Line
                case '\u2028': // Line Separator
                case '\u2029': // Paragraph Separator
                    return true;
                default:
                    return false;
            }
        }
    }
}
