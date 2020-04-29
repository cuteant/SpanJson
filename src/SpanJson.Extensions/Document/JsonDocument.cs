// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using CuteAnt;
using SpanJson.Internal;

namespace SpanJson.Document
{
    /// <summary>
    ///   Provides a mechanism for examining the structural content of a JSON value without
    ///   automatically instantiating data values.
    /// </summary>
    /// <remarks>
    ///   This class utilizes resources from pooled memory to minimize the garbage collector (GC)
    ///   impact in high-usage scenarios. Failure to properly Dispose this object will result in
    ///   the memory not being returned to the pool, which will cause an increase in GC impact across
    ///   various parts of the framework.
    /// </remarks>
    public sealed partial class JsonDocument : IDisposable
    {
        private ReadOnlyMemory<byte> _utf8Json;
        private MetadataDb _parsedData;
        private byte[] _extraRentedBytes;
        private (int, string) _lastIndexAndString = (-1, null);

        internal bool IsDisposable { get; }

        /// <summary>
        ///   The <see cref="JsonElement"/> representing the value of the document.
        /// </summary>
        public JsonElement RootElement => new JsonElement(this, 0);

        /// <summary>
        ///   Gets the original input data backing this value, returning it as a <see cref="T:ReadOnlySpan{byte}"/>.
        /// </summary>
        /// <returns>
        ///  The original input data backing this value, returning it as a <see cref="T:ReadOnlySpan{byte}"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public ReadOnlySpan<byte> RawSpan
        {
            get
            {
                CheckNotDisposed();
                return _utf8Json.Span;
            }
        }

        /// <summary>
        ///   Gets the original input data backing this value, returning it as a <see cref="T:ReadOnlyMemory{byte}"/>.
        /// </summary>
        /// <returns>
        ///  The original input data backing this value, returning it as a <see cref="T:ReadOnlyMemory{byte}"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public ReadOnlyMemory<byte> RawMemory
        {
            get
            {
                CheckNotDisposed();
                return _utf8Json;
            }
        }

        private JsonDocument(
            in ReadOnlyMemory<byte> utf8Json,
            MetadataDb parsedData,
            byte[] extraRentedBytes,
            bool isDisposable = true)
        {
            Debug.Assert(!utf8Json.IsEmpty);

            _utf8Json = utf8Json;
            _parsedData = parsedData;
            _extraRentedBytes = extraRentedBytes;

            IsDisposable = isDisposable;

            // extraRentedBytes better be null if we're not disposable.
            Debug.Assert(isDisposable || extraRentedBytes is null);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            int length = _utf8Json.Length;
            if (0u >= (uint)length || !IsDisposable)
            {
                return;
            }

            _parsedData.Dispose();
            _utf8Json = ReadOnlyMemory<byte>.Empty;

            // When "extra rented bytes exist" they contain the document,
            // and thus need to be cleared before being returned.
            byte[] extraRentedBytes = Interlocked.Exchange(ref _extraRentedBytes, null);

            if (extraRentedBytes is object)
            {
                //extraRentedBytes.AsSpan(0, length).Clear();
                ArrayPool<byte>.Shared.Return(extraRentedBytes);
            }
        }

        /// <summary>
        ///  Write the document into the provided writer as a JSON value.
        /// </summary>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   This <see cref="RootElement"/>'s <see cref="JsonElement.ValueKind"/> would result in an invalid JSON.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(ref Utf8JsonWriter writer)
        {
            RootElement.WriteTo(ref writer);
        }

        internal JsonTokenType GetJsonTokenType(int index)
        {
            CheckNotDisposed();

            return _parsedData.GetJsonTokenType(index);
        }

        internal int GetArrayLength(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.BeginArray, row.TokenType);

            return row.SizeOrLength;
        }

        internal JsonElement GetArrayIndexElement(int currentIndex, int arrayIndex)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(currentIndex);

            CheckExpectedType(JsonTokenType.BeginArray, row.TokenType);

            int arrayLength = row.SizeOrLength;

