﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SpanJson.Internal;

namespace SpanJson
{
    /// <summary>
    /// Provides a high-performance API for forward-only, read-only access to the UTF-8 encoded JSON text.
    /// It processes the text sequentially with no caching and adheres strictly to the JSON RFC
    /// by default (https://tools.ietf.org/html/rfc8259). When it encounters invalid JSON, it throws
    /// a JsonException with basic error information like line number and byte position on the line.
    /// Since this type is a ref struct, it does not directly support async. However, it does provide
    /// support for reentrancy to read incomplete data, and continue reading once more data is presented.
    /// To be able to set max depth while reading OR allow skipping comments, create an instance of 
    /// <see cref="JsonReaderState"/> and pass that in to the reader.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public ref partial struct Utf8JsonReader
    {
        private ReadOnlySpan<byte> _buffer;

        private bool _isFinalBlock;
        private bool _isInputSequence;

        private long _lineNumber;
        private long _bytePositionInLine;

        // bytes consumed in the current segment (not token)
        private int _consumed;
        private bool _inObject;
        private bool _isNotPrimitive;
        internal char _numberFormat;
        private JsonTokenType _tokenType;
        private JsonTokenType _previousTokenType;
        private JsonReaderOptions _readerOptions;
        private BitStack _bitStack;

        private long _totalConsumed;
        private bool _isLastSegment;
        internal bool _stringHasEscaping;
        private readonly bool _isMultiSegment;
        private bool _trailingCommaBeforeComment;

        private SequencePosition _nextPosition;
        private SequencePosition _currentPosition;
        private ReadOnlySequence<byte> _sequence;

        private bool IsLastSpan => _isFinalBlock && (!_isMultiSegment || _isLastSegment);

        internal ReadOnlySequence<byte> OriginalSequence => _sequence;

        internal ReadOnlySpan<byte> OriginalSpan => _sequence.IsEmpty ? _buffer : default;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the JSON is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is true, <see cref="ValueSpan"/> contains useless data, likely for
        /// a previous single-segment token. Therefore, only access <see cref="ValueSpan"/> if <see cref="HasValueSequence"/> is false.
        /// Otherwise, the token value must be accessed from <see cref="ValueSequence"/>.
        /// </remarks>
        public ReadOnlySpan<byte> ValueSpan { get; private set; }

        /// <summary>
        /// Returns the total amount of bytes consumed by the <see cref="Utf8JsonReader"/> so far
        /// for the current instance of the <see cref="Utf8JsonReader"/> with the given UTF-8 encoded input text.
        /// </summary>
        public long BytesConsumed
        {
            get
            {
#if DEBUG
                if (!_isInputSequence)
                {
                    Debug.Assert(_totalConsumed == 0);
                }
#endif
                return _totalConsumed + _consumed;
            }
        }

        /// <summary>
        /// Returns the index that the last processed JSON token starts at
        /// within the given UTF-8 encoded input text, skipping any white space.
        /// </summary>
        /// <remarks>
        /// For JSON strings (including property names), this points to before the start quote.
        /// For comments, this points to before the first comment delimiter (i.e. '/').
        /// </remarks>
        public long TokenStartIndex { get; private set; }

        /// <summary>
        /// Tracks the recursive depth of the nested objects / arrays within the JSON text
        /// processed so far. This provides the depth of the current token.
        /// </summary>
        public int CurrentDepth
        {
            get
            {
                int readerDepth = _bitStack.CurrentDepth;
                if (TokenType == JsonTokenType.BeginArray || TokenType == JsonTokenType.BeginObject)
                {
                    Debug.Assert(readerDepth >= 1);
                    readerDepth--;
                }
                return readerDepth;
            }
        }

        internal bool IsInArray => !_inObject;

        /// <summary>
        /// Gets the type of the last processed JSON token in the UTF-8 encoded JSON text.
        /// </summary>
        public JsonTokenType TokenType => _tokenType;

        /// <summary>
        /// Lets the caller know which of the two 'Value' properties to read to get the 
        /// token value. For input data within a ReadOnlySpan&lt;byte&gt; this will
        /// always return false. For input data within a ReadOnlySequence&lt;byte&gt;, this
        /// will only return true if the token value straddles more than a single segment and
        /// hence couldn't be represented as a span.
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Returns the mode of this instance of the <see cref="Utf8JsonReader"/>.
        /// True when the reader was constructed with the input span containing the entire data to process.
        /// False when the reader was constructed knowing that the input span may contain partial data with more data to follow.
        /// </summary>
        public bool IsFinalBlock => _isFinalBlock;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the JSON is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is false, <see cref="ValueSequence"/> contains useless data, likely for
        /// a previous multi-segment token. Therefore, only access <see cref="ValueSequence"/> if <see cref="HasValueSequence"/> is true.
        /// Otherwise, the token value must be accessed from <see cref="ValueSpan"/>.
        /// </remarks>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Returns the current <see cref="SequencePosition"/> within the provided UTF-8 encoded
        /// input ReadOnlySequence&lt;byte&gt;. If the <see cref="Utf8JsonReader"/> was constructed
        /// with a ReadOnlySpan&lt;byte&gt; instead, this will always return a default <see cref="SequencePosition"/>.
        /// </summary>
        public SequencePosition Position
        {
            get
            {
                if (_isInputSequence)
                {
                    Debug.Assert(_currentPosition.GetObject() is object);
                    return _sequence.GetPosition(_consumed, _currentPosition);
                }
                return default;
            }
        }

        /// <summary>
        /// Returns the current snapshot of the <see cref="Utf8JsonReader"/> state which must
        /// be captured by the caller and passed back in to the <see cref="Utf8JsonReader"/> ctor with more data.
        /// Unlike the <see cref="Utf8JsonReader"/>, which is a ref struct, the state can survive
        /// across async/await boundaries and hence this type is required to provide support for reading
        /// in more data asynchronously before continuing with a new instance of the <see cref="Utf8JsonReader"/>.
        /// </summary>
        public JsonReaderState CurrentState => new JsonReaderState
        {
            _lineNumber = _lineNumber,
            _bytePositionInLine = _bytePositionInLine,
            _inObject = _inObject,
            _isNotPrimitive = _isNotPrimitive,
            _numberFormat = _numberFormat,
            _stringHasEscaping = _stringHasEscaping,
            _trailingCommaBeforeComment = _trailingCommaBeforeComment,
            _tokenType = _tokenType,
            _previousTokenType = _previousTokenType,
            _readerOptions = _readerOptions,
            _bitStack = _bitStack,
        };

        /// <summary>
        /// Constructs a new <see cref="Utf8JsonReader"/> instance.
        /// </summary>
        /// <param name="jsonData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded JSON text to process.</param>
        /// <param name="isFinalBlock">True when the input span contains the entire data to process.
        /// Set to false only if it is known that the input span contains partial data with more data to follow.</param>
        /// <param name="state">If this is the first call to the ctor, pass in a default state. Otherwise,
        /// capture the state from the previous instance of the <see cref="Utf8JsonReader"/> and pass that back.</param>
        /// <remarks>
        /// Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        /// This is the reason why the ctor accepts a <see cref="JsonReaderState"/>.
        /// </remarks>
        public Utf8JsonReader(in ReadOnlySpan<byte> jsonData, bool isFinalBlock, JsonReaderState state)
        {
            _buffer = jsonData;

            _isFinalBlock = isFinalBlock;
            _isInputSequence = false;

            _lineNumber = state._lineNumber;
            _bytePositionInLine = state._bytePositionInLine;
            _inObject = state._inObject;
            _isNotPrimitive = state._isNotPrimitive;
            _numberFormat = state._numberFormat;
            _stringHasEscaping = state._stringHasEscaping;
            _trailingCommaBeforeComment = state._trailingCommaBeforeComment;
            _tokenType = state._tokenType;
            _previousTokenType = state._previousTokenType;
            _readerOptions = state._readerOptions;
            if (0u >= (uint)_readerOptions.MaxDepth)
            {
                _readerOptions.MaxDepth = JsonReaderOptions.DefaultMaxDepth;  // If max depth is not set, revert to the default depth.
            }
            _bitStack = state._bitStack;

            _consumed = 0;
            TokenStartIndex = 0;
            _totalConsumed = 0;
            _isLastSegment = _isFinalBlock;
            _isMultiSegment = false;

            ValueSpan = ReadOnlySpan<byte>.Empty;

            _currentPosition = default;
            _nextPosition = default;
            _sequence = default;
            HasValueSequence = false;
            ValueSequence = ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8JsonReader"/> instance.
        /// </summary>
        /// <param name="jsonData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded JSON text to process.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8JsonReader"/>
        /// that is different from the JSON RFC (for example how to handle comments or maximum depth allowed when reading).
        /// By default, the <see cref="Utf8JsonReader"/> follows the JSON RFC strictly (i.e. comments within the JSON are invalid) and reads up to a maximum depth of 64.</param>
        /// <remarks>
        ///   <para>
        ///     Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        ///   </para>
        ///   <para>
        ///     This assumes that the entire JSON payload is passed in (equivalent to <see cref="IsFinalBlock"/> = true)
        ///   </para>
        /// </remarks>
        public Utf8JsonReader(in ReadOnlySpan<byte> jsonData, JsonReaderOptions options = default)
            : this(jsonData, isFinalBlock: true, new JsonReaderState(options))
        {
        }

        /// <summary>
        /// Read the next JSON token from input source.
        /// </summary>
        /// <returns>True if the token was read successfully, else false.</returns>
        /// <exception cref="JsonException">
        /// Thrown when an invalid JSON token is encountered according to the JSON RFC
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        public bool Read()
        {
            bool retVal = _isMultiSegment ? ReadMultiSegment() : ReadSingleSegment();

            if (!retVal)
            {
                if (_isFinalBlock && TokenType == JsonTokenType.None)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedJsonTokens);
                }
            }
            return retVal;
        }

