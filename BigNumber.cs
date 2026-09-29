using System;
using System.Globalization;

namespace Moshah.BigNumbers
{
    /// <summary>
    /// Arbitrary-scale number for idle/incremental games — plain C#, no Unity or JSON-library
    /// dependency, safe to copy into any other project as-is (see BigNumberJsonConverter.cs for the
    /// optional Newtonsoft.Json integration, which is the only file with an external dependency).
    ///
    /// Internally stored as mantissa * 10^exponent, with |mantissa| kept normalized to [1, 10) (or
    /// exactly 0 for the value zero) and exponent as a long — this is the same "scientific notation"
    /// representation idle-game big-number libraries (e.g. break_infinity.js) use, and it's what lets
    /// values grow far past double.MaxValue (~1.8e308): a double can't hold an exponent of, say,
    /// 2000, but a `long` exponent can.
    ///
    /// Display/parsing uses a letter-tier suffix: every 3 decimal digits (10^3) is one "tier", tiers
    /// 1-26 are a single letter A-Z, tier 27 onward switches to two letters AA, AB, ... ZZ, and beyond
    /// that naturally continues to three letters (AAA...) via bijective base-26 — the same scheme
    /// Excel uses for column names. So 1000 -> "1A", 1_000_000 -> "1B", 1_000_000_000 -> "1C", and so
    /// on; nothing has to change in this code to go past "ZZ", it just grows another letter.
    /// </summary>
    public readonly struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>, IFormattable
    {
        public double Mantissa { get; }
        public long Exponent { get; }

        public static readonly BigNumber Zero = default;
        public static readonly BigNumber One = new BigNumber(1, 0, raw: true);

        private BigNumber(double mantissa, long exponent, bool raw)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        public BigNumber(double value)
        {
            this = FromDouble(value);
        }

        public BigNumber(long value) : this((double)value) { }

        private static BigNumber FromDouble(double value)
        {
            if (value == 0 || double.IsNaN(value))
                return Zero;

            return Normalize(value, 0);
        }

        /// <summary>Builds a normalized BigNumber from any mantissa/exponent pair — the mantissa does
        /// not need to already be in [1, 10); every operator below routes through this.</summary>
        private static BigNumber Normalize(double mantissa, long exponent)
        {
            if (mantissa == 0 || double.IsNaN(mantissa))
                return Zero;

            double sign = mantissa < 0 ? -1 : 1;
            double abs = Math.Abs(mantissa);

            if (double.IsInfinity(abs))
                return new BigNumber(sign, long.MaxValue, raw: true);

            // abs can be anywhere in (0, +inf) coming in (e.g. after an addition); walk it into [1,10).
            if (abs >= 10)
            {
                int shift = (int)Math.Floor(Math.Log10(abs));
                abs /= Math.Pow(10, shift);
                exponent += shift;
                while (abs >= 10) { abs /= 10; exponent++; }
            }
            else if (abs < 1)
            {
                int shift = (int)Math.Ceiling(-Math.Log10(abs));
                abs *= Math.Pow(10, shift);
                exponent -= shift;
                while (abs < 1) { abs *= 10; exponent--; }
            }

            return new BigNumber(sign * abs, exponent, raw: true);
        }

        public static implicit operator BigNumber(double value) => new BigNumber(value);
        public static implicit operator BigNumber(int value) => new BigNumber((double)value);
        public static implicit operator BigNumber(long value) => new BigNumber((double)value);

        public static BigNumber operator -(BigNumber a) =>
            a.Mantissa == 0 ? Zero : new BigNumber(-a.Mantissa, a.Exponent, raw: true);

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.Mantissa == 0) return b;
            if (b.Mantissa == 0) return a;

            if (a.Exponent < b.Exponent) (a, b) = (b, a);
            long diff = a.Exponent - b.Exponent;
            // Beyond ~17 orders of magnitude the smaller operand can't affect a double's precision.
            if (diff > 17) return a;

