namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Deterministic trigonometry on integer angles.
    /// Angle units: 65536 = one full turn. Angle 0 points along +Z (Unity forward),
    /// and angles increase clockwise when seen from above (same direction as Unity yaw).
    /// Tables are generated offline and stored as constants so every machine uses identical values.
    /// </summary>
    public static class FixMath
    {
        public const int FullTurn = 65536;
        public const int HalfTurn = 32768;
        public const int QuarterTurn = 16384;
        public const int EighthTurn = 8192;

        private static readonly int[] SinQuarter =
        {
            0, 402, 804, 1206, 1608, 2010, 2412, 2814, 3216, 3617, 4019, 4420, 4821, 5222, 5623, 6023,
            6424, 6824, 7224, 7623, 8022, 8421, 8820, 9218, 9616, 10014, 10411, 10808, 11204, 11600, 11996, 12391,
            12785, 13180, 13573, 13966, 14359, 14751, 15143, 15534, 15924, 16314, 16703, 17091, 17479, 17867, 18253, 18639,
            19024, 19409, 19792, 20175, 20557, 20939, 21320, 21699, 22078, 22457, 22834, 23210, 23586, 23961, 24335, 24708,
            25080, 25451, 25821, 26190, 26558, 26925, 27291, 27656, 28020, 28383, 28745, 29106, 29466, 29824, 30182, 30538,
            30893, 31248, 31600, 31952, 32303, 32652, 33000, 33347, 33692, 34037, 34380, 34721, 35062, 35401, 35738, 36075,
            36410, 36744, 37076, 37407, 37736, 38064, 38391, 38716, 39040, 39362, 39683, 40002, 40320, 40636, 40951, 41264,
            41576, 41886, 42194, 42501, 42806, 43110, 43412, 43713, 44011, 44308, 44604, 44898, 45190, 45480, 45769, 46056,
            46341, 46624, 46906, 47186, 47464, 47741, 48015, 48288, 48559, 48828, 49095, 49361, 49624, 49886, 50146, 50404,
            50660, 50914, 51166, 51417, 51665, 51911, 52156, 52398, 52639, 52878, 53114, 53349, 53581, 53812, 54040, 54267,
            54491, 54714, 54934, 55152, 55368, 55582, 55794, 56004, 56212, 56418, 56621, 56823, 57022, 57219, 57414, 57607,
            57798, 57986, 58172, 58356, 58538, 58718, 58896, 59071, 59244, 59415, 59583, 59750, 59914, 60075, 60235, 60392,
            60547, 60700, 60851, 60999, 61145, 61288, 61429, 61568, 61705, 61839, 61971, 62101, 62228, 62353, 62476, 62596,
            62714, 62830, 62943, 63054, 63162, 63268, 63372, 63473, 63572, 63668, 63763, 63854, 63944, 64031, 64115, 64197,
            64277, 64354, 64429, 64501, 64571, 64639, 64704, 64766, 64827, 64884, 64940, 64993, 65043, 65091, 65137, 65180,
            65220, 65259, 65294, 65328, 65358, 65387, 65413, 65436, 65457, 65476, 65492, 65505, 65516, 65525, 65531, 65535,
            65536
        };

        private static readonly int[] AtanTable =
        {
            0, 41, 81, 122, 163, 204, 244, 285, 326, 367, 407, 448, 489, 529, 570, 610,
            651, 692, 732, 773, 813, 854, 894, 935, 975, 1015, 1056, 1096, 1136, 1177, 1217, 1257,
            1297, 1337, 1377, 1417, 1457, 1497, 1537, 1577, 1617, 1656, 1696, 1736, 1775, 1815, 1854, 1894,
            1933, 1973, 2012, 2051, 2090, 2129, 2168, 2207, 2246, 2285, 2324, 2363, 2401, 2440, 2478, 2517,
            2555, 2594, 2632, 2670, 2708, 2746, 2784, 2822, 2860, 2897, 2935, 2973, 3010, 3047, 3085, 3122,
            3159, 3196, 3233, 3270, 3307, 3344, 3380, 3417, 3453, 3490, 3526, 3562, 3599, 3635, 3670, 3706,
            3742, 3778, 3813, 3849, 3884, 3920, 3955, 3990, 4025, 4060, 4095, 4129, 4164, 4199, 4233, 4267,
            4302, 4336, 4370, 4404, 4438, 4471, 4505, 4539, 4572, 4605, 4639, 4672, 4705, 4738, 4771, 4803,
            4836, 4869, 4901, 4933, 4966, 4998, 5030, 5062, 5094, 5125, 5157, 5188, 5220, 5251, 5282, 5313,
            5344, 5375, 5406, 5437, 5467, 5498, 5528, 5559, 5589, 5619, 5649, 5679, 5708, 5738, 5768, 5797,
            5826, 5856, 5885, 5914, 5943, 5972, 6000, 6029, 6058, 6086, 6114, 6142, 6171, 6199, 6227, 6254,
            6282, 6310, 6337, 6365, 6392, 6419, 6446, 6473, 6500, 6527, 6554, 6580, 6607, 6633, 6660, 6686,
            6712, 6738, 6764, 6790, 6815, 6841, 6867, 6892, 6917, 6943, 6968, 6993, 7018, 7043, 7068, 7092,
            7117, 7141, 7166, 7190, 7214, 7238, 7262, 7286, 7310, 7334, 7358, 7381, 7405, 7428, 7451, 7475,
            7498, 7521, 7544, 7566, 7589, 7612, 7635, 7657, 7679, 7702, 7724, 7746, 7768, 7790, 7812, 7834,
            7856, 7877, 7899, 7920, 7942, 7963, 7984, 8005, 8026, 8047, 8068, 8089, 8110, 8131, 8151, 8172,
            8192
        };

        public static int NormalizeAngle(int angle)
        {
            return angle & (FullTurn - 1);
        }

        public static int DegreesToAngle(int degrees)
        {
            return NormalizeAngle((int)(((long)degrees * FullTurn) / 360));
        }

        public static Fix Sin(int angle)
        {
            angle = NormalizeAngle(angle);
            int quadrant = angle >> 14;
            int within = angle & (QuarterTurn - 1);
            switch (quadrant)
            {
                case 0: return Fix.FromRaw(SinLookup(within));
                case 1: return Fix.FromRaw(SinLookup(QuarterTurn - within));
                case 2: return Fix.FromRaw(-SinLookup(within));
                default: return Fix.FromRaw(-SinLookup(QuarterTurn - within));
            }
        }

        public static Fix Cos(int angle)
        {
            return Sin(angle + QuarterTurn);
        }

        private static long SinLookup(int quarterAngle)
        {
            if (quarterAngle >= QuarterTurn) return SinQuarter[256];
            int index = quarterAngle >> 6;
            int frac = quarterAngle & 63;
            long a = SinQuarter[index];
            long b = SinQuarter[index + 1];
            return a + (((b - a) * frac) >> 6);
        }

        /// <summary>Angle of the horizontal direction (x, z), using this project's angle convention.</summary>
        public static int Atan2(Fix x, Fix z)
        {
            long ax = x.Raw < 0 ? -x.Raw : x.Raw;
            long az = z.Raw < 0 ? -z.Raw : z.Raw;
            if (ax == 0 && az == 0) return 0;

            int baseAngle;
            if (ax <= az)
            {
                baseAngle = AtanLookup((ax << 16) / az);
            }
            else
            {
                baseAngle = QuarterTurn - AtanLookup((az << 16) / ax);
            }

            int result;
            if (x.Raw >= 0 && z.Raw >= 0) result = baseAngle;
            else if (x.Raw >= 0) result = HalfTurn - baseAngle;
            else if (z.Raw < 0) result = HalfTurn + baseAngle;
            else result = FullTurn - baseAngle;
            return NormalizeAngle(result);
        }

        private static int AtanLookup(long ratioRaw)
        {
            if (ratioRaw >= Fix.OneRaw) return AtanTable[256];
            long scaled = ratioRaw * 256;
            int index = (int)(scaled >> 16);
            long frac = scaled & 0xFFFF;
            long a = AtanTable[index];
            long b = AtanTable[index + 1];
            return (int)(a + (((b - a) * frac) >> 16));
        }

        /// <summary>Signed shortest difference from 'from' to 'to', in the range [-32768, 32767].</summary>
        public static int AngleDelta(int from, int to)
        {
            int d = NormalizeAngle(to - from);
            if (d >= HalfTurn) d -= FullTurn;
            return d;
        }

        public static int RotateTowards(int from, int to, int maxStep)
        {
            int d = AngleDelta(from, to);
            if (d > maxStep) d = maxStep;
            else if (d < -maxStep) d = -maxStep;
            return NormalizeAngle(from + d);
        }

        public static int AbsAngleDelta(int a, int b)
        {
            int d = AngleDelta(a, b);
            return d < 0 ? -d : d;
        }
    }
}
