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

			Utf8Constants.DateJParserState state = Utf8Constants.DateJParserState.Year;
			bool continueParsing = true;
			int sourceLength = source.Length;
			int sourceIndex = 0;

			value = default;
			bytesConsumed = 0;
			kind = default;

			while (continueParsing && (sourceIndex < sourceLength))
			{
				switch (state)
				{
					case Utf8Constants.DateJParserState.Year:
						if (source.Length - sourceIndex < 4)
							return false;

						uint digit1 = source[sourceIndex++] - (uint)0;
						uint digit2 = source[sourceIndex++] - (uint)0;
						uint digit3 = source[sourceIndex++] - (uint)0;
						uint digit4 = source[sourceIndex++] - (uint)0;

						if (digit1 > 9 || digit2 > 9 || digit3 > 9 || digit4 > 9)
							return false;

						year = (int)(digit1 * 1000 + digit2 * 100 + digit3 * 10 + digit4);

						if ((sourceLength - sourceIndex > 1) && (source[sourceIndex++] == Utf8Constants.Hyphen))
							state = Utf8Constants.DateJParserState.Month;
						else
							return false;
						break;
					case Utf8Constants.DateJParserState.Month:
						if (ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out month) && (sourceLength - sourceIndex > 1) && (source[sourceIndex++] == Utf8Constants.Hyphen))
							state = Utf8Constants.DateJParserState.Day;
						else
							return false;
						break;
					case Utf8Constants.DateJParserState.Day:
						if (!ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out day))
							return false;

						if (sourceLength - sourceIndex > 1)
						{
							switch (source[sourceIndex])
							{
								case Utf8Constants.TimePrefix:
									state = Utf8Constants.DateJParserState.Hour;
									sourceIndex++;
									break;
								case Utf8Constants.UtcOffsetChar:
								case Utf8Constants.Plus:
								case Utf8Constants.Minus:
									return false;
								default:
									continueParsing = false;
									break;
							}
						}
						break;
					case Utf8Constants.DateJParserState.Hour:
						if (ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out hour) && (sourceLength - sourceIndex > 1) && (source[sourceIndex++] == Utf8Constants.Colon))
							state = Utf8Constants.DateJParserState.Minute;
						else
							return false;
						break;
					case Utf8Constants.DateJParserState.Minute:
						if (!ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out minute))
							return false;

						if (sourceLength - sourceIndex > 1)
						{
                            switch (source[sourceIndex])
							{
								case Utf8Constants.Colon:
									state = Utf8Constants.DateJParserState.Second;
									sourceIndex++;
									break;
                                case Utf8Constants.UtcOffsetChar:
									offsetChar = Utf8Constants.UtcOffsetChar;
									continueParsing = false;
									sourceIndex++;
									break;
								case Utf8Constants.Plus:
									offsetChar = Utf8Constants.Plus;
									state = Utf8Constants.DateJParserState.OffsetHours;
									sourceIndex++;
									break;
								case Utf8Constants.Minus:
									offsetChar = Utf8Constants.Minus;
									state = Utf8Constants.DateJParserState.OffsetHours;
									sourceIndex++;
									break;
								default:
									continueParsing = false;
									break;
							}
						}
						break;
					case Utf8Constants.DateJParserState.Second:
						if (!ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out second))
							return false;

						if (sourceLength - sourceIndex > 1)
						{
							switch (source[sourceIndex])
							{
								case Utf8Constants.Period:
									state = Utf8Constants.DateJParserState.Fraction;
									sourceIndex++;
									break;
								case Utf8Constants.UtcOffsetChar:
									offsetChar = Utf8Constants.UtcOffsetChar;
									continueParsing = false;
									sourceIndex++;
									break;
								case Utf8Constants.Plus:
									offsetChar = Utf8Constants.Plus;
									state = Utf8Constants.DateJParserState.OffsetHours;
									sourceIndex++;
									break;
								case Utf8Constants.Minus:
									offsetChar = Utf8Constants.Minus;
									state = Utf8Constants.DateJParserState.OffsetHours;
									sourceIndex++;
									break;
								default:
									continueParsing = false;
									sourceIndex++;
									break;
							}
						}
						break;
					case Utf8Constants.DateJParserState.Fraction:
						while (sourceIndex < sourceLength && ParserHelpers.IsDigit(source[sourceIndex]))
						{
							if (!((fraction * 10) + (int)(source[sourceIndex] - (uint)0) <= Utf8Constants.MaxDateTimeFraction))
							{
								sourceIndex++;
								break;
							}

							fraction = (fraction * 10) + (int)(source[sourceIndex++] - (uint)0);
						}

						if (fraction != 0)
						{
							while (fraction * 10 <= Utf8Constants.MaxDateTimeFraction)
								fraction *= 10;
						}

						if (sourceIndex == sourceLength)
							break;

						switch (source[sourceIndex])
						{
							case Utf8Constants.UtcOffsetChar:
								offsetChar = Utf8Constants.UtcOffsetChar;
								continueParsing = false;
								sourceIndex++;
								break;
							case Utf8Constants.Plus:
								offsetChar = Utf8Constants.Plus;
								state = Utf8Constants.DateJParserState.OffsetHours;
								sourceIndex++;
								break;
							case Utf8Constants.Minus:
								offsetChar = Utf8Constants.Minus;
								state = Utf8Constants.DateJParserState.OffsetHours;
								sourceIndex++;
								break;
							default:
								continueParsing = false;
								break;
						}
						break;
					case Utf8Constants.DateJParserState.OffsetHours:
						if (ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out offsetHours) && (sourceLength - sourceIndex > 1) && (source[sourceIndex++] == Utf8Constants.Colon))
							state = Utf8Constants.DateJParserState.OffsetMinutes;
						else
							return false;
						break;
					case Utf8Constants.DateJParserState.OffsetMinutes:
						if (!ParserHelpers.TryGetNextTwoDigits(source, ref sourceIndex, out offsetMinutes))
							return false;

						continueParsing = false;
						break;
					default:
						Debug.Fail("Parser reached invalid state.");
						return false;
				}
			}

			switch (state)
			{
				case Utf8Constants.DateJParserState.Day:
				case Utf8Constants.DateJParserState.Minute:
				case Utf8Constants.DateJParserState.Second:
				case Utf8Constants.DateJParserState.Fraction:
				case Utf8Constants.DateJParserState.OffsetMinutes:
					break;
				default:
					value = default;
					bytesConsumed = 0;
					kind = default;
					return false;
			}

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
