// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// Largely based on https://github.com/dotnet/corefx/blob/8135319caa7e457ed61053ca1418313b88057b51/src/System.Text.Json/src/System/Text/Json/Reader/JsonReaderHelper.Unescaping.cs

namespace SpanJson.Internal
{
    internal static partial class JsonReaderHelper
    {
        public static bool TryGetUnescapedBase64Bytes(in ReadOnlySpan<byte> utf8Source, int idx, out byte[] bytes)
        {
            byte[] unescapedArray = null;

            Span<byte> utf8Unescaped = (uint)utf8Source.Length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[utf8Source.Length] :
                (unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length));

            Unescape(utf8Source, utf8Unescaped, idx, out int written);
            Debug.Assert(written > 0);

            utf8Unescaped = utf8Unescaped.Slice(0, written);
            Debug.Assert(!utf8Unescaped.IsEmpty);

            bool result = TryDecodeBase64InPlace(utf8Unescaped, out bytes);

            if (unescapedArray is object)
            {
                //utf8Unescaped.Clear();
                ArrayPool<byte>.Shared.Return(unescapedArray);
            }

            return result;
        }

        //// Reject any invalid UTF-8 data rather than silently replacing.
        //public static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // TODO: Similar to escaping, replace the unescaping logic with publicly shipping APIs from https://github.com/dotnet/corefx/issues/33509
        public static string GetUnescapedString(in ReadOnlySpan<byte> utf8Source, int idx)
        {
            byte[] unescapedArray = null;
            Span<byte> utf8Unescaped = (uint)utf8Source.Length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[utf8Source.Length] :
                (unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length));
            try
            {
                Unescape(utf8Source, utf8Unescaped, idx, out int written);
                Debug.Assert(written > 0);

                utf8Unescaped = utf8Unescaped.Slice(0, written);
                Debug.Assert(!utf8Unescaped.IsEmpty);

                return TranscodeHelper(utf8Unescaped);
            }
            finally
            {
                if (unescapedArray is object)
                {
                    //utf8Unescaped.Clear();
                    ArrayPool<byte>.Shared.Return(unescapedArray);
                }
            }
        }

        public static bool UnescapeAndCompare(in ReadOnlySpan<byte> utf8Source, ReadOnlySpan<byte> other)
        {
            Debug.Assert(utf8Source.Length >= other.Length && utf8Source.Length / JsonSharedConstant.MaxExpansionFactorWhileEscaping <= other.Length);

            byte[] unescapedArray = null;

            Span<byte> utf8Unescaped = (uint)utf8Source.Length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[utf8Source.Length] :
                (unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length));

            Unescape(utf8Source, utf8Unescaped, 0, out int written);
            Debug.Assert(written > 0);

            utf8Unescaped = utf8Unescaped.Slice(0, written);
            Debug.Assert(!utf8Unescaped.IsEmpty);

            bool result = other.SequenceEqual(utf8Unescaped);

            if (unescapedArray is object)
            {
                //utf8Unescaped.Clear();
                ArrayPool<byte>.Shared.Return(unescapedArray);
            }

            return result;
        }

        public static bool UnescapeAndCompare(in ReadOnlySequence<byte> utf8Source, ReadOnlySpan<byte> other)
        {
            Debug.Assert(!utf8Source.IsSingleSegment);
            Debug.Assert(utf8Source.Length >= other.Length && utf8Source.Length / JsonSharedConstant.MaxExpansionFactorWhileEscaping <= other.Length);

            byte[] escapedArray = null;
            byte[] unescapedArray = null;

            int length = checked((int)utf8Source.Length);

            Span<byte> utf8Unescaped = (uint)length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[length] :
                (unescapedArray = ArrayPool<byte>.Shared.Rent(length));

            Span<byte> utf8Escaped = (uint)length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[length] :
                (escapedArray = ArrayPool<byte>.Shared.Rent(length));

            utf8Source.CopyTo(utf8Escaped);
            utf8Escaped = utf8Escaped.Slice(0, length);

            Unescape(utf8Escaped, utf8Unescaped, 0, out int written);
            Debug.Assert(written > 0);

            utf8Unescaped = utf8Unescaped.Slice(0, written);
            Debug.Assert(!utf8Unescaped.IsEmpty);

            bool result = other.SequenceEqual(utf8Unescaped);

            if (unescapedArray is object)
            {
                Debug.Assert(escapedArray is object);
                //utf8Unescaped.Clear();
                ArrayPool<byte>.Shared.Return(unescapedArray);
                //utf8Escaped.Clear();
                ArrayPool<byte>.Shared.Return(escapedArray);
            }

            return result;
        }

        public static bool TryDecodeBase64InPlace(Span<byte> utf8Unescaped, out byte[] bytes)
        {
            OperationStatus status = Base64.DecodeFromUtf8InPlace(utf8Unescaped, out int bytesWritten);
            if (status != OperationStatus.Done)
            {
                bytes = null;
                return false;
            }
            bytes = utf8Unescaped.Slice(0, bytesWritten).ToArray();
            return true;
        }

        public static bool TryDecodeBase64(in ReadOnlySpan<byte> utf8Unescaped, out byte[] bytes)
        {
            byte[] pooledArray = null;

            Span<byte> byteSpan = (uint)utf8Unescaped.Length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[utf8Unescaped.Length] :
                (pooledArray = ArrayPool<byte>.Shared.Rent(utf8Unescaped.Length));

            OperationStatus status = Base64.DecodeFromUtf8(utf8Unescaped, byteSpan, out int bytesConsumed, out int bytesWritten);

            if (status != OperationStatus.Done)
            {
                bytes = null;

                if (pooledArray is object)
                {
                    //byteSpan.Clear();
                    ArrayPool<byte>.Shared.Return(pooledArray);
                }

                return false;
            }
            Debug.Assert(bytesConsumed == utf8Unescaped.Length);

            bytes = byteSpan.Slice(0, bytesWritten).ToArray();

            if (pooledArray is object)
            {
                //byteSpan.Clear();
                ArrayPool<byte>.Shared.Return(pooledArray);
            }

            return true;
        }

        public static string TranscodeHelper(in ReadOnlySpan<byte> utf8Text)
        {
#if NET451
            return TextEncodings.Utf8.GetString(utf8Text);
#else
            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                return TextEncodings.UTF8NoBOM.GetString(utf8Text);
#else
                if (utf8Text.IsEmpty) { return string.Empty; }

                unsafe
                {
                    fixed (byte* bytePtr = utf8Text)
                    {
                        return TextEncodings.UTF8NoBOM.GetString(bytePtr, utf8Text.Length);
                    }
                }
#endif
            }
            catch (DecoderFallbackException ex)
            {
                // We want to be consistent with the exception being thrown
                // so the user only has to catch a single exception.
                // Since we already throw InvalidOperationException for mismatch token type,
                // and while unescaping, using that exception for failure to decode invalid UTF-8 bytes as well.
                // Therefore, wrapping the DecoderFallbackException around an InvalidOperationException.
                throw ThrowHelper.GetInvalidOperationException_ReadInvalidUTF8(ex);
            }
