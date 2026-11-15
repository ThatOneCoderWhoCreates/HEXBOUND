namespace HEXBOUND.Simulation
{
    /// <summary>Moves shared by every fighter: dodge, air dash, grab, magic recharge, heal and the crouch attack.</summary>
    public sealed class CommonMoves
    {
        public MoveDef Dodge;
        public MoveDef AirDash;
        public MoveDef Grab;
        public MoveDef Recharge;
        public MoveDef Heal;
        public MoveDef LowSweep;
        public MoveDef[] FistChain;
        public MoveDef FistHeavy;
        public MoveDef FistAirLight;
        public MoveDef FistAirHeavy;

        public static CommonMoves Build(ContentBuilder b)
        {
            var c = new CommonMoves();

            c.Dodge = b.Move("Dodge", 24, MovePose.Dodge);
            c.Dodge.Motions = new[] { ContentBuilder.Motion(0, 11, 270), ContentBuilder.Motion(12, 16, 110) };
            c.Dodge.InvulnStart = 1;
            c.Dodge.InvulnEnd = 12;
            c.Dodge.Slot = CooldownSlot.Dodge;
            c.Dodge.Cooldown = 36;
            c.Dodge.SoftTarget = false;
            c.Dodge.CancelStart = 18;
            c.Dodge.CancelEnd = 23;

            c.AirDash = b.Move("Air Dash", 20, MovePose.Dodge);
            c.AirDash.Motions = new[] { ContentBuilder.Vertical(0, 11, 250, 0) };
            c.AirDash.InvulnStart = 1;
            c.AirDash.InvulnEnd = 9;
            c.AirDash.Slot = CooldownSlot.Dodge;
            c.AirDash.Cooldown = 36;
            c.AirDash.SoftTarget = false;
            c.AirDash.UsableOnGround = false;
            c.AirDash.UsableInAir = true;
            c.AirDash.GravityScale = Fix.Half;
            c.AirDash.CancelStart = 14;
            c.AirDash.CancelEnd = 19;

            c.Grab = b.Move("Grab", 38, MovePose.Grab);
            var grab = ContentBuilder.Hit(7, 9, 1100, 1000, 750, 80);
            grab.Flags = HitFlags.Grab | HitFlags.Launch;
            grab.KnockbackH = ContentBuilder.M(230);
            grab.KnockbackV = ContentBuilder.M(260);
            grab.Hitstun = 30;
            c.Grab.Hitboxes = new[] { grab };
            c.Grab.Motions = new[] { ContentBuilder.Motion(2, 7, 60) };

            c.Recharge = b.Move("Recharge", 30, MovePose.Channel);
            c.Recharge.MagicGain = 30;
            c.Recharge.MagicGainTick = 12;
            c.Recharge.Slot = CooldownSlot.Recharge;
            c.Recharge.Cooldown = ContentBuilder.Seconds(5);
            c.Recharge.SoftTarget = false;
            c.Recharge.UsableInAir = true;

            c.Heal = b.Move("Heal", 30, MovePose.Channel);
            c.Heal.HealPercent = 5;
            c.Heal.HealTick = 14;
            c.Heal.Slot = CooldownSlot.Heal;
            c.Heal.Cooldown = ContentBuilder.Seconds(20);
            c.Heal.SoftTarget = false;
            c.Heal.UsableInAir = true;

            // C + left mouse: a low leg sweep from the crouch. Trips (knocks down), must be blocked with the low guard.
            c.LowSweep = b.Move("Low Sweep", 32, MovePose.Sweep);
            var sweep = ContentBuilder.Hit(9, 12, 1150, 250, 750, 50);
            sweep.Flags = HitFlags.Low | HitFlags.Launch;
            sweep.KnockbackH = ContentBuilder.M(60);
            sweep.KnockbackV = ContentBuilder.M(140);
            sweep.Hitstun = 30;
            sweep.Blockstun = 14;
            c.LowSweep.Hitboxes = new[] { sweep };
            c.LowSweep.Motions = new[] { ContentBuilder.Motion(1, 8, 40) };
            c.LowSweep.Crouching = true;

            // Bare hands (after throwing the weapon): quick, weaker punches.
            var jab = b.Move("Fist Jab", 14, MovePose.JabLeft);
            var jabHit = ContentBuilder.Hit(4, 6, 1050, 1250, 650, 18);
            jabHit.Hitstun = 14;
            jabHit.KnockbackH = ContentBuilder.M(30);
            jab.Hitboxes = new[] { jabHit };
            jab.Motions = new[] { ContentBuilder.Motion(1, 4, 60) };
            jab.CancelStart = 6;
            jab.CancelEnd = 13;
            var cross = b.Move("Fist Cross", 16, MovePose.Thrust);
            var crossHit = ContentBuilder.Hit(5, 7, 1150, 1250, 650, 22);
            crossHit.Hitstun = 16;
            crossHit.KnockbackH = ContentBuilder.M(40);
            cross.Hitboxes = new[] { crossHit };
            cross.Motions = new[] { ContentBuilder.Motion(2, 5, 70) };
            cross.CancelStart = 7;
            cross.CancelEnd = 15;
            var hook = b.Move("Fist Hook", 22, MovePose.Swing);
            var hookHit = ContentBuilder.Hit(6, 9, 1050, 1200, 850, 30);
            hookHit.Hitstun = 20;
            hookHit.KnockbackH = ContentBuilder.M(160);
            hook.Hitboxes = new[] { hookHit };
            hook.CancelStart = 9;
            hook.CancelEnd = 21;
            c.FistChain = new[] { jab, cross, hook };

            c.FistHeavy = b.Move("Haymaker", 32, MovePose.Thrust);
            var hay = ContentBuilder.Hit(10, 13, 1150, 1300, 800, 40);
            hay.Hitstun = 24;
            hay.KnockbackH = ContentBuilder.M(280);
            hay.KnockbackV = ContentBuilder.M(80);
            c.FistHeavy.Hitboxes = new[] { hay };
            c.FistHeavy.Motions = new[] { ContentBuilder.Motion(4, 10, 90) };
            c.FistHeavy.CancelStart = 22;
            c.FistHeavy.CancelEnd = 31;

            c.FistAirLight = b.Move("Air Punch", 18, MovePose.Thrust);
            var airHit = ContentBuilder.Hit(4, 8, 1000, 800, 800, 22);
            airHit.Hitstun = 18;
            airHit.KnockbackH = ContentBuilder.M(60);
            airHit.KnockbackV = ContentBuilder.M(100);
            c.FistAirLight.Hitboxes = new[] { airHit };
            c.FistAirLight.UsableOnGround = false;
            c.FistAirLight.UsableInAir = true;
            c.FistAirLight.GravityScale = ContentBuilder.M(350);
            c.FistAirLight.EndsOnLanding = true;
            c.FistAirLight.LandingRecovery = 5;
            c.FistAirLight.CancelStart = 10;
            c.FistAirLight.CancelEnd = 17;

            c.FistAirHeavy = Plunge(b, "Dive Punch", 1000, 45);

            return c;
        }

        /// <summary>The thrown weapon: spins forward in a shallow arc and drops where it stops.</summary>
        public static ProjectileDef ThrownWeapon(ContentBuilder b, string name, int damage)
        {
            ProjectileDef p = b.Projectile(name);
            p.Speed = ContentBuilder.M(520);
            p.Lifetime = 45;
            p.Radius = ContentBuilder.M(500);
            p.SpawnForward = ContentBuilder.M(700);
            p.SpawnUp = ContentBuilder.M(1400);
            p.Gravity = ContentBuilder.M(8);
            p.Damage = damage;
            p.Hitstun = 22;
            p.KnockbackH = ContentBuilder.M(200);
            p.KnockbackV = ContentBuilder.M(80);
            p.AimAtTarget = true;
            p.VisualId = ThrownVisual;
            return p;
        }

        public const int ThrownVisual = 20;

        public static MoveDef ThrowMove(ContentBuilder b, string name, ProjectileDef thrown)
        {
            MoveDef m = b.Move(name, 26, MovePose.Throw);
            m.Spawns = new[] { ContentBuilder.Spawn(10, thrown) };
            m.ThrowsWeapon = true;
            m.UsableInAir = true;
            m.GravityScale = ContentBuilder.M(400);
            m.CancelStart = 18;
            m.CancelEnd = 25;
            return m;
        }

        public void ApplyTo(CharacterDef character)
        {
            character.Dodge = Dodge;
            character.AirDash = AirDash;
            character.Grab = Grab;
            character.Recharge = Recharge;
            character.Heal = Heal;
            character.CrouchAttack = LowSweep;
            character.FistChain = FistChain;
            character.FistHeavy = FistHeavy;
            character.FistAirLight = FistAirLight;
            character.FistAirHeavy = FistAirHeavy;
            character.FistCrouchAttack = LowSweep;
        }

        /// <summary>Plunging air attack: small hop, then a fast dive that ends on landing.</summary>
        public static MoveDef Plunge(ContentBuilder b, string name, int radiusMilli, int damage)
        {
            var m = b.Move(name, 70, MovePose.Slam);
            m.Motions = new[] { ContentBuilder.Vertical(0, 6, 20, 140), ContentBuilder.Vertical(7, 69, 40, -620) };
            var hit = ContentBuilder.Hit(7, 69, 500, 300, radiusMilli, damage);
            hit.Flags = HitFlags.Launch;
            hit.KnockbackH = ContentBuilder.M(200);
            hit.KnockbackV = ContentBuilder.M(250);
            hit.Hitstun = 30;
            m.Hitboxes = new[] { hit };
            m.UsableOnGround = false;
            m.UsableInAir = true;
            m.EndsOnLanding = true;
            m.LandingRecovery = 16;
            return m;
        }
    }
}
