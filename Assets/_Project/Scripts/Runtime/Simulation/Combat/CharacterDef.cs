using System;

namespace HEXBOUND.Simulation
{
    /// <summary>How the fighter stands and holds its hands (presentation only).</summary>
    public enum FighterStance : byte
    {
        Weapon,
        Boxer,
    }

    /// <summary>Text shown in menus and the HUD for one ability.</summary>
    public sealed class AbilityInfo
    {
        public string Key;
        public string Name;
        public string Description;
    }

    /// <summary>All static data for one fighter: stats and move list.</summary>
    public sealed class CharacterDef
    {
        public int Id;
        public string Name;
        public string Title;
        public string Weapon;
        /// <summary>Model file name in Resources/Fighters (without extension). Empty uses the placeholder body.</summary>
        public string ModelName = string.Empty;
        public FighterStance Stance = FighterStance.Weapon;
        public string Description;

        public int MaxHealth = 1000;
        public Fix Radius = Fix.Milli(450);
        public Fix Height = Fix.Milli(1800);
        /// <summary>Ground speed in units per tick.</summary>
        public Fix MoveSpeed = Fix.Milli(105);
        public Fix AirSpeed = Fix.Milli(95);
        public Fix JumpVelocity = Fix.Milli(320);
        public Fix Gravity = Fix.Milli(18);
        /// <summary>Knockback multiplier (lighter fighters fly further).</summary>
        public Fix KnockbackScale = Fix.One;

        public MoveDef[] LightChain = Array.Empty<MoveDef>();
        public MoveDef Heavy;
        public MoveDef AirLight;
        public MoveDef AirHeavy;
        public MoveDef Grab;
        public MoveDef Skill1;
        public MoveDef Skill2;
        public MoveDef Ultimate;
        public MoveDef Dodge;
        public MoveDef AirDash;
        public MoveDef Recharge;
        public MoveDef Heal;

        /// <summary>Moves that replace the normal ones while the ultimate buff is active (null keeps the normal move).</summary>
        public BuffId UltimateBuff = BuffId.None;
        public MoveDef[] BuffLightChain;
        public MoveDef BuffHeavy;
        public MoveDef BuffAirLight;
        public MoveDef BuffAirHeavy;
        /// <summary>Movement speed multiplier while the buff is active.</summary>
        public Fix BuffSpeedScale = Fix.One;

        public AbilityInfo[] Abilities = Array.Empty<AbilityInfo>();

        /// <summary>Base color for placeholder visuals (RGB 0-255).</summary>
        public int ColorR = 200;
        public int ColorG = 200;
        public int ColorB = 200;
    }
}
