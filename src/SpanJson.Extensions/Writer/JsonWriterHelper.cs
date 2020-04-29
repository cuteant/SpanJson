// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SpanJson.Internal;

namespace SpanJson
{
    internal static partial class JsonWriterHelper
    {
        // Depending on OS, either '\r\n' OR '\n'
        public static readonly int NewLineLength = Environment.NewLine.Length;

        public const int DefaultGrowthSize = 4096;
        public const int InitialGrowthSize = 256;

        public static void WriteIndentation(ref byte output, int indent, ref int pos)
        {
            Debug.Assert(indent % JsonSharedConstant.SpacesPerIndent == 0);
            //Debug.Assert(buffer.Length >= indent);

            // Based on perf tests, the break-even point where vectorized Fill is faster
            // than explicitly writing the space in a loop is 8.
            if (indent < 8)
            {
                int i = 0;
                IntPtr offset = (IntPtr)pos;
                while (i < indent)
                {
                    Unsafe.AddByteOffset(ref output, offset + i++) = JsonUtf8Constant.Space;
                    Unsafe.AddByteOffset(ref output, offset + i++) = JsonUtf8Constant.Space;
                }
            }
            else
            {
                Unsafe.InitBlockUnaligned(ref Unsafe.Add(ref output, pos), JsonUtf8Constant.Space, unchecked((uint)indent));
            }
            pos += indent;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateProperty(in ReadOnlySpan<byte> propertyName)
        {
            if (propertyName.Length > JsonSharedConstant.MaxUnescapedTokenSize)
                SysJsonThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateValue(in ReadOnlySpan<byte> value)
        {
            if (value.Length > JsonSharedConstant.MaxUnescapedTokenSize)
                SysJsonThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateBytes(in ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > JsonSharedConstant.MaxBase64ValueTokenSize)
                SysJsonThrowHelper.ThrowArgumentException_ValueTooLarge(bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateDouble(double value)
        {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            if (!double.IsFinite(value))
#else
            if (double.IsNaN(value) || double.IsInfinity(value))
#endif
            {
                SysJsonThrowHelper.ThrowArgumentException_ValueNotSupported();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateSingle(float value)
        {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            if (!float.IsFinite(value))
#else
            if (float.IsNaN(value) || float.IsInfinity(value))
#endif
            {
                SysJsonThrowHelper.ThrowArgumentException_ValueNotSupported();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateProperty(in ReadOnlySpan<char> propertyName)
        {
            if (propertyName.Length > JsonSharedConstant.MaxCharacterTokenSize)
                SysJsonThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateValue(in ReadOnlySpan<char> value)
        {
            if (value.Length > JsonSharedConstant.MaxCharacterTokenSize)
                SysJsonThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(in ReadOnlySpan<char> propertyName, in ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > JsonSharedConstant.MaxCharacterTokenSize || value.Length > JsonSharedConstant.MaxUnescapedTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(in ReadOnlySpan<byte> propertyName, in ReadOnlySpan<char> value)
        {
            if (propertyName.Length > JsonSharedConstant.MaxUnescapedTokenSize || value.Length > JsonSharedConstant.MaxCharacterTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(in ReadOnlySpan<byte> propertyName, in ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > JsonSharedConstant.MaxUnescapedTokenSize || value.Length > JsonSharedConstant.MaxUnescapedTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(in ReadOnlySpan<char> propertyName, in ReadOnlySpan<char> value)
        {
            if (propertyName.Length > JsonSharedConstant.MaxCharacterTokenSize || value.Length > JsonSharedConstant.MaxCharacterTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndBytes(in ReadOnlySpan<char> propertyName, in ReadOnlySpan<byte> bytes)
        {
            if (propertyName.Length > JsonSharedConstant.MaxCharacterTokenSize || bytes.Length > JsonSharedConstant.MaxBase64ValueTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndBytes(in ReadOnlySpan<byte> propertyName, in ReadOnlySpan<byte> bytes)
        {
            if (propertyName.Length > JsonSharedConstant.MaxUnescapedTokenSize || bytes.Length > JsonSharedConstant.MaxBase64ValueTokenSize)
                SysJsonThrowHelper.ThrowArgumentException(propertyName, bytes);
        }

        internal static void ValidateNumber(in ReadOnlySpan<byte> utf8FormattedNumber)
        {
            // This is a simplified version of the number reader from Utf8JsonReader.TryGetNumber,
            // because it doesn't need to deal with "NeedsMoreData", or remembering the format.
            //
            // The Debug.Asserts in this method should change to validated ArgumentExceptions if/when
            // writing a formatted number becomes public API.
            Debug.Assert(!utf8FormattedNumber.IsEmpty);

            int i = 0;

            if (utf8FormattedNumber[i] == '-')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }
            }

            if (utf8FormattedNumber[i] == '0')
            {
                i++;
            }
            else
            {
                while (i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]))
                {
                    i++;
                }
            }

            if (i == utf8FormattedNumber.Length)
            {
                return;
            }

            // The non digit character inside the number
            byte val = utf8FormattedNumber[i];

            if (val == '.')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }

                while (i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]))
                {
                    i++;
                }

                if (i == utf8FormattedNumber.Length)
                {
                    return;
                }

                Debug.Assert(i < utf8FormattedNumber.Length);
                val = utf8FormattedNumber[i];
            }

            if (val == 'e' || val == 'E')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }

                val = utf8FormattedNumber[i];

                if (val == '+' || val == '-')
                {
                    i++;
                }
            }
            else
            {
                throw new ArgumentException(
                    SR.Format(SR.ExpectedEndOfDigitNotFound, SysJsonThrowHelper.GetPrintableString(val)),
                    nameof(utf8FormattedNumber));
            }

            if (utf8FormattedNumber.Length <= i)
            {
                throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
            }

            while (i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]))
            {
                i++;
            }

            if (i != utf8FormattedNumber.Length)
            {
                throw new ArgumentException(
                    SR.Format(SR.ExpectedEndOfDigitNotFound, SysJsonThrowHelper.GetPrintableString(utf8FormattedNumber[i])),
                    nameof(utf8FormattedNumber));
            }
        }
    }
}
