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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidatePropertyNameAndDepth(in ReadOnlySpan<char> propertyName)
        {
            if (propertyName.Length > JsonSharedConstant.MaxCharacterTokenSize || CurrentDepth >= JsonSharedConstant.MaxWriterDepth)
                SysJsonThrowHelper.ThrowInvalidOperationOrArgumentException(propertyName, _currentDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidatePropertyNameAndDepth(in ReadOnlySpan<byte> utf8PropertyName)
        {
            if (utf8PropertyName.Length > JsonSharedConstant.MaxUnescapedTokenSize || CurrentDepth >= JsonSharedConstant.MaxWriterDepth)
                SysJsonThrowHelper.ThrowInvalidOperationOrArgumentException(utf8PropertyName, _currentDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateDepth()
        {
            if (CurrentDepth >= JsonSharedConstant.MaxWriterDepth)
                SysJsonThrowHelper.ThrowInvalidOperationException(_currentDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingProperty()
        {
            if (!_options.SkipValidation)
            {
                if (!_inObject || _tokenType == JsonTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != JsonTokenType.BeginObject);
                    SysJsonThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray, currentDepth: default, token: default, _tokenType);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingProperty(byte token)
        {
            if (!_options.SkipValidation)
            {
                if (!_inObject || _tokenType == JsonTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != JsonTokenType.BeginObject);
                    SysJsonThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray, currentDepth: default, token: default, _tokenType);
                }
                UpdateBitStackOnStart(token);
            }
        }

        private void WritePropertyNameMinimized(in ReadOnlySpan<byte> escapedPropertyName, byte token)
        {
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - 5);

            var nameLen = escapedPropertyName.Length;
            int minRequired = nameLen + 4; // 2 quotes, 1 colon, and 1 start token
            int maxRequired = minRequired + 1; // Optionally, 1 list separator

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            BinaryUtil.CopyMemory(ref MemoryMarshal.GetReference(escapedPropertyName), ref Unsafe.Add(ref output, pos), nameLen);
            pos += nameLen;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.KeyValueSeperator;
            Unsafe.Add(ref output, pos++) = token;
        }

        private void WritePropertyNameIndented(in ReadOnlySpan<byte> escapedPropertyName, byte token)
        {
            int indent = Indentation;
            var nameLen = escapedPropertyName.Length;
            Debug.Assert(indent <= 2 * JsonSharedConstant.MaxWriterDepth);

            Debug.Assert(nameLen < int.MaxValue - indent - 6 - JsonWriterHelper.NewLineLength);

            int minRequired = indent + nameLen + 5; // 2 quotes, 1 colon, 1 space, and 1 start token
            int maxRequired = minRequired + 1 + JsonWriterHelper.NewLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);

            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(ref output, ref pos);
            }

            JsonWriterHelper.WriteIndentation(ref output, indent, ref pos);

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            BinaryUtil.CopyMemory(ref MemoryMarshal.GetReference(escapedPropertyName), ref Unsafe.Add(ref output, pos), nameLen);
            pos += nameLen;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.KeyValueSeperator;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Space;
            Unsafe.Add(ref output, pos++) = token;
        }

        private void WritePropertyNameMinimized(in ReadOnlySpan<char> escapedPropertyName, byte token)
        {
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / JsonSharedConstant.MaxExpansionFactorWhileTranscoding) - 5);

            // All ASCII, 2 quotes, 1 colon, and 1 start token => escapedPropertyName.Length + 4
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding) + 5;

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            TranscodeAndWrite(escapedPropertyName, ref output, FreeCapacity, ref pos);

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.KeyValueSeperator;
            Unsafe.Add(ref output, pos++) = token;
        }

        private void WritePropertyNameIndented(in ReadOnlySpan<char> escapedPropertyName, byte token)
        {
            int indent = Indentation;
            Debug.Assert(indent <= 2 * JsonSharedConstant.MaxWriterDepth);

            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / JsonSharedConstant.MaxExpansionFactorWhileTranscoding) - indent - 6 - JsonWriterHelper.NewLineLength);

            // All ASCII, 2 quotes, 1 colon, 1 space, and 1 start token => indent + escapedPropertyName.Length + 5 
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding) + 6 + JsonWriterHelper.NewLineLength;

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);

            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(ref output, ref pos);
            }

            JsonWriterHelper.WriteIndentation(ref output, indent, ref pos);

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            TranscodeAndWrite(escapedPropertyName, ref output, FreeCapacity, ref pos);

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.DoubleQuote;

            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.KeyValueSeperator;
            Unsafe.Add(ref output, pos++) = JsonUtf8Constant.Space;
            Unsafe.Add(ref output, pos++) = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TranscodeAndWrite(in ReadOnlySpan<char> escapedPropertyName, ref byte output, int freeCapacity, ref int pos)
        {
            OperationStatus status = TextEncodings.Utf16.ToUtf8(escapedPropertyName,
                ref Unsafe.Add(ref output, pos), freeCapacity, out int consumed, out int written);
            Debug.Assert(status == OperationStatus.Done);
            Debug.Assert(consumed == escapedPropertyName.Length);
            pos += written;
        }
    }
}
