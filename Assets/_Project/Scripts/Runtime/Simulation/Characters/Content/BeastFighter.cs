namespace HEXBOUND.Simulation
{
    /// <summary>
    /// The beast, fought only in "Fight the Beast" mode. Big, slightly slower, uses no magic, and every punch
    /// hits hard. Once per round, when its health drops to 30%, it awakens: a roar that pushes you away and heals
    /// it a little, then a purple aura for the rest of the round with faster attacks, more speed and more damage.
    /// </summary>
    public static class BeastFighter
    {
        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Gorath",
                Title = "The Hexbound Beast",
                Weapon = "Fists",
                ModelName = "Beast",
                Stance = FighterStance.Beast,
                Description = "A huge armored gorilla that learns how you fight. Slow, no magic, crushing punches, and it awakens when hurt.",
                Selectable = false,
                CanThrowWeapon = false,
                MaxHealth = 1300,
                MaxMagic = 0,
                Radius = ContentBuilder.M(750),
                Height = ContentBuilder.M(2700),
                MoveSpeed = ContentBuilder.M(88),
                AirSpeed = ContentBuilder.M(80),
                JumpVelocity = ContentBuilder.M(315),
                KnockbackScale = ContentBuilder.M(600),
                ColorR = 170,
                ColorG = 90,
                ColorB = 255,
                AwakenPercent = 30,
                AwakenDamagePercent = 125,
                UltimateBuff = BuffId.Awakened,
                BuffSpeedScale = ContentBuilder.M(1350),
            };
            common.ApplyTo(c);
            c.Recharge = null;
            c.Heal = null;

            c.LightChain = new[]
            {
                Punch(b, "Heavy Jab", MovePose.JabLeft, 28, 10, 60),
                Punch(b, "Heavy Cross", MovePose.Thrust, 30, 11, 65),
                HammerFist(b, "Hammer Fist", 40, 16),
            };
            c.Heavy = CrushingPunch(b, "Crushing Punch", 52, 20);

            // Awakened versions: the same attacks, a few ticks faster.
            c.BuffLightChain = new[]
            {
                Punch(b, "Heavy Jab+", MovePose.JabLeft, 22, 7, 60),
                Punch(b, "Heavy Cross+", MovePose.Thrust, 23, 7, 65),
                HammerFist(b, "Hammer Fist+", 32, 12),
            };
            c.BuffHeavy = CrushingPunch(b, "Crushing Punch+", 40, 14);

            c.AirLight = b.Move("Air Swipe", 24, MovePose.Swing);
            var airSwipe = ContentBuilder.Hit(6, 10, 1400, 800, 1000, 55);
            airSwipe.Hitstun = 22;
            airSwipe.KnockbackH = ContentBuilder.M(120);
            airSwipe.KnockbackV = ContentBuilder.M(120);
            c.AirLight.Hitboxes = new[] { airSwipe };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(400);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 8;

            c.AirHeavy = CommonMoves.Plunge(b, "Meteor Drop", 1600, 120);

            c.Grab = b.Move("Maul", 44, MovePose.Grab);
            var maul = ContentBuilder.Hit(8, 10, 1600, 1100, 800, 130);
            maul.Flags = HitFlags.Grab | HitFlags.Launch;
            maul.KnockbackH = ContentBuilder.M(180);
            maul.KnockbackV = ContentBuilder.M(280);
            maul.Hitstun = 34;
            c.Grab.Hitboxes = new[] { maul };
            c.Grab.Motions = new[] { ContentBuilder.Motion(2, 8, 70) };

            c.CrouchAttack = b.Move("Tail Sweep", 36, MovePose.Sweep);
            var tail = ContentBuilder.Hit(11, 15, 1500, 250, 1000, 70);
            tail.Flags = HitFlags.Low | HitFlags.Launch;
            tail.KnockbackH = ContentBuilder.M(90);
            tail.KnockbackV = ContentBuilder.M(150);
            tail.Hitstun = 32;
            tail.Blockstun = 16;
            c.CrouchAttack.Hitboxes = new[] { tail };
            c.CrouchAttack.Crouching = true;

            // Q: Ground Slam - both fists into the floor; a shockwave all around. Overhead (beats the low guard).
            c.Skill1 = b.Move("Ground Slam", 60, MovePose.Slam);
            var slam = ContentBuilder.Hit(24, 27, 900, 300, 2800, 130);
            slam.Flags = HitFlags.Launch | HitFlags.Radial | HitFlags.Overhead;
            slam.KnockbackH = ContentBuilder.M(120);
            slam.KnockbackV = ContentBuilder.M(260);
            slam.Hitstun = 38;
            slam.Blockstun = 18;
            c.Skill1.Hitboxes = new[] { slam };
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(7);

            // E: Pounce - leaps at the opponent and crashes down on them.
            c.Skill2 = b.Move("Pounce", 56, MovePose.Slam);
            c.Skill2.Motions = new[] { ContentBuilder.Vertical(4, 12, 380, 220), ContentBuilder.Vertical(13, 19, 380, -300) };
            var pounce = ContentBuilder.Hit(15, 24, 900, 500, 1500, 120);
            pounce.Flags = HitFlags.Launch | HitFlags.Overhead;
            pounce.KnockbackH = ContentBuilder.M(140);
            pounce.KnockbackV = ContentBuilder.M(240);
            pounce.Hitstun = 34;
            pounce.Blockstun = 16;
            c.Skill2.Hitboxes = new[] { pounce };
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(9);

            // R: Rampage - a steerable storm of punches, then a launching double-fist.
            c.Ultimate = b.Move("Rampage", 130, MovePose.Flurry);
            var storm = ContentBuilder.Hit(14, 95, 1700, 1200, 1200, 18);
            storm.RehitInterval = 6;
            storm.Hitstun = 14;
            storm.Blockstun = 8;
            storm.KnockbackH = ContentBuilder.M(12);
            storm.Group = 0;
            var crush = ContentBuilder.Hit(104, 108, 1700, 1200, 1300, 160);
            crush.Flags = HitFlags.Launch | HitFlags.GuardBreak;
            crush.Group = 1;
            crush.Hitstun = 40;
            crush.KnockbackH = ContentBuilder.M(150);
            crush.KnockbackV = ContentBuilder.M(300);
            c.Ultimate.Hitboxes = new[] { storm, crush };
            c.Ultimate.Steerable = true;
            c.Ultimate.SteerSpeed = ContentBuilder.M(70);
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(22);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 10;

            // Awakening: roar, shockwave that pushes the opponent away, small heal, then the Awakened buff.
            c.AwakenMove = b.Move("Primal Awakening", 70, MovePose.Roar);
            var roar = ContentBuilder.Hit(22, 25, 0, 1000, 3200, 40);
            roar.Flags = HitFlags.Radial | HitFlags.Launch | HitFlags.Unblockable;
            roar.KnockbackH = ContentBuilder.M(300);
            roar.KnockbackV = ContentBuilder.M(200);
            roar.Hitstun = 30;
            c.AwakenMove.Hitboxes = new[] { roar };
            c.AwakenMove.InvulnStart = 0;
            c.AwakenMove.InvulnEnd = 69;
            c.AwakenMove.ApplyBuff = BuffId.Awakened;
            c.AwakenMove.BuffTick = 22;
            c.AwakenMove.BuffDuration = ContentBuilder.Seconds(200);
            c.AwakenMove.HealPercent = 10;
            c.AwakenMove.HealTick = 30;
            c.AwakenMove.SoftTarget = false;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "Q", Name = "Ground Slam", Description = "Shockwave all around. Overhead." },
                new AbilityInfo { Key = "E", Name = "Pounce", Description = "Leaps at you and crashes down." },
                new AbilityInfo { Key = "R", Name = "Rampage", Description = "A storm of punches, then a launching double-fist." },
                new AbilityInfo { Key = "!", Name = "Primal Awakening", Description = "At 30% health: roars, heals 10%, fire bursts from its shoulder exhausts, and it turns faster and stronger." },
            };
            return c;
        }

        private static MoveDef Punch(ContentBuilder b, string name, MovePose pose, int total, int start, int damage)
        {
            MoveDef m = b.Move(name, total, pose);
            var hit = ContentBuilder.Hit(start, start + 3, 1800, 1200, 900, damage);
            hit.Hitstun = 20;
            hit.Blockstun = 12;
            hit.KnockbackH = ContentBuilder.M(120);
            m.Hitboxes = new[] { hit };
            m.Motions = new[] { ContentBuilder.Motion(1, start, 50) };
            m.CancelStart = start + 4;
            m.CancelEnd = total - 2;
            return m;
        }

        private static MoveDef HammerFist(ContentBuilder b, string name, int total, int start)
        {
            MoveDef m = b.Move(name, total, MovePose.Overhead);
            var hit = ContentBuilder.Hit(start, start + 4, 1800, 900, 1100, 95);
            hit.Flags = HitFlags.Launch;
            hit.Hitstun = 34;
            hit.Blockstun = 16;
            hit.KnockbackH = ContentBuilder.M(120);
            hit.KnockbackV = ContentBuilder.M(200);
            m.Hitboxes = new[] { hit };
            m.Motions = new[] { ContentBuilder.Motion(2, start, 60) };
            return m;
        }

        private static MoveDef CrushingPunch(ContentBuilder b, string name, int total, int start)
        {
            MoveDef m = b.Move(name, total, MovePose.Thrust);
            var hit = ContentBuilder.Hit(start, start + 4, 1900, 1250, 1000, 170);
            hit.Flags = HitFlags.Launch | HitFlags.GuardBreak;
            hit.Hitstun = 40;
            hit.Blockstun = 20;
            hit.KnockbackH = ContentBuilder.M(180);
            hit.KnockbackV = ContentBuilder.M(160);
            m.Hitboxes = new[] { hit };
            m.Motions = new[] { ContentBuilder.Motion(start - 8, start, 150) };
            return m;
        }
    }
}
