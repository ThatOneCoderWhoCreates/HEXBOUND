namespace HEXBOUND.Simulation
{
    /// <summary>
    /// The mage with the long staff. Left click: swing from the left, swing from the right, then a thrust that
    /// pushes the opponent far away. Right click: guard with the staff held sideways (blocks and parries).
    /// C + left click: a low slide with the staff tip leading. Ultimate: a translucent golden dragon that flies
    /// straight out from the staff tip.
    /// </summary>
    public static class StaffFighter
    {
        public const int DragonVisual = 12;

        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Bram",
                Title = "The Dragon Mage",
                Weapon = "Staff",
                ModelName = "Staff",
                WeaponModel = "MageWeapon",
                HeavyIsGuard = true,
                Description = "A mage with a long staff. Long reach, a sideways staff guard, and a golden dragon.",
                MaxHealth = 1100,
                Radius = ContentBuilder.M(500),
                Height = ContentBuilder.M(1900),
                MoveSpeed = ContentBuilder.M(95),
                AirSpeed = ContentBuilder.M(85),
                JumpVelocity = ContentBuilder.M(360),
                KnockbackScale = ContentBuilder.M(900),
                ColorR = 235,
                ColorG = 165,
                ColorB = 60,
            };
            common.ApplyTo(c);

            // Left click: swing from the left, swing from the right, then a pushing thrust.
            var l1 = b.Move("Left Swing", 24, MovePose.SwingReverse);
            l1.Hitboxes = new[] { Light(7, 10, 1900, 36, 80) };
            l1.CancelStart = 10;
            l1.CancelEnd = 22;

            var l2 = b.Move("Right Swing", 24, MovePose.Swing);
            l2.Hitboxes = new[] { Light(7, 10, 1900, 40, 90) };
            l2.CancelStart = 10;
            l2.CancelEnd = 22;

            var l3 = b.Move("Repelling Thrust", 34, MovePose.Thrust);
            var l3hit = ContentBuilder.Hit(9, 13, 2300, 1100, 900, 55);
            l3hit.Flags = HitFlags.Launch;
            l3hit.Hitstun = 30;
            l3hit.KnockbackH = ContentBuilder.M(420);
            l3hit.KnockbackV = ContentBuilder.M(120);
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

            // C + left click: slide forward low with the staff tip pointed at the opponent.
            c.CrouchAttack = b.Move("Dragon Slide", 38, MovePose.Lunge);
            var slide = ContentBuilder.Hit(6, 16, 1600, 600, 800, 60);
            slide.Flags = HitFlags.Low | HitFlags.Launch;
            slide.Hitstun = 28;
            slide.Blockstun = 14;
            slide.KnockbackH = ContentBuilder.M(220);
            slide.KnockbackV = ContentBuilder.M(150);
            c.CrouchAttack.Hitboxes = new[] { slide };
            c.CrouchAttack.Motions = new[] { ContentBuilder.Motion(3, 14, 330), ContentBuilder.Motion(15, 20, 90) };
            c.CrouchAttack.Crouching = true;

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

            // R: Golden Dragon - a translucent golden dragon bursts from the staff tip and flies straight ahead.
            ProjectileDef dragon = b.Projectile("Golden Dragon");
            dragon.Speed = ContentBuilder.M(330);
            dragon.Lifetime = 80;
            dragon.Radius = ContentBuilder.M(1300);
            dragon.SpawnForward = ContentBuilder.M(1600);
            dragon.SpawnUp = ContentBuilder.M(1300);
            dragon.Damage = 150;
            dragon.Hitstun = 40;
            dragon.Blockstun = 24;
            dragon.KnockbackH = ContentBuilder.M(330);
            dragon.KnockbackV = ContentBuilder.M(300);
            dragon.Flags = HitFlags.Launch;
            dragon.ExplodeRadius = ContentBuilder.M(2600);
            dragon.ExplodeDamage = 60;
            dragon.ExplodeKnockbackH = ContentBuilder.M(220);
            dragon.ExplodeKnockbackV = ContentBuilder.M(240);
            dragon.Unstoppable = true;
            dragon.VisualId = DragonVisual;

            c.Ultimate = b.Move("Golden Dragon", 60, MovePose.Cast);
            c.Ultimate.Spawns = new[] { ContentBuilder.Spawn(26, dragon) };
            c.Ultimate.MagicCost = 300;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(22);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 26;
            c.Ultimate.UsableInAir = true;
            c.Ultimate.GravityScale = ContentBuilder.M(200);

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "LMB", Name = "Staff combo", Description = "Swing from the left, swing from the right, then a thrust that pushes far away." },
                new AbilityInfo { Key = "RMB", Name = "Staff Guard", Description = "Hold the staff sideways to block. Tap just before a hit to parry." },
                new AbilityInfo { Key = "C+LMB", Name = "Dragon Slide", Description = "Slide forward low, staff tip first. Beats a standing block." },
                new AbilityInfo { Key = "Q", Name = "Flurry", Description = "Rapid staff jabs ending in a push. 120 magic." },
                new AbilityInfo { Key = "E", Name = "Shockwave", Description = "Slam the ground and blast everyone nearby away. 170 magic." },
                new AbilityInfo { Key = "R", Name = "Golden Dragon", Description = "A golden dragon flies straight out from the staff and crashes into the opponent. 300 magic." },
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
