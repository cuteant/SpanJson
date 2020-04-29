namespace SpanJson.Internal
{
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public static partial class TextEncodings
    {
        // borrowed from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Primitives/System/Text/Encoders

        public static partial class Utf8
        {
            static readonly int MaxBytesPerCharUtf8 = UTF8NoBOM.GetMaxByteCount(1);
            /// <summary>For short strings use only.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetMaxByteCount(string seq) => seq.Length * MaxBytesPerCharUtf8;
            /// <summary>For short strings use only.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetMaxByteCount(int charCount) => charCount * MaxBytesPerCharUtf8;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetCharCount(in ReadOnlySpan<byte> utf8Bytes)
            {
                if (utf8Bytes.IsEmpty) { return 0; }
                if (TryGetCharCountFast(utf8Bytes, out int totalCharCount)) { return totalCharCount; }
                return GetCharCountSlow(utf8Bytes);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static int GetCharCountSlow(in ReadOnlySpan<byte> utf8Bytes)
            {
                // TryFast
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                return UTF8NoBOM.GetCharCount(utf8Bytes);
#else
                // It's ok for us to pass null pointers down to the workhorse routine.

                unsafe
                {
                    fixed (byte* bytesPtr = &MemoryMarshal.GetReference(utf8Bytes))
                    {
                        return UTF8NoBOM.GetCharCount(bytesPtr, utf8Bytes.Length);
                    }
                }
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetByteCount(in ReadOnlySpan<char> text)
            {
                if (text.IsEmpty) { return 0; }
                if (TryGetByteCountFast(text, out var bytesNeeded)) { return bytesNeeded; }
                return GetByteCountSlow(text);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static int GetByteCountSlow(in ReadOnlySpan<char> text)
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                return UTF8NoBOM.GetByteCount(text);
#else
                unsafe
                {
                    fixed (char* charPtr = text)
                    {
                        return UTF8NoBOM.GetByteCount(charPtr, text.Length);
                    }
                }
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetBytes(in ReadOnlySpan<char> chars, Span<byte> bytes)
            {
#if NETCOREAPP_2_X_GREATER || NETSTANDARD_2_0_GREATER
                return UTF8NoBOM.GetBytes(chars, bytes);
#else
                if (chars.IsEmpty) { return 0; }

                var result = Utf16.ToUtf8(chars, bytes, out _, out var bytesWritten);
                Debug.Assert(result == OperationStatus.Done);
                return bytesWritten;
#endif
            }

            private static readonly ConcurrentDictionary<string, byte[]> s_cachedUtf8Bytes = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            /// <summary>For short strings use only.</summary>
            public static byte[] GetBytesWithCache(string formattedName)
            {
                return s_cachedUtf8Bytes.GetOrAdd(formattedName, _ => UTF8NoBOM.GetBytes(_));
            }

            static readonly AsymmetricKeyHashTable<string> s_stringCache = new AsymmetricKeyHashTable<string>(StringReadOnlySpanByteAscymmetricEqualityComparer.Instance);
            /// <summary>For short strings use only.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string GetStringWithCache(in ReadOnlySpan<byte> utf8Source)
            {
                if (utf8Source.IsEmpty) { return string.Empty; }

                if (s_stringCache.TryGetValue(utf8Source, out var value)) { return value; }

                return GetStringWithCacheSlow(utf8Source);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static string GetStringWithCacheSlow(in ReadOnlySpan<byte> utf8Source)
            {
                var buffer = utf8Source.ToArray();
                var value = UTF8NoBOM.GetString(buffer);
                s_stringCache.TryAdd(buffer, value);
                return value;
            }
        }
    }
}
