namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Launcher. Ranged fighter with two weapons (mouse wheel swaps):
    /// - Missile launcher (primary): slow, explosive missiles that also hurt anyone near where they land.
    /// - Pistol (secondary): fast, light shots and a quick-draw.
    /// Its own explosions never hurt it, so shooting straight down (E, or right click in the air) blasts it
    /// into the air: the highest jumper in the roster.
    /// </summary>
    public static class LauncherFighter
    {
        public const int MissileVisual = 10;
        public const int BulletVisual = 11;

        public static CharacterDef Build(ContentBuilder b, CommonMoves common)
        {
            var c = new CharacterDef
            {
                Name = "Vex",
                Title = "The Hexgunner",
                Weapon = "Launcher",
                SecondaryWeapon = "Pistol",
                ModelName = "Launcher",
                Stance = FighterStance.Gunner,
                Description = "Missiles that explode where they land, a pistol for close range, and rocket jumps.",
                MaxHealth = 950,
                MoveSpeed = ContentBuilder.M(100),
                AirSpeed = ContentBuilder.M(100),
                JumpVelocity = ContentBuilder.M(430),
                ColorR = 70,
                ColorG = 210,
                ColorB = 190,
                ThrowDamage = 60,
                ThrowDamageSecondary = 30,
            };
            common.ApplyTo(c);

            // Rockets fly to the crosshair, a little slower than pistol rounds. Fired steeply downward they kick Vex up.
            ProjectileDef missile = Missile(b, "Missile", 700, 50, 1800, 45);
            missile.RecoilLift = ContentBuilder.M(420);
            ProjectileDef bigMissile = Missile(b, "Cluster Missile", 450, 70, 3200, 90);
            bigMissile.RecoilLift = ContentBuilder.M(500);
            bigMissile.ExplodeKnockbackH = ContentBuilder.M(300);
            bigMissile.ExplodeKnockbackV = ContentBuilder.M(300);

            ProjectileDef blast = b.Projectile("Rocket Blast");
            blast.Speed = ContentBuilder.M(600);
            blast.Lifetime = 8;
            blast.Radius = ContentBuilder.M(300);
            blast.SpawnUp = ContentBuilder.M(300);
            blast.Downward = true;
            blast.Damage = 30;
            blast.ExplodeRadius = ContentBuilder.M(2600);
            blast.ExplodeDamage = 55;
            blast.ExplodeKnockbackH = ContentBuilder.M(220);
            blast.ExplodeKnockbackV = ContentBuilder.M(240);
            blast.VisualId = MissileVisual;

            ProjectileDef rain = b.Projectile("Sky Missile");
            rain.Speed = ContentBuilder.M(500);
            rain.Lifetime = 60;
            rain.Radius = ContentBuilder.M(450);
            rain.SpawnUp = Fix.FromInt(12);
            rain.SpawnForward = ContentBuilder.M(1400);
            rain.FromSky = true;
            rain.Damage = 40;
            rain.Hitstun = 20;
            rain.ExplodeRadius = ContentBuilder.M(1900);
            rain.ExplodeDamage = 45;
            rain.ExplodeKnockbackH = ContentBuilder.M(150);
            rain.ExplodeKnockbackV = ContentBuilder.M(220);
            rain.VisualId = MissileVisual;

            ProjectileDef bullet = b.Projectile("Bullet");
            bullet.Speed = ContentBuilder.M(900);
            bullet.Lifetime = 26;
            bullet.Radius = ContentBuilder.M(300);
            bullet.SpawnForward = ContentBuilder.M(800);
            bullet.SpawnUp = ContentBuilder.M(1300);
            bullet.Damage = 22;
            bullet.Hitstun = 14;
            bullet.Blockstun = 8;
            bullet.KnockbackH = ContentBuilder.M(50);
            bullet.AimAtTarget = true;
            bullet.UseAim = true;
            bullet.VisualId = BulletVisual;

            ProjectileDef heavyBullet = b.Projectile("Quick-Draw Round");
            heavyBullet.Speed = ContentBuilder.M(1100);
            heavyBullet.Lifetime = 26;
            heavyBullet.Radius = ContentBuilder.M(350);
            heavyBullet.SpawnForward = ContentBuilder.M(800);
            heavyBullet.SpawnUp = ContentBuilder.M(1300);
            heavyBullet.Damage = 70;
            heavyBullet.Hitstun = 26;
            heavyBullet.KnockbackH = ContentBuilder.M(260);
            heavyBullet.KnockbackV = ContentBuilder.M(100);
            heavyBullet.AimAtTarget = true;
            heavyBullet.UseAim = true;
            heavyBullet.VisualId = BulletVisual;

            // Launcher (primary): one missile per click; right click fires two.
            var shot = b.Move("Missile", 36, MovePose.Aim);
            shot.Spawns = new[] { ContentBuilder.Spawn(12, missile) };
            shot.CancelStart = 24;
            shot.CancelEnd = 35;
            c.LightChain = new[] { shot };

            c.Heavy = b.Move("Twin Missiles", 50, MovePose.Aim);
            c.Heavy.Spawns = new[] { ContentBuilder.Spawn(16, missile, -9), ContentBuilder.Spawn(20, missile, 9) };
            c.Heavy.Motions = new[] { ContentBuilder.Motion(16, 22, -60) };

            c.AirLight = b.Move("Air Missile", 30, MovePose.Aim);
            c.AirLight.Spawns = new[] { ContentBuilder.Spawn(8, missile) };
            c.AirLight.UsableOnGround = false;
            c.AirLight.UsableInAir = true;
            c.AirLight.GravityScale = ContentBuilder.M(300);
            c.AirLight.EndsOnLanding = true;
            c.AirLight.LandingRecovery = 6;

            // Right click in the air: shoot straight down for a small boost upward.
            c.AirHeavy = b.Move("Down Shot", 26, MovePose.Aim);
            c.AirHeavy.Spawns = new[] { ContentBuilder.Spawn(3, blast) };
            c.AirHeavy.Motions = new[] { ContentBuilder.Vertical(4, 8, 30, 260) };
            c.AirHeavy.UsableOnGround = false;
            c.AirHeavy.UsableInAir = true;

            // Pistol (secondary): three quick shots and a quick-draw.
            var p1 = PistolShot(b, "Shot", bullet);
            var p2 = PistolShot(b, "Second Shot", bullet);
            var p3 = PistolShot(b, "Third Shot", bullet);
            c.AltLightChain = new[] { p1, p2, p3 };
            c.AltHeavy = b.Move("Quick Draw", 32, MovePose.Shoot);
            c.AltHeavy.Spawns = new[] { ContentBuilder.Spawn(10, heavyBullet) };
            c.AltAirLight = b.Move("Air Shot", 16, MovePose.Shoot);
            c.AltAirLight.Spawns = new[] { ContentBuilder.Spawn(4, bullet) };
            c.AltAirLight.UsableOnGround = false;
            c.AltAirLight.UsableInAir = true;
            c.AltAirLight.GravityScale = ContentBuilder.M(400);
            c.AltAirLight.CancelStart = 8;
            c.AltAirLight.CancelEnd = 15;

            // Q: Cluster Missile - a slow missile with a huge blast.
            c.Skill1 = b.Move("Cluster Missile", 44, MovePose.Aim);
            c.Skill1.Spawns = new[] { ContentBuilder.Spawn(16, bigMissile) };
            c.Skill1.MagicCost = 150;
            c.Skill1.Slot = CooldownSlot.Skill1;
            c.Skill1.Cooldown = ContentBuilder.Seconds(6);
            c.Skill1.UsableInAir = true;
            c.Skill1.GravityScale = ContentBuilder.M(300);

            // E: Rocket Jump - fire straight down; the blast throws Vex high into the air and hits anyone nearby.
            c.Skill2 = b.Move("Rocket Jump", 26, MovePose.Aim);
            c.Skill2.Spawns = new[] { ContentBuilder.Spawn(2, blast) };
            c.Skill2.Motions = new[] { ContentBuilder.Vertical(3, 9, 40, 380) };
            c.Skill2.MagicCost = 80;
            c.Skill2.Slot = CooldownSlot.Skill2;
            c.Skill2.Cooldown = ContentBuilder.Seconds(3);
            c.Skill2.UsableInAir = true;
            c.Skill2.SoftTarget = false;
            c.Skill2.InvulnStart = 2;
            c.Skill2.InvulnEnd = 6;

            // R: Missile Rain - eight missiles fall from the sky around the opponent.
            c.Ultimate = b.Move("Missile Rain", 80, MovePose.Aim);
            c.Ultimate.Spawns = new[]
            {
                ContentBuilder.Spawn(20, rain, 0),
                ContentBuilder.Spawn(26, rain, 135),
                ContentBuilder.Spawn(32, rain, 270),
                ContentBuilder.Spawn(38, rain, 45),
                ContentBuilder.Spawn(44, rain, 180),
                ContentBuilder.Spawn(50, rain, 315),
                ContentBuilder.Spawn(56, rain, 90),
                ContentBuilder.Spawn(62, rain, 225),
            };
            c.Ultimate.MagicCost = 300;
            c.Ultimate.Slot = CooldownSlot.Ultimate;
            c.Ultimate.Cooldown = ContentBuilder.Seconds(25);
            c.Ultimate.UsableInAir = true;
            c.Ultimate.GravityScale = ContentBuilder.M(200);
            c.Ultimate.InvulnStart = 0;
            c.Ultimate.InvulnEnd = 12;
            c.Ultimate.SoftTarget = false;

            c.Abilities = new[]
            {
                new AbilityInfo { Key = "LMB", Name = "Rocket", Description = "Fires at the crosshair (snaps onto the opponent when close). Aim at the floor to rocket-jump." },
                new AbilityInfo { Key = "Wheel", Name = "Switch weapon", Description = "Missile launcher (explosive) or pistol (fast shots)." },
                new AbilityInfo { Key = "Q", Name = "Cluster Missile", Description = "A slow missile with a huge blast. 150 magic." },
                new AbilityInfo { Key = "E", Name = "Rocket Jump", Description = "Shoot the floor: fly high, blast anyone nearby. Your blasts never hurt you. 80 magic." },
                new AbilityInfo { Key = "R", Name = "Missile Rain", Description = "Eight missiles fall around the opponent. 300 magic." },
            };
            return c;
        }

        private static ProjectileDef Missile(ContentBuilder b, string name, int speedMilli, int lifetime, int blastMilli, int blastDamage)
        {
            ProjectileDef m = b.Projectile(name);
            m.Speed = ContentBuilder.M(speedMilli);
            m.Lifetime = lifetime;
            m.Radius = ContentBuilder.M(450);
            m.SpawnForward = ContentBuilder.M(900);
            m.SpawnUp = ContentBuilder.M(1500);
            m.Damage = 60;
            m.Hitstun = 24;
            m.KnockbackH = ContentBuilder.M(160);
            m.KnockbackV = ContentBuilder.M(140);
            m.Flags = HitFlags.Launch;
            m.AimAtTarget = true;
            m.UseAim = true;
            m.ExplodeRadius = ContentBuilder.M(blastMilli);
            m.ExplodeDamage = blastDamage;
            m.ExplodeKnockbackH = ContentBuilder.M(180);
            m.ExplodeKnockbackV = ContentBuilder.M(200);
            m.VisualId = MissileVisual;
            return m;
        }

        private static MoveDef PistolShot(ContentBuilder b, string name, ProjectileDef bullet)
        {
            MoveDef m = b.Move(name, 14, MovePose.Shoot);
            m.Spawns = new[] { ContentBuilder.Spawn(4, bullet) };
            m.CancelStart = 7;
            m.CancelEnd = 13;
            return m;
        }
    }
}
