namespace HEXBOUND.Simulation
{
    /// <summary>
    /// The arena floor: the flat rock platform at the center of the lava arena stage (Lava_Stage.fbx, node "base"),
    /// with its top at height 0 and centered at the origin. Its edge is irregular, so the playable radius is stored
    /// for 72 directions (every 5 degrees) and interpolated in between. The table and the three rock spikes were
    /// measured from the model (scale 1, minus a 0.3 unit safety margin; Unity's FBX import mirrors X, which is
    /// accounted for). Values are fixed-point, identical on every machine.
    /// Angle convention matches FixMath: index 0 points along +Z, increasing toward +X.
    /// </summary>
    public static class Arena
    {
        public const int EdgeSamples = 72;
        private const int AnglePerSample = FixMath.FullTurn / EdgeSamples;

        /// <summary>Playable radius per direction, as Fix raw values (65536 = 1 unit).</summary>
        private static readonly long[] EdgeRadiusRaw =
        {
            616038, 642253, 691405, 760218, 763494, 766771, 773325, 766771,
            763494, 766771, 724173, 697958, 681574, 694682, 727450, 763494,
            756941, 760218, 770048, 786432, 809370, 855245, 835584, 799539,
            773325, 802816, 825754, 858522, 825754, 786432, 773325, 707789,
            655360, 648806, 652083, 655360, 678298, 694682, 730726, 760218,
            737280, 707789, 697958, 707789, 740557, 766771, 688128, 625869,
            576717, 622592, 688128, 763494, 756941, 760218, 773325, 799539,
            865075, 894566, 878182, 868352, 865075, 881459, 878182, 842138,
            809370, 786432, 773325, 766771, 763494, 760218, 691405, 642253
        };

        /// <summary>The three rock spikes standing at the platform's rim. Fighters cannot pass through them.</summary>
        public static readonly FixVec3[] Pillars =
        {
            new FixVec3(Fix.Milli(8700), Fix.Zero, Fix.Milli(-6810)),
            new FixVec3(Fix.Milli(4240), Fix.Zero, Fix.Milli(9670)),
            new FixVec3(Fix.Milli(-10570), Fix.Zero, Fix.Milli(-1050)),
        };

        /// <summary>Solid radius of each spike near the floor.</summary>
        public static readonly Fix PillarRadius = Fix.Milli(1700);

        /// <summary>Spikes block anything below this height.</summary>
        public static readonly Fix PillarHeight = Fix.FromInt(7);

        /// <summary>Height of the lava surface below the platform (presentation and ring-out).</summary>
        public static readonly Fix LavaHeight = Fix.Milli(-8450);

        /// <summary>Largest playable radius in any direction.</summary>
        public static readonly Fix MaxRadius;

        /// <summary>Smallest playable radius in any direction.</summary>
        public static readonly Fix MinRadius;

        public static readonly FixVec3 Spawn0 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(-5));
        public static readonly FixVec3 Spawn1 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5));

        static Arena()
        {
            long max = 0;
            long min = long.MaxValue;
            for (int i = 0; i < EdgeRadiusRaw.Length; i++)
            {
                if (EdgeRadiusRaw[i] > max) max = EdgeRadiusRaw[i];
                if (EdgeRadiusRaw[i] < min) min = EdgeRadiusRaw[i];
            }
            MaxRadius = Fix.FromRaw(max);
            MinRadius = Fix.FromRaw(min);
        }

        /// <summary>Playable radius in the direction of the given angle (linear interpolation between samples).</summary>
        public static Fix EdgeRadius(int angle)
        {
            angle = FixMath.NormalizeAngle(angle);
            int index = angle / AnglePerSample;
            int frac = angle - index * AnglePerSample;
            long a = EdgeRadiusRaw[index % EdgeSamples];
            long b = EdgeRadiusRaw[(index + 1) % EdgeSamples];
            return Fix.FromRaw(a + (b - a) * frac / AnglePerSample);
        }

        public static bool IsInside(FixVec3 p)
        {
            return IsInside(p, Fix.Zero);
        }

        /// <summary>True when the point (height ignored) is inside the floor shrunk by the margin.</summary>
        public static bool IsInside(FixVec3 p, Fix margin)
        {
            Fix distSq = p.X * p.X + p.Z * p.Z;
            Fix quickLimit = MinRadius - margin;
            if (quickLimit > Fix.Zero && distSq <= quickLimit * quickLimit) return true;
            Fix limit = EdgeRadius(FixMath.Atan2(p.X, p.Z)) - margin;
            if (limit <= Fix.Zero) return false;
            return distSq <= limit * limit;
        }

        /// <summary>Moves a point horizontally back inside the floor (shrunk by the margin). Height is kept.</summary>
        public static FixVec3 ClampInside(FixVec3 p, Fix margin)
        {
            if (IsInside(p, margin)) return p;
            Fix dist = Fix.Sqrt(p.X * p.X + p.Z * p.Z);
            if (dist.Raw == 0) return p;
            Fix limit = EdgeRadius(FixMath.Atan2(p.X, p.Z)) - margin;
            if (limit < Fix.Zero) limit = Fix.Zero;
            Fix k = limit / dist;
            p.X = p.X * k;
            p.Z = p.Z * k;
            return p;
        }

        /// <summary>
        /// Pushes a body of the given radius out of the rock spikes. Returns true if it was touching one,
        /// and outputs the push direction (unit, horizontal) so velocity into the spike can be removed.
        /// </summary>
        public static bool PushOutOfPillars(ref FixVec3 position, Fix radius, out FixVec3 normal)
        {
            normal = FixVec3.Zero;
            if (position.Y > PillarHeight) return false;
            bool touched = false;
            for (int i = 0; i < Pillars.Length; i++)
            {
                FixVec3 d = new FixVec3(position.X - Pillars[i].X, Fix.Zero, position.Z - Pillars[i].Z);
                Fix min = PillarRadius + radius;
                Fix distSq = d.LengthSquared();
                if (distSq >= min * min) continue;
                Fix dist = Fix.Sqrt(distSq);
                FixVec3 n = dist.Raw > 0 ? d / dist : FixVec3.FromAngle(FixMath.Atan2(Pillars[i].X, Pillars[i].Z) + FixMath.HalfTurn);
                position.X = Pillars[i].X + n.X * min;
                position.Z = Pillars[i].Z + n.Z * min;
                normal = n;
                touched = true;
            }
            return touched;
        }

        /// <summary>True if a point is inside a spike (used to stop projectiles).</summary>
        public static bool HitsPillar(FixVec3 p, Fix radius)
        {
            if (p.Y > PillarHeight) return false;
            for (int i = 0; i < Pillars.Length; i++)
            {
                Fix dx = p.X - Pillars[i].X;
                Fix dz = p.Z - Pillars[i].Z;
                Fix min = PillarRadius + radius;
                if (dx * dx + dz * dz < min * min) return true;
            }
            return false;
        }
    }
}
