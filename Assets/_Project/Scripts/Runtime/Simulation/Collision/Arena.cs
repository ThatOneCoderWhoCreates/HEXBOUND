namespace HEXBOUND.Simulation
{
    /// <summary>One map's fighting floor: its edge, its solid pillars and the two start points.</summary>
    public sealed class ArenaLayout
    {
        public string Name;
        /// <summary>Playable radius per direction (Arena.EdgeSamples entries), as Fix raw values (65536 = 1 unit).</summary>
        public long[] EdgeRadiusRaw;
        public FixVec3[] Pillars;
        public Fix PillarRadius;
        public Fix PillarHeight;
        /// <summary>Health of each pillar if attacks can break them; 0 means the pillars cannot break.</summary>
        public int PillarHealth;
        /// <summary>
        /// Stretches of the edge that are solid walls (no ring-out): center angle and half width, in simulation
        /// angle units. Used where scenery continues past the edge, like the Sanctuary's gate stairs.
        /// </summary>
        public int[] WallCenters = new int[0];
        public int[] WallHalfWidths = new int[0];
        public FixVec3 Spawn0;
        public FixVec3 Spawn1;
        public Fix MaxRadius;
        public Fix MinRadius;

        internal void Measure()
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
    }

    /// <summary>
    /// The arena floor of the map in play, with its top at height 0 and centered at the origin. Each map's edge is
    /// stored for 72 directions (every 5 degrees) and interpolated in between. Values are fixed-point, identical on
    /// every machine. GameSimulation selects the map from MatchSetup.Map at the start of every tick.
    /// Angle convention matches FixMath: index 0 points along +Z, increasing toward +X.
    ///
    /// Map 0, Lava Pit: the flat rock platform at the center of Lava_Stage.fbx (node "base"). The table and the three
    /// rock spikes were measured from the model (scale 1, minus a 0.3 unit safety margin; Unity's FBX import mirrors X).
    /// Map 1, Celestial Sanctuary: a round stone courtyard (radius 12) with four crystal pillars on the diagonals.
    /// </summary>
    public static class Arena
    {
        public const int EdgeSamples = 72;
        private const int AnglePerSample = FixMath.FullTurn / EdgeSamples;

        public const int MapLava = 0;
        public const int MapSanctuary = 1;
        public const int MapCount = 2;

        /// <summary>Most pillars any map has (GameState keeps one health value per pillar).</summary>
        public const int MaxPillars = 4;

        /// <summary>Height of the lava surface below the Lava Pit platform (presentation only).</summary>
        public static readonly Fix LavaHeight = Fix.Milli(-8450);

        private static readonly ArenaLayout[] Layouts;
        private static ArenaLayout _current;

        static Arena()
        {
            var lava = new ArenaLayout
            {
                Name = "Lava Pit",
                EdgeRadiusRaw = new long[]
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
                },
                Pillars = new[]
                {
                    new FixVec3(Fix.Milli(8700), Fix.Zero, Fix.Milli(-6810)),
                    new FixVec3(Fix.Milli(4240), Fix.Zero, Fix.Milli(9670)),
                    new FixVec3(Fix.Milli(-10570), Fix.Zero, Fix.Milli(-1050)),
                },
                PillarRadius = Fix.Milli(1700),
                PillarHeight = Fix.FromInt(7),
                Spawn0 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(-5)),
                Spawn1 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5)),
            };

            var sanctuaryEdge = new long[EdgeSamples];
            for (int i = 0; i < EdgeSamples; i++) sanctuaryEdge[i] = SanctuaryRadius.Raw;
            Fix d = Fix.Milli(SanctuaryPillarDiagonalMilli);
            var sanctuary = new ArenaLayout
            {
                Name = "Celestial Sanctuary",
                EdgeRadiusRaw = sanctuaryEdge,
                Pillars = new[]
                {
                    new FixVec3(d, Fix.Zero, d),
                    new FixVec3(d, Fix.Zero, -d),
                    new FixVec3(-d, Fix.Zero, -d),
                    new FixVec3(-d, Fix.Zero, d),
                },
                PillarRadius = Fix.Milli(900),
                PillarHeight = Fix.FromInt(4),
                PillarHealth = 700,
                // Invisible walls where a walkway continues past the edge, so nobody falls through it: the gate stairs
                // at the north opening (7.4 m wide) and the three bridges at the east, south and west openings (5.8 m wide).
                WallCenters = new[] { 0, FixMath.HalfTurn / 2, FixMath.HalfTurn, FixMath.HalfTurn * 3 / 2 },
                WallHalfWidths = new[] { FixMath.FullTurn * 20 / 360, FixMath.FullTurn * 14 / 360, FixMath.FullTurn * 14 / 360, FixMath.FullTurn * 14 / 360 },
                Spawn0 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(-5)),
                Spawn1 = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5)),
            };

            Layouts = new[] { lava, sanctuary };
            for (int i = 0; i < Layouts.Length; i++) Layouts[i].Measure();
            _current = lava;
        }

        /// <summary>Celestial Sanctuary: playable radius of the round courtyard.</summary>
        public static readonly Fix SanctuaryRadius = Fix.Milli(12000);

        /// <summary>Celestial Sanctuary: X and Z offset of each crystal pillar (about 7 units from the center).</summary>
        public const int SanctuaryPillarDiagonalMilli = 4950;

        /// <summary>Selects the map used by every Arena query. Out-of-range values fall back to the Lava Pit.</summary>
        public static void Use(int map)
        {
            _current = Layout(map);
            _broken = 0;
        }

        private static int _broken;

        /// <summary>
        /// Marks which pillars are broken (bit i = pillar i) for the collision queries below.
        /// GameSimulation sets this from the game state at the start of every tick.
        /// </summary>
        public static void SetBroken(int mask)
        {
            _broken = mask;
        }

        public static bool IsBroken(int pillar)
        {
            return (_broken & (1 << pillar)) != 0;
        }

        /// <summary>Broken-pillar mask from the game state.</summary>
        public static int BrokenMask(GameState s)
        {
            if (_current.PillarHealth <= 0) return 0;
            int mask = 0;
            for (int i = 0; i < _current.Pillars.Length && i < MaxPillars; i++)
            {
                if (s.PillarHealth[i] <= 0) mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>True if the edge in the direction of the point (from the center) is a solid wall.</summary>
        public static bool IsWallAt(FixVec3 p)
        {
            if (_current.WallCenters.Length == 0) return false;
            int angle = FixMath.Atan2(p.X, p.Z);
            for (int i = 0; i < _current.WallCenters.Length; i++)
            {
                if (FixMath.AbsAngleDelta(angle, _current.WallCenters[i]) <= _current.WallHalfWidths[i]) return true;
            }
            return false;
        }

        /// <summary>Index of a standing pillar touched by a sphere at the point, or -1.</summary>
        public static int PillarAt(FixVec3 p, Fix radius)
        {
            if (p.Y > PillarHeight) return -1;
            for (int i = 0; i < Pillars.Length; i++)
            {
                if (IsBroken(i)) continue;
                Fix dx = p.X - Pillars[i].X;
                Fix dz = p.Z - Pillars[i].Z;
                Fix min = PillarRadius + radius;
                if (dx * dx + dz * dz < min * min) return i;
            }
            return -1;
        }

        public static ArenaLayout Layout(int map)
        {
            return map >= 0 && map < Layouts.Length ? Layouts[map] : Layouts[0];
        }

        public static int CurrentMap
        {
            get { return System.Array.IndexOf(Layouts, _current); }
        }

        public static FixVec3[] Pillars { get { return _current.Pillars; } }
        public static Fix PillarRadius { get { return _current.PillarRadius; } }
        public static Fix PillarHeight { get { return _current.PillarHeight; } }
        public static Fix MaxRadius { get { return _current.MaxRadius; } }
        public static Fix MinRadius { get { return _current.MinRadius; } }
        public static FixVec3 Spawn0 { get { return _current.Spawn0; } }
        public static FixVec3 Spawn1 { get { return _current.Spawn1; } }

        /// <summary>Playable radius in the direction of the given angle (linear interpolation between samples).</summary>
        public static Fix EdgeRadius(int angle)
        {
            return EdgeRadius(_current, angle);
        }

        /// <summary>Playable radius of a given layout in the direction of the angle.</summary>
        public static Fix EdgeRadius(ArenaLayout layout, int angle)
        {
            angle = FixMath.NormalizeAngle(angle);
            int index = angle / AnglePerSample;
            int frac = angle - index * AnglePerSample;
            long a = layout.EdgeRadiusRaw[index % EdgeSamples];
            long b = layout.EdgeRadiusRaw[(index + 1) % EdgeSamples];
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
                if (IsBroken(i)) continue;
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
                if (IsBroken(i)) continue;
                Fix dx = p.X - Pillars[i].X;
                Fix dz = p.Z - Pillars[i].Z;
                Fix min = PillarRadius + radius;
                if (dx * dx + dz * dz < min * min) return true;
            }
            return false;
        }
    }
}
