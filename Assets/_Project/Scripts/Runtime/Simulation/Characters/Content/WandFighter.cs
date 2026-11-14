namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Wand fighter. Light and fast, mostly ranged: hex bolts, a short teleport,
    /// and an ultimate that lets her fly for a while and shoot from the air.
    /// </summary>
    public static class WandFighter
    {
        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Lyra",
                Title = "The Hexcaller",
                Weapon = "Wand",
                ModelName = "Wizard",
                Description = "Wizard. Fragile but full of magic: fights from range, teleports, and takes to the air.",
                MaxHealth = 900,
                Radius = ContentBuilder.M(420),
                Height = ContentBuilder.M(1700),
                MoveSpeed = ContentBuilder.M(112),
                AirSpeed = ContentBuilder.M(100),
                JumpVelocity = ContentBuilder.M(310),
                KnockbackScale = ContentBuilder.M(1150),
                ColorR = 175,
                ColorG = 95,
                ColorB = 235,
            };
            common.ApplyTo(c);

            var bolt = b.Projectile("Hex Bolt");
            bolt.Speed = ContentBuilder.M(600);
            bolt.Lifetime = 40;
            bolt.Radius = ContentBuilder.M(450);
            bolt.SpawnForward = ContentBuilder.M(900);
            bolt.SpawnUp = ContentBuilder.M(1200);
            bolt.Damage = 34;
            bolt.Hitstun = 18;
            bolt.KnockbackH = ContentBuilder.M(80);
            bolt.AimAtTarget = true;
            bolt.VisualId = 3;

            var spark = b.Projectile("Hex Spark");
            spark.Speed = ContentBuilder.M(500);
            spark.Lifetime = 12;
            spark.Radius = ContentBuilder.M(700);
            spark.SpawnForward = ContentBuilder.M(900);
            spark.SpawnUp = ContentBuilder.M(1100);
            spark.Damage = 50;
            spark.Hitstun = 24;
            spark.KnockbackH = ContentBuilder.M(240);
            spark.KnockbackV = ContentBuilder.M(100);
            spark.VisualId = 4;

            var orb = b.Projectile("Hex Orb");
            orb.Speed = ContentBuilder.M(220);
            orb.Lifetime = 100;
            orb.Radius = ContentBuilder.M(800);
            orb.SpawnForward = ContentBuilder.M(1000);
            orb.SpawnUp = ContentBuilder.M(1100);
            orb.Damage = 90;
            orb.Flags = HitFlags.GuardBreak | HitFlags.Launch;
            orb.Hitstun = 30;
            orb.KnockbackH = ContentBuilder.M(200);
            orb.KnockbackV = ContentBuilder.M(280);
            orb.VisualId = 5;

            var aimedOrb = b.Projectile("Falling Orb");
            aimedOrb.Speed = ContentBuilder.M(300);
            aimedOrb.Lifetime = 80;
            aimedOrb.Radius = ContentBuilder.M(800);
            aimedOrb.SpawnForward = ContentBuilder.M(800);
            aimedOrb.SpawnUp = ContentBuilder.M(600);
            aimedOrb.Damage = 80;
            aimedOrb.Flags = HitFlags.Launch;
            aimedOrb.Hitstun = 30;
            aimedOrb.KnockbackH = ContentBuilder.M(200);
            aimedOrb.KnockbackV = ContentBuilder.M(250);
            aimedOrb.AimAtTarget = true;
            aimedOrb.VisualId = 5;

            var l1 = b.Move("Hex Poke", 20, MovePose.Thrust);
            l1.Hitboxes = new[] { Light(5, 8, 1600, 32, 70) };
            l1.CancelStart = 8;
            l1.CancelEnd = 18;

            var l2 = b.Move("Hex Swipe", 20, MovePose.Swing);
            l2.Hitboxes = new[] { Light(5, 8, 1600, 34, 80) };
            l2.CancelStart = 8;
            l2.CancelEnd = 18;

            var l3 = b.Move("Hex Spark", 30, MovePose.Cast);
            l3.Spawns = new[] { ContentBuilder.Spawn(9, spark) };
            l3.CancelStart = 16;
            l3.CancelEnd = 28;
            c.LightChain = new[] { l1, l2, l3 };

            c.Heavy = b.Move("Hex Orb", 46, MovePose.Cast);
            c.Heavy.Spawns = new[] { ContentBuilder.Spawn(18, orb) };
            c.Heavy.CancelStart = 30;
            c.Heavy.CancelEnd = 44;

            c.AirLight = b.Move("Air Bolt", 22, MovePose.Cast);
            c.AirLight.Spawns = new[] { ContentBuilder.Spawn(7, bolt) };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(300);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 6;
            c.AirLight.CancelStart = 12;
            c.AirLight.CancelEnd = 20;

            c.AirHeavy = b.Move("Hex Burst", 34, MovePose.Spin);
            var burst = ContentBuilder.Hit(10, 14, 0, 800, 2400, 60);
            burst.Flags = HitFlags.Launch | HitFlags.Radial;
            burst.Hitstun = 28;
            burst.KnockbackH = ContentBuilder.M(220);
            burst.KnockbackV = ContentBuilder.M(180);
            c.AirHeavy.Hitboxes = new[] { burst };
            c.AirHeavy.UsableOnGround = false;
            c.AirHeavy.UsableInAir = true;
            c.AirHeavy.GravityScale = ContentBuilder.M(200);
            c.AirHeavy.EndsOnLanding = true;
            c.AirHeavy.LandingRecovery = 8;
            c.AirHeavy.SoftTarget = false;

            // Q: Hex Bolts - three aimed bolts.
            c.Skill1 = b.Move("Hex Bolts", 34, MovePose.Cast);
            c.Skill1.Spawns = new[]
            {
                ContentBuilder.Spawn(8, bolt),
                ContentBuilder.Spawn(14, bolt, -8),
                ContentBuilder.Spawn(20, bolt, 8),
            };
            c.Skill1.MagicCost = 100;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(4);
            c.Skill1.UsableInAir = true;
            c.Skill1.GravityScale = ContentBuilder.M(300);
            c.Skill1.CancelStart = 24;
            c.Skill1.CancelEnd = 32;

            // E: Blink - short teleport in the movement direction.
            c.Skill2 = b.Move("Blink", 22, MovePose.Dodge);
            c.Skill2.TeleportDistance = Fix.FromInt(6);
            c.Skill2.TeleportTick = 6;
            c.Skill2.InvulnStart = 0;
            c.Skill2.InvulnEnd = 12;
            c.Skill2.MagicCost = 120;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(7);
            c.Skill2.UsableInAir = true;
            c.Skill2.GravityScale = Fix.Zero;
            c.Skill2.SoftTarget = false;
            c.Skill2.CancelStart = 12;
            c.Skill2.CancelEnd = 21;

            // R: Levitate - fly for 8 seconds.
            c.Ultimate = b.Move("Levitate", 24, MovePose.Channel);
            var lift = ContentBuilder.Hit(8, 12, 0, 800, 2200, 40);
            lift.Flags = HitFlags.Launch | HitFlags.Radial;
            lift.Hitstun = 26;
            lift.KnockbackH = ContentBuilder.M(300);
            lift.KnockbackV = ContentBuilder.M(200);
            c.Ultimate.Hitboxes = new[] { lift };
            c.Ultimate.Motions = new[] { ContentBuilder.Vertical(0, 20, 0, 120) };
            c.Ultimate.ApplyBuff = BuffId.Levitate;
            c.Ultimate.BuffTick = 10;
            // Flight has no timer: it lasts until magic runs out (see FighterLogic) or R is pressed again.
            c.Ultimate.BuffDuration = ContentBuilder.Seconds(600);
            c.Ultimate.MagicCost = 260;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(25);
            c.Ultimate.UsableInAir = true;
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 12;
            c.Ultimate.SoftTarget = false;
            c.UltimateBuff = BuffId.Levitate;

            c.BuffAirLight = b.Move("Sky Bolt", 18, MovePose.Cast);
            c.BuffAirLight.Spawns = new[] { ContentBuilder.Spawn(5, bolt) };
            c.BuffAirLight.UsableOnGround = false;
            c.BuffAirLight.UsableInAir = true;
            c.BuffAirLight.CancelStart = 10;
            c.BuffAirLight.CancelEnd = 16;

            c.BuffAirHeavy = b.Move("Falling Orb", 40, MovePose.Cast);
            c.BuffAirHeavy.Spawns = new[] { ContentBuilder.Spawn(14, aimedOrb) };
            c.BuffAirHeavy.UsableOnGround = false;
            c.BuffAirHeavy.UsableInAir = true;
            c.BuffAirHeavy.CancelStart = 26;
            c.BuffAirHeavy.CancelEnd = 38;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "Q", Name = "Hex Bolts", Description = "Fire three aimed bolts. Works in the air. 100 magic." },
                new AbilityInfo { Key = "E", Name = "Blink", Description = "Teleport a short distance in the direction you move. 120 magic." },
                new AbilityInfo { Key = "R", Name = "Levitate", Description = "Fly as high as you like while magic lasts (40 per second). Space rises, Ctrl descends, R again lands. 260 magic." },
            };
            return c;
        }

        private static HitboxDef Light(int start, int end, int forwardMilli, int damage, int knockbackMilli)
        {
            var h = ContentBuilder.Hit(start, end, forwardMilli, 1100, 800, damage);
            h.Hitstun = 18;
            h.Blockstun = 10;
            h.KnockbackH = ContentBuilder.M(knockbackMilli);
            return h;
        }
    }
}
