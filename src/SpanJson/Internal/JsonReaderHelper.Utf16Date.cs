﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// borrowed from https://github.com/dotnet/corefx/blob/8135319caa7e457ed61053ca1418313b88057b51/src/System.Text.Json/src/System/Text/Json/JsonHelpers.Date.cs#L12

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SpanJson.Internal
{
    internal static partial class JsonReaderHelper
    {
        private struct Utf16DateTimeParseData
        {
            public int Year;
            public int Month;
            public int Day;
            public int Hour;
            public int Minute;
            public int Second;
            public int Fraction; // This value should never be greater than 9_999_999.
            public int OffsetHours;
            public int OffsetMinutes;
            public bool OffsetNegative => OffsetToken == JsonUtf16Constant.Hyphen;
            public char OffsetToken;
        }

        /// <summary>Parse the given UTF-8 <paramref name="source"/> as extended ISO 8601 format.</summary>
        /// <param name="source">UTF-8 source to parse.</param>
        /// <param name="value">The parsed <see cref="DateTime"/> if successful.</param>
        /// <returns>"true" if successfully parsed.</returns>
        public static bool TryParseAsISO(ReadOnlySpan<char> source, out DateTime value)
        {
            if (!TryParseDateTimeOffset(source, out Utf16DateTimeParseData parseData))
            {
                value = default;
                return false;
            }

            if (parseData.OffsetToken == JsonUtf16Constant.UtcOffsetToken)
            {
                return TryCreateDateTime(parseData, DateTimeKind.Utc, out value);
            }
            else if (parseData.OffsetToken == JsonUtf16Constant.Plus || parseData.OffsetToken == JsonUtf16Constant.Hyphen)
            {
                if (!TryCreateDateTimeOffset(ref parseData, out DateTimeOffset dateTimeOffset))
                {
                    value = default;
                    return false;
                }

                value = dateTimeOffset.LocalDateTime;
                return true;
            }

            return TryCreateDateTime(parseData, DateTimeKind.Unspecified, out value);
        }

        /// <summary>Parse the given UTF-8 <paramref name="source"/> as extended ISO 8601 format.</summary>
        /// <param name="source">UTF-8 source to parse.</param>
        /// <param name="value">The parsed <see cref="DateTimeOffset"/> if successful.</param>
        /// <returns>"true" if successfully parsed.</returns>
        public static bool TryParseAsISO(ReadOnlySpan<char> source, out DateTimeOffset value)
        {
            if (!TryParseDateTimeOffset(source, out Utf16DateTimeParseData parseData))
            {
                value = default;
                return false;
            }

            if (parseData.OffsetToken == JsonUtf16Constant.UtcOffsetToken || // Same as specifying an offset of "+00:00", except that DateTime's Kind gets set to UTC rather than Local
                parseData.OffsetToken == JsonUtf16Constant.Plus || parseData.OffsetToken == JsonUtf16Constant.Hyphen)
            {
                return TryCreateDateTimeOffset(ref parseData, out value);
            }

            // No offset, attempt to read as local time.
            return TryCreateDateTimeOffsetInterpretingDataAsLocalTime(parseData, out value);
        }

        /// <summary>ISO 8601 date time parser (ISO 8601-1:2019).</summary>
        /// <param name="source">The date/time to parse in UTF-8 format.</param>
        /// <param name="parseData">The parsed <see cref="Utf16DateTimeParseData"/> for the given <paramref name="source"/>.</param>
        /// <remarks>
        /// Supports extended calendar date (5.2.2.1) and complete (5.4.2.1) calendar date/time of day
        /// representations with optional specification of seconds and fractional seconds.
        /// 
        /// Times can be explicitly specified as UTC ("Z" - 5.3.3) or offsets from UTC ("+/-hh:mm" 5.3.4.2).
        /// If unspecified they are considered to be local per spec. 
        /// 
        /// Examples: (TZD is either "Z" or hh:mm offset from UTC)
        /// 
        ///  YYYY-MM-DD               (eg 1997-07-16)
        ///  YYYY-MM-DDThh:mm         (eg 1997-07-16T19:20)
        ///  YYYY-MM-DDThh:mm:ss      (eg 1997-07-16T19:20:30)
        ///  YYYY-MM-DDThh:mm:ss.s    (eg 1997-07-16T19:20:30.45)
        ///  YYYY-MM-DDThh:mmTZD      (eg 1997-07-16T19:20+01:00)
        ///  YYYY-MM-DDThh:mm:ssTZD   (eg 1997-07-16T19:20:3001:00)
        ///  YYYY-MM-DDThh:mm:ss.sTZD (eg 1997-07-16T19:20:30.45Z)
        /// 
        /// Generally speaking we always require the "extended" option when one exists (3.1.3.5).
        /// The extended variants have separator characters between components ('-', ':', '.', etc.).
        /// Spaces are not permitted.
        /// </remarks>
        /// <returns>"true" if successfully parsed.</returns>
        private static bool TryParseDateTimeOffset(ReadOnlySpan<char> source, out Utf16DateTimeParseData parseData)
        {
            uint srcLen = (uint)source.Length;
            // Source does not have enough characters for YYYY-MM-DD
            if (srcLen < 10u)
            {
                parseData = default;
                return false;
            }

            // Parse the calendar date
            // -----------------------
            // ISO 8601-1:2019 5.2.2.1b "Calendar date complete extended format"
            //  [dateX] = [year][“-”][month][“-”][day]
            //  [year]  = [YYYY] [0000 - 9999] (4.3.2)
            //  [month] = [MM] [01 - 12] (4.3.3)
            //  [day]   = [DD] [01 - 28, 29, 30, 31] (4.3.4)
            //
            // Note: 5.2.2.2 "Representations with reduced precision" allows for
            // just [year][“-”][month] (a) and just [year] (b), but we currently
            // don't permit it.

            parseData = new Utf16DateTimeParseData();

            {
                uint digit1 = source[0] - (uint)'0';
                uint digit2 = source[1] - (uint)'0';
                uint digit3 = source[2] - (uint)'0';
                uint digit4 = source[3] - (uint)'0';

                if (digit1 > 9 || digit2 > 9 || digit3 > 9 || digit4 > 9)
                {
                    return false;
                }

                parseData.Year = (int)(digit1 * 1000 + digit2 * 100 + digit3 * 10 + digit4);
            }

            if (source[4] != JsonUtf16Constant.Hyphen
                || !TryGetNextTwoDigits(source.Slice(start: 5, length: 2), ref parseData.Month)
                || source[7] != JsonUtf16Constant.Hyphen
                || !TryGetNextTwoDigits(source.Slice(start: 8, length: 2), ref parseData.Day))
            {
                return false;
            }

            // We now have YYYY-MM-DD [dateX]

            Debug.Assert(srcLen >= 10u);
            if (srcLen == 10u)
            {
                // Just a calendar date
                return true;
            }

            // Parse the time of day
            // ---------------------
            //
            // ISO 8601-1:2019 5.3.1.2b "Local time of day complete extended format"
            //  [timeX]   = [“T”][hour][“:”][min][“:”][sec]
            //  [hour]    = [hh] [00 - 23] (4.3.8a)
            //  [minute]  = [mm] [00 - 59] (4.3.9a)
            //  [sec]     = [ss] [00 - 59, 60 with a leap second] (4.3.10a)
            //
            // ISO 8601-1:2019 5.3.3 "UTC of day"
            //  [timeX][“Z”]
            //
            // ISO 8601-1:2019 5.3.4.2 "Local time of day with the time shift between
            // local time scale and UTC" (Extended format)
            //
            //  [shiftX] = [“+”|“-”][hour][“:”][min]
            //
            // Notes:
            //
            // "T" is optional per spec, but _only_ when times are used alone. In our
            // case, we're reading out a complete date & time and as such require "T".
            // (5.4.2.1b).
            //
            // For [timeX] We allow seconds to be omitted per 5.3.1.3a "Representations
            // with reduced precision". 5.3.1.3b allows just specifying the hour, but
            // we currently don't permit this.
            //
            // Decimal fractions are allowed for hours, minutes and seconds (5.3.14).
            // We only allow fractions for seconds currently. Lower order components
            // can't follow, i.e. you can have T23.3, but not T23.3:04. There must be
            // one digit, but the max number of digits is implemenation defined. We
            // currently allow up to 16 digits of fractional seconds only. While we
            // support 16 fractional digits we only parse the first seven, anything
            // past that is considered a zero. This is to stay compatible with the
            // DateTime implementation which is limited to this resolution.

            if (srcLen < 16u)
            {
                // Source does not have enough characters for YYYY-MM-DDThh:mm
                return false;
            }

            // Parse THH:MM (e.g. "T10:32")
            if (source[10] != JsonUtf16Constant.TimePrefix || source[13] != JsonUtf16Constant.Colon
                || !TryGetNextTwoDigits(source.Slice(start: 11, length: 2), ref parseData.Hour)
                || !TryGetNextTwoDigits(source.Slice(start: 14, length: 2), ref parseData.Minute))
            {
                return false;
            }

            // We now have YYYY-MM-DDThh:mm
            Debug.Assert(srcLen >= 16u);
            if (srcLen == 16u)
            {
                return true;
            }

            char curByte = source[16];
            int sourceIndex = 17;

            // Either a TZD ['Z'|'+'|'-'] or a seconds separator [':'] is valid at this point
            switch (curByte)
            {
                case JsonUtf16Constant.UtcOffsetToken:
                    parseData.OffsetToken = JsonUtf16Constant.UtcOffsetToken;
                    return (uint)sourceIndex == srcLen ? true : false;
                case JsonUtf16Constant.Plus:
                case JsonUtf16Constant.Hyphen:
                    parseData.OffsetToken = curByte;
                    return ParseOffset(ref parseData, source.Slice(sourceIndex));
                case JsonUtf16Constant.Colon:
                    break;
                default:
                    return false;
            }

            // Try reading the seconds
            if (srcLen < 19u
                || !TryGetNextTwoDigits(source.Slice(start: 17, length: 2), ref parseData.Second))
            {
                return false;
            }

            // We now have YYYY-MM-DDThh:mm:ss
            Debug.Assert(srcLen >= 19u);
            if (srcLen == 19u)
            {
                return true;
            }

            curByte = source[19];
            sourceIndex = 20;

            // Either a TZD ['Z'|'+'|'-'] or a seconds decimal fraction separator ['.'] is valid at this point
            switch (curByte)
            {
                case JsonUtf16Constant.UtcOffsetToken:
                    parseData.OffsetToken = JsonUtf16Constant.UtcOffsetToken;
                    return (uint)sourceIndex == srcLen ? true : false;
                case JsonUtf16Constant.Plus:
                case JsonUtf16Constant.Hyphen:
                    parseData.OffsetToken = curByte;
                    return ParseOffset(ref parseData, source.Slice(sourceIndex));
                case JsonUtf16Constant.Period:
                    break;
                default:
                    return false;
            }

            // Source does not have enough characters for second fractions (i.e. ".s")
            // YYYY-MM-DDThh:mm:ss.s
            if (srcLen < 21u)
            {
                return false;
            }

            // Parse fraction. This value should never be greater than 9_999_999
            {
                int numDigitsRead = 0;
                int fractionEnd = Math.Min(sourceIndex + JsonSharedConstant.DateTimeParseNumFractionDigits, source.Length);

                while (sourceIndex < fractionEnd && JsonHelpers.IsDigit(curByte = source[sourceIndex]))
                {
                    if (numDigitsRead < JsonSharedConstant.DateTimeNumFractionDigits)
                    {
                        parseData.Fraction = (parseData.Fraction * 10) + (int)(curByte - (uint)'0');
                        numDigitsRead++;
                    }

                    sourceIndex++;
                }

                if (parseData.Fraction != 0)
                {
                    while (numDigitsRead < JsonSharedConstant.DateTimeNumFractionDigits)
                    {
                        parseData.Fraction *= 10;
                        numDigitsRead++;
                    }
                }
            }

            // We now have YYYY-MM-DDThh:mm:ss.s
            Debug.Assert((uint)sourceIndex <= srcLen);
            if ((uint)sourceIndex == srcLen)
            {
                return true;
            }

            curByte = source[sourceIndex++];

            // TZD ['Z'|'+'|'-'] is valid at this point
            switch (curByte)
            {
                case JsonUtf16Constant.UtcOffsetToken:
                    parseData.OffsetToken = JsonUtf16Constant.UtcOffsetToken;
                    return (uint)sourceIndex == srcLen ? true : false;
                case JsonUtf16Constant.Plus:
                case JsonUtf16Constant.Hyphen:
                    parseData.OffsetToken = curByte;
                    return ParseOffset(ref parseData, source.Slice(sourceIndex))
                        && true;
                default:
                    return false;
            }

            static bool ParseOffset(ref Utf16DateTimeParseData parseData, ReadOnlySpan<char> offsetData)
            {
                uint offsetDataLen = (uint)offsetData.Length;
                // Parse the hours for the offset
                if (offsetDataLen < 2u
                    || !TryGetNextTwoDigits(offsetData.Slice(0, 2), ref parseData.OffsetHours))
                {
                    return false;
                }

                // We now have YYYY-MM-DDThh:mm:ss.s+|-hh

                if (offsetDataLen == 2u)
                {
                    // Just hours offset specified
                    return true;
                }

                // Ensure we have enough for ":mm"
                if (offsetDataLen != 5u
                    || offsetData[2] != JsonUtf16Constant.Colon
                    || !TryGetNextTwoDigits(offsetData.Slice(3), ref parseData.OffsetMinutes))
                {
                    return false;
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetNextTwoDigits(ReadOnlySpan<char> source, ref int value)
        {
            Debug.Assert(source.Length == 2);

            uint digit1 = source[0] - (uint)'0';
            uint digit2 = source[1] - (uint)'0';

            if (digit1 > 9u || digit2 > 9u)
            {
                value = default;
                return false;
            }

            value = (int)(digit1 * 10 + digit2);
            return true;
        }

        // The following methods are borrowed verbatim from src/Common/src/CoreLib/System/Buffers/Text/Utf8Parser/Utf8Parser.Date.Helpers.cs

        /// <summary>Overflow-safe DateTimeOffset factory.</summary>
        private static bool TryCreateDateTimeOffset(DateTime dateTime, ref Utf16DateTimeParseData parseData, out DateTimeOffset value)
        {
            if (((uint)parseData.OffsetHours) > JsonSharedConstant.MaxDateTimeUtcOffsetHours)
            {
                value = default;
                return false;
            }

            if (((uint)parseData.OffsetMinutes) > 59u)
            {
                value = default;
                return false;
            }

            if (parseData.OffsetHours == JsonSharedConstant.MaxDateTimeUtcOffsetHours && parseData.OffsetMinutes != 0)
            {
                value = default;
                return false;
            }

            long offsetTicks = (((long)parseData.OffsetHours) * 3600 + ((long)parseData.OffsetMinutes) * 60) * TimeSpan.TicksPerSecond;
            if (parseData.OffsetNegative)
            {
                offsetTicks = -offsetTicks;
            }

            try
            {
                value = new DateTimeOffset(ticks: dateTime.Ticks, offset: new TimeSpan(ticks: offsetTicks));
            }
            catch (ArgumentOutOfRangeException)
            {
                // If we got here, the combination of the DateTime + UTC offset strayed outside the 1..9999 year range. This case seems rare enough
                // that it's better to catch the exception rather than replicate DateTime's range checking (which it's going to do anyway.)
                value = default;
                return false;
            }

            return true;
        }

        /// <summary>Overflow-safe DateTimeOffset factory.</summary>
        private static bool TryCreateDateTimeOffset(ref Utf16DateTimeParseData parseData, out DateTimeOffset value)
        {
            if (!TryCreateDateTime(parseData, kind: DateTimeKind.Unspecified, out DateTime dateTime))
            {
                value = default;
                return false;
            }

            if (!TryCreateDateTimeOffset(dateTime: dateTime, ref parseData, out value))
            {
                value = default;
                return false;
            }

            return true;
        }

        /// <summary>Overflow-safe DateTimeOffset/Local time conversion factory.</summary>
        private static bool TryCreateDateTimeOffsetInterpretingDataAsLocalTime(Utf16DateTimeParseData parseData, out DateTimeOffset value)
        {
            if (!TryCreateDateTime(parseData, DateTimeKind.Local, out DateTime dateTime))
            {
                value = default;
                return false;
            }

            try
            {
                value = new DateTimeOffset(dateTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                // If we got here, the combination of the DateTime + UTC offset strayed outside the 1..9999 year range. This case seems rare enough
                // that it's better to catch the exception rather than replicate DateTime's range checking (which it's going to do anyway.)
                value = default;
                return false;
            }

            return true;
        }

        /// <summary>Overflow-safe DateTime factory.</summary>
        private static bool TryCreateDateTime(Utf16DateTimeParseData parseData, DateTimeKind kind, out DateTime value)
        {
            if (parseData.Year == 0)
            {
                value = default;
                return false;
            }

            Debug.Assert(parseData.Year <= 9999); // All of our callers to date parse the year from fixed 4-digit fields so this value is trusted.

            if ((((uint)parseData.Month) - 1) >= 12u)
            {
                value = default;
                return false;
            }

            uint dayMinusOne = ((uint)parseData.Day) - 1u;
            if (dayMinusOne >= 28 && dayMinusOne >= DateTime.DaysInMonth(parseData.Year, parseData.Month))
            {
                value = default;
                return false;
            }

            if (((uint)parseData.Hour) > 23u)
            {
                value = default;
                return false;
            }

            if (((uint)parseData.Minute) > 59u)
            {
                value = default;
                return false;
            }

            // This needs to allow leap seconds when appropriate.
            // See https://github.com/dotnet/corefx/issues/39185.
            if (((uint)parseData.Second) > 59u)
            {
                value = default;
                return false;
            }

            Debug.Assert(parseData.Fraction >= 0 && parseData.Fraction <= JsonSharedConstant.MaxDateTimeFraction); // All of our callers to date parse the fraction from fixed 7-digit fields so this value is trusted.

            int[] days = DateTime.IsLeapYear(parseData.Year) ? s_daysToMonth366 : s_daysToMonth365;
            int yearMinusOne = parseData.Year - 1;
            int totalDays = (yearMinusOne * 365) + (yearMinusOne / 4) - (yearMinusOne / 100) + (yearMinusOne / 400) + days[parseData.Month - 1] + parseData.Day - 1;
            long ticks = totalDays * TimeSpan.TicksPerDay;
            int totalSeconds = (parseData.Hour * 3600) + (parseData.Minute * 60) + parseData.Second;
            ticks += totalSeconds * TimeSpan.TicksPerSecond;
            ticks += parseData.Fraction;
            value = new DateTime(ticks: ticks, kind: kind);
            return true;
        }
    }
}