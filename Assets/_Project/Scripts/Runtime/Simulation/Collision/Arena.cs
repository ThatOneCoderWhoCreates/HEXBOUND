namespace HEXBOUND.Simulation
{
    /// <summary>
    /// The hexagonal arena floor, centered at the origin at height 0, with flat sides facing +Z and -Z.
    /// A point is inside when its distance along each of the three side normals is within the apothem.
    /// </summary>
    public static class Arena
    {
        /// <summary>Distance from the center to a corner.</summary>
        public static readonly Fix Circumradius = Fix.FromInt(13);

        /// <summary>Distance from the center to the middle of a side (circumradius * sqrt(3) / 2).</summary>
        public static readonly Fix Apothem = Fix.Milli(11258);

        private static readonly Fix Sin60 = Fix.FromRaw(56756);
        private static readonly Fix Cos60 = Fix.Half;

        private static readonly FixVec3[] Normals =
        {
            new FixVec3(Fix.Zero, Fix.Zero, Fix.One),
            new FixVec3(Sin60, Fix.Zero, Cos60),
            new FixVec3(Sin60, Fix.Zero, -Cos60),
        };

        public static readonly FixVec3 Spawn0 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(-5));
        public static readonly FixVec3 Spawn1 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5));

        public static bool IsInside(FixVec3 p)
        {
            return IsInside(p, Fix.Zero);
        }

        /// <summary>True when the point is inside the hexagon shrunk by the margin.</summary>
        public static bool IsInside(FixVec3 p, Fix margin)
        {
            Fix limit = Apothem - margin;
            for (int i = 0; i < Normals.Length; i++)
            {
                Fix d = p.X * Normals[i].X + p.Z * Normals[i].Z;
                if (Fix.Abs(d) > limit) return false;
            }
            return true;
        }

        /// <summary>Moves a point horizontally back inside the hexagon (shrunk by the margin). Height is kept.</summary>
        public static FixVec3 ClampInside(FixVec3 p, Fix margin)
        {
            Fix limit = Apothem - margin;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < Normals.Length; i++)
                {
                    FixVec3 n = Normals[i];
                    Fix d = p.X * n.X + p.Z * n.Z;
                    if (d > limit)
                    {
                        Fix push = d - limit;
                        p.X -= n.X * push;
                        p.Z -= n.Z * push;
                    }
                    else if (d < -limit)
                    {
                        Fix push = -limit - d;
                        p.X += n.X * push;
                        p.Z += n.Z * push;
                    }
                }
            }
            return p;
        }
    }
}
