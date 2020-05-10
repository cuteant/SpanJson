using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;
using SpanJson.Internal;

namespace SpanJson
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        o,
        s,
        t,
        doc,
        key,
        json,
        type,
        name,
        inner,
        array,
        count,
        index,
        input,
        token,
        value,
        other,
        stream,
        writer,
        reader,
        length,
        format,
        offset,
        method,
        source,
        initial,
        inArray,
        element,
        newSize,
        utf8Json,
        offsetIn,
        outArray,
        jsonData,
        property,
        utf16Json,
        offsetOut,
        container,
        fieldInfo,
        annotation,
        enumerable,
        expression,
        objectType,
        initialValue,
        propertyInfo,
        propertyName,
        configSettings,
        jsonSerializer,
        configSerializer,
        serializerSettings,
        deserializerSettings,
        genericInterfaceDefinition,
    }

    #endregion

    #region -- ExceptionResource --

    /// <summary>The convention for this enum is using the resource name as the enum name</summary>
    internal enum ExceptionResource
    {
        ArrayDepthTooLarge,
        EndOfCommentNotFound,
        EndOfStringNotFound,
        RequiredDigitNotFoundAfterDecimal,
        RequiredDigitNotFoundAfterSign,
        RequiredDigitNotFoundEndOfData,
        ExpectedEndAfterSingleJson,
        ExpectedEndOfDigitNotFound,
        ExpectedFalse,
        ExpectedNextDigitEValueNotFound,
        ExpectedNull,
        ExpectedSeparatorAfterPropertyNameNotFound,
        ExpectedStartOfPropertyNotFound,
        ExpectedStartOfPropertyOrValueNotFound,
        ExpectedStartOfPropertyOrValueAfterComment,
        ExpectedStartOfValueNotFound,
        ExpectedTrue,
        ExpectedValueAfterPropertyNameNotFound,
        FoundInvalidCharacter,
        InvalidCharacterWithinString,
        InvalidCharacterAfterEscapeWithinString,
        InvalidHexCharacterWithinString,
        InvalidEndOfJsonNonPrimitive,
        MismatchedObjectArray,
        ObjectDepthTooLarge,
        ZeroDepthAtEnd,
        DepthTooLarge,
        CannotStartObjectArrayWithoutProperty,
        CannotStartObjectArrayAfterPrimitiveOrClose,
        CannotWriteValueWithinObject,
        CannotWriteValueAfterPrimitiveOrClose,
        CannotWritePropertyWithinArray,
        ExpectedJsonTokens,
        TrailingCommaNotAllowedBeforeArrayEnd,
        TrailingCommaNotAllowedBeforeObjectEnd,
        InvalidCharacterAtStartOfComment,
        UnexpectedEndOfDataWhileReadingComment,
        UnexpectedEndOfLineSeparator,
        ExpectedOneCompleteToken,
        NotEnoughData,
    }

    #endregion

    partial class ThrowHelper
    {
        #region -- Exception --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_UnreachedCode()
        {
            throw GetException();
            static Exception GetException()
            {
                return new Exception("unreached code.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_UnreachableCode()
        {
            throw GetException();
            static Exception GetException()
            {
                return new Exception("Unreachable code.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_InvalidMode()
        {
            throw GetException();
            static Exception GetException()
            {
                return new Exception("Invalid Mode.");
            }
        }

        #endregion

        #region -- ArgumentException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Guid_Pattern()
        {
            return new ArgumentException("Invalid Guid Pattern.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Guid_Pattern()
        {
            throw GetArgumentException_Guid_Pattern();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Length()
        {
            throw GetException();
            static ArgumentException GetException()
            {
                return new ArgumentException("length < newSize");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidDoubleValueForJson()
        {
            throw GetException();
            static ArgumentException GetException()
            {
                return new ArgumentException("Invalid double value for JSON", "value");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidFloatValueForJson()
        {
            throw GetException();
            static ArgumentException GetException()
            {
                return new ArgumentException("Invalid float value for JSON", "value");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EnumIllegalVal(Base64FormattingOptions options)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Illegal enum value: {options}.", nameof(options));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_IsNotAnInterface(Type interfaceType)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{interfaceType} is not an interface.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException_InvalidUTF16(int charAsInt)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException(string.Format("Cannot encode invalid UTF-16 text as JSON. Invalid surrogate value: '{0}'.", $"0x{charAsInt:X2}"));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException_InvalidUTF8(ReadOnlySpan<byte> value, int bytesWritten)
        {
            var builder = new StringBuilder();

            value = value.Slice(bytesWritten);
            int printFirst10 = Math.Min(value.Length, 10);

            for (int i = 0; i < printFirst10; i++)
            {
                byte nextByte = value[i];
                if (IsPrintable(nextByte))
                {
                    builder.Append((char)nextByte);
                }
                else
                {
                    builder.Append($"0x{nextByte:X2}");
                }
            }

            if (printFirst10 < value.Length)
            {
                builder.Append("...");
            }

            static bool IsPrintable(byte value) => value >= 0x20 && value < 0x7F;

            throw new ArgumentException($"Cannot encode invalid UTF-8 text as JSON. Invalid input: '{builder}'.");
        }

        public static ArgumentException GetArgumentException_ReadInvalidUTF16(EncoderFallbackException innerException)
        {
            return new ArgumentException("Cannot transcode invalid UTF-16 string to UTF-8 JSON text.", innerException);
        }

        #endregion

        #region -- ArgumentOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentOutOfRangeException GetArgumentOutOfRangeException()
        {
            return new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRange_IndexException()
        {
            throw GetException();

            static ArgumentOutOfRangeException GetException()
            {
                return new ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, "Index was out of range. Must be non-negative and less than the size of the collection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_GenericPositive(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, "Value must be positive.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_OffsetLength(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, "Offset and length must refer to a position in the string.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_OffsetOut(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, "Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Nonnegative(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, $"{argumentName} should be non negative.");
            }
        }

        public static void ThrowArgumentOutOfRangeException_CommentEnumMustBeInRange(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, SR.CommentHandlingMustBeValid);
            }
        }

        public static void ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                var argumentName = GetArgumentName(argument);
                return new ArgumentOutOfRangeException(argumentName, SR.MaxDepthMustBePositive);
            }
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException()
        {
            throw GetException();
            static InvalidOperationException GetException()
            {
                return new InvalidOperationException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NullArray()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("The underlying array is null.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Register_Resolver_Err()
        {
            throw GetException();
            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Register must call on startup(before use GetFormatter<T>).");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NotSupport_Value(float value)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("not support float value:" + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NotSupport_Value(double value)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("not support double value:" + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Reached_MaximumSize()
        {
            throw GetException();
            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("byte[] size reached maximum size of array(0x7FFFFFC7), can not write to single byte[]. Details: https://msdn.microsoft.com/en-us/library/system.array");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NestingLimitOfExceeded()
        {
            throw GetException();
            static InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Nesting Limit of {JsonSharedConstant.NestingLimit} exceeded.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CanotMapConstructorParameterToAnyMember(ParameterInfo constructorParameter)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Can't map constructor parameter {constructorParameter.Name} to any member.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_ReadInvalidUTF16(int charAsInt)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException(string.Format("Cannot read invalid UTF-16 JSON text as string. Invalid surrogate value: '{0}'.", $"0x{charAsInt:X2}"));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_ReadInvalidUTF16()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_AdvancedTooFar(int capacity)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Cannot advance past the end of the buffer, which has a size of {capacity}.");
            }
        }

        #endregion

        #region -- NotSupportedException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException()
        {
            return new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException()
        {
            throw GetNotSupportedException();
        }

        #endregion

        #region -- NotImplementedException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotImplementedException GetNotImplementedException()
        {
            return new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotImplementedException()
        {
            throw GetNotImplementedException();
        }

        #endregion

        #region -- InvalidCastException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidCastException GetInvalidCastException()
        {
            return new InvalidCastException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCastException()
        {
            throw GetInvalidCastException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static InvalidOperationException GetInvalidOperationException_ReadInvalidUTF8(DecoderFallbackException innerException)
        {
            return new InvalidOperationException("Cannot transcode invalid UTF-8 JSON text to UTF-16 string.", innerException);
        }

        #endregion

        #region -- JsonParserException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonParserException(JsonParserException.ParserError error, JsonParserException.ValueType type, int position)
        {
            throw GetJsonParserException(error, type, position);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonParserException GetJsonParserException(JsonParserException.ParserError error, JsonParserException.ValueType type, int position)
        {
            return new JsonParserException(error, type, position);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonParserException(JsonParserException.ParserError error, int position)
        {
            throw GetJsonParserException(error, position);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonParserException GetJsonParserException(JsonParserException.ParserError error, int position)
        {
            return new JsonParserException(error, position);
        }

        #endregion

        #region -- FormatException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException_BadBase64Char()
        {
            throw GetException();
            static FormatException GetException()
            {
                return new FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");
            }
        }

        #endregion

        #region -- OutOfMemoryException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowOutOfMemoryException()
        {
            throw GetException();
            static OutOfMemoryException GetException()
            {
                return new OutOfMemoryException();
            }
        }

        #endregion
    }
}
