// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Buffers.Text
{
	public static partial class Utf8Parser
	{
		//
		// Flexible ISO 8601 format. One of
		//
		// ---------------------------------
		// YYYY-MM-DD (eg 1997-07-16)
		// YYYY-MM-DDThh:mm (eg 1997-07-16T19:20)
		// YYYY-MM-DDThh:mm:ss (eg 1997-07-16T19:20:30)
		// YYYY-MM-DDThh:mm:ss.s (eg 1997-07-16T19:20:30.45)
		// YYYY-MM-DDThh:mmTZD (eg 1997-07-16T19:20+01:00)
		// YYYY-MM-DDThh:mm:ssTZD (eg 1997-07-16T19:20:30+01:00)
		// YYYY-MM-DDThh:mm:ss.sTZD (eg 1997-07-16T19:20:30.45Z)
		// YYYY-MM-DDThh:mm:ss.sTZD (eg 1997-07-16T19:20:30.45+01:00)
		// YYYY-MM-DDThh:mm:ss.sTZD (eg 1997-07-16T19:20:30.45-01:00)
		private static bool TryParseDateTimeOffsetJ(ReadOnlySpan<byte> source, out DateTimeOffset value, out int bytesConsumed, out DateTimeKind kind)
		{
			int year = 0;
			int month = 0;
			int day = 0;
			int hour = 0;
			int minute = 0;
			int second = 0;
			int fraction = 0;

			byte offsetChar = default;
			int offsetHours = 0;
			int offsetMinutes = 0;

			int sourceLength = source.Length;
			int sourceIndex = 0;

			value = default;
			bytesConsumed = 0;
			kind = default;

			// Parse year.
			if (source.Length - sourceIndex < 4)
			{
				return false;
			}

			uint digit1 = source[sourceIndex++] - (uint)'0';
			uint digit2 = source[sourceIndex++] - (uint)'0';
			uint digit3 = source[sourceIndex++] - (uint)'0';
			uint digit4 = source[sourceIndex++] - (uint)'0';

			if (digit1 > 9 || digit2 > 9 || digit3 > 9 || digit4 > 9)
			{
				return false;
			}

			year = (int)(digit1 * 1000 + digit2 * 100 + digit3 * 10 + digit4);

			if (!((sourceLength - sourceIndex > 1) && (source[sourceIndex++] == Utf8Constants.Hyphen)))
			{
				return false;
			}

			// Parse month and day.
			if (!(ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out month) &&
				(sourceLength - sourceIndex > 1) &&
				(source[sourceIndex++] == Utf8Constants.Hyphen) &&
				ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out day)))
			{
				return false;
			}

			if (sourceLength - sourceIndex < 1)
			{
				goto FinishedParsing;
			}
			else
			{
				switch (source[sourceIndex])
				{
					case Utf8Constants.UtcOffsetChar:
					case Utf8Constants.Plus:
					case Utf8Constants.Minus:
						return false;
					case Utf8Constants.TimePrefix:
						sourceIndex++;
						// Continue to parse hour and minute.
						break;
					default:
						goto FinishedParsing;
				}
			}

			// Parse hour and minute.
			if (!(ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out hour) &&
				(sourceLength - sourceIndex > 1) &&
				(source[sourceIndex++] == Utf8Constants.Colon) &&
				ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out minute)))
			{
				return false;
			}

			if (sourceLength - sourceIndex < 1)
			{
				goto FinishedParsing;
			}
			else
			{
				switch (source[sourceIndex])
				{
					case Utf8Constants.UtcOffsetChar:
						offsetChar = Utf8Constants.UtcOffsetChar;
						sourceIndex++;
						goto FinishedParsing;
					case Utf8Constants.Plus:
					case Utf8Constants.Minus:
						offsetChar = source[sourceIndex];
						sourceIndex++;
						goto ParseOffset;
					case Utf8Constants.Colon:
						sourceIndex++;
						// Continue to parse second.
						break;
					default:
						goto FinishedParsing;
				}
			}

			// Parse second.
			if (!ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out second))
			{
				return false;
			}

			if (sourceLength - sourceIndex < 1)
			{
				goto FinishedParsing;
			}
			else
			{
				switch (source[sourceIndex])
				{
					case Utf8Constants.UtcOffsetChar:
						offsetChar = Utf8Constants.UtcOffsetChar;
						sourceIndex++;
						goto FinishedParsing;
					case Utf8Constants.Plus:
					case Utf8Constants.Minus:
						offsetChar = source[sourceIndex];
						sourceIndex++;
						goto ParseOffset;
					case Utf8Constants.Period:
						sourceIndex++;
						// Continue to parse fraction.
						break;
					default:
						goto FinishedParsing;
				}
			}

			// Parse fraction.
			while (sourceIndex < sourceLength && ParserHelpers.IsDigit(source[sourceIndex]))
			{
				if (!((fraction * 10) + (int)(source[sourceIndex] - (uint)'0') <= Utf8Constants.MaxDateTimeFraction))
				{
					sourceIndex++;
					break;
				}

				fraction = (fraction * 10) + (int)(source[sourceIndex++] - (uint)'0');
			}

			if (fraction != 0)
			{
				while (fraction * 10 <= Utf8Constants.MaxDateTimeFraction)
				{
					fraction *= 10;
				}
			}

			if (sourceIndex == sourceLength)
			{
				goto FinishedParsing;
			}

			switch (source[sourceIndex])
			{
				case Utf8Constants.UtcOffsetChar:
					offsetChar = Utf8Constants.UtcOffsetChar;
					sourceIndex++;
					goto FinishedParsing;
				case Utf8Constants.Plus:
				case Utf8Constants.Minus:
					offsetChar = source[sourceIndex];
					sourceIndex++;
					goto ParseOffset;
				default:
					goto FinishedParsing;
			}

		ParseOffset:
			// Parse offset hours and minutes.
			if (!(ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out offsetHours) &&
				(sourceLength - sourceIndex > 1) &&
				(source[sourceIndex++] == Utf8Constants.Colon) &&
				ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out offsetMinutes)))
			{
				return false;
			}
			goto FinishedParsing;

		FinishedParsing:
			bytesConsumed = sourceIndex;

			if ((offsetChar != Utf8Constants.UtcOffsetChar) && (offsetChar != Utf8Constants.Plus) && (offsetChar != Utf8Constants.Minus))
			{
				if (!TryCreateDateTimeOffsetInterpretingDataAsLocalTime(year: year, month: month, day: day, hour: hour, minute: minute, second: second, fraction: fraction, out value))
				{
					value = default;
					bytesConsumed = 0;
					kind = default;
					return false;
				}

				kind = DateTimeKind.Unspecified;
				return true;
			}

			if (offsetChar == Utf8Constants.UtcOffsetChar)
			{
				// Same as specifying an offset of "+00:00", except that DateTime's Kind gets set to UTC rather than Local
				if (!TryCreateDateTimeOffset(year: year, month: month, day: day, hour: hour, minute: minute, second: second, fraction: fraction, offsetNegative: false, offsetHours: 0, offsetMinutes: 0, out value))
				{
					value = default;
					bytesConsumed = 0;
					kind = default;
					return false;
				}

				kind = DateTimeKind.Utc;
				return true;
			}

			Debug.Assert(offsetChar == Utf8Constants.Plus || offsetChar == Utf8Constants.Minus);

			if (!TryCreateDateTimeOffset(year: year, month: month, day: day, hour: hour, minute: minute, second: second, fraction: fraction, offsetNegative: offsetChar == Utf8Constants.Minus, offsetHours: offsetHours, offsetMinutes: offsetMinutes, out value))
			{
				value = default;
				bytesConsumed = 0;
				kind = default;
				return false;
			}

			kind = DateTimeKind.Local;
			return true;
		}
	}
}