            if ((uint)arrayIndex >= (uint)arrayLength)
            {
                throw ThrowHelper2.GetIndexOutOfRangeException();
            }

            if (!row.HasComplexChildren)
            {
                // Since we wouldn't be here without having completed the document parse, and we
                // already vetted the index against the length, this new index will always be
                // within the table.
                return new JsonElement(this, currentIndex + ((arrayIndex + 1) * DbRow.Size));
            }

            int elementCount = 0;
            int objectOffset = currentIndex + DbRow.Size;

            for (; objectOffset < _parsedData.Length; objectOffset += DbRow.Size)
            {
                if (arrayIndex == elementCount)
                {
                    return new JsonElement(this, objectOffset);
                }

                row = _parsedData.Get(objectOffset);

                if (!row.IsSimpleValue)
                {
                    objectOffset += DbRow.Size * row.NumberOfRows;
                }

                elementCount++;
            }

            Debug.Fail(
                $"Ran out of database searching for array index {arrayIndex} from {currentIndex} when length was {arrayLength}");
            throw ThrowHelper2.GetIndexOutOfRangeException();
        }

        internal int GetEndIndex(int index, bool includeEndElement)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                return index + DbRow.Size;
            }

            int endIndex = index + DbRow.Size * row.NumberOfRows;

            if (includeEndElement)
            {
                endIndex += DbRow.Size;
            }

            return endIndex;
        }

        internal ReadOnlyMemory<byte> GetRawValue(int index, bool includeQuotes)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                if (includeQuotes && row.TokenType == JsonTokenType.String)
                {
                    // Start one character earlier than the value (the open quote)
                    // End one character after the value (the close quote)
                    return _utf8Json.Slice(row.Location - 1, row.SizeOrLength + 2);
                }

                return _utf8Json.Slice(row.Location, row.SizeOrLength);
            }

            int endElementIdx = GetEndIndex(index, includeEndElement: false);
            int start = row.Location;
            row = _parsedData.Get(endElementIdx);
            return _utf8Json.Slice(start, row.Location - start + row.SizeOrLength);
        }

        internal ReadOnlyMemory<byte> GetPropertyRawValue(int valueIndex)
        {
            CheckNotDisposed();

            // The property name is stored one row before the value
            DbRow row = _parsedData.Get(valueIndex - DbRow.Size);
            Debug.Assert(row.TokenType == JsonTokenType.PropertyName);

            // Subtract one for the open quote.
            int start = row.Location - 1;
            int end;

            row = _parsedData.Get(valueIndex);

            if (row.IsSimpleValue)
            {
                end = row.Location + row.SizeOrLength;

                // If the value was a string, pick up the terminating quote.
                if (row.TokenType == JsonTokenType.String)
                {
                    end++;
                }

                return _utf8Json.Slice(start, end - start);
            }

            int endElementIdx = GetEndIndex(valueIndex, includeEndElement: false);
            row = _parsedData.Get(endElementIdx);
            end = row.Location + row.SizeOrLength;
            return _utf8Json.Slice(start, end - start);
        }

        internal string GetString(int index, JsonTokenType expectedType)
        {
            CheckNotDisposed();

            (int lastIdx, string lastString) = _lastIndexAndString;

            if (lastIdx == index)
            {
                return lastString;
            }

            DbRow row = _parsedData.Get(index);

            JsonTokenType tokenType = row.TokenType;

            if (tokenType == JsonTokenType.Null)
            {
                return null;
            }

            CheckExpectedType(expectedType, tokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (row.HasComplexChildren)
            {
                int backslash = segment.IndexOf(JsonUtf8Constant.BackSlash);
                lastString = JsonReaderHelper.GetUnescapedString(segment, backslash);
            }
            else
            {
                lastString = JsonReaderHelper.TranscodeHelper(segment);
            }

            _lastIndexAndString = (index, lastString);
            return lastString;
        }

        internal bool TextEquals(int index, in ReadOnlySpan<char> otherText, bool isPropertyName)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? index - DbRow.Size : index;

            (int lastIdx, string lastString) = _lastIndexAndString;

            if (lastIdx == matchIndex)
            {
                return otherText.SequenceEqual(lastString.AsSpan());
            }

            byte[] otherUtf8TextArray = null;

            int length = checked(otherText.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding);
            Span<byte> otherUtf8Text = (uint)length <= JsonSharedConstant.StackallocThreshold ?
                stackalloc byte[JsonSharedConstant.StackallocMaxLength] :
                (otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length));

            OperationStatus status = TextEncodings.Utf16.ToUtf8(otherText,
                ref MemoryMarshal.GetReference(otherUtf8Text), otherUtf8Text.Length, out int consumed, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            bool result;
            if (status > OperationStatus.DestinationTooSmall)   // Equivalent to: (status == NeedMoreData || status == InvalidData)
            {
                result = false;
            }
            else
            {
                Debug.Assert(status == OperationStatus.Done);
                Debug.Assert(consumed == otherText.Length);

                result = TextEquals(index, otherUtf8Text.Slice(0, written), isPropertyName);
            }

            if (otherUtf8TextArray is object)
            {
                //otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        internal bool TextEquals(int index, in ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? index - DbRow.Size : index;

            DbRow row = _parsedData.Get(matchIndex);

            CheckExpectedType(
                isPropertyName ? JsonTokenType.PropertyName : JsonTokenType.String,
                row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if ((uint)otherUtf8Text.Length > (uint)segment.Length)
            {
                return false;
            }

            if (row.HasComplexChildren)
            {
                if (otherUtf8Text.Length < segment.Length / JsonSharedConstant.MaxExpansionFactorWhileEscaping)
                {
                    return false;
                }

                int idx = segment.IndexOf(JsonUtf8Constant.BackSlash);
                Debug.Assert(idx != -1);

                if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
                {
                    return false;
                }

                return JsonReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
            }

            return segment.SequenceEqual(otherUtf8Text);
        }

        internal string GetNameOfPropertyValue(int index)
        {
            // The property name is one row before the property value
            return GetString(index - DbRow.Size, JsonTokenType.PropertyName);
        }

        internal bool TryGetValue(int index, out byte[] value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                int idx = segment.IndexOf(JsonUtf8Constant.BackSlash);
                Debug.Assert(idx != -1);
                return JsonReaderHelper.TryGetUnescapedBase64Bytes(segment, idx, out value);
            }

            Debug.Assert(segment.IndexOf(JsonUtf8Constant.BackSlash) == -1);
            return JsonReaderHelper.TryDecodeBase64(segment, out value);
        }

        internal bool TryGetValue(int index, out sbyte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out byte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out byte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out short value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out short tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out ushort value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out ushort tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out int value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out int tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out uint value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out uint tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out long value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out long tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out ulong value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out ulong tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out double value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            char standardFormat = row.HasComplexChildren ? JsonSharedConstant.ScientificNotationFormat : default;

            if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed, standardFormat) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out float value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            char standardFormat = row.HasComplexChildren ? JsonSharedConstant.ScientificNotationFormat : default;

            if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed, standardFormat) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out decimal value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            char standardFormat = row.HasComplexChildren ? JsonSharedConstant.ScientificNotationFormat : default;

            if (Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed, standardFormat) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out DateTime value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(segment.Length))
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return JsonReaderHelper.TryGetEscapedDateTime(segment, out value);
            }

            Debug.Assert(segment.IndexOf(JsonUtf8Constant.BackSlash) == -1);

            if (segment.Length <= JsonSharedConstant.MaximumDateTimeOffsetParseLength
                && JsonReaderHelper.TryParseAsISO(segment, out DateTime tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        internal bool TryGetValue(int index, out DateTimeOffset value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(segment.Length))
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return JsonReaderHelper.TryGetEscapedDateTimeOffset(segment, out value);
            }

            Debug.Assert(segment.IndexOf(JsonUtf8Constant.BackSlash) == -1);

            if (segment.Length <= JsonSharedConstant.MaximumDateTimeOffsetParseLength
                && JsonReaderHelper.TryParseAsISO(segment, out DateTimeOffset tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        internal bool TryGetValue(int index, out Guid value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (segment.Length > JsonSharedConstant.MaximumEscapedGuidLength)
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return JsonReaderHelper.TryGetEscapedGuid(segment, out value);
            }

            Debug.Assert(segment.IndexOf(JsonUtf8Constant.BackSlash) == -1);

            if (segment.Length == JsonSharedConstant.MaximumFormatGuidLength
                && Utf8Parser.TryParse(segment, out Guid tmp, out _, 'D'))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        internal bool TryGetValue(int index, out CombGuid value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (segment.Length > JsonSharedConstant.MaximumEscapedGuidLength)
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return JsonReaderHelper.TryGetEscapedCombGuid(segment, out value);
            }

            Debug.Assert(segment.IndexOf(JsonUtf8Constant.BackSlash) == -1);

            if (segment.Length == JsonSharedConstant.MaximumFormatGuidLength &&
#if NETSTANDARD2_0 || NET471 || NET451
                CombGuid.TryParse(TextEncodings.Utf8.GetString(segment), CombGuidSequentialSegmentType.Comb, out CombGuid tmp)
#else
                CombGuid.TryParse(segment, CombGuidSequentialSegmentType.Comb, out CombGuid tmp, out _)
#endif
                )
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        internal string GetRawValueAsString(int index)
        {
            ReadOnlyMemory<byte> segment = GetRawValue(index, includeQuotes: true);
            return JsonReaderHelper.TranscodeHelper(segment.Span);
        }

        internal string GetPropertyRawValueAsString(int valueIndex)
        {
            ReadOnlyMemory<byte> segment = GetPropertyRawValue(valueIndex);
            return JsonReaderHelper.TranscodeHelper(segment.Span);
        }

        internal JsonElement CloneElement(int index)
        {
            int endIndex = GetEndIndex(index, true);
            MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
            ReadOnlyMemory<byte> segment = GetRawValue(index, includeQuotes: true);
            ReadOnlyMemory<byte> segmentCopy = _extraRentedBytes is null ? segment : segment.ToArray();

            JsonDocument newDocument =
                new JsonDocument(segmentCopy, newDb, extraRentedBytes: null, isDisposable: false);

            return newDocument.RootElement;
        }

        internal void WriteElementTo(
            int index,
            ref Utf8JsonWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            switch (row.TokenType)
            {
                case JsonTokenType.BeginObject:
                    writer.WriteStartObject();
                    WriteComplexElement(index, ref writer);
                    return;
                case JsonTokenType.BeginArray:
                    writer.WriteStartArray();
                    WriteComplexElement(index, ref writer);
                    return;
                case JsonTokenType.String:
                    WriteString(row, ref writer);
                    return;
                case JsonTokenType.Number:
                    writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
                    return;
                case JsonTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    return;
                case JsonTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    return;
                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    return;
            }

            Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
        }

        private void WriteComplexElement(int index, ref Utf8JsonWriter writer)
        {
            int endIndex = GetEndIndex(index, true);

            for (int i = index + DbRow.Size; i < endIndex; i += DbRow.Size)
            {
                DbRow row = _parsedData.Get(i);

                // All of the types which don't need the value span
                switch (row.TokenType)
                {
                    case JsonTokenType.String:
                        WriteString(row, ref writer);
                        continue;
                    case JsonTokenType.Number:
                        writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
                        continue;
                    case JsonTokenType.True:
                        writer.WriteBooleanValue(value: true);
                        continue;
                    case JsonTokenType.False:
                        writer.WriteBooleanValue(value: false);
                        continue;
                    case JsonTokenType.Null:
                        writer.WriteNullValue();
                        continue;
                    case JsonTokenType.BeginObject:
                        writer.WriteStartObject();
                        continue;
                    case JsonTokenType.EndObject:
                        writer.WriteEndObject();
                        continue;
                    case JsonTokenType.BeginArray:
                        writer.WriteStartArray();
                        continue;
                    case JsonTokenType.EndArray:
                        writer.WriteEndArray();
                        continue;
                    case JsonTokenType.PropertyName:
                        WritePropertyName(row, ref writer);
                        continue;
                }

                Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
            }
        }

        private ReadOnlySpan<byte> UnescapeString(in DbRow row, out ArraySegment<byte> rented)
        {
            Debug.Assert(row.TokenType == JsonTokenType.String || row.TokenType == JsonTokenType.PropertyName);
            int loc = row.Location;
            int length = row.SizeOrLength;
            ReadOnlySpan<byte> text = _utf8Json.Slice(loc, length).Span;

            if (!row.HasComplexChildren)
            {
                rented = default;
                return text;
            }

            int idx = text.IndexOf(JsonUtf8Constant.BackSlash);
            Debug.Assert(idx >= 0);

            byte[] rent = ArrayPool<byte>.Shared.Rent(length);
            text.Slice(0, idx).CopyTo(rent);

            JsonReaderHelper.Unescape(text, rent, idx, out int written);
            rented = new ArraySegment<byte>(rent, 0, written);
            return rented.AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClearAndReturn(ArraySegment<byte> rented)
        {
            if (rented.Array is object)
            {
                //rented.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(rented.Array);
            }
        }

        private void WritePropertyName(in DbRow row, ref Utf8JsonWriter writer)
        {
            ArraySegment<byte> rented = default;

            try
            {
                writer.WritePropertyName(UnescapeString(row, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }

        private void WriteString(in DbRow row, ref Utf8JsonWriter writer)
        {
            ArraySegment<byte> rented = default;

            try
            {
                var utf8Value = UnescapeString(row, out rented);
                var str = JsonReaderHelper.GetTextFromUtf8(utf8Value);
                writer.WriteStringValue(utf8Value);
            }
            finally
            {
                ClearAndReturn(rented);

            }
        }


        private static void ParseCore(
            ReadOnlySpan<byte> utf8JsonSpan,
            Utf8JsonReader reader,
            ref MetadataDb database,
            ref StackRowStack stack)
        {
            bool inArray = false;
            int arrayItemsCount = 0;
            int numberOfRowsForMembers = 0;
            int numberOfRowsForValues = 0;

            while (reader.Read())
            {
                JsonTokenType tokenType = reader.TokenType;

                // Since the input payload is contained within a Span, 
                // token start index can never be larger than int.MaxValue (i.e. utf8JsonSpan.Length).
                Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
                int tokenStart = (int)reader.TokenStartIndex;

                switch (tokenType)
                {
                    case JsonTokenType.BeginObject:
                        {
                            if (inArray) { arrayItemsCount++; }

                            numberOfRowsForValues++;
                            database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                            var row = new StackRow(numberOfRowsForMembers + 1);
                            stack.Push(row);
                            numberOfRowsForMembers = 0;
                            break;
                        }
                    case JsonTokenType.EndObject:
                        {
                            int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.BeginObject);

                            numberOfRowsForValues++;
                            numberOfRowsForMembers++;
                            database.SetLength(rowIndex, numberOfRowsForMembers);

                            int newRowIndex = database.Length;
                            database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                            database.SetNumberOfRows(rowIndex, numberOfRowsForMembers);
                            database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                            StackRow row = stack.Pop();
                            numberOfRowsForMembers += row.SizeOrLength;
                            break;
                        }
                    case JsonTokenType.BeginArray:
                        {
                            if (inArray) { arrayItemsCount++; }

                            numberOfRowsForMembers++;
                            database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                            var row = new StackRow(arrayItemsCount, numberOfRowsForValues + 1);
                            stack.Push(row);
                            arrayItemsCount = 0;
                            numberOfRowsForValues = 0;
                            break;
                        }
                    case JsonTokenType.EndArray:
                        {
                            int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.BeginArray);

                            numberOfRowsForValues++;
                            numberOfRowsForMembers++;
                            database.SetLength(rowIndex, arrayItemsCount);
                            database.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                            // If the array item count is (e.g.) 12 and the number of rows is (e.g.) 13
                            // then the extra row is just this EndArray item, so the array was made up
                            // of simple values.
                            //
                            // If the off-by-one relationship does not hold, then one of the values was
                            // more than one row, making it a complex object.
                            //
                            // This check is similar to tracking the start array and painting it when
                            // StartObject or StartArray is encountered, but avoids the mixed state
                            // where "UnknownSize" implies "has complex children".
                            if (arrayItemsCount + 1 != numberOfRowsForValues)
                            {
                                database.SetHasComplexChildren(rowIndex);
                            }

                            int newRowIndex = database.Length;
                            database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                            database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                            StackRow row = stack.Pop();
                            arrayItemsCount = row.SizeOrLength;
                            numberOfRowsForValues += row.NumberOfRows;
                            break;
                        }
                    case JsonTokenType.PropertyName:
                        {
                            numberOfRowsForValues++;
                            numberOfRowsForMembers++;

                            // Adding 1 to skip the start quote will never overflow
                            Debug.Assert(tokenStart < int.MaxValue);

                            database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                            if (reader._stringHasEscaping)
                            {
                                database.SetHasComplexChildren(database.Length - DbRow.Size);
                            }

                            Debug.Assert(!inArray);
                            break;
                        }
                    default:
                        {
                            Debug.Assert(tokenType >= JsonTokenType.String && tokenType <= JsonTokenType.Null);
                            numberOfRowsForValues++;
                            numberOfRowsForMembers++;

                            if (inArray)
                            {
                                arrayItemsCount++;
                            }

                            if (tokenType == JsonTokenType.String)
                            {
                                // Adding 1 to skip the start quote will never overflow
                                Debug.Assert(tokenStart < int.MaxValue);

                                database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                                if (reader._stringHasEscaping)
                                {
                                    database.SetHasComplexChildren(database.Length - DbRow.Size);
                                }
                            }
                            else
                            {
                                database.Append(tokenType, tokenStart, reader.ValueSpan.Length);

                                if (tokenType == JsonTokenType.Number)
                                {
                                    switch (reader._numberFormat)
                                    {
                                        case JsonSharedConstant.ScientificNotationFormat:
                                            database.SetHasComplexChildren(database.Length - DbRow.Size);
                                            break;
                                        default:
                                            Debug.Assert(
                                                reader._numberFormat == default,
                                                $"Unhandled numeric format {reader._numberFormat}");
                                            break;
                                    }
                                }
                            }
                            break;
                        }
                }

                inArray = reader.IsInArray;
            }

            Debug.Assert(reader.BytesConsumed == utf8JsonSpan.Length);
            database.TrimExcess();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckNotDisposed()
        {
            if (_utf8Json.IsEmpty) { ThrowObjectDisposedException(); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowObjectDisposedException()
        {
            throw GetObjectDisposedException();
            static ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException(nameof(JsonDocument)); ;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckExpectedType(JsonTokenType expected, JsonTokenType actual)
        {
            if (expected != actual)
            {
                throw SysJsonThrowHelper.GetJsonElementWrongTypeException(expected, actual);
            }
        }

        private static void CheckSupportedOptions(
            JsonReaderOptions readerOptions,
            ExceptionArgument paramName)
        {
            // Since these are coming from a valid instance of Utf8JsonReader, the JsonReaderOptions must already be valid
            Debug.Assert(readerOptions.CommentHandling >= 0 && readerOptions.CommentHandling <= JsonCommentHandling.Allow);

            if (readerOptions.CommentHandling == JsonCommentHandling.Allow)
            {
                ThrowHelper2.ThrowArgumentException_JsonDocumentDoesNotSupportComments(paramName);
            }
        }
    }
}
