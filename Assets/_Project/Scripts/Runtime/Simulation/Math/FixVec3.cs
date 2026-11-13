using System;

namespace HEXBOUND.Simulation
{
    /// <summary>Deterministic 3D vector. X is right, Y is up, Z is forward (Unity axes).</summary>
    [Serializable]
    public struct FixVec3 : IEquatable<FixVec3>
    {
        public Fix X;
        public Fix Y;
        public Fix Z;

        public FixVec3(Fix x, Fix y, Fix z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly FixVec3 Zero = new FixVec3(Fix.Zero, Fix.Zero, Fix.Zero);
        public static readonly FixVec3 Up = new FixVec3(Fix.Zero, Fix.One, Fix.Zero);

        /// <summary>Horizontal unit vector for an angle (see FixMath for the convention).</summary>
        public static FixVec3 FromAngle(int angle)
        {
            return new FixVec3(FixMath.Sin(angle), Fix.Zero, FixMath.Cos(angle));
        }

        public static FixVec3 operator +(FixVec3 a, FixVec3 b)
        {
            return new FixVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static FixVec3 operator -(FixVec3 a, FixVec3 b)
        {
            return new FixVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static FixVec3 operator -(FixVec3 a)
        {
            return new FixVec3(-a.X, -a.Y, -a.Z);
        }

        public static FixVec3 operator *(FixVec3 a, Fix s)
        {
            return new FixVec3(a.X * s, a.Y * s, a.Z * s);
        }

        public static FixVec3 operator /(FixVec3 a, Fix s)
        {
            return new FixVec3(a.X / s, a.Y / s, a.Z / s);
        }

        public static bool operator ==(FixVec3 a, FixVec3 b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(FixVec3 a, FixVec3 b)
        {
            return !(a == b);
        }

        public static Fix Dot(FixVec3 a, FixVec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public Fix LengthSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        public Fix Length()
        {
            return Fix.Sqrt(LengthSquared());
        }

        public FixVec3 Horizontal()
        {
            return new FixVec3(X, Fix.Zero, Z);
        }

        public Fix HorizontalLength()
        {
            return Fix.Sqrt(X * X + Z * Z);
        }

        /// <summary>Unit vector in the same direction, or zero if the length is zero.</summary>
        public FixVec3 Normalized()
        {
            Fix len = Length();
            if (len.Raw == 0) return Zero;
            return this / len;
        }

        /// <summary>Horizontal angle of this vector (Y ignored).</summary>
        public int ToAngle()
        {
            return FixMath.Atan2(X, Z);
        }

        public bool Equals(FixVec3 other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is FixVec3 other && this == other;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 7) ^ (Z.GetHashCode() << 13);
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Z + ")";
        }
    }
}
