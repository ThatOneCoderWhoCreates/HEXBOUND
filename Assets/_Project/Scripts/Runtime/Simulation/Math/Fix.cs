using System;

namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Deterministic fixed-point number with 16 fractional bits, stored in a 64-bit integer.
    /// Every gameplay value in the simulation uses this type instead of float, so both players'
    /// machines always compute bit-identical results.
    /// </summary>
    [Serializable]
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        public const int FractionalBits = 16;
        public const long OneRaw = 1L << FractionalBits;

        public readonly long Raw;

        private Fix(long raw)
        {
            Raw = raw;
        }

        public static readonly Fix Zero = new Fix(0);
        public static readonly Fix One = new Fix(OneRaw);
        public static readonly Fix Half = new Fix(OneRaw / 2);
        public static readonly Fix Two = new Fix(OneRaw * 2);
        public static readonly Fix MinusOne = new Fix(-OneRaw);
        public static readonly Fix MaxValue = new Fix(long.MaxValue);
        public static readonly Fix MinValue = new Fix(long.MinValue);

        public static Fix FromRaw(long raw)
        {
            return new Fix(raw);
        }

        public static Fix FromInt(int value)
        {
            return new Fix((long)value << FractionalBits);
        }

        /// <summary>Creates a value from thousandths. Milli(1500) is 1.5.</summary>
        public static Fix Milli(int thousandths)
        {
            return new Fix(((long)thousandths << FractionalBits) / 1000);
        }

        /// <summary>Creates numerator / denominator.</summary>
        public static Fix Ratio(int numerator, int denominator)
        {
            return new Fix(((long)numerator << FractionalBits) / denominator);
        }

        public static implicit operator Fix(int value)
        {
            return FromInt(value);
        }

        public static Fix operator +(Fix a, Fix b)
        {
            return new Fix(a.Raw + b.Raw);
        }

        public static Fix operator -(Fix a, Fix b)
        {
            return new Fix(a.Raw - b.Raw);
        }

        public static Fix operator -(Fix a)
        {
            return new Fix(-a.Raw);
        }

        public static Fix operator *(Fix a, Fix b)
        {
            return new Fix((a.Raw * b.Raw) >> FractionalBits);
        }

        public static Fix operator *(Fix a, int b)
        {
            return new Fix(a.Raw * b);
        }

        public static Fix operator /(Fix a, Fix b)
        {
            if (b.Raw == 0) return Zero;
            return new Fix((a.Raw << FractionalBits) / b.Raw);
        }

        public static Fix operator /(Fix a, int b)
        {
            if (b == 0) return Zero;
            return new Fix(a.Raw / b);
        }

        public static bool operator ==(Fix a, Fix b) { return a.Raw == b.Raw; }
        public static bool operator !=(Fix a, Fix b) { return a.Raw != b.Raw; }
        public static bool operator <(Fix a, Fix b) { return a.Raw < b.Raw; }
        public static bool operator >(Fix a, Fix b) { return a.Raw > b.Raw; }
        public static bool operator <=(Fix a, Fix b) { return a.Raw <= b.Raw; }
        public static bool operator >=(Fix a, Fix b) { return a.Raw >= b.Raw; }

        public static Fix Abs(Fix a)
        {
            return a.Raw < 0 ? new Fix(-a.Raw) : a;
        }

        public static Fix Min(Fix a, Fix b)
        {
            return a.Raw < b.Raw ? a : b;
        }

        public static Fix Max(Fix a, Fix b)
        {
            return a.Raw > b.Raw ? a : b;
        }

        public static Fix Clamp(Fix value, Fix min, Fix max)
        {
            if (value.Raw < min.Raw) return min;
            if (value.Raw > max.Raw) return max;
            return value;
        }

        public static int Sign(Fix a)
        {
            return a.Raw > 0 ? 1 : (a.Raw < 0 ? -1 : 0);
        }

        /// <summary>Largest integer less than or equal to the value.</summary>
        public int FloorToInt()
        {
            return (int)(Raw >> FractionalBits);
        }

        /// <summary>Integer square root based square root. Returns zero for negative input.</summary>
        public static Fix Sqrt(Fix a)
        {
            if (a.Raw <= 0) return Zero;
            ulong value = (ulong)a.Raw << FractionalBits;
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value) bit >>= 2;
            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return new Fix((long)result);
        }

        /// <summary>Linear interpolation, t in [0, 1].</summary>
        public static Fix Lerp(Fix a, Fix b, Fix t)
        {
            return a + (b - a) * t;
        }

        /// <summary>For presentation only. Never feed the result back into the simulation.</summary>
        public float ToFloat()
        {
            return Raw / (float)OneRaw;
        }

        public bool Equals(Fix other)
        {
            return Raw == other.Raw;
        }

        public override bool Equals(object obj)
        {
            return obj is Fix other && other.Raw == Raw;
        }

        public override int GetHashCode()
        {
            return Raw.GetHashCode();
        }

        public int CompareTo(Fix other)
        {
            return Raw.CompareTo(other.Raw);
        }

        public override string ToString()
        {
            long whole = Raw / OneRaw;
            long frac = (Math.Abs(Raw % OneRaw) * 1000) / OneRaw;
            string sign = Raw < 0 && whole == 0 ? "-" : string.Empty;
            return sign + whole + "." + frac.ToString("000");
        }
    }
}
