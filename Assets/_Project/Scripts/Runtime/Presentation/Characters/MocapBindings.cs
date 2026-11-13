using System;
using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    [Flags]
    public enum MocapMask
    {
        None = 0,
        Arms = 1,
        Legs = 2,
        Spine = 4,
        All = Arms | Legs | Spine,
    }

    /// <summary>Which clip to show right now, at what time, and on which body parts.</summary>
    public struct MocapChoice
    {
        public string Clip;
        public float Time;
        public bool Mirror;
        public MocapMask Mask;
        /// <summary>The clip moves the whole body (falls, get-ups), so procedural whole-body tilt is turned off.</summary>
        public bool FullBody;
    }

    /// <summary>
    /// Maps fighter states and moves to KinemIQ clips. Attack clips are time-warped so their impact frame
    /// lands on the move's first active hitbox frame. Anything without a clip keeps its procedural pose.
    /// Impact fractions were measured from the clips (peak hand reach for punches, peak hand speed for the slash).
    /// </summary>
    public static class MocapBindings
    {
        private struct AttackClip
        {
            public string Clip;
            public bool Mirror;
            public float Impact;
            public MocapMask Mask;

            public AttackClip(string clip, bool mirror, float impact, MocapMask mask)
            {
                Clip = clip;
                Mirror = mirror;
                Impact = impact;
                Mask = mask;
            }
        }

        private const string Jab = "Punch_jab_left_2";
        private const string Cross = "Punch_cross_right_2";
        private const string Hook = "Punch_hook_right_2";
        private const string Slash = "Sword_slash";

        private static readonly Dictionary<string, AttackClip> Attacks = new Dictionary<string, AttackClip>
        {
            { "Jab", new AttackClip(Jab, false, 0.22f, MocapMask.All) },
            { "Cross", new AttackClip(Cross, false, 0.25f, MocapMask.All) },
            { "Hook", new AttackClip(Hook, false, 0.60f, MocapMask.All) },
            { "Body Blow", new AttackClip(Hook, true, 0.60f, MocapMask.All) },
            { "Dash Punch", new AttackClip(Cross, false, 0.25f, MocapMask.Arms | MocapMask.Spine) },
            { "Air Jab", new AttackClip(Jab, false, 0.22f, MocapMask.Arms | MocapMask.Spine) },
            { "Slash", new AttackClip(Slash, false, 0.22f, MocapMask.All) },
            { "Twin Slash", new AttackClip(Slash, false, 0.22f, MocapMask.All) },
            { "Twin Back Slash", new AttackClip(Slash, true, 0.22f, MocapMask.All) },
            { "Air Slash", new AttackClip(Slash, false, 0.22f, MocapMask.Arms | MocapMask.Spine) },
        };

        private const int RollArc = 10923;

        public static bool Resolve(MocapLibrary lib, ref FighterState f, CharacterDef c, float idleTime, float runTime, bool celebrating, out MocapChoice choice)
        {
            choice = new MocapChoice { Mask = MocapMask.All };
            if (lib == null || c.Stance == FighterStance.Beast || FighterLogic.IsCrouched(ref f)) return false;
            bool flying = f.Buff == BuffId.Levitate && (f.Action == FighterAction.Air || f.Action == FighterAction.Attack);
            bool boxer = c.Stance == FighterStance.Boxer;

            if (celebrating && (f.Action == FighterAction.Idle || f.Action == FighterAction.Move || f.Action == FighterAction.Victory))
            {
                return Loop(lib, "Action_dance", idleTime, MocapMask.All, ref choice);
            }

            switch (f.Action)
            {
                case FighterAction.Idle:
                    if (boxer) return false;
                    return Loop(lib, c.Weapon == "Sword" ? "Idle_with_a_sword" : "Idle_relaxed", idleTime, MocapMask.All, ref choice);

                case FighterAction.Move:
                    return Loop(lib, "Run_sprint", runTime, boxer ? MocapMask.Legs | MocapMask.Spine : MocapMask.All, ref choice);

                case FighterAction.Air:
                    if (flying) return false;
                    return Loop(lib, "Jump_in_the_air", f.ActionTicks / 60f, MocapMask.All, ref choice);

                case FighterAction.Block:
                case FighterAction.BlockStun:
                    if (c.Weapon != "Sword" || !lib.Has("Block_with_the_sword")) return false;
                    return At(lib, "Block_with_the_sword", Mathf.Min(f.BlockTicks / 60f + 0.25f, lib.Length("Block_with_the_sword") * 0.55f), false, MocapMask.All, false, ref choice);

                case FighterAction.HitStun:
                {
                    string clip = (f.ComboCount & 1) == 0 ? "Hit_to_the_chest" : "Hit_to_the_head";
                    return At(lib, clip, Mathf.Min(f.ActionTicks / 60f, lib.Length(clip) * 0.95f), false, MocapMask.All, false, ref choice);
                }

                case FighterAction.Launched:
                    return At(lib, "Hit_knockback", Mathf.Min(f.ActionTicks / 60f * 0.9f, lib.Length("Hit_knockback") * 0.75f), false, MocapMask.All, true, ref choice);

                case FighterAction.Knockdown:
                {
                    float len = lib.Length("Death_fall_backwards");
                    return At(lib, "Death_fall_backwards", Mathf.Min(len * 0.55f + f.ActionTicks / 60f, len * 0.98f), false, MocapMask.All, true, ref choice);
                }

                case FighterAction.KO:
                case FighterAction.RingOut:
                {
                    float len = lib.Length("Death_fall_backwards");
                    return At(lib, "Death_fall_backwards", Mathf.Min(f.ActionTicks / 60f * 1.1f, len * 0.98f), false, MocapMask.All, true, ref choice);
                }

                case FighterAction.GetUp:
                {
                    float len = lib.Length("Move_get_up_from_the_ground");
                    float k = Mathf.Clamp01(f.ActionTicks / (float)GameConfig.GetUpTicks);
                    return At(lib, "Move_get_up_from_the_ground", Mathf.Lerp(0.35f, 0.98f, k) * len, false, MocapMask.All, true, ref choice);
                }

                case FighterAction.Victory:
                    return Loop(lib, "Action_dance", idleTime, MocapMask.All, ref choice);

                case FighterAction.Attack:
                    return ResolveAttack(lib, ref f, flying, ref choice);
            }
            return false;
        }

        private static bool ResolveAttack(MocapLibrary lib, ref FighterState f, bool flying, ref MocapChoice choice)
        {
            MoveDef m = GameContent.GetMove(f.MoveId);
            if (m == null || flying) return false;

            if (m.Pose == MovePose.Dodge && m.TeleportDistance == Fix.Zero && f.Grounded)
            {
                // Forward roll only when rolling roughly the way the fighter faces.
                if (FixMath.AbsAngleDelta(f.Facing, f.MoveDirection) > RollArc || !lib.Has("Dodge_roll")) return false;
                float len = lib.Length("Dodge_roll");
                float k = Mathf.Clamp01(f.MoveTick / (float)Mathf.Max(1, m.Total - 1));
                return At(lib, "Dodge_roll", Mathf.Lerp(0.05f, 0.95f, k) * len, false, MocapMask.All, true, ref choice);
            }

            AttackClip a;
            if (!Attacks.TryGetValue(m.Name, out a) || !lib.Has(a.Clip)) return false;

            int start, end;
            FighterAnimator.Phases(m, out start, out end);
            float clipLen = lib.Length(a.Clip);
            float impact = a.Impact * clipLen;
            float hold = Mathf.Min(0.08f * clipLen, clipLen - impact);
            int t = f.MoveTick;
            float time;
            if (t < start) time = Mathf.Lerp(0f, impact, t / (float)Mathf.Max(1, start));
            else if (t <= end) time = impact + hold * ((t - start) / (float)Mathf.Max(1, end - start));
            else time = Mathf.Lerp(impact + hold, clipLen * 0.98f, (t - end) / (float)Mathf.Max(1, m.Total - end));
            return At(lib, a.Clip, time, a.Mirror, a.Mask, false, ref choice);
        }

        private static bool Loop(MocapLibrary lib, string clip, float time, MocapMask mask, ref MocapChoice choice)
        {
            if (!lib.Has(clip)) return false;
            float len = lib.Length(clip);
            return At(lib, clip, Mathf.Repeat(time, len), false, mask, false, ref choice);
        }

        private static bool At(MocapLibrary lib, string clip, float time, bool mirror, MocapMask mask, bool fullBody, ref MocapChoice choice)
        {
            if (!lib.Has(clip)) return false;
            choice.Clip = clip;
            choice.Time = Mathf.Max(0f, time);
            choice.Mirror = mirror;
            choice.Mask = mask;
            choice.FullBody = fullBody;
            return true;
        }
    }
}