            double shiftedB = b.Mantissa / Math.Pow(10, diff);
            return Normalize(a.Mantissa + shiftedB, a.Exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b) => a + (-b);

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.Mantissa == 0 || b.Mantissa == 0) return Zero;
            return Normalize(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            if (b.Mantissa == 0) throw new DivideByZeroException("BigNumber division by zero.");
            if (a.Mantissa == 0) return Zero;
            return Normalize(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        /// <summary>Raises the number to a power. Only meaningful for non-negative bases (upgrade
        /// costs, production curves etc.) — negative bases with fractional powers aren't supported.</summary>
        public BigNumber Pow(double power)
        {
            if (Mantissa == 0) return power == 0 ? One : Zero;

            double log10 = Math.Log10(Math.Abs(Mantissa)) + Exponent;
            double newLog10 = log10 * power;
            long newExponent = (long)Math.Floor(newLog10);
            double newMantissa = Math.Pow(10, newLog10 - newExponent);

            bool negativeResult = Mantissa < 0 && ((long)power % 2 != 0);
            return Normalize(negativeResult ? -newMantissa : newMantissa, newExponent);
        }

        public int CompareTo(BigNumber other)
        {
            if (Mantissa == 0 && other.Mantissa == 0) return 0;

            int signA = Math.Sign(Mantissa);
            int signB = Math.Sign(other.Mantissa);
            if (signA != signB) return signA.CompareTo(signB);

            int expCompare = Exponent.CompareTo(other.Exponent);
            if (expCompare != 0) return signA > 0 ? expCompare : -expCompare;

            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigNumber other) => Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;
        public override bool Equals(object obj) => obj is BigNumber other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);

        public static bool operator ==(BigNumber a, BigNumber b) => a.CompareTo(b) == 0;
        public static bool operator !=(BigNumber a, BigNumber b) => a.CompareTo(b) != 0;
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;

        public static BigNumber Min(BigNumber a, BigNumber b) => a <= b ? a : b;
        public static BigNumber Max(BigNumber a, BigNumber b) => a >= b ? a : b;
        public BigNumber Abs() => Mantissa < 0 ? new BigNumber(-Mantissa, Exponent, raw: true) : this;

        /// <summary>Collapses back to a double — only exact/finite for values within double's range
        /// (roughly up to 1e308, i.e. below letter-tier "PY" or so); anything past that returns
        /// +/-Infinity. Prefer keeping values as BigNumber end-to-end and only calling this at a
        /// boundary that genuinely needs a primitive (e.g. a physics/animation curve).</summary>
        public double ToDouble() => Mantissa == 0 ? 0 : Mantissa * Math.Pow(10, Exponent);

        public override string ToString() => ToString(2);
        public string ToString(string format, IFormatProvider formatProvider) => ToString(2);

        /// <summary>Formats using the letter-tier suffix (e.g. "1A", "2.5B", "13.4AC"); plain numbers
        /// below 1000 have no suffix. <paramref name="decimals"/> is the max fractional digits shown
        /// on the mantissa (trailing zeros are trimmed).</summary>
        public string ToString(int decimals)
        {
            if (Mantissa == 0)
                return "0";

            string sign = Mantissa < 0 ? "-" : "";

            if (Exponent < 3)
                return sign + FormatMantissa(Math.Abs(ToDouble()), decimals);

            long tier = Exponent / 3;
            int remainder = (int)(Exponent - tier * 3);
            double displayValue = Math.Abs(Mantissa) * Math.Pow(10, remainder);
            string letters = LetterTier.ToLetters(tier);

            return sign + FormatMantissa(displayValue, decimals) + letters;
        }

        private static string FormatMantissa(double value, int decimals)
        {
            value = Math.Round(value, Math.Max(decimals, 0));
            string text = value.ToString("F" + Math.Max(decimals, 0), CultureInfo.InvariantCulture);
            if (decimals > 0 && text.Contains('.'))
                text = text.TrimEnd('0').TrimEnd('.');
            return text;
        }

        /// <summary>Parses either a plain number ("1500", "2.5") or a letter-suffixed one ("1A",
        /// "2.5B", "13.4AC"). Letters are case-insensitive.</summary>
        public static bool TryParse(string text, out BigNumber result)
        {
            result = Zero;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            int splitIndex = text.Length;
            while (splitIndex > 0 && char.IsLetter(text[splitIndex - 1]))
                splitIndex--;

            string numberPart = text.Substring(0, splitIndex);
            string letterPart = text.Substring(splitIndex);

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                return false;

            if (letterPart.Length == 0)
            {
                result = new BigNumber(number);
                return true;
            }

            if (!LetterTier.TryFromLetters(letterPart, out long tier))
                return false;

            result = Normalize(number, tier * 3);
            return true;
        }

        public static BigNumber Parse(string text)
        {
            if (!TryParse(text, out var result))
                throw new FormatException($"'{text}' is not a valid BigNumber (expected e.g. \"1500\", \"2.5B\", \"13.4AC\").");
            return result;
        }
    }

    /// <summary>
    /// Bijective base-26 conversion between a 1-indexed tier number and its letter suffix — tier 1 is
    /// "A", tier 26 is "Z", tier 27 is "AA", tier 702 is "ZZ", tier 703 is "AAA", and so on
    /// indefinitely. Same scheme as Excel column names.
    /// </summary>
    internal static class LetterTier
    {
        public static string ToLetters(long tier)
        {
            if (tier <= 0)
                return string.Empty;

            var buffer = new char[16];
            int pos = buffer.Length;

            long n = tier;
            while (n > 0)
            {
                n--;
                int remainder = (int)(n % 26);
                buffer[--pos] = (char)('A' + remainder);
                n /= 26;
            }

            return new string(buffer, pos, buffer.Length - pos);
        }

        public static bool TryFromLetters(string letters, out long tier)
        {
            tier = 0;
            if (string.IsNullOrEmpty(letters))
                return false;

            foreach (char c in letters)
            {
                char upper = char.ToUpperInvariant(c);
                if (upper < 'A' || upper > 'Z')
                    return false;
                tier = tier * 26 + (upper - 'A' + 1);
            }

            return true;
        }
    }
}