        /// <summary>
        /// Skips the children of the current JSON token.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the reader was given partial data with more data to follow (i.e. <see cref="IsFinalBlock"/> is false).
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when an invalid JSON token is encountered while skipping, according to the JSON RFC,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        /// When <see cref="TokenType"/> is <see cref="JsonTokenType.PropertyName" />, the reader first moves to the property value.
        /// When <see cref="TokenType"/> (originally, or after advancing) is <see cref="JsonTokenType.BeginObject" /> or 
        /// <see cref="JsonTokenType.BeginArray" />, the reader advances to the matching
        /// <see cref="JsonTokenType.EndObject" /> or <see cref="JsonTokenType.EndArray" />.
        /// 
        /// For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        /// the next value (when in an array), the next property name (when in an object), or the end array/object token.
        /// </remarks>
        public void Skip()
        {
            if (!_isFinalBlock)
            {
                throw SysJsonThrowHelper.GetInvalidOperationException_CannotSkipOnPartial();
            }

            SkipHelper();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipHelper()
        {
            Debug.Assert(_isFinalBlock);

            if (TokenType == JsonTokenType.PropertyName)
            {
                bool result = Read();
                // Since _isFinalBlock == true here, and the JSON token is not a primitive value or comment.
                // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                Debug.Assert(result);
            }

            if (TokenType == JsonTokenType.BeginObject || TokenType == JsonTokenType.BeginArray)
            {
                int depth = CurrentDepth;
                do
                {
                    bool result = Read();
                    // Since _isFinalBlock == true here, and the JSON token is not a primitive value or comment.
                    // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                    Debug.Assert(result);
                }
                while (depth < CurrentDepth);
            }
        }

        /// <summary>
        /// Tries to skip the children of the current JSON token.
        /// </summary>
        /// <returns>True if there was enough data for the children to be skipped successfully, else false.</returns>
        /// <exception cref="JsonException">
        /// Thrown when an invalid JSON token is encountered while skipping, according to the JSON RFC,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the reader did not have enough data to completely skip the children of the current token,
        ///     it will be reset to the state it was in before the method was called.
        ///   </para>
        ///   <para>
        ///     When <see cref="TokenType"/> is <see cref="JsonTokenType.PropertyName" />, the reader first moves to the property value.
        ///     When <see cref="TokenType"/> (originally, or after advancing) is <see cref="JsonTokenType.BeginObject" /> or 
        ///     <see cref="JsonTokenType.BeginArray" />, the reader advances to the matching
        ///     <see cref="JsonTokenType.EndObject" /> or <see cref="JsonTokenType.EndArray" />.
        /// 
        ///     For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        ///     the next value (when in an array), the next property name (when in an object), or the end array/object token.
        ///   </para>
        /// </remarks>
        public bool TrySkip()
        {
            if (_isFinalBlock)
            {
                SkipHelper();
                return true;
            }

            return TrySkipHelper();
        }

        private bool TrySkipHelper()
        {
            Debug.Assert(!_isFinalBlock);

            Utf8JsonReader restore = this;

            if (TokenType == JsonTokenType.PropertyName)
            {
                if (!Read())
                {
                    goto Restore;
                }
            }

            if (TokenType == JsonTokenType.BeginObject || TokenType == JsonTokenType.BeginArray)
            {
                int depth = CurrentDepth;
                do
                {
                    if (!Read())
                    {
                        goto Restore;
                    }
                }
                while (depth < CurrentDepth);
            }

            return true;

        Restore:
            this = restore;
            return false;
        }

        /// <summary>
        /// Compares the UTF-8 encoded text to the unescaped JSON token value in the source and returns true if they match.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>True if the JSON token value in the source matches the UTF-8 encoded look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a JSON token that is not a string
        /// (i.e. other than <see cref="JsonTokenType.String"/> or <see cref="JsonTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have 
        ///     invalid UTF-8 within the JSON payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the JSON token value in the source and the look up text is done by first unescaping the JSON value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public bool ValueTextEquals(in ReadOnlySpan<byte> utf8Text)
        {
            if (!IsTokenTypeString(TokenType))
            {
                throw SysJsonThrowHelper.GetInvalidOperationException_ExpectedStringComparison(TokenType);
            }
            return TextEqualsHelper(utf8Text);
        }

        /// <summary>
        /// Compares the string text to the unescaped JSON token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the JSON token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a JSON token that is not a string
        /// (i.e. other than <see cref="JsonTokenType.String"/> or <see cref="JsonTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have 
        ///     invalid UTF-8 within the JSON payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the JSON token value in the source and the look up text is done by first unescaping the JSON value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public bool ValueTextEquals(string text)
        {
            return ValueTextEquals(text.AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TextEqualsHelper(in ReadOnlySpan<byte> otherUtf8Text)
        {
            if (HasValueSequence)
            {
                return CompareToSequence(otherUtf8Text);
            }

            if (_stringHasEscaping)
            {
                return UnescapeAndCompare(otherUtf8Text);
            }

            return otherUtf8Text.SequenceEqual(ValueSpan);
        }

        /// <summary>
        /// Compares the text to the unescaped JSON token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the JSON token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a JSON token that is not a string
        /// (i.e. other than <see cref="JsonTokenType.String"/> or <see cref="JsonTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid or incomplete UTF-16 text (i.e. unpaired surrogates), the method will return false
        ///     since you cannot have invalid UTF-16 within the JSON payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the JSON token value in the source and the look up text is done by first unescaping the JSON value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public bool ValueTextEquals(in ReadOnlySpan<char> text)
        {
            if (!IsTokenTypeString(TokenType))
            {
                throw SysJsonThrowHelper.GetInvalidOperationException_ExpectedStringComparison(TokenType);
            }

            if (MatchNotPossible(text.Length))
            {
                return false;
            }

            byte[] otherUtf8TextArray = null;

            Span<byte> otherUtf8Text;

            int length = checked(text.Length * JsonSharedConstant.MaxExpansionFactorWhileTranscoding);

            if ((uint)length > JsonSharedConstant.StackallocThreshold)
            {
                otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length);
                otherUtf8Text = otherUtf8TextArray;
            }
            else
            {
                // Cannot create a span directly since it gets passed to instance methods on a ref struct.
                unsafe
                {
                    byte* ptr = stackalloc byte[JsonSharedConstant.StackallocMaxLength];
                    otherUtf8Text = new Span<byte>(ptr, JsonSharedConstant.StackallocMaxLength);
                }
            }

            OperationStatus status = TextEncodings.Utf16.ToUtf8(text,
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
                Debug.Assert(consumed == text.Length);

                result = TextEqualsHelper(otherUtf8Text.Slice(0, written));
            }

            if (otherUtf8TextArray is object)
            {
                //otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        private bool CompareToSequence(in ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);

            if (_stringHasEscaping)
            {
                return UnescapeSequenceAndCompare(other);
            }

            ReadOnlySequence<byte> localSequence = ValueSequence;

            Debug.Assert(!localSequence.IsSingleSegment);

            if (localSequence.Length != other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                if (other.Slice(matchedSoFar).StartsWith(span))
                {
                    matchedSoFar += span.Length;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private bool UnescapeAndCompare(in ReadOnlySpan<byte> other)
        {
            Debug.Assert(!HasValueSequence);
            ReadOnlySpan<byte> localSpan = ValueSpan;

            if (localSpan.Length < other.Length || localSpan.Length / JsonSharedConstant.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int idx = localSpan.IndexOf(JsonUtf8Constant.BackSlash);
            Debug.Assert(idx != -1);

            if (!other.StartsWith(localSpan.Slice(0, idx)))
            {
                return false;
            }

            return JsonReaderHelper.UnescapeAndCompare(localSpan.Slice(idx), other.Slice(idx));
        }

        private bool UnescapeSequenceAndCompare(in ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);
            Debug.Assert(!ValueSequence.IsSingleSegment);

            ReadOnlySequence<byte> localSequence = ValueSequence;
            long sequenceLength = localSequence.Length;

            // The JSON token value will at most shrink by 6 when unescaping.
            // If it is still larger than the lookup string, there is no value in unescaping and doing the comparison.
            if (sequenceLength < other.Length || sequenceLength / JsonSharedConstant.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            bool result = false;

            var value = other;
            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                int idx = span.IndexOf(JsonUtf8Constant.BackSlash);

                if (idx != -1)
                {
                    if (!value.Slice(matchedSoFar).StartsWith(span.Slice(0, idx)))
                    {
                        break;
                    }
                    matchedSoFar += idx;

                    value = value.Slice(matchedSoFar);
                    localSequence = localSequence.Slice(matchedSoFar);

                    if (localSequence.IsSingleSegment)
                    {
                        result = JsonReaderHelper.UnescapeAndCompare(localSequence.First.Span, value);
                    }
                    else
                    {
                        result = JsonReaderHelper.UnescapeAndCompare(localSequence, value);
                    }
                    break;
                }

                if (!value.Slice(matchedSoFar).StartsWith(span))
                {
                    break;
                }
                matchedSoFar += span.Length;
            }

            return result;
        }

        // Returns true if the TokenType is a primitive string "value", i.e. PropertyName or String
        // Otherwise, return false.
        private static bool IsTokenTypeString(JsonTokenType tokenType)
        {
            return tokenType == JsonTokenType.PropertyName || tokenType == JsonTokenType.String;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MatchNotPossible(int charTextLength)
        {
            if (HasValueSequence)
            {
                return MatchNotPossibleSequence(charTextLength);
            }

            int sourceLength = ValueSpan.Length;

            // Transcoding from UTF-16 to UTF-8 will change the length by somwhere between 1x and 3x.
            // Unescaping the token value will at most shrink its length by 6x.
            // There is no point incurring the transcoding/unescaping/comparing cost if:
            // - The token value is smaller than charTextLength
            // - The token value needs to be transcoded AND unescaped and it is more than 6x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x, escaping = 6x => 6x factor
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x, but they are represented as a single escaped hex value, \uXXXX => 6x factor
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 4x, but the surrogate pair (2 characters) are represented by 16 bytes \uXXXX\uXXXX => 6x factor
            // - The token value needs to be transcoded, but NOT escaped and it is more than 3x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x,
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x,
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 2x, (surrogate pairs - 2 characters transcode to 4 UTF-8 bytes)

            if (sourceLength < charTextLength
                || sourceLength / (_stringHasEscaping ? JsonSharedConstant.MaxExpansionFactorWhileEscaping : JsonSharedConstant.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MatchNotPossibleSequence(int charTextLength)
        {
            long sourceLength = ValueSequence.Length;

            if (sourceLength < charTextLength
                || sourceLength / (_stringHasEscaping ? JsonSharedConstant.MaxExpansionFactorWhileEscaping : JsonSharedConstant.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        private void StartObject()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ObjectDepthTooLarge);

            _bitStack.PushTrue();

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = JsonTokenType.BeginObject;
            _inObject = true;
        }

        private void EndObject()
        {
            if (!_inObject || _bitStack.CurrentDepth <= 0)
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.MismatchedObjectArray, JsonUtf8Constant.CloseBrace);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            _tokenType = JsonTokenType.EndObject;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        private void StartArray()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse();

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = JsonTokenType.BeginArray;
            _inObject = false;
        }

        private void EndArray()
        {
            if (_inObject || _bitStack.CurrentDepth <= 0)
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.MismatchedObjectArray, JsonUtf8Constant.CloseBracket);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            _tokenType = JsonTokenType.EndArray;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnEndToken()
        {
            _consumed++;
            _bytePositionInLine++;
            _inObject = _bitStack.Pop();
        }

        private bool ReadSingleSegment()
        {
            bool retVal = false;
            ValueSpan = default;

            if (!HasMoreData())
            {
                goto Done;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            // SkipWhiteSpace only skips the whitespace characters as defined by JSON RFC 8259 section 2.
            // We do not validate if 'first' is an invalid JSON byte here (such as control characters).
            // Those cases are captured in ConsumeNextToken and ConsumeValue.
            if (first <= JsonUtf8Constant.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData())
                {
                    goto Done;
                }
                first = _buffer[_consumed];
            }

            TokenStartIndex = _consumed;

            if (_tokenType == JsonTokenType.None)
            {
                goto ReadFirstToken;
            }

            if (first == JsonUtf8Constant.Slash)
            {
                retVal = ConsumeNextTokenOrRollback(first);
                goto Done;
            }

            switch (_tokenType)
            {
                case JsonTokenType.BeginObject:
                    if (first == JsonUtf8Constant.CloseBrace)
                    {
                        EndObject();
                    }
                    else
                    {
                        if (first != JsonUtf8Constant.DoubleQuote)
                        {
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                        }

                        int prevConsumed = _consumed;
                        long prevPosition = _bytePositionInLine;
                        long prevLineNumber = _lineNumber;
                        retVal = ConsumePropertyName();
                        if (!retVal)
                        {
                            // roll back potential changes
                            _consumed = prevConsumed;
                            _tokenType = JsonTokenType.BeginObject;
                            _bytePositionInLine = prevPosition;
                            _lineNumber = prevLineNumber;
                        }
                        goto Done;
                    }
                    break;

                case JsonTokenType.BeginArray:
                    if (first == JsonUtf8Constant.CloseBracket)
                    {
                        EndArray();
                    }
                    else
                    {
                        retVal = ConsumeValue(first);
                        goto Done;
                    }
                    break;

                case JsonTokenType.PropertyName:
                    retVal = ConsumeValue(first);
                    goto Done;

                default:
                    retVal = ConsumeNextTokenOrRollback(first);
                    goto Done;
            }

            retVal = true;

        Done:
            return retVal;

        ReadFirstToken:
            retVal = ReadFirstToken(first);
            goto Done;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreData()
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (_isNotPrimitive && IsLastSpan)
                {
                    if (_bitStack.CurrentDepth != 0)
                    {
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ZeroDepthAtEnd);
                    }

                    if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && _tokenType == JsonTokenType.Comment)
                    {
                        return false;
                    }

                    if (_tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
                    {
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive);
                    }
                }
                return false;
            }
            return true;
        }

        // Unlike the parameter-less overload of HasMoreData, if there is no more data when this method is called, we know the JSON input is invalid.
        // This is because, this method is only called after a ',' (i.e. we expect a value/property name) or after 
        // a property name, which means it must be followed by a value.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreData(ExceptionResource resource)
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (IsLastSpan)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, resource);
                }
                return false;
            }
            return true;
        }

        private bool ReadFirstToken(byte first)
        {
            switch (first)
            {
                case JsonUtf8Constant.OpenBrace:
                    _bitStack.SetFirstBit();
                    _tokenType = JsonTokenType.BeginObject;
                    ValueSpan = _buffer.Slice(_consumed, 1);
                    _consumed++;
                    _bytePositionInLine++;
                    _inObject = true;
                    _isNotPrimitive = true;
                    break;

                case JsonUtf8Constant.OpenBracket:
                    _bitStack.ResetFirstBit();
                    _tokenType = JsonTokenType.BeginArray;
                    ValueSpan = _buffer.Slice(_consumed, 1);
                    _consumed++;
                    _bytePositionInLine++;
                    _isNotPrimitive = true;
                    break;

                default:
                    // Create local copy to avoid bounds checks.
                    ReadOnlySpan<byte> localBuffer = _buffer;

                    if (JsonHelpers.IsDigit(first) || first == '-')
                    {
                        if (!TryGetNumber(localBuffer.Slice(_consumed), out int numberOfBytes))
                        {
                            return false;
                        }
                        _tokenType = JsonTokenType.Number;
                        _consumed += numberOfBytes;
                        _bytePositionInLine += numberOfBytes;
                        return true;
                    }
                    else if (!ConsumeValue(first))
                    {
                        return false;
                    }

                    if (_tokenType == JsonTokenType.BeginObject || _tokenType == JsonTokenType.BeginArray)
                    {
                        _isNotPrimitive = true;
                    }
                    // Intentionally fall out of the if-block to return true
                    break;
            }
            return true;
        }

        private void SkipWhiteSpace()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer;
            for (; _consumed < localBuffer.Length; _consumed++)
            {
                byte val = localBuffer[_consumed];

                // JSON RFC 8259 section 2 says only these 4 characters count, not all of the Unicode defintions of whitespace.
                switch (val)
                {
                    case JsonUtf8Constant.Space:
                    case JsonUtf8Constant.Tab:
                    case JsonUtf8Constant.CarriageReturn:
                        _bytePositionInLine++;
                        break;

                    case JsonUtf8Constant.LineFeed:
                        _lineNumber++;
                        _bytePositionInLine = 0;
                        break;

                    default: return;
                }
            }
        }

        /// <summary>
        /// This method contains the logic for processing the next value token and determining
        /// what type of data it is.
        /// </summary>
        private bool ConsumeValue(byte marker)
        {
            while (true)
            {
                Debug.Assert((_trailingCommaBeforeComment && _readerOptions.CommentHandling == JsonCommentHandling.Allow) || !_trailingCommaBeforeComment);
                Debug.Assert((_trailingCommaBeforeComment && marker != JsonUtf8Constant.Slash) || !_trailingCommaBeforeComment);
                _trailingCommaBeforeComment = false;

                switch ((uint)marker)
                {
                    case JsonUtf8Constant.DoubleQuote:
                        return ConsumeString();

                    case JsonUtf8Constant.OpenBrace:
                        StartObject();
                        break;

                    case JsonUtf8Constant.OpenBracket:
                        StartArray();
                        break;

                    case '-':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '0':
                        return ConsumeNumber();

                    case 'f':
                        return ConsumeLiteral(JsonUtf8Constant.FalseValue, JsonTokenType.False);

                    case 't':
                        return ConsumeLiteral(JsonUtf8Constant.TrueValue, JsonTokenType.True);

                    case 'n':
                        return ConsumeLiteral(JsonUtf8Constant.NullValue, JsonTokenType.Null);

                    default:
                        switch (_readerOptions.CommentHandling)
                        {
                            case JsonCommentHandling.Disallow:
                                break;
                            case JsonCommentHandling.Allow:
                                if (marker == JsonUtf8Constant.Slash)
                                {
                                    return ConsumeComment();
                                }
                                break;
                            default:
                                Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
                                if (marker == JsonUtf8Constant.Slash)
                                {
                                    if (SkipComment())
                                    {
                                        if (_consumed >= (uint)_buffer.Length)
                                        {
                                            if (_isNotPrimitive && IsLastSpan && _tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
                                            {
                                                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive);
                                            }
                                            return false;
                                        }

                                        marker = _buffer[_consumed];

                                        // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                                        if (marker <= JsonUtf8Constant.Space)
                                        {
                                            SkipWhiteSpace();
                                            if (!HasMoreData())
                                            {
                                                return false;
                                            }
                                            marker = _buffer[_consumed];
                                        }

                                        TokenStartIndex = _consumed;

                                        // Skip comments and consume the actual JSON value.
                                        continue;
                                    }
                                    return false;
                                }
                                break;
                        }
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, marker);
                        break;
                }
                break;
            }
            return true;
        }

        // Consumes 'null', or 'true', or 'false'
        private bool ConsumeLiteral(in ReadOnlySpan<byte> literal, JsonTokenType tokenType)
        {
            ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
            Debug.Assert(span.Length > 0);
            Debug.Assert(span[0] == 'n' || span[0] == 't' || span[0] == 'f');

            if (!span.StartsWith(literal))
            {
                return CheckLiteral(span, literal);
            }

            ValueSpan = span.Slice(0, literal.Length);
            _tokenType = tokenType;
            _consumed += literal.Length;
            _bytePositionInLine += literal.Length;
            return true;
        }

        private bool CheckLiteral(in ReadOnlySpan<byte> span, in ReadOnlySpan<byte> literal)
        {
            Debug.Assert(span.Length > 0 && span[0] == literal[0]);

            int indexOfFirstMismatch = 0;

            for (int i = 1; i < literal.Length; i++)
            {
                if ((uint)span.Length > (uint)i)
                {
                    if (span[i] != literal[i])
                    {
                        _bytePositionInLine += i;
                        ThrowInvalidLiteral(span);
                    }
                }
                else
                {
                    indexOfFirstMismatch = i;
                    break;
                }
            }

            Debug.Assert(indexOfFirstMismatch > 0 && indexOfFirstMismatch < literal.Length);

            if (IsLastSpan)
            {
                _bytePositionInLine += indexOfFirstMismatch;
                ThrowInvalidLiteral(span);
            }
            return false;
        }

        private void ThrowInvalidLiteral(in ReadOnlySpan<byte> span)
        {
            byte firstByte = span[0];

            ExceptionResource resource;
            switch (firstByte)
            {
                case (byte)'t':
                    resource = ExceptionResource.ExpectedTrue;
                    break;
                case (byte)'f':
                    resource = ExceptionResource.ExpectedFalse;
                    break;
                default:
                    Debug.Assert(firstByte == 'n');
                    resource = ExceptionResource.ExpectedNull;
                    break;
            }
            SysJsonThrowHelper.ThrowJsonReaderException(ref this, resource, bytes: span);
        }

        private bool ConsumeNumber()
        {
            if (!TryGetNumber(_buffer.Slice(_consumed), out int consumed))
            {
                return false;
            }

            _tokenType = JsonTokenType.Number;
            _consumed += consumed;
            _bytePositionInLine += consumed;

            if ((uint)_consumed >= (uint)_buffer.Length)
            {
                Debug.Assert(IsLastSpan);

                // If there is no more data, and the JSON is not a single value, throw.
                if (_isNotPrimitive)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, _buffer[_consumed - 1]);
                }
            }

