// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SpanJson.Internal;

namespace SpanJson
{
    partial struct Utf8JsonWriter
    {
        private static char[] s_singleLineCommentDelimiter = new char[2] { '*', '/' };
        private static ReadOnlySpan<byte> SingleLineCommentDelimiterUtf8 => new byte[2] { (byte)'*', (byte)'/' };

        /// <summary>
        /// Writes the string text value (as a JSON comment).
        /// </summary>
        /// <param name="value">The value to write as a JSON comment within /*..*/.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given string text value contains a comment delimiter (that is, */).
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="value"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        public void WriteCommentValue(string value)
        {
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
            WriteCommentValue(value.AsSpan());
        }

        /// <summary>
        /// Writes the text value (as a JSON comment).
        /// </summary>
        /// <param name="value">The value to write as a JSON comment within /*..*/.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given text value contains a comment delimiter (that is, */).
        /// </exception>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        public void WriteCommentValue(in ReadOnlySpan<char> value)
        {
            JsonWriterHelper.ValidateValue(value);

            if (value.IndexOf(s_singleLineCommentDelimiter) != -1)
            {
                SysJsonThrowHelper.ThrowArgumentException_InvalidCommentValue();
            }

            WriteCommentByOptions(value);
        }

        private void WriteCommentByOptions(in ReadOnlySpan<char> value)
        {
            if (_options.Indented)
            {
                WriteCommentIndented(value);
            }
            else
            {
                WriteCommentMinimized(value);
            }
        }

        private void WriteCommentMinimized(in ReadOnlySpan<char> value)
        {
            Debug.Assert(value.Length < (int.MaxValue / JsonSharedConstant.MaxExpansionFactorWhileTranscoding) - 4);

            // All ASCII, /*...*/ => escapedValue.Length + 4
            // Optionally, up to 3x growth when transcoding
            int maxRequired = (value.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding) + 4;

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;

            OperationStatus status = TextEncodings.Utf16.ToUtf8(value,
                ref Unsafe.Add(ref output, pos), FreeCapacity, out _, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            pos += written;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
        }

        private void WriteCommentIndented(in ReadOnlySpan<char> value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= 2 * JsonSharedConstant.MaxWriterDepth);

            Debug.Assert(value.Length < (int.MaxValue / JsonSharedConstant.MaxExpansionFactorWhileTranscoding) - indent - 4 - JsonWriterHelper.NewLineLength);

            // All ASCII, /*...*/ => escapedValue.Length + 4
            // Optionally, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (value.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding) + 4 + JsonWriterHelper.NewLineLength;

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(ref output, ref pos);
            }

            JsonWriterHelper.WriteIndentation(ref output, indent, ref pos);

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;

            OperationStatus status = TextEncodings.Utf16.ToUtf8(value,
                ref Unsafe.Add(ref output, pos), FreeCapacity, out _, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            pos += written;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
        }

        /// <summary>
        /// Writes the UTF-8 text value (as a JSON comment).
        /// </summary>
        /// <param name="utf8Value">The UTF-8 encoded value to be written as a JSON comment within /*..*/.</param>
        /// <remarks>
        /// The comment value is not escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large OR if the given UTF-8 text value contains a comment delimiter (that is, */).
        /// </exception>
        public void WriteCommentValue(in ReadOnlySpan<byte> utf8Value)
        {
            JsonWriterHelper.ValidateValue(utf8Value);

            if (utf8Value.IndexOf(SingleLineCommentDelimiterUtf8) != -1)
            {
                SysJsonThrowHelper.ThrowArgumentException_InvalidCommentValue();
            }

            WriteCommentByOptions(utf8Value);
        }

        private void WriteCommentByOptions(in ReadOnlySpan<byte> utf8Value)
        {
            if (_options.Indented)
            {
                WriteCommentIndented(utf8Value);
            }
            else
            {
                WriteCommentMinimized(utf8Value);
            }
        }

        private void WriteCommentMinimized(in ReadOnlySpan<byte> utf8Value)
        {
            int valueLen = utf8Value.Length;
            Debug.Assert(valueLen < int.MaxValue - 4);

            int maxRequired = valueLen + 4; // /*...*/

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;

            BinaryUtil.CopyMemory(ref MemoryMarshal.GetReference(utf8Value), ref Unsafe.Add(ref output, pos), valueLen);
            pos += valueLen;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
        }

        private void WriteCommentIndented(in ReadOnlySpan<byte> utf8Value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= 2 * JsonSharedConstant.MaxWriterDepth);

            int valueLen = utf8Value.Length;
            Debug.Assert(valueLen < int.MaxValue - indent - 4 - JsonWriterHelper.NewLineLength);

            int minRequired = indent + valueLen + 4; // /*...*/
            int maxRequired = minRequired + JsonWriterHelper.NewLineLength; // Optionally, 1-2 bytes for new line

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(ref output, ref pos);
                }
                JsonWriterHelper.WriteIndentation(ref output, indent, ref pos);
            }

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;

            BinaryUtil.CopyMemory(ref MemoryMarshal.GetReference(utf8Value), ref Unsafe.Add(ref output, pos), valueLen);
            pos += valueLen;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Asterisk;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Slash;
        }
    }
}
