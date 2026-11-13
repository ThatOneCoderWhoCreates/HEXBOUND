namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Staff fighter. Heavy, long reach: a rapid-attack flurry, a ground shockwave that is great for ring-outs,
    /// and a spinning ultimate that pulls the opponent in.
    /// </summary>
    public static class StaffFighter
    {
        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Bram",
                Title = "The Hexwarden",
                Weapon = "Staff",
                Description = "Heavy staff fighter with long reach. Controls space and pushes enemies off the edge.",
                MaxHealth = 1100,
                Radius = ContentBuilder.M(500),
                Height = ContentBuilder.M(1900),
                MoveSpeed = ContentBuilder.M(95),
                AirSpeed = ContentBuilder.M(85),
                JumpVelocity = ContentBuilder.M(300),
                KnockbackScale = ContentBuilder.M(900),
                ColorR = 235,
                ColorG = 165,
                ColorB = 60,
            };
            common.ApplyTo(c);

            var l1 = b.Move("Sweep", 24, MovePose.Swing);
            l1.Hitboxes = new[] { Light(7, 10, 1900, 36, 80) };
            l1.CancelStart = 10;
            l1.CancelEnd = 22;

            var l2 = b.Move("Return Sweep", 24, MovePose.SwingReverse);
            l2.Hitboxes = new[] { Light(7, 10, 1900, 40, 90) };
            l2.CancelStart = 10;
            l2.CancelEnd = 22;

            var l3 = b.Move("Long Jab", 34, MovePose.Thrust);
            var l3hit = ContentBuilder.Hit(9, 13, 2300, 1100, 800, 58);
            l3hit.Hitstun = 26;
            l3hit.KnockbackH = ContentBuilder.M(260);
            l3hit.KnockbackV = ContentBuilder.M(100);
            l3.Hitboxes = new[] { l3hit };
            l3.Motions = new[] { ContentBuilder.Motion(4, 9, 100) };
            l3.CancelStart = 20;
            l3.CancelEnd = 32;
            c.LightChain = new[] { l1, l2, l3 };

            c.Heavy = b.Move("Crushing Slam", 48, MovePose.Slam);
            var heavy = ContentBuilder.Hit(20, 24, 2000, 500, 1500, 100);
            heavy.Flags = HitFlags.GuardBreak | HitFlags.Launch;
            heavy.Hitstun = 32;
            heavy.Blockstun = 20;
            heavy.KnockbackH = ContentBuilder.M(180);
            heavy.KnockbackV = ContentBuilder.M(300);
            c.Heavy.Hitboxes = new[] { heavy };
            c.Heavy.CancelStart = 34;
            c.Heavy.CancelEnd = 46;

            c.AirLight = b.Move("Air Spin", 26, MovePose.Spin);
            var air = ContentBuilder.Hit(5, 11, 800, 800, 1600, 40);
            air.Hitstun = 22;
            air.KnockbackH = ContentBuilder.M(100);
            air.KnockbackV = ContentBuilder.M(120);
            c.AirLight.Hitboxes = new[] { air };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(350);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 6;
            c.AirLight.CancelStart = 13;
            c.AirLight.CancelEnd = 24;

            c.AirHeavy = CommonMoves.Plunge(b, "Meteor Staff", 1500, 90);

            // Q: Flurry - rapid multi-hit jabs.
            c.Skill1 = b.Move("Flurry", 62, MovePose.Flurry);
            var jabs = ContentBuilder.Hit(8, 47, 2000, 1100, 1000, 13);
            jabs.RehitInterval = 5;
            jabs.Hitstun = 14;
            jabs.Blockstun = 8;
            jabs.KnockbackH = ContentBuilder.M(20);
            jabs.Group = 0;
            var finisher = ContentBuilder.Hit(50, 53, 2100, 1100, 1100, 40);
            finisher.Group = 1;
            finisher.Hitstun = 28;
            finisher.KnockbackH = ContentBuilder.M(240);
            finisher.KnockbackV = ContentBuilder.M(120);
            c.Skill1.Hitboxes = new[] { jabs, finisher };
            c.Skill1.Motions = new[] { ContentBuilder.Motion(8, 47, 20) };
            c.Skill1.MagicCost = 120;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(7);
            c.Skill1.CancelStart = 56;
            c.Skill1.CancelEnd = 60;

            // E: Shockwave - ground slam around the fighter with strong knockback.
            c.Skill2 = b.Move("Shockwave", 50, MovePose.Slam);
            var wave = ContentBuilder.Hit(16, 20, 0, 400, 4200, 70);
            wave.Flags = HitFlags.Launch | HitFlags.Radial;
            wave.Hitstun = 30;
            wave.Blockstun = 20;
            wave.KnockbackH = ContentBuilder.M(380);
            wave.KnockbackV = ContentBuilder.M(220);
            c.Skill2.Hitboxes = new[] { wave };
            c.Skill2.MagicCost = 170;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(9);
            c.Skill2.SoftTarget = false;
            c.Skill2.CancelStart = 38;
            c.Skill2.CancelEnd = 48;

            // R: Tempest - steerable spin that pulls the opponent in, then blasts them away.
            c.Ultimate = b.Move("Tempest", 200, MovePose.Spin);
            var spin = ContentBuilder.Hit(10, 185, 0, 1000, 2600, 18);
            spin.Flags = HitFlags.Radial;
            spin.RehitInterval = 10;
            spin.Hitstun = 16;
            spin.Blockstun = 10;
            spin.KnockbackH = ContentBuilder.M(-30);
            spin.Group = 0;
            var blast = ContentBuilder.Hit(188, 192, 0, 1000, 3000, 80);
            blast.Flags = HitFlags.Radial | HitFlags.Launch;
            blast.Group = 1;
            blast.Hitstun = 34;
            blast.KnockbackH = ContentBuilder.M(420);
            blast.KnockbackV = ContentBuilder.M(300);
            c.Ultimate.Hitboxes = new[] { spin, blast };
            c.Ultimate.Steerable = true;
            c.Ultimate.SteerSpeed = ContentBuilder.M(70);
            c.Ultimate.MagicCost = 280;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(25);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 10;
            c.Ultimate.SoftTarget = false;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "Q", Name = "Flurry", Description = "Rapid staff jabs ending in a push. 120 magic." },
                new AbilityInfo { Key = "E", Name = "Shockwave", Description = "Slam the ground and blast everyone nearby away. 170 magic." },
                new AbilityInfo { Key = "R", Name = "Tempest", Description = "Spin for 3 seconds, pulling the opponent in, then blast them away. You can steer. 280 magic." },
            };
            return c;
        }

        private static HitboxDef Light(int start, int end, int forwardMilli, int damage, int knockbackMilli)
        {
            var h = ContentBuilder.Hit(start, end, forwardMilli, 1100, 900, damage);
            h.Hitstun = 20;
            h.Blockstun = 12;
            h.KnockbackH = ContentBuilder.M(knockbackMilli);
            return h;
        }
    }
}
