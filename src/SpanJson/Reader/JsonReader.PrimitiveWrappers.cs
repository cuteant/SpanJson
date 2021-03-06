﻿// Autogenerated
// ReSharper disable BuiltInTypeReferenceStyle
using System;
using System.Runtime.CompilerServices;
namespace SpanJson
{
    public ref partial struct JsonReader<TSymbol> where TSymbol : struct
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SByte ReadSByte()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8SByte();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16SByte();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int16 ReadInt16()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Int16();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Int16();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 ReadInt32()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Int32();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Int32();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int64 ReadInt64()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Int64();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Int64();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Byte ReadByte()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Byte();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Byte();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 ReadUInt16()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8UInt16();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16UInt16();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt32 ReadUInt32()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8UInt32();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16UInt32();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt64 ReadUInt64()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8UInt64();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16UInt64();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Single ReadSingle()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Single();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Single();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Double ReadDouble()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Double();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Double();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Decimal ReadDecimal()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Decimal();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Decimal();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Boolean ReadBoolean()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Boolean();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Boolean();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Char ReadChar()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Char();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Char();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTime ReadDateTime()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8DateTime();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16DateTime();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset ReadDateTimeOffset()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8DateTimeOffset();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16DateTimeOffset();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ReadTimeSpan()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8TimeSpan();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16TimeSpan();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid ReadGuid()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Guid();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Guid();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String ReadString()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8String();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16String();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Version ReadVersion()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Version();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Version();
            }

            throw ThrowHelper.GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Uri ReadUri()
        {
            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.ByteSize)
            {
                return ReadUtf8Uri();
            }

            if ((uint)Unsafe.SizeOf<TSymbol>() == JsonSharedConstant.CharSize)
            {
                return ReadUtf16Uri();
            }

            throw ThrowHelper.GetNotSupportedException();
        }
    }
}