namespace HEXBOUND.Simulation
{
    /// <summary>Moves shared by every fighter: dodge, air dash, grab and magic recharge.</summary>
    public sealed class CommonMoves
    {
        public MoveDef Dodge;
        public MoveDef AirDash;
        public MoveDef Grab;
        public MoveDef Recharge;

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

            return c;
        }

        public void ApplyTo(CharacterDef character)
        {
            character.Dodge = Dodge;
            character.AirDash = AirDash;
            character.Grab = Grab;
            character.Recharge = Recharge;
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
