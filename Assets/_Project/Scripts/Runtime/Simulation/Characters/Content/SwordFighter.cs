namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Swordsman. Balanced fighter: fast light combo, a launching dash slash, a ranged sword wave,
    /// and an ultimate that switches to dual swords for a while.
    /// All numbers are in ticks (60 per second), thousandths of a unit, or units per tick.
    /// </summary>
    public static class SwordFighter
    {
        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Kael",
                Title = "The Hexblade",
                Weapon = "Sword",
                Description = "Balanced swordsman. Closes distance fast and can switch to twin blades.",
                MaxHealth = 1000,
                MoveSpeed = ContentBuilder.M(105),
                AirSpeed = ContentBuilder.M(95),
                KnockbackScale = Fix.One,
                ColorR = 90,
                ColorG = 150,
                ColorB = 235,
            };
            common.ApplyTo(c);

            // Light combo: slash, back slash, thrust.
            var l1 = b.Move("Slash", 22, MovePose.Swing);
            l1.Hitboxes = new[] { Light(6, 9, 1400, 38, 60) };
            l1.Motions = new[] { ContentBuilder.Motion(2, 6, 80) };
            l1.CancelStart = 9;
            l1.CancelEnd = 20;

            var l2 = b.Move("Back Slash", 22, MovePose.SwingReverse);
            l2.Hitboxes = new[] { Light(6, 9, 1400, 42, 70) };
            l2.Motions = new[] { ContentBuilder.Motion(2, 6, 80) };
            l2.CancelStart = 9;
            l2.CancelEnd = 20;

            var l3 = b.Move("Piercing Thrust", 32, MovePose.Thrust);
            var l3hit = ContentBuilder.Hit(8, 12, 1650, 1100, 1000, 60);
            l3hit.Hitstun = 26;
            l3hit.KnockbackH = ContentBuilder.M(220);
            l3hit.KnockbackV = ContentBuilder.M(120);
            l3.Hitboxes = new[] { l3hit };
            l3.Motions = new[] { ContentBuilder.Motion(3, 8, 120) };
            l3.CancelStart = 18;
            l3.CancelEnd = 30;
            c.LightChain = new[] { l1, l2, l3 };

            c.Heavy = b.Move("Hex Cleave", 44, MovePose.Overhead);
            var heavy = ContentBuilder.Hit(18, 22, 1500, 1000, 1200, 95);
            heavy.Flags = HitFlags.GuardBreak | HitFlags.Launch;
            heavy.Hitstun = 30;
            heavy.Blockstun = 18;
            heavy.KnockbackH = ContentBuilder.M(250);
            heavy.KnockbackV = ContentBuilder.M(220);
            c.Heavy.Hitboxes = new[] { heavy };
            c.Heavy.Motions = new[] { ContentBuilder.Motion(12, 18, 70) };
            c.Heavy.CancelStart = 30;
            c.Heavy.CancelEnd = 42;

            c.AirLight = b.Move("Air Slash", 24, MovePose.Swing);
            var air = ContentBuilder.Hit(5, 10, 1200, 600, 1000, 40);
            air.Hitstun = 22;
            air.KnockbackH = ContentBuilder.M(80);
            air.KnockbackV = ContentBuilder.M(120);
            c.AirLight.Hitboxes = new[] { air };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(350);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 6;
            c.AirLight.CancelStart = 12;
            c.AirLight.CancelEnd = 22;

            c.AirHeavy = CommonMoves.Plunge(b, "Falling Blade", 1200, 85);

            // Q: Rising Slash - dash forward and launch the opponent.
            c.Skill1 = b.Move("Rising Slash", 40, MovePose.Uppercut);
            var rise = ContentBuilder.Hit(8, 15, 1300, 1200, 1100, 90);
            rise.Flags = HitFlags.Launch;
            rise.Hitstun = 34;
            rise.KnockbackH = ContentBuilder.M(60);
            rise.KnockbackV = ContentBuilder.M(360);
            c.Skill1.Hitboxes = new[] { rise };
            c.Skill1.Motions = new[] { ContentBuilder.Motion(3, 9, 420), ContentBuilder.Vertical(10, 16, 60, 180) };
            c.Skill1.MagicCost = 120;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(6);
            c.Skill1.CancelStart = 28;
            c.Skill1.CancelEnd = 38;

            // E: Hex Wave - ranged sword wave.
            var wave = b.Projectile("Hex Wave");
            wave.Speed = ContentBuilder.M(420);
            wave.Lifetime = 42;
            wave.Radius = ContentBuilder.M(1000);
            wave.SpawnForward = ContentBuilder.M(1000);
            wave.SpawnUp = ContentBuilder.M(1000);
            wave.Damage = 85;
            wave.Hitstun = 26;
            wave.KnockbackH = ContentBuilder.M(180);
            wave.KnockbackV = ContentBuilder.M(100);
            wave.VisualId = 1;

            c.Skill2 = b.Move("Hex Wave", 36, MovePose.Swing);
            c.Skill2.Spawns = new[] { ContentBuilder.Spawn(12, wave) };
            c.Skill2.MagicCost = 150;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(8);
            c.Skill2.UsableInAir = true;
            c.Skill2.GravityScale = ContentBuilder.M(300);
            c.Skill2.CancelStart = 24;
            c.Skill2.CancelEnd = 34;

            // R: Twin Blades - dual swords for 10 seconds.
            c.Ultimate = b.Move("Twin Blades", 40, MovePose.Spin);
            var burst = ContentBuilder.Hit(12, 16, 0, 1000, 2600, 60);
            burst.Flags = HitFlags.Launch | HitFlags.Radial;
            burst.Hitstun = 30;
            burst.KnockbackH = ContentBuilder.M(300);
            burst.KnockbackV = ContentBuilder.M(150);
            c.Ultimate.Hitboxes = new[] { burst };
            c.Ultimate.ApplyBuff = BuffId.TwinBlades;
            c.Ultimate.BuffTick = 14;
            c.Ultimate.BuffDuration = ContentBuilder.Seconds(10);
            c.Ultimate.MagicCost = 300;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(20);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 16;
            c.Ultimate.SoftTarget = false;
            c.UltimateBuff = BuffId.TwinBlades;
            c.BuffSpeedScale = ContentBuilder.M(1150);

            // Twin Blades moves: faster four-hit combo and a cross slash.
            var d1 = b.Move("Twin Slash", 16, MovePose.Swing);
            d1.Hitboxes = new[] { Light(4, 7, 1400, 34, 50) };
            d1.CancelStart = 7;
            d1.CancelEnd = 15;
            var d2 = b.Move("Twin Back Slash", 16, MovePose.SwingReverse);
            d2.Hitboxes = new[] { Light(4, 7, 1400, 34, 50) };
            d2.CancelStart = 7;
            d2.CancelEnd = 15;
            var d3 = b.Move("Twin Whirl", 18, MovePose.Spin);
            d3.Hitboxes = new[] { Light(4, 8, 900, 36, 60) };
            d3.Hitboxes[0].Radius = ContentBuilder.M(1600);
            d3.CancelStart = 8;
            d3.CancelEnd = 17;
            var d4 = b.Move("Twin Rise", 28, MovePose.Uppercut);
            var d4hit = ContentBuilder.Hit(5, 9, 1400, 1200, 1100, 58);
            d4hit.Flags = HitFlags.Launch;
            d4hit.Hitstun = 30;
            d4hit.KnockbackH = ContentBuilder.M(150);
            d4hit.KnockbackV = ContentBuilder.M(300);
            d4.Hitboxes = new[] { d4hit };
            d4.CancelStart = 14;
            d4.CancelEnd = 26;
            c.BuffLightChain = new[] { d1, d2, d3, d4 };

            c.BuffHeavy = b.Move("Cross Slash", 36, MovePose.Overhead);
            var cross = ContentBuilder.Hit(12, 17, 1600, 1000, 1400, 110);
            cross.Flags = HitFlags.GuardBreak | HitFlags.Launch;
            cross.Hitstun = 32;
            cross.Blockstun = 20;
            cross.KnockbackH = ContentBuilder.M(300);
            cross.KnockbackV = ContentBuilder.M(250);
            c.BuffHeavy.Hitboxes = new[] { cross };
            c.BuffHeavy.Motions = new[] { ContentBuilder.Motion(6, 12, 200) };
            c.BuffHeavy.CancelStart = 24;
            c.BuffHeavy.CancelEnd = 34;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "Q", Name = "Rising Slash", Description = "Dash forward and launch the opponent. 120 magic." },
                new AbilityInfo { Key = "E", Name = "Hex Wave", Description = "Send a slicing wave forward. 150 magic." },
                new AbilityInfo { Key = "R", Name = "Twin Blades", Description = "Draw a second sword for 10 seconds: faster combos, stronger heavy. 300 magic." },
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
