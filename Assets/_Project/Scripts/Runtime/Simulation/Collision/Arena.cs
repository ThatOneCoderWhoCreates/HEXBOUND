namespace HEXBOUND.Simulation
{
    /// <summary>
    /// A solid block standing on the floor, axis-aligned: a raised platform, a wall, a column or a flight of stairs.
    /// Fighters stand on its top and are stopped by its sides; projectiles hit it. A ramp (stairs) rises from 0 at one
    /// end to Height at the other: 1 = rises toward +X, 2 = toward -X, 3 = toward +Z, 4 = toward -Z.
    /// </summary>
    public struct TerrainBox
    {
        public Fix MinX, MaxX, MinZ, MaxZ, Height;
        public byte Ramp;
        /// <summary>Underside height: 0 for blocks standing on the floor; above 0 for roofs and bridge decks you can walk under.</summary>
        public Fix Bottom;
        /// <summary>Breakable structure this block belongs to (index into GameState.StructureHealth), or -1.</summary>
        public sbyte Group;

        public TerrainBox(int minXMilli, int maxXMilli, int minZMilli, int maxZMilli, int heightMilli, byte ramp = 0, int group = -1, int bottomMilli = 0)
        {
            MinX = Fix.Milli(minXMilli); MaxX = Fix.Milli(maxXMilli);
            MinZ = Fix.Milli(minZMilli); MaxZ = Fix.Milli(maxZMilli);
            Height = Fix.Milli(heightMilli);
            Ramp = ramp;
            Group = (sbyte)group;
            Bottom = Fix.Milli(bottomMilli);
        }

        public bool Floating { get { return Bottom.Raw > 0; } }

        /// <summary>The same block turned half a turn around the map center (for symmetric maps).</summary>
        public TerrainBox Mirrored()
        {
            var m = this;
            m.MinX = -MaxX; m.MaxX = -MinX; m.MinZ = -MaxZ; m.MaxZ = -MinZ;
            m.Ramp = Ramp == 1 ? (byte)2 : Ramp == 2 ? (byte)1 : Ramp == 3 ? (byte)4 : Ramp == 4 ? (byte)3 : Ramp;
            // Structures come in mirrored pairs: group 0 <-> 1, 2 <-> 3.
            if (Group >= 0) m.Group = (sbyte)(Group ^ 1);
            return m;
        }

        public bool Contains(Fix x, Fix z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        /// <summary>Top height at a point on (or clamped to) the block.</summary>
        public Fix TopAt(Fix x, Fix z)
        {
            switch (Ramp)
            {
                case 1: return Height * Clamp01((x - MinX) / (MaxX - MinX));
                case 2: return Height * Clamp01((MaxX - x) / (MaxX - MinX));
                case 3: return Height * Clamp01((z - MinZ) / (MaxZ - MinZ));
                case 4: return Height * Clamp01((MaxZ - z) / (MaxZ - MinZ));
                default: return Height;
            }
        }

        private static Fix Clamp01(Fix v)
        {
            return v < Fix.Zero ? Fix.Zero : v > Fix.One ? Fix.One : v;
        }
    }

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
        /// <summary>Raised platforms, stairs, walls and columns (empty on flat maps).</summary>
        public TerrainBox[] Terrain = new TerrainBox[0];
        /// <summary>Health of each breakable structure (temples, bridges); 0 means none can break.</summary>
        public int StructureHealth;
        /// <summary>Number of breakable structures (highest TerrainBox.Group + 1).</summary>
        public int StructureCount;
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
            StructureCount = 0;
            for (int i = 0; i < Terrain.Length; i++) if (Terrain[i].Group + 1 > StructureCount) StructureCount = Terrain[i].Group + 1;
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
    /// Map 2, Sky Ruins: a long courtyard with raised platforms, stairs, roofs, walkways and ruined walls (TerrainBox), so the
    /// ground height changes; GroundAt gives it. Temples and walkways can be destroyed.
    /// </summary>
    public static class Arena
    {
        public const int EdgeSamples = 72;
        private const int AnglePerSample = FixMath.FullTurn / EdgeSamples;

        public const int MapLava = 0;
        public const int MapSanctuary = 1;
        public const int MapRuins = 2;
        public const int MapCount = 3;

        /// <summary>Highest ledge a fighter walks up (or down) without jumping or falling.</summary>
        public static readonly Fix StepHeight = Fix.Milli(450);

        /// <summary>Most pillars any map has (GameState keeps one health value per pillar).</summary>
        public const int MaxPillars = 4;

        /// <summary>Most breakable structures any map has (GameState keeps one health value per structure).</summary>
        public const int MaxStructures = 4;

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

            // Map 2, Sky Ruins: a long cracked courtyard on a mountain top (44 x 52 m, spawn platforms out to 34 m,
            // alcoves to 27 m). Two spawn platforms (north and south) with stairs down, raised terraces in the four
            // corners (two carry ruined temples with roofs you can stand on), raised stone walkways on both sides that
            // you can walk on and under, broken walls and columns for cover and a collapsed altar in the middle.
            // The temples and the walkways are breakable structures (groups 0 to 3, StructureHealth each): at 0 they
            // collapse and crush anyone underneath. Point-symmetric (fair for both sides). Outline: Tools/RuinsBuilder.
            var ruinsHalf = new[]
            {
                new TerrainBox(-9000, 9000, -34000, -24000, 1600),             // south spawn platform
                new TerrainBox(-4000, 4000, -24000, -18000, 1600, 4),          // its stairs
                new TerrainBox(-21000, -13000, -21000, -11000, 1300),          // south-west terrace
                new TerrainBox(-13000, -8000, -17500, -13500, 1300, 2),        // its stairs
                new TerrainBox(13000, 21000, -21000, -11000, 1300),            // south-east terrace (temple)
                new TerrainBox(8000, 13000, -18000, -14000, 1300, 1),          // its stairs
                new TerrainBox(13600, 15000, -20400, -19000, 3500, 0, 0),      // temple columns (structure 0)
                new TerrainBox(19000, 20400, -20400, -19000, 3500, 0, 0),
                new TerrainBox(13600, 15000, -13000, -11600, 3500, 0, 0),
                new TerrainBox(19000, 20400, -13000, -11600, 3500, 0, 0),
                new TerrainBox(13000, 21000, -21000, -11000, 4000, 0, 0, 3500), // temple roof (jump onto it, walk under it)
                new TerrainBox(-19000, -15000, -5000, 5000, 3200, 0, 2, 2600),  // west walkway deck (structure 2)
                new TerrainBox(-19000, -18200, -4800, -4000, 2600, 0, 2),      // its pillars
                new TerrainBox(-15800, -15000, -4800, -4000, 2600, 0, 2),
                new TerrainBox(-19000, -18200, 4000, 4800, 2600, 0, 2),
                new TerrainBox(-15800, -15000, 4000, 4800, 2600, 0, 2),
                new TerrainBox(-19000, -15000, 5000, 9500, 3200, 4, 2),        // its stairs
                new TerrainBox(-11000, -5000, -10000, -8600, 1400),            // broken walls (cover)
                new TerrainBox(5600, 7000, -7000, -2000, 1400),
                new TerrainBox(-12000, -8000, -3000, -1600, 1200),
                new TerrainBox(5000, 9000, -23000, -21800, 1100),
                new TerrainBox(6000, 7600, -21600, -20000, 2800),              // broken columns
                new TerrainBox(10400, 12000, -6000, -4400, 2800),
            };
            var ruinsTerrain = new TerrainBox[ruinsHalf.Length * 2 + 1];
            for (int i = 0; i < ruinsHalf.Length; i++)
            {
                ruinsTerrain[i] = ruinsHalf[i];
                ruinsTerrain[ruinsHalf.Length + i] = ruinsHalf[i].Mirrored();
            }
            ruinsTerrain[ruinsTerrain.Length - 1] = new TerrainBox(-3200, 3200, -3200, 3200, 800); // collapsed altar
            var ruins = new ArenaLayout
            {
                Name = "Sky Ruins",
                EdgeRadiusRaw = new long[]
                {
            2228224, 2236735, 2262598, 2278905, 1813291, 1879976, 1937589, 1963950,
            1960187, 1926101, 1860232, 1760103, 1664838, 1590841, 1534323, 1772482,
            1796769, 1776231, 1769472, 1776231, 1796769, 1772482, 1534323, 1590841,
            1664838, 1760103, 1860232, 1926101, 1960187, 1963950, 1937589, 1879976,
            1813291, 2278905, 2262598, 2236735, 2228224, 2236735, 2262598, 2278905,
            1813291, 1879976, 1937589, 1963950, 1960187, 1926101, 1860232, 1760103,
            1664838, 1590841, 1534323, 1772482, 1796769, 1776231, 1769472, 1776231,
            1796769, 1772482, 1534323, 1590841, 1664838, 1760103, 1860232, 1926101,
            1960187, 1963950, 1937589, 1879976, 1813291, 2278905, 2262598, 2236735,
                },
                Pillars = new FixVec3[0],
                PillarRadius = Fix.Zero,
                PillarHeight = Fix.Zero,
                // The spawn platforms and the side alcoves continue into stairs and paths: invisible walls there.
                WallCenters = new[] { 0, FixMath.HalfTurn / 2, FixMath.HalfTurn, FixMath.HalfTurn * 3 / 2 },
                WallHalfWidths = new[] { FixMath.FullTurn * 17 / 360, FixMath.FullTurn * 16 / 360, FixMath.FullTurn * 17 / 360, FixMath.FullTurn * 16 / 360 },
                Terrain = ruinsTerrain,
                StructureHealth = 2000,
                Spawn0 = new FixVec3(Fix.Zero, Fix.Milli(1600), Fix.Milli(-29000)),
                Spawn1 = new FixVec3(Fix.Zero, Fix.Milli(1600), Fix.Milli(29000)),
            };

            Layouts = new[] { lava, sanctuary, ruins };
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
            _brokenStructures = 0;
        }

        private static int _brokenStructures;

        /// <summary>Marks which structures have collapsed (bit g = group g). GameSimulation sets it every tick.</summary>
        public static void SetBrokenStructures(int mask)
        {
            _brokenStructures = mask;
        }

        public static bool IsStructureBroken(int group)
        {
            return group >= 0 && (_brokenStructures & (1 << group)) != 0;
        }

        /// <summary>Collapsed-structure mask from the game state.</summary>
        public static int BrokenStructureMask(GameState s)
        {
            if (_current.StructureHealth <= 0) return 0;
            int mask = 0;
            for (int g = 0; g < _current.StructureCount && g < MaxStructures; g++)
            {
                if (s.StructureHealth[g] <= 0) mask |= 1 << g;
            }
            return mask;
        }

        /// <summary>The standing structure whose blocks a sphere touches, or -1.</summary>
        public static int StructureNear(FixVec3 p, Fix radius)
        {
            TerrainBox[] t = _current.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].Group < 0 || !IsActive(ref t[i])) continue;
                Fix cx = Fix.Clamp(p.X, t[i].MinX, t[i].MaxX);
                Fix cz = Fix.Clamp(p.Z, t[i].MinZ, t[i].MaxZ);
                Fix cy = Fix.Clamp(p.Y, t[i].Bottom, t[i].Height);
                FixVec3 d = new FixVec3(p.X - cx, p.Y - cy, p.Z - cz);
                if (d.LengthSquared() <= radius * radius) return t[i].Group;
            }
            return -1;
        }

        /// <summary>Middle of a structure's roof or deck (for effects).</summary>
        public static FixVec3 StructureCenter(int group)
        {
            TerrainBox[] t = _current.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].Group != group || !t[i].Floating) continue;
                return new FixVec3((t[i].MinX + t[i].MaxX) * Fix.Half, t[i].Height, (t[i].MinZ + t[i].MaxZ) * Fix.Half);
            }
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].Group == group) return new FixVec3((t[i].MinX + t[i].MaxX) * Fix.Half, t[i].Height, (t[i].MinZ + t[i].MaxZ) * Fix.Half);
            }
            return FixVec3.Zero;
        }

        /// <summary>True if a point is beneath the roof or deck of a structure (inside its footprint, below its underside).</summary>
        public static bool IsUnderStructure(int group, FixVec3 p)
        {
            TerrainBox[] t = _current.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].Group != group || !t[i].Floating) continue;
                if (t[i].Contains(p.X, p.Z) && p.Y < t[i].Bottom) return true;
            }
            return false;
        }

        /// <summary>True if the block exists right now (not part of a collapsed structure).</summary>
        public static bool IsActive(ref TerrainBox b)
        {
            return b.Group < 0 || (_brokenStructures & (1 << b.Group)) == 0;
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
        public static TerrainBox[] Terrain { get { return _current.Terrain; } }

        /// <summary>
        /// Height of the ground under a point: the floor (0) or the highest block top at or a step above the point's
        /// height. A roof or bridge deck overhead does not count while the point is under it.
        /// </summary>
        public static Fix GroundAt(FixVec3 p)
        {
            Fix ground = Fix.Zero;
            Fix reach = p.Y + StepHeight;
            TerrainBox[] t = _current.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                if (!IsActive(ref t[i]) || !t[i].Contains(p.X, p.Z)) continue;
                Fix top = t[i].TopAt(p.X, p.Z);
                if (top > ground && top <= reach) ground = top;
            }
            return ground;
        }

        /// <summary>Index of the block a point is inside (below its top and above its underside), or -1.</summary>
        public static int TerrainIndexAt(FixVec3 p)
        {
            TerrainBox[] t = _current.Terrain;
            for (int i = 0; i < t.Length; i++)
            {
                if (!IsActive(ref t[i]) || !t[i].Contains(p.X, p.Z)) continue;
                if (p.Y >= t[i].TopAt(p.X, p.Z)) continue;
                if (t[i].Floating && p.Y <= t[i].Bottom) continue;
                return i;
            }
            return -1;
        }

        /// <summary>True if a point is inside a block. Used to stop projectiles.</summary>
        public static bool InsideTerrain(FixVec3 p)
        {
            return TerrainIndexAt(p) >= 0;
        }

        /// <summary>True if nothing solid lies on the straight line between two points (blasts do not pass through walls or roofs).</summary>
        public static bool ClearLine(FixVec3 from, FixVec3 to)
        {
            if (_current.Terrain.Length == 0) return true;
            for (int k = 1; k <= 8; k++)
            {
                FixVec3 q = from + (to - from) * Fix.Milli(k * 100 + 50);
                if (TerrainIndexAt(q) >= 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Rising into the underside of a roof or bridge deck: stops the head at the underside. Returns true if it hit.
        /// </summary>
        public static bool HitCeiling(ref FixVec3 next, Fix previousY, Fix height)
        {
            TerrainBox[] t = _current.Terrain;
            bool hit = false;
            for (int i = 0; i < t.Length; i++)
            {
                if (!t[i].Floating || !IsActive(ref t[i]) || !t[i].Contains(next.X, next.Z)) continue;
                if (previousY + height <= t[i].Bottom + Fix.Milli(20) && next.Y + height > t[i].Bottom)
                {
                    next.Y = t[i].Bottom - height;
                    hit = true;
                }
            }
            return hit;
        }

        /// <summary>
        /// Pushes a body (feet at position.Y, the given radius, 1.8 tall) out of the sides of blocks it cannot step onto.
        /// Returns true if it touched one; normal is the last push direction (unit, horizontal).
        /// </summary>
        public static bool PushOutOfTerrain(ref FixVec3 position, Fix radius, out FixVec3 normal)
        {
            return PushOutOfTerrain(ref position, radius, out normal, StepHeight, Fix.Milli(1800));
        }

        /// <summary>As above, with the ledge height the body can still climb (smaller in the air than on foot).</summary>
        public static bool PushOutOfTerrain(ref FixVec3 position, Fix radius, out FixVec3 normal, Fix step)
        {
            return PushOutOfTerrain(ref position, radius, out normal, step, Fix.Milli(1800));
        }

        /// <summary>As above, for a body of the given height (it fits under a roof only if its head clears the underside).</summary>
        public static bool PushOutOfTerrain(ref FixVec3 position, Fix radius, out FixVec3 normal, Fix step, Fix height)
        {
            normal = FixVec3.Zero;
            TerrainBox[] t = _current.Terrain;
            if (t.Length == 0) return false;
            bool touched = false;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < t.Length; i++)
                {
                    TerrainBox b = t[i];
                    if (!IsActive(ref b)) continue;
                    Fix cx = Fix.Clamp(position.X, b.MinX, b.MaxX);
                    Fix cz = Fix.Clamp(position.Z, b.MinZ, b.MaxZ);
                    Fix dx = position.X - cx;
                    Fix dz = position.Z - cz;
                    Fix distSq = dx * dx + dz * dz;
                    if (distSq >= radius * radius) continue;
                    if (position.Y >= b.TopAt(cx, cz) - step) continue;
                    if (b.Floating && position.Y + height <= b.Bottom) continue;
                    if (distSq.Raw > 0)
                    {
                        Fix dist = Fix.Sqrt(distSq);
                        FixVec3 n = new FixVec3(dx / dist, Fix.Zero, dz / dist);
                        position.X = cx + n.X * radius;
                        position.Z = cz + n.Z * radius;
                        normal = n;
                    }
                    else
                    {
                        // The center is inside the block: leave through the nearest side.
                        Fix left = position.X - b.MinX, right = b.MaxX - position.X;
                        Fix back = position.Z - b.MinZ, front = b.MaxZ - position.Z;
                        Fix best = Fix.Min(Fix.Min(left, right), Fix.Min(back, front));
                        if (best == left) { position.X = b.MinX - radius; normal = new FixVec3(-Fix.One, Fix.Zero, Fix.Zero); }
                        else if (best == right) { position.X = b.MaxX + radius; normal = new FixVec3(Fix.One, Fix.Zero, Fix.Zero); }
                        else if (best == back) { position.Z = b.MinZ - radius; normal = new FixVec3(Fix.Zero, Fix.Zero, -Fix.One); }
                        else { position.Z = b.MaxZ + radius; normal = new FixVec3(Fix.Zero, Fix.Zero, Fix.One); }
                    }
                    touched = true;
                }
            }
            return touched;
        }
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
