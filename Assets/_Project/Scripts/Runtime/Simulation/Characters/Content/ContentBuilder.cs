using System.Collections.Generic;

namespace HEXBOUND.Simulation
{
    /// <summary>Helper used by the character content files to create moves and projectiles with unique ids.</summary>
    public sealed class ContentBuilder
    {
        public readonly List<MoveDef> Moves = new List<MoveDef>();
        public readonly List<ProjectileDef> Projectiles = new List<ProjectileDef>();

        public MoveDef Move(string name, int total, MovePose pose)
        {
            var move = new MoveDef { Id = Moves.Count, Name = name, Total = total, Pose = pose };
            Moves.Add(move);
            return move;
        }

        public ProjectileDef Projectile(string name)
        {
            var projectile = new ProjectileDef { Id = Projectiles.Count, Name = name };
            Projectiles.Add(projectile);
            return projectile;
        }

        /// <summary>Shorthand for thousandths: M(1500) is 1.5.</summary>
        public static Fix M(int thousandths)
        {
            return Fix.Milli(thousandths);
        }

        public static HitboxDef Hit(int start, int end, int forwardMilli, int upMilli, int radiusMilli, int damage)
        {
            return new HitboxDef
            {
                Start = start,
                End = end,
                Forward = M(forwardMilli),
                Up = M(upMilli),
                Radius = M(radiusMilli),
                Damage = damage,
            };
        }

        public static MotionDef Motion(int start, int end, int forwardMilli)
        {
            return new MotionDef { Start = start, End = end, Forward = M(forwardMilli) };
        }

        public static MotionDef Vertical(int start, int end, int forwardMilli, int upMilli)
        {
            return new MotionDef { Start = start, End = end, Forward = M(forwardMilli), Up = M(upMilli), SetVertical = true };
        }

        public static ProjectileSpawn Spawn(int tick, ProjectileDef projectile, int angleOffsetDegrees = 0)
        {
            return new ProjectileSpawn
            {
                Tick = tick,
                ProjectileId = projectile.Id,
                AngleOffset = (int)(((long)angleOffsetDegrees * FixMath.FullTurn) / 360),
            };
        }

        public static int Seconds(int seconds)
        {
            return seconds * GameConfig.TickRate;
        }
    }
}
