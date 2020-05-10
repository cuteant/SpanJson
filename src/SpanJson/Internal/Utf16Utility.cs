﻿// borrowed from https://github.com/dotnet/corefx/tree/release/3.1/src/Common/src/CoreLib/System/Text/Unicode

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETCOREAPP_3_0_GREATER
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace SpanJson.Internal
{
    internal static class Utf16Utility
    {
        /// <summary>
        /// Returns true iff the UInt32 represents two ASCII UTF-16 characters in machine endianness.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AllCharsInUInt32AreAscii(uint value)
        {
            return 0u >= (value & ~0x007F_007Fu);
        }

        /// <summary>
        /// Returns true iff the UInt64 represents four ASCII UTF-16 characters in machine endianness.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AllCharsInUInt64AreAscii(ulong value)
        {
            return 0ul >= (value & ~0x007F_007F_007F_007Ful);
        }

        /// <summary>
        /// Given a UInt32 that represents two ASCII UTF-16 characters, returns the invariant
        /// lowercase representation of those characters. Requires the input value to contain
        /// two ASCII UTF-16 characters in machine endianness.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ConvertAllAsciiCharsInUInt32ToLowercase(uint value)
        {
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllCharsInUInt32AreAscii(value));

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value >= 'A'
            uint lowerIndicator = value + 0x0080_0080u - 0x0041_0041u;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff the word has value > 'Z'
            uint upperIndicator = value + 0x0080_0080u - 0x005B_005Bu;

            // the 0x80 bit of each word of 'combinedIndicator' will be set iff the word has value >= 'A' and <= 'Z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            // the 0x20 bit of each word of 'mask' will be set iff the word has value >= 'A' and <= 'Z'
            uint mask = (combinedIndicator & 0x0080_0080u) >> 2;

            return value ^ mask; // bit flip uppercase letters [A-Z] => [a-z]
        }

        /// <summary>
        /// Given a UInt32 that represents two ASCII UTF-16 characters, returns the invariant
        /// uppercase representation of those characters. Requires the input value to contain
        /// two ASCII UTF-16 characters in machine endianness.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ConvertAllAsciiCharsInUInt32ToUppercase(uint value)
        {
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllCharsInUInt32AreAscii(value));

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value >= 'a'
            uint lowerIndicator = value + 0x0080_0080u - 0x0061_0061u;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff the word has value > 'z'
            uint upperIndicator = value + 0x0080_0080u - 0x007B_007Bu;

            // the 0x80 bit of each word of 'combinedIndicator' will be set iff the word has value >= 'a' and <= 'z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            // the 0x20 bit of each word of 'mask' will be set iff the word has value >= 'a' and <= 'z'
            uint mask = (combinedIndicator & 0x0080_0080u) >> 2;

            return value ^ mask; // bit flip lowercase letters [a-z] => [A-Z]
        }

        /// <summary>
        /// Given a UInt32 that represents two ASCII UTF-16 characters, returns true iff
        /// the input contains one or more lowercase ASCII characters.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UInt32ContainsAnyLowercaseAsciiChar(uint value)
        {
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllCharsInUInt32AreAscii(value));

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value >= 'a'
            uint lowerIndicator = value + 0x0080_0080u - 0x0061_0061u;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff the word has value > 'z'
            uint upperIndicator = value + 0x0080_0080u - 0x007B_007Bu;

            // the 0x80 bit of each word of 'combinedIndicator' will be set iff the word has value >= 'a' and <= 'z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            return (combinedIndicator & 0x0080_0080u) != 0;
        }

        /// <summary>
        /// Given a UInt32 that represents two ASCII UTF-16 characters, returns true iff
        /// the input contains one or more uppercase ASCII characters.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UInt32ContainsAnyUppercaseAsciiChar(uint value)
        {
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllCharsInUInt32AreAscii(value));

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value >= 'A'
            uint lowerIndicator = value + 0x0080_0080u - 0x0041_0041u;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff the word has value > 'Z'
            uint upperIndicator = value + 0x0080_0080u - 0x005B_005Bu;

            // the 0x80 bit of each word of 'combinedIndicator' will be set iff the word has value >= 'A' and <= 'Z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            return (combinedIndicator & 0x0080_0080u) != 0;
        }

        /// <summary>
        /// Given two UInt32s that represent two ASCII UTF-16 characters each, returns true iff
        /// the two inputs are equal using an ordinal case-insensitive comparison.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UInt32OrdinalIgnoreCaseAscii(uint valueA, uint valueB)
        {
            // ASSUMPTION: Caller has validated that input values are ASCII.
            Debug.Assert(AllCharsInUInt32AreAscii(valueA));
            Debug.Assert(AllCharsInUInt32AreAscii(valueB));

            // a mask of all bits which are different between A and B
            uint differentBits = valueA ^ valueB;

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value < 'A'
            uint lowerIndicator = valueA + 0x0100_0100u - 0x0041_0041u;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff (word | 0x20) has value > 'z'
            uint upperIndicator = (valueA | 0x0020_0020u) + 0x0080_0080u - 0x007B_007Bu;

            // the 0x80 bit of each word of 'combinedIndicator' will be set iff the word is *not* [A-Za-z]
            uint combinedIndicator = lowerIndicator | upperIndicator;

            // Shift all the 0x80 bits of 'combinedIndicator' into the 0x20 positions, then set all bits
            // aside from 0x20. This creates a mask where all bits are set *except* for the 0x20 bits
            // which correspond to alpha chars (either lower or upper). For these alpha chars only, the
            // 0x20 bit is allowed to differ between the two input values. Every other char must be an
            // exact bitwise match between the two input values. In other words, (valueA & mask) will
            // convert valueA to uppercase, so (valueA & mask) == (valueB & mask) answers "is the uppercase
            // form of valueA equal to the uppercase form of valueB?" (Technically if valueA has an alpha
            // char in the same position as a non-alpha char in valueB, or vice versa, this operation will
            // result in nonsense, but it'll still compute as inequal regardless, which is what we want ultimately.)
            // The line below is a more efficient way of doing the same check taking advantage of the XOR
            // computation we performed at the beginning of the method.

            return 0u >= (((combinedIndicator >> 2) | ~0x0020_0020u) & differentBits);
        }

        /// <summary>
        /// Given two UInt64s that represent four ASCII UTF-16 characters each, returns true iff
        /// the two inputs are equal using an ordinal case-insensitive comparison.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UInt64OrdinalIgnoreCaseAscii(ulong valueA, ulong valueB)
        {
            // ASSUMPTION: Caller has validated that input values are ASCII.
            Debug.Assert(AllCharsInUInt64AreAscii(valueA));
            Debug.Assert(AllCharsInUInt64AreAscii(valueB));

            // the 0x80 bit of each word of 'lowerIndicator' will be set iff the word has value >= 'A'
            ulong lowerIndicator = valueA + 0x0080_0080_0080_0080ul - 0x0041_0041_0041_0041ul;

            // the 0x80 bit of each word of 'upperIndicator' will be set iff (word | 0x20) has value <= 'z'
            ulong upperIndicator = (valueA | 0x0020_0020_0020_0020ul) + 0x0100_0100_0100_0100ul - 0x007B_007B_007B_007Bul;

            // the 0x20 bit of each word of 'combinedIndicator' will be set iff the word is [A-Za-z]
            ulong combinedIndicator = (0x0080_0080_0080_0080ul & lowerIndicator & upperIndicator) >> 2;

            // Convert both values to lowercase (using the combined indicator from the first value)
            // and compare for equality. It's possible that the first value will contain an alpha character
            // where the second value doesn't (or vice versa), and applying the combined indicator will
            // create nonsensical data, but the comparison would have failed anyway in this case so it's
            // a safe operation to perform.
            //
            // This 64-bit method is similar to the 32-bit method, but it performs the equivalent of convert-to-
            // lowercase-then-compare rather than convert-to-uppercase-and-compare. This particular operation
            // happens to be faster on x64.

            return (valueA | combinedIndicator) == (valueB | combinedIndicator);
        }
    }
}
#endif
