// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Buffers.Text
{
    internal static class ParserHelpers
    {
        public const int ByteOverflowLength = 3;
        public const int ByteOverflowLengthHex = 2;
        public const int UInt16OverflowLength = 5;
        public const int UInt16OverflowLengthHex = 4;
        public const int UInt32OverflowLength = 10;
        public const int UInt32OverflowLengthHex = 8;
        public const int UInt64OverflowLength = 20;
        public const int UInt64OverflowLengthHex = 16;

        public const int SByteOverflowLength = 3;
        public const int SByteOverflowLengthHex = 2;
        public const int Int16OverflowLength = 5;
        public const int Int16OverflowLengthHex = 4;
        public const int Int32OverflowLength = 10;
        public const int Int32OverflowLengthHex = 8;
        public const int Int64OverflowLength = 19;
        public const int Int64OverflowLengthHex = 16;

        public static readonly byte[] s_hexLookup =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 47
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                       // 63
            0xFF, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                   // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 95
            0xFF, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                   // 111
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 127
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 143
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 159
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 175
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 191
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 207
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 223
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 239
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF              // 255
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigit(int i)
        {
            return (uint)(i - '0') <= ('9' - '0');
        }

        //
        // Enable use of ThrowHelper from TryParse() routines without introducing dozens of non-code-coveraged "value= default; bytesConsumed = 0; return false" boilerplate.
        //
        public static bool TryParseThrowFormatException(out int bytesConsumed)
        {
            bytesConsumed = 0;
            ThrowHelper.ThrowFormatException_BadFormatSpecifier();
            return false;
        }

        //
        // Enable use of ThrowHelper from TryParse() routines without introducing dozens of non-code-coveraged "value= default; bytesConsumed = 0; return false" boilerplate.
        //
        public static bool TryParseThrowFormatException<T>(out T value, out int bytesConsumed)
        {
            value = default;
            return TryParseThrowFormatException(out bytesConsumed);
        }

        public static bool TryGetNextTwoDigits(ReadOnlySpan<byte> source, ref int sourceIndex, out int value)
        {
            if (source.Length - sourceIndex < 2)
            {
                value = default;
                return false;
            }

            uint digit1 = source[sourceIndex++] - (uint)'0';
            uint digit2 = source[sourceIndex++] - (uint)'0';

            if (digit1 > 9 || digit2 > 9)
            {
                value = default;
                return false;
            }

            value = (int)(digit1 * 10 + digit2);
            return true;
        }
    }
}
