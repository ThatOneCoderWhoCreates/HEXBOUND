using System;

namespace HEXBOUND.Simulation
{
    [Flags]
    public enum InputButtons : ushort
    {
        None = 0,
        Light = 1 << 0,
        Heavy = 1 << 1,
        Skill1 = 1 << 2,
        Skill2 = 1 << 3,
        Ultimate = 1 << 4,
        Dodge = 1 << 5,
        Jump = 1 << 6,
        Block = 1 << 7,
        Grab = 1 << 8,
        Recharge = 1 << 9,
        LockOn = 1 << 10,
        Heal = 1 << 11,
        Crouch = 1 << 12,
        /// <summary>Mouse wheel: swap primary and secondary weapon (Launcher).</summary>
        SwitchWeapon = 1 << 13,
        /// <summary>G: throw the weapon in hand, or pick up your dropped weapon when standing next to it.</summary>
        Throw = 1 << 14,
    }

    /// <summary>
    /// One player's input for one simulation tick. This is the only data sent over the network.
    /// The move direction is already in world space (the local camera was applied before quantizing),
    /// so the simulation never needs to know about cameras.
    /// </summary>
    [Serializable]
    public struct InputFrame : IEquatable<InputFrame>
    {
        public InputButtons Buttons;

        /// <summary>World-space move direction in 256 steps (0 = +Z, clockwise). Only valid when HasMove is true.</summary>
        public byte MoveAngle;

        public bool HasMove;

        /// <summary>Camera aim (crosshair): yaw in 256 steps like MoveAngle, pitch in 1.5 degree steps (+ is up). Valid when HasAim.</summary>
        public byte AimYaw;
        public sbyte AimPitch;
        public bool HasAim;

        /// <summary>Degrees per AimPitch step.</summary>
        public const float AimPitchStep = 1.5f;

        public static readonly InputFrame Empty = default(InputFrame);

        public bool Held(InputButtons button)
        {
            return (Buttons & button) != 0;
        }

        /// <summary>Move direction as a simulation angle (65536 per turn).</summary>
        public int MoveAngleFull
        {
            get { return MoveAngle << 8; }
        }

        public ulong Pack()
        {
            return (ulong)(ushort)Buttons | ((ulong)MoveAngle << 16) | (HasMove ? 1UL << 24 : 0UL) | (HasAim ? 1UL << 25 : 0UL)
                | ((ulong)AimYaw << 32) | ((ulong)(byte)AimPitch << 40);
        }

        public static InputFrame Unpack(ulong packed)
        {
            InputFrame frame;
            frame.Buttons = (InputButtons)(packed & 0xFFFF);
            frame.MoveAngle = (byte)((packed >> 16) & 0xFF);
            frame.HasMove = ((packed >> 24) & 1) != 0;
            frame.HasAim = ((packed >> 25) & 1) != 0;
            frame.AimYaw = (byte)((packed >> 32) & 0xFF);
            frame.AimPitch = (sbyte)(byte)((packed >> 40) & 0xFF);
            return frame;
        }

        /// <summary>Aim yaw as a simulation angle.</summary>
        public int AimYawFull
        {
            get { return AimYaw << 8; }
        }

        /// <summary>Converts a simulation angle into the 8-bit network direction (rounded).</summary>
        public static byte QuantizeAngle(int angle)
        {
            return (byte)(((FixMath.NormalizeAngle(angle) + 128) >> 8) & 0xFF);
        }

        public bool Equals(InputFrame other)
        {
            return Pack() == other.Pack();
        }

        public override bool Equals(object obj)
        {
            return obj is InputFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Pack();
        }
    }
}
