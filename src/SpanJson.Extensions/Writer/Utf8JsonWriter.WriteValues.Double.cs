// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace SpanJson
{
    partial struct Utf8JsonWriter
    {
        /// <summary>
        /// Writes the <see cref="double"/> value (as a JSON number) as an element of a JSON array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> on .NET Core 3 or higher
        /// and 'G17' on any other framework.
        /// </remarks>
        public void WriteNumberValue(double value)
        {
            JsonWriterHelper.ValidateDouble(value);

            ValidateWritingValue();
            if (_options.Indented)
            {
                WriteNumberValueIndented(value);
            }
            else
            {
                WriteNumberValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = JsonTokenType.Number;
        }

        private void WriteNumberValueMinimized(double value)
        {
            int maxRequired = JsonSharedConstant.MaximumFormatDoubleLength + 1; // Optionally, 1 list separator

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }

            bool result = TryFormatDouble(value, FreeSpan, out int bytesWritten);
            Debug.Assert(result);
            pos += bytesWritten;
        }

        private void WriteNumberValueIndented(double value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= 2 * JsonSharedConstant.MaxWriterDepth);

            int maxRequired = indent + JsonSharedConstant.MaximumFormatDoubleLength + 1 + JsonWriterHelper.NewLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

            ref var pos = ref _pos;
            EnsureUnsafe(pos, maxRequired);

            ref byte output = ref PinnableAddress;

            if ((uint)_currentDepth > JsonSharedConstant.TooBigOrNegative)
            {
                Unsafe.Add(ref output, pos++) = JsonUtf8Constant.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(ref output, ref pos);
                }
                JsonWriterHelper.WriteIndentation(ref output, indent, ref pos);
            }

            bool result = TryFormatDouble(value, FreeSpan, out int bytesWritten);
            Debug.Assert(result);
            pos += bytesWritten;
        }

        private static bool TryFormatDouble(double value, Span<byte> destination, out int bytesWritten)
        {
            // Frameworks that are not .NET Core 3.0 or higher do not produce roundtrippable strings by
            // default. Further, the Utf8Formatter on older frameworks does not support taking a precision
            // specifier for 'G' nor does it represent other formats such as 'R'. As such, we duplicate
            // the .NET Core 3.0 logic of forwarding to the UTF16 formatter and transcoding it back to UTF8,
            // with some additional changes to remove dependencies on Span APIs which don't exist downlevel.

#if NETCOREAPP_2_X_GREATER || NETSTANDARD_2_0_GREATER
            return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
#else
            const string FormatString = "G17";

            string utf16Text = value.ToString(FormatString, CultureInfo.InvariantCulture);

            // Copy the value to the destination, if it's large enough.

            if (utf16Text.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(utf16Text);

                if (bytes.Length > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                bytes.CopyTo(destination);
                bytesWritten = bytes.Length;

                return true;
            }
            catch
            {
                bytesWritten = 0;
                return false;
            }
#endif
        }
    }
}
