namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Boxer. Fastest on foot, short reach, fast punch strings. Special attack is Rapid Punches.
    /// </summary>
    public static class BoxerFighter
    {
        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Rook",
                Title = "The Hexfist",
                Weapon = "Fists",
                ModelName = "Boxer",
                Stance = FighterStance.Boxer,
                Description = "Fast boxer with short reach. Rushes in with quick punch strings and rapid barrages.",
                MaxHealth = 1000,
                MoveSpeed = ContentBuilder.M(122),
                AirSpeed = ContentBuilder.M(105),
                JumpVelocity = ContentBuilder.M(330),
                KnockbackScale = Fix.One,
                ColorR = 235,
                ColorG = 85,
                ColorB = 70,
            };
            common.ApplyTo(c);

            // Light combo: jab, cross, hook, body blow.
            var jab = b.Move("Jab", 16, MovePose.JabLeft);
            jab.Hitboxes = new[] { Punch(4, 6, 1150, 26, 16, 10, 40) };
            jab.Motions = new[] { ContentBuilder.Motion(1, 4, 60) };
            jab.CancelStart = 6;
            jab.CancelEnd = 15;

            var cross = b.Move("Cross", 18, MovePose.Thrust);
            cross.Hitboxes = new[] { Punch(5, 7, 1250, 32, 18, 10, 50) };
            cross.Motions = new[] { ContentBuilder.Motion(2, 5, 80) };
            cross.CancelStart = 7;
            cross.CancelEnd = 17;

            var hook = b.Move("Hook", 22, MovePose.Swing);
            hook.Hitboxes = new[] { Punch(6, 9, 1100, 38, 22, 12, 120) };
            hook.Hitboxes[0].Radius = ContentBuilder.M(900);
            hook.CancelStart = 9;
            hook.CancelEnd = 20;

            var body = b.Move("Body Blow", 30, MovePose.Thrust);
            var bodyHit = Punch(8, 11, 1200, 52, 26, 14, 260);
            bodyHit.Up = ContentBuilder.M(1000);
            bodyHit.KnockbackV = ContentBuilder.M(100);
            body.Hitboxes = new[] { bodyHit };
            body.Motions = new[] { ContentBuilder.Motion(3, 8, 110) };
            body.CancelStart = 18;
            body.CancelEnd = 28;
            c.LightChain = new[] { jab, cross, hook, body };

            c.Heavy = b.Move("Uppercut", 38, MovePose.Uppercut);
            var upper = ContentBuilder.Hit(12, 16, 1000, 1300, 1000, 85);
            upper.Flags = HitFlags.Launch | HitFlags.GuardBreak;
            upper.Hitstun = 32;
            upper.Blockstun = 16;
            upper.KnockbackH = ContentBuilder.M(60);
            upper.KnockbackV = ContentBuilder.M(380);
            c.Heavy.Hitboxes = new[] { upper };
            c.Heavy.Motions = new[] { ContentBuilder.Motion(6, 11, 120) };
            c.Heavy.CancelStart = 26;
            c.Heavy.CancelEnd = 36;

            c.AirLight = b.Move("Air Jab", 20, MovePose.Thrust);
            var airJab = ContentBuilder.Hit(4, 8, 1000, 800, 800, 34);
            airJab.Hitstun = 20;
            airJab.KnockbackH = ContentBuilder.M(60);
            airJab.KnockbackV = ContentBuilder.M(120);
            c.AirLight.Hitboxes = new[] { airJab };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(350);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 5;
            c.AirLight.CancelStart = 10;
            c.AirLight.CancelEnd = 18;

            c.AirHeavy = CommonMoves.Plunge(b, "Meteor Fist", 1100, 80);

            // Q: Rapid Punches - the boxer's special attack.
            c.Skill1 = b.Move("Rapid Punches", 56, MovePose.Flurry);
            var rapid = ContentBuilder.Hit(6, 41, 1200, 1300, 950, 12);
            rapid.RehitInterval = 4;
            rapid.Hitstun = 12;
            rapid.Blockstun = 7;
            rapid.KnockbackH = ContentBuilder.M(15);
            rapid.Group = 0;
            var rapidEnd = ContentBuilder.Hit(44, 47, 1300, 1300, 1000, 40);
            rapidEnd.Group = 1;
            rapidEnd.Hitstun = 28;
            rapidEnd.KnockbackH = ContentBuilder.M(260);
            rapidEnd.KnockbackV = ContentBuilder.M(120);
            c.Skill1.Hitboxes = new[] { rapid, rapidEnd };
            c.Skill1.Motions = new[] { ContentBuilder.Motion(6, 41, 25) };
            c.Skill1.MagicCost = 110;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(6);
            c.Skill1.CancelStart = 50;
            c.Skill1.CancelEnd = 54;

            // E: Dash Punch - a lunging straight that closes distance.
            c.Skill2 = b.Move("Dash Punch", 34, MovePose.Thrust);
            var dash = ContentBuilder.Hit(6, 13, 1000, 1300, 900, 70);
            dash.Hitstun = 28;
            dash.KnockbackH = ContentBuilder.M(320);
            dash.KnockbackV = ContentBuilder.M(120);
            c.Skill2.Hitboxes = new[] { dash };
            c.Skill2.Motions = new[] { ContentBuilder.Motion(3, 12, 480) };
            c.Skill2.MagicCost = 120;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(7);
            c.Skill2.CancelStart = 24;
            c.Skill2.CancelEnd = 32;

            // R: Final Round - a long steerable barrage, finished by a huge uppercut.
            c.Ultimate = b.Move("Final Round", 150, MovePose.Flurry);
            var barrage = ContentBuilder.Hit(12, 110, 1200, 1300, 1200, 15);
            barrage.RehitInterval = 5;
            barrage.Hitstun = 14;
            barrage.Blockstun = 8;
            barrage.KnockbackH = ContentBuilder.M(10);
            barrage.Group = 0;
            var finisher = ContentBuilder.Hit(118, 123, 1100, 1300, 1300, 120);
            finisher.Flags = HitFlags.Launch | HitFlags.GuardBreak;
            finisher.Group = 1;
            finisher.Hitstun = 40;
            finisher.KnockbackH = ContentBuilder.M(380);
            finisher.KnockbackV = ContentBuilder.M(420);
            c.Ultimate.Hitboxes = new[] { barrage, finisher };
            c.Ultimate.Steerable = true;
            c.Ultimate.SteerSpeed = ContentBuilder.M(60);
            c.Ultimate.MagicCost = 280;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(25);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 12;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "Q", Name = "Rapid Punches", Description = "A flurry of fast punches ending in a push. 110 magic." },
                new AbilityInfo { Key = "E", Name = "Dash Punch", Description = "Lunge forward with a powerful straight. 120 magic." },
                new AbilityInfo { Key = "R", Name = "Final Round", Description = "Two seconds of nonstop punches you can steer, then a huge uppercut. 280 magic." },
            };
            return c;
        }

        private static HitboxDef Punch(int start, int end, int forwardMilli, int damage, int hitstun, int blockstun, int knockbackMilli)
        {
            var h = ContentBuilder.Hit(start, end, forwardMilli, 1350, 700, damage);
            h.Hitstun = hitstun;
            h.Blockstun = blockstun;
            h.KnockbackH = ContentBuilder.M(knockbackMilli);
            return h;
        }
    }
}