            // If there is more data and the JSON is not a single value, assert that there is an end of number delimiter.
            // Else, if either the JSON is a single value XOR if there is no more data, don't assert anything since there won't always be an end of number delimiter.
            Debug.Assert(
                ((_consumed < _buffer.Length) &&
                !_isNotPrimitive &&
                JsonUtf8Constant.Delimiters.IndexOf(_buffer[_consumed]) >= 0)
                || (_isNotPrimitive ^ (_consumed >= _buffer.Length)));

            return true;
        }

        private bool ConsumePropertyName()
        {
            _trailingCommaBeforeComment = false;

            if (!ConsumeString())
            {
                return false;
            }

            if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
            {
                return false;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            // We do not validate if 'first' is an invalid JSON byte here (such as control characters).
            // Those cases are captured below where we only accept ':'.
            if (first <= JsonUtf8Constant.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
                {
                    return false;
                }
                first = _buffer[_consumed];
            }

            // The next character must be a key / value seperator. Validate and skip.
            if (first != JsonUtf8Constant.NameSeparator)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound, first);
            }

            _consumed++;
            _bytePositionInLine++;
            _tokenType = JsonTokenType.PropertyName;
            return true;
        }

        private bool ConsumeString()
        {
            Debug.Assert(_buffer.Length >= _consumed + 1);
            Debug.Assert(_buffer[_consumed] == JsonUtf8Constant.DoubleQuote);

            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            // Vectorized search for either quote, backslash, or any control character.
            // If the first found byte is a quote, we have reached an end of string, and
            // can avoid validation.
            // Otherwise, in the uncommon case, iterate one character at a time and validate.
            int idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();

            if (idx >= 0)
            {
                byte foundByte = localBuffer[idx];
                if (foundByte == JsonUtf8Constant.DoubleQuote)
                {
                    _bytePositionInLine += idx + 2; // Add 2 for the start and end quotes.
                    ValueSpan = localBuffer.Slice(0, idx);
                    _stringHasEscaping = false;
                    _tokenType = JsonTokenType.String;
                    _consumed += idx + 2;
                    return true;
                }
                else
                {
                    return ConsumeStringAndValidate(localBuffer, idx);
                }
            }
            else
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += localBuffer.Length + 1;  // Account for the start quote
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                }
                return false;
            }
        }

        // Found a backslash or control characters which are considered invalid within a string.
        // Search through the rest of the string one byte at a time.
        // https://tools.ietf.org/html/rfc8259#section-7
        private bool ConsumeStringAndValidate(in ReadOnlySpan<byte> data, int idx)
        {
            Debug.Assert(idx >= 0 && idx < data.Length);
            Debug.Assert(data[idx] != JsonUtf8Constant.DoubleQuote);
            Debug.Assert(data[idx] == JsonUtf8Constant.BackSlash || data[idx] < JsonUtf8Constant.Space);

            long prevLineBytePosition = _bytePositionInLine;
            long prevLineNumber = _lineNumber;

            _bytePositionInLine += idx + 1; // Add 1 for the first quote

            bool nextCharEscaped = false;
            var dataLen = data.Length;
            for (; idx < dataLen; idx++)
            {
                byte currentByte = data[idx];
                switch (currentByte)
                {
                    case JsonUtf8Constant.DoubleQuote:
                        if (!nextCharEscaped)
                        {
                            goto Done;
                        }
                        nextCharEscaped = false;
                        break;

                    case JsonUtf8Constant.BackSlash:
                        nextCharEscaped = !nextCharEscaped;
                        break;

                    default:
                        if (nextCharEscaped)
                        {
                            int index = JsonUtf8Constant.EscapableChars.IndexOf(currentByte);
                            if (index == -1)
                            {
                                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
                            }

                            if ((uint)currentByte == 'u')
                            {
                                // Expecting 4 hex digits to follow the escaped 'u'
                                _bytePositionInLine++;  // move past the 'u'
                                if (ValidateHexDigits(data, idx + 1))
                                {
                                    idx += 4;   // Skip the 4 hex digits, the for loop accounts for idx incrementing past the 'u'
                                }
                                else
                                {
                                    // We found less than 4 hex digits. Check if there is more data to follow, otherwise throw.
                                    idx = dataLen;
                                    goto LoopDone;
                                }
                            }
                            nextCharEscaped = false;
                        }
                        else if ((uint)currentByte < JsonUtf8Constant.Space)
                        {
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
                        }
                        break;
                }

                _bytePositionInLine++;
            }
        LoopDone:

            if ((uint)idx >= (uint)dataLen)
            {
                if (IsLastSpan)
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                }
                _lineNumber = prevLineNumber;
                _bytePositionInLine = prevLineBytePosition;
                return false;
            }

        Done:
            _bytePositionInLine++;  // Add 1 for the end quote
            ValueSpan = data.Slice(0, idx);
            _stringHasEscaping = true;
            _tokenType = JsonTokenType.String;
            _consumed += idx + 2;
            return true;
        }

        private bool ValidateHexDigits(in ReadOnlySpan<byte> data, int idx)
        {
            for (int j = idx; j < data.Length; j++)
            {
                byte nextByte = data[j];
                if (!JsonReaderHelper.IsHexDigit(nextByte))
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
                }
                if (j - idx >= 3)
                {
                    return true;
                }
                _bytePositionInLine++;
            }

            return false;
        }

        // https://tools.ietf.org/html/rfc7159#section-6
        private bool TryGetNumber(in ReadOnlySpan<byte> source, out int consumed)
        {
            // TODO: https://github.com/dotnet/corefx/issues/33294
            Debug.Assert(source.Length > 0);

            _numberFormat = default;
            consumed = 0;
            int i = 0;

            var data = source;
            ConsumeNumberResult signResult = ConsumeNegativeSign(ref data, ref i);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            byte nextByte = data[i];
            Debug.Assert(nextByte >= '0' && nextByte <= '9');

            if (nextByte == '0')
            {
                ConsumeNumberResult result = ConsumeZero(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];
            }
            else
            {
                i++;
                ConsumeNumberResult result = ConsumeIntegerDigits(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];
                if (nextByte != '.' && nextByte != 'E' && nextByte != 'e')
                {
                    _bytePositionInLine += i;
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == '.' || nextByte == 'E' || nextByte == 'e');

            if (nextByte == '.')
            {
                i++;
                ConsumeNumberResult result = ConsumeDecimalDigits(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];
                if (nextByte != 'E' && nextByte != 'e')
                {
                    _bytePositionInLine += i;
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedNextDigitEValueNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == 'E' || nextByte == 'e');
            i++;
            _numberFormat = JsonSharedConstant.ScientificNotationFormat;

            signResult = ConsumeSign(ref data, ref i);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            i++;
            ConsumeNumberResult resultExponent = ConsumeIntegerDigits(ref data, ref i);
            if (resultExponent == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }
            if (resultExponent == ConsumeNumberResult.Success)
            {
                goto Done;
            }

            Debug.Assert(resultExponent == ConsumeNumberResult.OperationIncomplete);

            _bytePositionInLine += i;
            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, data[i]);

        Done:
            ValueSpan = data.Slice(0, i);
            consumed = i;
            return true;
        }

        private ConsumeNumberResult ConsumeNegativeSign(ref ReadOnlySpan<byte> data, ref int i)
        {
            byte nextByte = data[i];

            if (nextByte == '-')
            {
                i++;
                if ((uint)i >= (uint)data.Length)
                {
                    if (IsLastSpan)
                    {
                        _bytePositionInLine += i;
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }

                nextByte = data[i];
                if (!JsonHelpers.IsDigit(nextByte))
                {
                    _bytePositionInLine += i;
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
                }
            }
            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeZero(ref ReadOnlySpan<byte> data, ref int i)
        {
            Debug.Assert(data[i] == (byte)'0');
            i++;
            byte nextByte = default;
            if ((uint)i < (uint)data.Length)
            {
                nextByte = data[i];
                if (JsonUtf8Constant.Delimiters.IndexOf(nextByte) >= 0)
                {
                    return ConsumeNumberResult.Success;
                }
            }
            else
            {
                if (IsLastSpan)
                {
                    // A payload containing a single value: "0" is valid
                    // If we are dealing with multi-value JSON,
                    // ConsumeNumber will validate that we have a delimiter following the "0".
                    return ConsumeNumberResult.Success;
                }
                else
                {
                    return ConsumeNumberResult.NeedMoreData;
                }
            }
            nextByte = data[i];
            if (nextByte != '.' && nextByte != 'E' && nextByte != 'e')
            {
                _bytePositionInLine += i;
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeIntegerDigits(ref ReadOnlySpan<byte> data, ref int i)
        {
            byte nextByte = default;
            for (; i < data.Length; i++)
            {
                nextByte = data[i];
                if (!JsonHelpers.IsDigit(nextByte))
                {
                    break;
                }
            }
            if ((uint)i >= (uint)data.Length)
            {
                if (IsLastSpan)
                {
                    // A payload containing a single value of integers (e.g. "12") is valid
                    // If we are dealing with multi-value JSON,
                    // ConsumeNumber will validate that we have a delimiter following the integer.
                    return ConsumeNumberResult.Success;
                }
                else
                {
                    return ConsumeNumberResult.NeedMoreData;
                }
            }
            if (JsonUtf8Constant.Delimiters.IndexOf(nextByte) >= 0)
            {
                return ConsumeNumberResult.Success;
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeDecimalDigits(ref ReadOnlySpan<byte> data, ref int i)
        {
            if ((uint)i >= (uint)data.Length)
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += i;
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }
                return ConsumeNumberResult.NeedMoreData;
            }
            byte nextByte = data[i];
            if (!JsonHelpers.IsDigit(nextByte))
            {
                _bytePositionInLine += i;
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterDecimal, nextByte);
            }
            i++;

            return ConsumeIntegerDigits(ref data, ref i);
        }

        private ConsumeNumberResult ConsumeSign(ref ReadOnlySpan<byte> data, ref int i)
        {
            if ((uint)i >= (uint)data.Length)
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += i;
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }
                return ConsumeNumberResult.NeedMoreData;
            }

            byte nextByte = data[i];
            if (nextByte == '+' || nextByte == '-')
            {
                i++;
                if ((uint)i >= (uint)data.Length)
                {
                    if (IsLastSpan)
                    {
                        _bytePositionInLine += i;
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }
                nextByte = data[i];
            }

            if (!JsonHelpers.IsDigit(nextByte))
            {
                _bytePositionInLine += i;
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private bool ConsumeNextTokenOrRollback(byte marker)
        {
            int prevConsumed = _consumed;
            long prevPosition = _bytePositionInLine;
            long prevLineNumber = _lineNumber;
            JsonTokenType prevTokenType = _tokenType;
            bool prevTrailingCommaBeforeComment = _trailingCommaBeforeComment;
            ConsumeTokenResult result = ConsumeNextToken(marker);
            if (result == ConsumeTokenResult.Success)
            {
                return true;
            }
            if (result == ConsumeTokenResult.NotEnoughDataRollBackState)
            {
                _consumed = prevConsumed;
                _tokenType = prevTokenType;
                _bytePositionInLine = prevPosition;
                _lineNumber = prevLineNumber;
                _trailingCommaBeforeComment = prevTrailingCommaBeforeComment;
            }
            return false;
        }

        /// <summary>
        /// This method consumes the next token regardless of whether we are inside an object or an array.
        /// For an object, it reads the next property name token. For an array, it just reads the next value.
        /// </summary>
        private ConsumeTokenResult ConsumeNextToken(byte marker)
        {
            if (_readerOptions.CommentHandling != JsonCommentHandling.Disallow)
            {
                if (_readerOptions.CommentHandling == JsonCommentHandling.Allow)
                {
                    if (marker == JsonUtf8Constant.Slash)
                    {
                        return ConsumeComment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    if (_tokenType == JsonTokenType.Comment)
                    {
                        return ConsumeNextTokenFromLastNonCommentToken();
                    }
                }
                else
                {
                    Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
                    return ConsumeNextTokenUntilAfterAllCommentsAreSkipped(marker);
                }
            }

            if (0u >= (uint)_bitStack.CurrentDepth)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
            }

            switch (marker)
            {
                case JsonUtf8Constant.ValueSeparator:
                    _consumed++;
                    _bytePositionInLine++;

                    if ((uint)_consumed >= (uint)_buffer.Length)
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    byte first = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (first <= JsonUtf8Constant.Space)
                    {
                        SkipWhiteSpace();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                        {
                            return ConsumeTokenResult.NotEnoughDataRollBackState;
                        }
                        first = _buffer[_consumed];
                    }

                    TokenStartIndex = _consumed;

                    if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && first == JsonUtf8Constant.Slash)
                    {
                        _trailingCommaBeforeComment = true;
                        return ConsumeComment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }

                    if (_inObject)
                    {
                        if (first != JsonUtf8Constant.DoubleQuote)
                        {
                            if (first == JsonUtf8Constant.CloseBrace)
                            {
                                if (_readerOptions.AllowTrailingCommas)
                                {
                                    EndObject();
                                    return ConsumeTokenResult.Success;
                                }
                                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                            }
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                        }
                        return ConsumePropertyName() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    else
                    {
                        if (first == JsonUtf8Constant.CloseBracket)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndArray();
                                return ConsumeTokenResult.Success;
                            }
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                        }
                        return ConsumeValue(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }

                case JsonUtf8Constant.CloseBrace:
                    EndObject();
                    break;

                case JsonUtf8Constant.CloseBracket:
                    EndArray();
                    break;

                default:
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
                    break;
            }
            return ConsumeTokenResult.Success;
        }

        private ConsumeTokenResult ConsumeNextTokenFromLastNonCommentToken()
        {
            Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Allow);
            Debug.Assert(_tokenType == JsonTokenType.Comment);

            if (JsonReaderHelper.IsTokenTypePrimitive(_previousTokenType))
            {
                _tokenType = _inObject ? JsonTokenType.BeginObject : JsonTokenType.BeginArray;
            }
            else
            {
                _tokenType = _previousTokenType;
            }

            Debug.Assert(_tokenType != JsonTokenType.Comment);

            if (!HasMoreData())
            {
                goto RollBack;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            if (first <= JsonUtf8Constant.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData())
                {
                    goto RollBack;
                }
                first = _buffer[_consumed];
            }

            if (0u >= (uint)_bitStack.CurrentDepth && _tokenType != JsonTokenType.None)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, first);
            }

            Debug.Assert(first != JsonUtf8Constant.Slash);

            TokenStartIndex = _consumed;

            switch (first)
            {
                case JsonUtf8Constant.ValueSeparator:
                    // A comma without some JSON value preceding it is invalid
                    if (_previousTokenType <= JsonTokenType.BeginObject || _previousTokenType == JsonTokenType.BeginArray || _trailingCommaBeforeComment)
                    {
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment, first);
                    }

                    _consumed++;
                    _bytePositionInLine++;

                    if ((uint)_consumed >= (uint)_buffer.Length)
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        goto RollBack;
                    }
                    first = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (first <= JsonUtf8Constant.Space)
                    {
                        SkipWhiteSpace();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                        {
                            goto RollBack;
                        }
                        first = _buffer[_consumed];
                    }

                    TokenStartIndex = _consumed;

                    if (first == JsonUtf8Constant.Slash)
                    {
                        _trailingCommaBeforeComment = true;
                        if (ConsumeComment())
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }

                    if (_inObject)
                    {
                        if (first != JsonUtf8Constant.DoubleQuote)
                        {
                            if (first == JsonUtf8Constant.CloseBrace)
                            {
                                if (_readerOptions.AllowTrailingCommas)
                                {
                                    EndObject();
                                    goto Done;
                                }
                                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                            }

                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                        }
                        if (ConsumePropertyName())
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }
                    else
                    {
                        if (first == JsonUtf8Constant.CloseBracket)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndArray();
                                goto Done;
                            }
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                        }

                        if (ConsumeValue(first))
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }

                case JsonUtf8Constant.CloseBrace:
                    EndObject();
                    goto Done;

                case JsonUtf8Constant.CloseBracket:
                    EndArray();
                    goto Done;
            }

            switch (_tokenType)
            {
                case JsonTokenType.None:
                    if (ReadFirstToken(first))
                    {
                        goto Done;
                    }
                    else
                    {
                        goto RollBack;
                    }

                case JsonTokenType.BeginObject:
                    Debug.Assert(first != JsonUtf8Constant.CloseBrace);
                    if (first != JsonUtf8Constant.DoubleQuote)
                    {
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                    }

                    int prevConsumed = _consumed;
                    long prevPosition = _bytePositionInLine;
                    long prevLineNumber = _lineNumber;
                    if (!ConsumePropertyName())
                    {
                        // roll back potential changes
                        _consumed = prevConsumed;
                        _tokenType = JsonTokenType.BeginObject;
                        _bytePositionInLine = prevPosition;
                        _lineNumber = prevLineNumber;
                        goto RollBack;
                    }
                    goto Done;

                case JsonTokenType.BeginArray:
                    Debug.Assert(first != JsonUtf8Constant.CloseBracket);
                    if (!ConsumeValue(first))
                    {
                        goto RollBack;
                    }
                    goto Done;

                case JsonTokenType.PropertyName:
                    if (!ConsumeValue(first))
                    {
                        goto RollBack;
                    }
                    goto Done;

                default:
                    Debug.Assert(_tokenType == JsonTokenType.EndArray || _tokenType == JsonTokenType.EndObject);
                    if (_inObject)
                    {
                        Debug.Assert(first != JsonUtf8Constant.CloseBrace);
                        if (first != JsonUtf8Constant.DoubleQuote)
                        {
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                        }

                        if (ConsumePropertyName())
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }
                    else
                    {
                        Debug.Assert(first != JsonUtf8Constant.CloseBracket);

                        if (ConsumeValue(first))
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }
            }

        Done:
            return ConsumeTokenResult.Success;

        RollBack:
            return ConsumeTokenResult.NotEnoughDataRollBackState;
        }

        private bool SkipAllComments(ref byte marker)
        {
            while (marker == JsonUtf8Constant.Slash)
            {
                if (SkipComment())
                {
                    if (!HasMoreData())
                    {
                        goto IncompleteNoRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= JsonUtf8Constant.Space)
                    {
                        SkipWhiteSpace();
                        if (!HasMoreData())
                        {
                            goto IncompleteNoRollback;
                        }
                        marker = _buffer[_consumed];
                    }
                }
                else
                {
                    goto IncompleteNoRollback;
                }
            }
            return true;

        IncompleteNoRollback:
            return false;
        }

        private bool SkipAllComments(ref byte marker, ExceptionResource resource)
        {
            while (marker == JsonUtf8Constant.Slash)
            {
                if (SkipComment())
                {
                    // The next character must be a start of a property name or value.
                    if (!HasMoreData(resource))
                    {
                        goto IncompleteRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= JsonUtf8Constant.Space)
                    {
                        SkipWhiteSpace();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreData(resource))
                        {
                            goto IncompleteRollback;
                        }
                        marker = _buffer[_consumed];
                    }
                }
                else
                {
                    goto IncompleteRollback;
                }
            }
            return true;

        IncompleteRollback:
            return false;
        }

        private ConsumeTokenResult ConsumeNextTokenUntilAfterAllCommentsAreSkipped(byte marker)
        {
            if (!SkipAllComments(ref marker))
            {
                goto IncompleteNoRollback;
            }

            TokenStartIndex = _consumed;

            switch (_tokenType)
            {
                case JsonTokenType.BeginObject:
                    if (marker == JsonUtf8Constant.CloseBrace)
                    {
                        EndObject();
                    }
                    else
                    {
                        if (marker != JsonUtf8Constant.DoubleQuote)
                        {
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
                        }

                        int prevConsumed = _consumed;
                        long prevPosition = _bytePositionInLine;
                        long prevLineNumber = _lineNumber;
                        if (!ConsumePropertyName())
                        {
                            // roll back potential changes
                            _consumed = prevConsumed;
                            _tokenType = JsonTokenType.BeginObject;
                            _bytePositionInLine = prevPosition;
                            _lineNumber = prevLineNumber;
                            goto IncompleteNoRollback;
                        }
                    }
                    goto Done;

                case JsonTokenType.BeginArray:
                    if (marker == JsonUtf8Constant.CloseBracket)
                    {
                        EndArray();
                    }
                    else
                    {
                        if (!ConsumeValue(marker))
                        {
                            goto IncompleteNoRollback;
                        }
                    }
                    goto Done;

                case JsonTokenType.PropertyName:
                    if (!ConsumeValue(marker))
                    {
                        goto IncompleteNoRollback;
                    }
                    goto Done;
            }

            if (0u >= (uint)_bitStack.CurrentDepth)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
            }

            switch (marker)
            {
                case JsonUtf8Constant.ValueSeparator:
                    _consumed++;
                    _bytePositionInLine++;

                    if ((uint)_consumed >= (uint)_buffer.Length)
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= JsonUtf8Constant.Space)
                    {
                        SkipWhiteSpace();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                        {
                            return ConsumeTokenResult.NotEnoughDataRollBackState;
                        }
                        marker = _buffer[_consumed];
                    }

                    if (!SkipAllComments(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        goto IncompleteRollback;
                    }

                    TokenStartIndex = _consumed;

                    if (_inObject)
                    {
                        if (marker != JsonUtf8Constant.DoubleQuote)
                        {
                            if (marker == JsonUtf8Constant.CloseBrace)
                            {
                                if (_readerOptions.AllowTrailingCommas)
                                {
                                    EndObject();
                                    goto Done;
                                }
                                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                            }

                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
                        }
                        return ConsumePropertyName() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    else
                    {
                        if (marker == JsonUtf8Constant.CloseBracket)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndArray();
                                goto Done;
                            }
                            SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                        }

                        return ConsumeValue(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }

                case JsonUtf8Constant.CloseBrace:
                    EndObject();
                    break;

                case JsonUtf8Constant.CloseBracket:
                    EndArray();
                    break;

                default:
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
                    break;
            }

        Done:
            return ConsumeTokenResult.Success;
        IncompleteNoRollback:
            return ConsumeTokenResult.IncompleteNoRollBackNecessary;
        IncompleteRollback:
            return ConsumeTokenResult.NotEnoughDataRollBackState;
        }

        private bool SkipComment()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            if (localBuffer.Length > 0)
            {
                byte marker = localBuffer[0];
                if (marker == JsonUtf8Constant.Slash)
                {
                    return SkipSingleLineComment(localBuffer.Slice(1), out _);
                }
                else if (marker == JsonUtf8Constant.Asterisk)
                {
                    return SkipMultiLineComment(localBuffer.Slice(1), out _);
                }
                else
                {
                    SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonUtf8Constant.Slash);
                }
            }

            if (IsLastSpan)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonUtf8Constant.Slash);
            }
            return false;
        }

        private bool SkipSingleLineComment(in ReadOnlySpan<byte> localBuffer, out int idx)
        {
            idx = FindLineSeparator(localBuffer);
            int toConsume = 0;
            if (idx != -1)
            {
                toConsume = idx;
                if (localBuffer[idx] == JsonUtf8Constant.LineFeed)
                {
                    goto EndOfComment;
                }

                // If we are here, we have definintely found a \r. So now to check if \n follows.
                Debug.Assert(localBuffer[idx] == JsonUtf8Constant.CarriageReturn);

                if ((uint)idx < (uint)(localBuffer.Length - 1))
                {
                    if (localBuffer[idx + 1] == JsonUtf8Constant.LineFeed)
                    {
                        toConsume++;
                    }

                    goto EndOfComment;
                }

                if (IsLastSpan)
                {
                    goto EndOfComment;
                }
                else
                {
                    // there might be LF in the next segment
                    return false;
                }
            }

            if (IsLastSpan)
            {
                idx = localBuffer.Length;
                toConsume = idx;
                // Assume everything on this line is a comment and there is no more data.
                _bytePositionInLine += 2 + localBuffer.Length;
                goto Done;
            }
            else
            {
                return false;
            }

        EndOfComment:
            toConsume++;
            _bytePositionInLine = 0;
            _lineNumber++;

        Done:
            _consumed += 2 + toConsume;
            return true;
        }

        private int FindLineSeparator(in ReadOnlySpan<byte> localBuffer)
        {
            int totalIdx = 0;
            var buffer = localBuffer;
            while (true)
            {
                int idx = buffer.IndexOfAny(JsonUtf8Constant.LineFeed, JsonUtf8Constant.CarriageReturn, JsonSharedConstant.StartingByteOfNonStandardSeparator);

                if (idx == -1)
                {
                    return -1;
                }

                totalIdx += idx;

                if (buffer[idx] != JsonSharedConstant.StartingByteOfNonStandardSeparator)
                {
                    return totalIdx;
                }

                totalIdx++;
                buffer = buffer.Slice(idx + 1);

                ThrowOnDangerousLineSeparator(buffer);
            }
        }

        // assumes first byte (JsonSharedConstant.StartingByteOfNonStandardSeparator) is already read
        private void ThrowOnDangerousLineSeparator(in ReadOnlySpan<byte> localBuffer)
        {
            // \u2028 and \u2029 are considered respectively line and paragraph separators
            // UTF-8 representation for them is E2, 80, A8/A9
            // we have already read E2, we need to check for remaining 2 bytes

            if ((uint)localBuffer.Length < 2u)
            {
                return;
            }

            byte next = localBuffer[1];
            if (localBuffer[0] == 0x80 && (next == 0xA8 || next == 0xA9))
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfLineSeparator);
            }
        }

        private bool SkipMultiLineComment(in ReadOnlySpan<byte> localBuffer, out int idx)
        {
            idx = 0;
            while (true)
            {
                int foundIdx = localBuffer.Slice(idx).IndexOf(JsonUtf8Constant.Slash);
                if (foundIdx == -1)
                {
                    if (IsLastSpan)
                    {
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfCommentNotFound);
                    }
                    return false;
                }
                if (foundIdx != 0 && localBuffer[foundIdx + idx - 1] == JsonUtf8Constant.Asterisk)
                {
                    // foundIdx points just after '*' in the end-of-comment delimiter. Hence increment idx by one
                    // position less to make it point right before beginning of end-of-comment delimiter i.e. */
                    idx += foundIdx - 1;
                    break;
                }
                idx += foundIdx + 1;
            }

            // Consume the /* and */ characters that are part of the multi-line comment.
            // idx points right before the final '*' (which is right before the last '/'). Hence increment _consumed
            // by 4 to exclude the start/end-of-comment delimiters.
            _consumed += 4 + idx;

            (int newLines, int newLineIndex) = JsonReaderHelper.CountNewLines(localBuffer.Slice(0, idx));
            _lineNumber += newLines;
            if (newLineIndex != -1)
            {
                // newLineIndex points at last newline character and byte positions in the new line start
                // after that. Hence add 1 to skip the newline character.
                _bytePositionInLine = idx - newLineIndex + 1;
            }
            else
            {
                _bytePositionInLine += 4 + idx;
            }
            return true;
        }

        private bool ConsumeComment()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            if ((uint)localBuffer.Length > 0u)
            {
                byte marker = localBuffer[0];
                switch (marker)
                {
                    case JsonUtf8Constant.Slash:
                        return ConsumeSingleLineComment(localBuffer.Slice(1), _consumed);

                    case JsonUtf8Constant.Asterisk:
                        return ConsumeMultiLineComment(localBuffer.Slice(1), _consumed);

                    default:
                        SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAtStartOfComment, marker);
                        break;
                }
            }

            if (IsLastSpan)
            {
                SysJsonThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
            }
            return false;
        }

        private bool ConsumeSingleLineComment(in ReadOnlySpan<byte> localBuffer, int previousConsumed)
        {
            if (!SkipSingleLineComment(localBuffer, out int idx))
            {
                return false;
            }

            // Exclude the // at start of the comment. idx points right before the line separator
            // at the end of the comment.
            ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
            if (_tokenType != JsonTokenType.Comment)
            {
                _previousTokenType = _tokenType;
            }
            _tokenType = JsonTokenType.Comment;
            return true;
        }

        private bool ConsumeMultiLineComment(in ReadOnlySpan<byte> localBuffer, int previousConsumed)
        {
            if (!SkipMultiLineComment(localBuffer, out int idx))
            {
                return false;
            }

            // Exclude the /* at start of the comment. idx already points right before the terminal '*/'
            // for the end of multiline comment.
            ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
            if (_tokenType != JsonTokenType.Comment)
            {
                _previousTokenType = _tokenType;
            }
            _tokenType = JsonTokenType.Comment;
            return true;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"TokenType = {DebugTokenType} (TokenStartIndex = {TokenStartIndex}) Consumed = {BytesConsumed}";

        // Using TokenType.ToString() (or {TokenType}) fails to render in the debug window. The
        // message "The runtime refused to evaluate the expression at this time." is shown. This
        // is a workaround until we root cause and fix the issue.
        private string DebugTokenType
            => TokenType switch
            {
                JsonTokenType.Comment => nameof(JsonTokenType.Comment),
                JsonTokenType.EndArray => nameof(JsonTokenType.EndArray),
                JsonTokenType.EndObject => nameof(JsonTokenType.EndObject),
                JsonTokenType.False => nameof(JsonTokenType.False),
                JsonTokenType.None => nameof(JsonTokenType.None),
                JsonTokenType.Null => nameof(JsonTokenType.Null),
                JsonTokenType.Number => nameof(JsonTokenType.Number),
                JsonTokenType.PropertyName => nameof(JsonTokenType.PropertyName),
                JsonTokenType.BeginArray => nameof(JsonTokenType.BeginArray),
                JsonTokenType.BeginObject => nameof(JsonTokenType.BeginObject),
                JsonTokenType.String => nameof(JsonTokenType.String),
                JsonTokenType.True => nameof(JsonTokenType.True),
                _ => ((byte)TokenType).ToString()
            };
    }
}
