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
                ThrowDamage = 60,
            };
            common.ApplyTo(c);

            // Left click: swing from the left, swing from the right, then a pushing thrust.
            var l1 = b.Move("Left Swing", 24, MovePose.SwingReverse);
            l1.Hitboxes = new[] { Light(7, 10, 1900, 36, 80) };
            l1.CancelStart = 10;
            l1.CancelEnd = 23;

            var l2 = b.Move("Right Swing", 24, MovePose.Swing);
            l2.Hitboxes = new[] { Light(7, 10, 1900, 40, 90) };
            l2.CancelStart = 10;
            l2.CancelEnd = 23;

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

            // C + left click: a plain low slide, staff tip first.
            c.CrouchAttack = b.Move("Slide", 34, MovePose.Lunge);
            var slide = ContentBuilder.Hit(6, 14, 1500, 600, 750, 45);
            slide.Flags = HitFlags.Low | HitFlags.Launch;
            slide.Hitstun = 26;
            slide.Blockstun = 12;
            slide.KnockbackH = ContentBuilder.M(160);
            slide.KnockbackV = ContentBuilder.M(130);
            c.CrouchAttack.Hitboxes = new[] { slide };
            c.CrouchAttack.Motions = new[] { ContentBuilder.Motion(3, 12, 280), ContentBuilder.Motion(13, 17, 80) };
            c.CrouchAttack.Crouching = true;

            // C + right click: guard slide - slips backward low with the staff held sideways; attacks pass through.
            c.CrouchGuardMove = b.Move("Guard Slide", 26, MovePose.GuardSlide);
            c.CrouchGuardMove.Motions = new[] { ContentBuilder.Motion(1, 12, -300), ContentBuilder.Motion(13, 16, -80) };
            c.CrouchGuardMove.InvulnStart = 1;
            c.CrouchGuardMove.InvulnEnd = 14;
            c.CrouchGuardMove.Crouching = true;
            c.CrouchGuardMove.SoftTarget = true;
            c.CrouchGuardMove.CancelStart = 18;
            c.CrouchGuardMove.CancelEnd = 25;

            // Q: Triple Strike - three fast, heavy thrusts in a row; the last one knocks back.
            c.Skill1 = b.Move("Triple Strike", 40, MovePose.Strikes);
            var strike1 = ContentBuilder.Hit(7, 9, 2100, 1150, 850, 38);
            strike1.Group = 0;
            strike1.Hitstun = 18;
            strike1.KnockbackH = ContentBuilder.M(40);
            var strike2 = ContentBuilder.Hit(14, 16, 2100, 1150, 850, 38);
            strike2.Group = 1;
            strike2.Hitstun = 18;
            strike2.KnockbackH = ContentBuilder.M(40);
            var strike3 = ContentBuilder.Hit(21, 24, 2200, 1150, 950, 55);
            strike3.Group = 2;
            strike3.Flags = HitFlags.Launch;
            strike3.Hitstun = 30;
            strike3.KnockbackH = ContentBuilder.M(280);
            strike3.KnockbackV = ContentBuilder.M(120);
            c.Skill1.Hitboxes = new[] { strike1, strike2, strike3 };
            c.Skill1.Motions = new[] { ContentBuilder.Motion(5, 22, 35) };
            c.Skill1.MagicCost = 120;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(6);
            c.Skill1.CancelStart = 32;
            c.Skill1.CancelEnd = 39;

            // E: Dragon Slide - a long, fast slide with the staff tip leading; hits hard and beats a standing block.
            c.Skill2 = b.Move("Dragon Slide", 40, MovePose.Lunge);
            var dslide = ContentBuilder.Hit(5, 18, 1600, 600, 900, 80);
            dslide.Flags = HitFlags.Low | HitFlags.Launch;
            dslide.Hitstun = 32;
            dslide.Blockstun = 16;
            dslide.KnockbackH = ContentBuilder.M(300);
            dslide.KnockbackV = ContentBuilder.M(180);
            c.Skill2.Hitboxes = new[] { dslide };
            c.Skill2.Motions = new[] { ContentBuilder.Motion(2, 16, 420), ContentBuilder.Motion(17, 22, 100) };
            c.Skill2.Crouching = true;
            c.Skill2.MagicCost = 120;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(5);

            // R: Golden Dragon - a translucent golden dragon bursts from the staff tip and flies straight ahead.
            ProjectileDef dragon = b.Projectile("Golden Dragon");
            dragon.Speed = ContentBuilder.M(330);
            dragon.Lifetime = 80;
            dragon.Radius = ContentBuilder.M(1300);
            dragon.SpawnForward = ContentBuilder.M(1600);
            dragon.SpawnUp = ContentBuilder.M(1300);
            dragon.Damage = 400;
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
                new AbilityInfo { Key = "C+LMB / C+RMB", Name = "Slide / Guard Slide", Description = "Low slide attack, or slip backward with the staff up (attacks pass through)." },
                new AbilityInfo { Key = "Q", Name = "Triple Strike", Description = "Three fast, heavy thrusts; the last knocks back. 120 magic." },
                new AbilityInfo { Key = "E", Name = "Dragon Slide", Description = "A long, fast slide, staff tip first. Hits hard and beats a standing block. 120 magic." },
                new AbilityInfo { Key = "R", Name = "Golden Dragon", Description = "A golden dragon flies straight out from the staff and crashes into the opponent for 400 damage. 300 magic." },
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