#endif
        }

        public static int GetUtf8ByteCount(in ReadOnlySpan<char> text)
        {
            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                return TextEncodings.UTF8NoBOM.GetByteCount(text);
#else
                if (text.IsEmpty) { return 0; }

                unsafe
                {
                    fixed (char* charPtr = text)
                    {
                        return TextEncodings.UTF8NoBOM.GetByteCount(charPtr, text.Length);
                    }
                }
#endif
            }
            catch (EncoderFallbackException ex)
            {
                // We want to be consistent with the exception being thrown
                // so the user only has to catch a single exception.
                // Since we already throw ArgumentException when validating other arguments,
                // using that exception for failure to encode invalid UTF-16 chars as well.
                // Therefore, wrapping the EncoderFallbackException around an ArgumentException.
                throw ThrowHelper.GetArgumentException_ReadInvalidUTF16(ex);
            }
        }

        public static int GetUtf8FromText(in ReadOnlySpan<char> text, Span<byte> dest)
        {
            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                return TextEncodings.UTF8NoBOM.GetBytes(text, dest);
#else
                if (text.IsEmpty) { return 0; }

                unsafe
                {
                    fixed (char* charPtr = text)
                    fixed (byte* destPtr = dest)
                    {
                        return TextEncodings.UTF8NoBOM.GetBytes(charPtr, text.Length, destPtr, dest.Length);
                    }
                }
#endif
            }
            catch (EncoderFallbackException ex)
            {
                // We want to be consistent with the exception being thrown
                // so the user only has to catch a single exception.
                // Since we already throw ArgumentException when validating other arguments,
                // using that exception for failure to encode invalid UTF-16 chars as well.
                // Therefore, wrapping the EncoderFallbackException around an ArgumentException.
                throw ThrowHelper.GetArgumentException_ReadInvalidUTF16(ex);
            }
        }

        public static string GetTextFromUtf8(in ReadOnlySpan<byte> utf8Text)
        {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            return TextEncodings.UTF8NoBOM.GetString(utf8Text);
#else
#if NET451
            return TextEncodings.Utf8.GetString(utf8Text);
#else
            if (utf8Text.IsEmpty)
            {
                return string.Empty;
            }

            unsafe
            {
                fixed (byte* bytePtr = utf8Text)
                {
                    return TextEncodings.UTF8NoBOM.GetString(bytePtr, utf8Text.Length);
                }
            }
#endif
#endif
        }

        internal static void Unescape(in ReadOnlySpan<byte> utf8Source, Span<byte> destination, int index, out int written)
        {
            utf8Source.Slice(0, index).CopyTo(destination);
            written = index;

            var from = index;
            var offset = (IntPtr)index;
            var nSrcLen = (uint)utf8Source.Length;
            ref byte sourceSpace = ref MemoryMarshal.GetReference(utf8Source);
            ref byte destSpace = ref MemoryMarshal.GetReference(destination);
            while ((uint)index < nSrcLen)
            {
                uint current = Unsafe.AddByteOffset(ref sourceSpace, offset);
                if (current == JsonUtf8Constant.ReverseSolidus)
                {
                    // We copy everything up to the escaped char as utf8 to the string
                    var sliceLength = index - from;
                    utf8Source.Slice(from, sliceLength).CopyTo(destination.Slice(written));
                    written += sliceLength;
                    current = Unsafe.AddByteOffset(ref sourceSpace, offset + 1);
                    index += 2;
                    byte unescaped = default;
                    switch (current)
                    {
                        case JsonUtf8Constant.DoubleQuote:
                            unescaped = JsonUtf8Constant.DoubleQuote;
                            break;
                        case JsonUtf8Constant.ReverseSolidus:
                            unescaped = JsonUtf8Constant.ReverseSolidus;
                            break;
                        case JsonUtf8Constant.Solidus:
                            unescaped = JsonUtf8Constant.Solidus;
                            break;
                        case 'b':
                            unescaped = JsonUtf8Constant.BackSpace;
                            break;
                        case 'f':
                            unescaped = JsonUtf8Constant.FormFeed;
                            break;
                        case 'n':
                            unescaped = JsonUtf8Constant.LineFeed;
                            break;
                        case 'r':
                            unescaped = JsonUtf8Constant.CarriageReturn;
                            break;
                        case 't':
                            unescaped = JsonUtf8Constant.Tab;
                            break;
                        case (byte)'u':
                            // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                            // Otherwise, the Utf8JsonReader would have alreayd thrown an exception.
                            Debug.Assert(nSrcLen >= ((uint)index + 4u));

                            bool result = Utf8Parser.TryParse(utf8Source.Slice(index, 4), out int scalar, out int bytesConsumed, 'x');
                            if (!result)
                            {
                                ThrowHelper.ThrowJsonParserException(JsonParserException.ParserError.InvalidSymbol, index);
                            }
                            Debug.Assert(bytesConsumed == 4);
                            index += bytesConsumed;

                            if (JsonHelpers.IsInRangeInclusive((uint)scalar, JsonSharedConstant.HighSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                            {
                                // The first hex value cannot be a low surrogate.
                                if (scalar >= JsonSharedConstant.LowSurrogateStartValue)
                                {
                                    ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(scalar);
                                }

                                Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, JsonSharedConstant.HighSurrogateStartValue, JsonSharedConstant.HighSurrogateEndValue));

                                index += 2; // Skip the next \u

                                // We must have a low surrogate following a high surrogate.
                                if (nSrcLen < ((uint)index + 4u) ||
                                    Unsafe.AddByteOffset(ref sourceSpace, offset + 6) != JsonUtf8Constant.ReverseSolidus ||
                                    Unsafe.AddByteOffset(ref sourceSpace, offset + 7) != 'u')
                                {
                                    ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16();
                                }

                                // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                                // Otherwise, the Utf8JsonReader would have alreayd thrown an exception.
                                result = Utf8Parser.TryParse(utf8Source.Slice(index, 4), out int lowSurrogate, out bytesConsumed, 'x');
                                if (!result)
                                {
                                    ThrowHelper.ThrowJsonParserException(JsonParserException.ParserError.InvalidSymbol, index);
                                }
                                Debug.Assert(bytesConsumed == 4);

                                // If the first hex value is a high surrogate, the next one must be a low surrogate.
                                if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, JsonSharedConstant.LowSurrogateStartValue, JsonSharedConstant.LowSurrogateEndValue))
                                {
                                    ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(lowSurrogate);
                                }

                                index += bytesConsumed;

                                // To find the unicode scalar:
                                // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                                scalar = (JsonSharedConstant.BitShiftBy10 * (scalar - JsonSharedConstant.HighSurrogateStartValue))
                                    + (lowSurrogate - JsonSharedConstant.LowSurrogateStartValue)
                                    + JsonSharedConstant.UnicodePlane01StartValue;

                                offset += 12;
                            }
                            else
                            {
                                offset += 6;
                            }

                            EncodeToUtf8Bytes((uint)scalar, ref destSpace, ref written);

                            from = index;
                            continue;

                        default:
                            ThrowHelper.ThrowJsonParserException(JsonParserException.ParserError.InvalidSymbol, index);
                            break;
                    }

                    destination[written++] = unescaped;
                    offset += 2;
                    from = index;
                }
                else
                {
                    index++;
                    offset += 1;
                }
            }

            if ((uint)from < nSrcLen) // still data to copy
            {
                var remaining = utf8Source.Slice(from);
                remaining.CopyTo(destination.Slice(written));
                written += remaining.Length;
            }
        }

        /// <summary>Copies the UTF-8 code unit representation of this scalar to an output buffer.
        /// The buffer must be large enough to hold the required number of <see cref="byte"/>s.</summary>
        private static void EncodeToUtf8Bytes(uint scalar, ref byte utf8Destination, ref int written)
        {
            Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));

            IntPtr offset = (IntPtr)written;
            if (scalar < 0x80U)
            {
                // Single UTF-8 code unit
                Unsafe.AddByteOffset(ref utf8Destination, offset) = (byte)scalar;
                written++;
            }
            else if (scalar < 0x800U)
            {
                // Two UTF-8 code units
                Unsafe.AddByteOffset(ref utf8Destination, offset) = (byte)(0xC0U | (scalar >> 6));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 1) = (byte)(0x80U | (scalar & 0x3FU));
                written += 2;
            }
            else if (scalar < 0x10000U)
            {
                // Three UTF-8 code units
                Unsafe.AddByteOffset(ref utf8Destination, offset) = (byte)(0xE0U | (scalar >> 12));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 1) = (byte)(0x80U | ((scalar >> 6) & 0x3FU));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 2) = (byte)(0x80U | (scalar & 0x3FU));
                written += 3;
            }
            else
            {
                // Four UTF-8 code units
                Unsafe.AddByteOffset(ref utf8Destination, offset) = (byte)(0xF0U | (scalar >> 18));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 1) = (byte)(0x80U | ((scalar >> 12) & 0x3FU));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 2) = (byte)(0x80U | ((scalar >> 6) & 0x3FU));
                Unsafe.AddByteOffset(ref utf8Destination, offset + 3) = (byte)(0x80U | (scalar & 0x3FU));
                written += 4;
            }
        }
    }
}
