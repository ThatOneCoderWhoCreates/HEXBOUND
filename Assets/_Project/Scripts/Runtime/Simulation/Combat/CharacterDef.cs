using System;

namespace HEXBOUND.Simulation
{
    /// <summary>How the fighter stands and holds its hands (presentation only).</summary>
    public enum FighterStance : byte
    {
        Weapon,
        Boxer,
        /// <summary>Hunched beast that walks on its knuckles.</summary>
        Beast,
        /// <summary>Carries a launcher on the shoulder (or a pistol).</summary>
        Gunner,
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
        /// <summary>Maximum magic; fighters start each round full. Zero means the fighter uses no magic.</summary>
        public int MaxMagic = GameConfig.MaxMagic;
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
        /// <summary>C + left mouse: low attack from the crouch.</summary>
        public MoveDef CrouchAttack;

        /// <summary>Name of the secondary weapon (mouse wheel swaps). Null means the fighter has one weapon.</summary>
        public string SecondaryWeapon;
        /// <summary>Model in Resources/Fighters held in the right hand (for example the mage's staff). Empty means none.</summary>
        public string WeaponModel = string.Empty;
        /// <summary>Right mouse is a guard (the staff held sideways) instead of a heavy attack on the ground.</summary>
        public bool HeavyIsGuard;
        /// <summary>C + right mouse starts this move instead of the low guard (the mage's guard slide). Null keeps the low guard.</summary>
        public MoveDef CrouchGuardMove;
        /// <summary>Moves used with the secondary weapon (null keeps the primary move).</summary>
        public MoveDef[] AltLightChain;
        public MoveDef AltHeavy;
        public MoveDef AltAirLight;
        public MoveDef AltAirHeavy;

        /// <summary>Shown in the character select screen. The beast is only fought in its own mode.</summary>
        public bool Selectable = true;
        /// <summary>Starts automatically once per round when health drops to AwakenPercent of max (the beast).</summary>
        public MoveDef AwakenMove;
        public int AwakenPercent;
        /// <summary>Damage multiplier (percent) for this fighter's hits while the Awakened buff is active.</summary>
        public int AwakenDamagePercent = 100;

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
