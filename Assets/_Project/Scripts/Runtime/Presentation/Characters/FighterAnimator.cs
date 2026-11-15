using HEXBOUND.Simulation;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Procedural animation. Each frame it builds a target pose (limb directions in the fighter's local space,
    /// plus spine and whole-body rotation) from the simulation state, blends toward it, and applies it to the rig.
    /// Attack poses follow the move's own frame data (startup, active, recovery), so what you see matches the hitboxes.
    /// </summary>
    public sealed class FighterAnimator
    {
        private struct Pose
        {
            public Vector3 UpperArmL, ForeArmL, UpperArmR, ForeArmR;
            public Vector3 ThighL, CalfL, ThighR, CalfR;
            public float SpinePitch, SpineYaw, SpineRoll;
            public float BodyPitch, BodyYaw, BodyRoll, BodyDrop;
            // Directions in body space used to apply the spine and pelvis (procedural angles are converted to these).
            public Vector3 SpineDir, NeckDir, PelvisUp, PelvisRight;
            // Staff: which way the staff points (body space) from the hand that grips its center.
            public Vector3 StaffDir;
            // Vex: launcher muzzle direction on the shoulder (body space), and 0 = on the shoulder .. 1 = slung on the back.
            public Vector3 LauncherDir;
            public float LauncherBack;
            public float LauncherGuard;
        }

        private readonly FighterVisual _v;
        private readonly MocapLibrary _mocap;
        private readonly MocapSampler _sampler;
        private MocapFrame _frame;
        private float _idleTime;
        private float _mocapRunTime;
        private Vector3 _restSpine = Vector3.up;
        private Vector3 _restNeck = Vector3.up;
        private Vector3 _restPelvisUp = Vector3.up;
        private Vector3 _restPelvisRight = Vector3.right;
        private float _restPelvisHeight = 0.95f;
        /// <summary>Rest directions of the 8 limb segments in body space (used by the beast, whose rest pose is its natural stance).</summary>
        private readonly Vector3[] _restLimb = new Vector3[8];
        private readonly bool _beast;
        private Pose _current;
        private bool _initialized;
        private float _runPhase;
        private float _time;
        private float _spinYaw;

        /// <summary>Run cycle phase in radians. A foot lands every half turn (pi).</summary>
        public float RunPhase
        {
            get { return _runPhase; }
        }

        public FighterAnimator(FighterVisual visual, bool beast = false)
        {
            _v = visual;
            _beast = beast;
            CaptureRest();
            if (beast) return;
            _mocap = MocapLibrary.Shared;
            if (_mocap != null)
            {
                _sampler = _mocap.CreateSampler(visual.Root);
                if (!_sampler.Valid)
                {
                    _sampler.Dispose();
                    _sampler = null;
                }
            }
        }

        /// <summary>Rest-pose directions of the target rig, so procedural poses stay relative to its natural posture.</summary>
        private void CaptureRest()
        {
            PoseRig rig = _v.Rig;
            Transform body = _v.Body;
            if (rig == null) return;
            Transform spine = rig.SpineLow != null ? rig.SpineLow : rig.Pelvis;
            if (spine != null && rig.Chest != null && spine != rig.Chest) _restSpine = SafeDir(body.InverseTransformDirection(rig.Chest.position - spine.position), Vector3.up);
            if (rig.Chest != null && rig.Head != null) _restNeck = SafeDir(body.InverseTransformDirection(rig.Head.position - rig.Chest.position), Vector3.up);
            if (rig.Pelvis != null && rig.Chest != null) _restPelvisUp = SafeDir(body.InverseTransformDirection(rig.Chest.position - rig.Pelvis.position), Vector3.up);
            if (rig.ThighL != null && rig.ThighR != null) _restPelvisRight = SafeDir(body.InverseTransformDirection(rig.ThighR.position - rig.ThighL.position), Vector3.right);
            if (rig.Pelvis != null) _restPelvisHeight = Mathf.Max(0.2f, body.InverseTransformPoint(rig.Pelvis.position).y);
            _restLimb[0] = RestDir(rig.UpperArmL, rig.ForeArmL, V(-0.28f, -1f, 0.08f));
            _restLimb[1] = RestDir(rig.ForeArmL, rig.HandL, V(-0.12f, -0.75f, 0.6f));
            _restLimb[2] = RestDir(rig.UpperArmR, rig.ForeArmR, V(0.28f, -1f, 0.12f));
            _restLimb[3] = RestDir(rig.ForeArmR, rig.HandR, V(0.08f, -0.45f, 0.9f));
            _restLimb[4] = RestDir(rig.ThighL, rig.CalfL, V(-0.14f, -1f, 0.06f));
            _restLimb[5] = RestDir(rig.CalfL, rig.FootL, V(-0.06f, -1f, -0.05f));
            _restLimb[6] = RestDir(rig.ThighR, rig.CalfR, V(0.14f, -1f, -0.04f));
            _restLimb[7] = RestDir(rig.CalfR, rig.FootR, V(0.06f, -1f, -0.08f));
        }

        private Vector3 RestDir(Transform bone, Transform child, Vector3 fallback)
        {
            if (bone == null || child == null) return fallback;
            return SafeDir(_v.Body.InverseTransformDirection(child.position - bone.position), fallback);
        }

        private static Vector3 SafeDir(Vector3 v, Vector3 fallback)
        {
            return v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
        }

        public void Dispose()
        {
            if (_sampler != null) _sampler.Dispose();
        }

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z).normalized;
        }

        /// <summary>True on the frame a chest-beat strike lands (beast intro and victory).</summary>
        public bool ChestBeatThisFrame { get; private set; }

        private bool _intro;
        private float _lastBeat;

        public void Animate(ref FighterState f, CharacterDef c, float deltaTime, float horizontalSpeed, bool celebrating = false, bool intro = false)
        {
            _intro = intro;
            ChestBeatThisFrame = false;
            _time += deltaTime;
            _idleTime += deltaTime;
            _mocapRunTime += deltaTime * Mathf.Clamp(horizontalSpeed / 7f, 0.4f, 1.6f);
            Pose target = BuildPose(ref f, c, deltaTime, horizontalSpeed);
            ApplyMocap(ref target, ref f, c, celebrating);
            float rate = f.Action == FighterAction.Attack ? 34f : (f.Action == FighterAction.Move ? 16f : 13f);
            if (f.Action == FighterAction.HitStun || f.Action == FighterAction.Launched || f.Action == FighterAction.Parried) rate = 28f;
            if (!_initialized)
            {
                _current = target;
                _initialized = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-rate * deltaTime);
                Blend(ref _current, ref target, k);
            }
            Apply(ref _current);
        }

        private static void Blend(ref Pose a, ref Pose b, float k)
        {
            a.UpperArmL = Vector3.Slerp(a.UpperArmL, b.UpperArmL, k);
            a.ForeArmL = Vector3.Slerp(a.ForeArmL, b.ForeArmL, k);
            a.UpperArmR = Vector3.Slerp(a.UpperArmR, b.UpperArmR, k);
            a.ForeArmR = Vector3.Slerp(a.ForeArmR, b.ForeArmR, k);
            a.ThighL = Vector3.Slerp(a.ThighL, b.ThighL, k);
            a.CalfL = Vector3.Slerp(a.CalfL, b.CalfL, k);
            a.ThighR = Vector3.Slerp(a.ThighR, b.ThighR, k);
            a.CalfR = Vector3.Slerp(a.CalfR, b.CalfR, k);
            a.SpinePitch = Mathf.Lerp(a.SpinePitch, b.SpinePitch, k);
            a.SpineYaw = Mathf.Lerp(a.SpineYaw, b.SpineYaw, k);
            a.SpineRoll = Mathf.Lerp(a.SpineRoll, b.SpineRoll, k);
            a.BodyPitch = Mathf.Lerp(a.BodyPitch, b.BodyPitch, k);
            a.BodyYaw = b.BodyYaw;
            a.BodyRoll = Mathf.Lerp(a.BodyRoll, b.BodyRoll, k);
            a.BodyDrop = Mathf.Lerp(a.BodyDrop, b.BodyDrop, k);
            a.SpineDir = Vector3.Slerp(a.SpineDir, b.SpineDir, k);
            a.NeckDir = Vector3.Slerp(a.NeckDir, b.NeckDir, k);
            a.PelvisUp = Vector3.Slerp(a.PelvisUp, b.PelvisUp, k);
            a.PelvisRight = Vector3.Slerp(a.PelvisRight, b.PelvisRight, k);
            a.StaffDir = a.StaffDir.sqrMagnitude < 1e-6f ? b.StaffDir : Vector3.Slerp(a.StaffDir, b.StaffDir, k);
            a.LauncherDir = a.LauncherDir.sqrMagnitude < 1e-6f ? b.LauncherDir : Vector3.Slerp(a.LauncherDir, b.LauncherDir, k);
            a.LauncherBack = Mathf.Lerp(a.LauncherBack, b.LauncherBack, k);
            a.LauncherGuard = Mathf.Lerp(a.LauncherGuard, b.LauncherGuard, k);
        }

        /// <summary>Overlays a motion-capture pose on the procedural one, for the body parts the clip covers.</summary>
        private void ApplyMocap(ref Pose p, ref FighterState f, CharacterDef c, bool celebrating)
        {
            if (_sampler == null) return;
            MocapChoice choice;
            if (!MocapBindings.Resolve(_mocap, ref f, c, _idleTime, _mocapRunTime, celebrating, out choice)) return;
            if (!_sampler.Sample(choice.Clip, choice.Time, choice.Mirror, ref _frame)) return;

            if ((choice.Mask & MocapMask.Arms) != 0)
            {
                p.UpperArmL = _frame.UpperArmL;
                p.ForeArmL = _frame.ForeArmL;
                p.UpperArmR = _frame.UpperArmR;
                p.ForeArmR = _frame.ForeArmR;
            }
            if ((choice.Mask & MocapMask.Legs) != 0)
            {
                p.ThighL = _frame.ThighL;
                p.CalfL = _frame.CalfL;
                p.ThighR = _frame.ThighR;
                p.CalfR = _frame.CalfR;
                p.PelvisUp = _frame.PelvisUp;
                p.PelvisRight = _frame.PelvisRight;
                p.BodyDrop = Mathf.Clamp(_restPelvisHeight * (1f - _frame.PelvisHeightRatio), -0.3f, _restPelvisHeight - 0.08f);
            }
            if ((choice.Mask & MocapMask.Spine) != 0)
            {
                p.SpineDir = _frame.SpineDir;
                p.NeckDir = _frame.NeckDir;
                p.SpineYaw = _frame.SpineYaw;
            }
            if (choice.FullBody)
            {
                p.BodyPitch = 0f;
                p.BodyRoll = 0f;
            }

            if (f.Buff == BuffId.TwinBlades && f.Action == FighterAction.Attack && !choice.Mirror)
            {
                p.UpperArmL = new Vector3(-p.UpperArmR.x, p.UpperArmR.y, p.UpperArmR.z);
                p.ForeArmL = new Vector3(-p.ForeArmR.x, p.ForeArmR.y, p.ForeArmR.z);
            }
        }

        private Pose Idle()
        {
            float breathe = Mathf.Sin(_time * 2.2f);
            return new Pose
            {
                UpperArmL = V(-0.28f, -1f, 0.08f),
                ForeArmL = V(-0.12f, -0.75f, 0.6f),
                UpperArmR = V(0.28f, -1f, 0.12f),
                ForeArmR = V(0.08f, -0.45f, 0.9f),
                ThighL = V(-0.14f, -1f, 0.06f),
                CalfL = V(-0.06f, -1f, -0.05f),
                ThighR = V(0.14f, -1f, -0.04f),
                CalfR = V(0.06f, -1f, -0.08f),
                SpinePitch = 4f + breathe * 1.2f,
                BodyDrop = 0.02f + breathe * 0.008f,
            };
        }

        private Pose BuildPose(ref FighterState f, CharacterDef c, float dt, float speed)
        {
            Pose p = Idle();
            bool flying = f.Buff == BuffId.Levitate && (f.Action == FighterAction.Air || f.Action == FighterAction.Attack);
            bool armed = FighterLogic.IsArmed(ref f, c);
            _spinYaw = 0f;
            p.LauncherDir = V(0f, 0.08f, 1f);
            p.LauncherBack = f.WeaponMode == 1 || (f.WeaponsHeld & 1) == 0 ? 1f : 0f;

            switch (f.Action)
            {
                case FighterAction.Move:
                {
                    float maxSpeed = c.MoveSpeed.ToFloat() * GameConfig.TickRate;
                    float s = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f));
                    _runPhase += dt * (6f + 6f * s);
                    float ph = _runPhase;
                    float swing = 0.85f * s;
                    p.ThighL = V(-0.1f, -1f, Mathf.Sin(ph) * swing);
                    p.CalfL = V(-0.05f, -1f, Mathf.Sin(ph) * swing * 0.3f - 0.75f * s * Mathf.Max(0f, Mathf.Cos(ph)));
                    p.ThighR = V(0.1f, -1f, -Mathf.Sin(ph) * swing);
                    p.CalfR = V(0.05f, -1f, -Mathf.Sin(ph) * swing * 0.3f - 0.75f * s * Mathf.Max(0f, -Mathf.Cos(ph)));
                    p.UpperArmL = V(-0.3f, -1f, -Mathf.Sin(ph) * 0.7f * s);
                    p.ForeArmL = V(-0.12f, -0.4f, 0.75f);
                    p.UpperArmR = V(0.3f, -1f, Mathf.Sin(ph) * 0.45f * s + 0.1f);
                    p.ForeArmR = V(0.08f, -0.3f, 0.95f);
                    p.SpinePitch = 6f + 10f * s;
                    p.BodyDrop = 0.03f + Mathf.Abs(Mathf.Sin(ph)) * 0.05f * s;
                    break;
                }
                case FighterAction.Air:
                    if (flying)
                    {
                        float hover = Mathf.Sin(_time * 3f);
                        p.ThighL = V(-0.12f, -1f, 0.1f);
                        p.CalfL = V(-0.08f, -1f, -0.35f);
                        p.ThighR = V(0.12f, -1f, 0.2f);
                        p.CalfR = V(0.08f, -1f, -0.25f);
                        p.UpperArmL = V(-0.9f, -0.15f + hover * 0.1f, 0.2f);
                        p.ForeArmL = V(-0.8f, 0.15f, 0.4f);
                        p.UpperArmR = V(0.9f, -0.15f - hover * 0.1f, 0.2f);
                        p.ForeArmR = V(0.8f, 0.15f, 0.4f);
                        p.SpinePitch = -4f;
                    }
                    else
                    {
                        bool rising = f.Velocity.Y > Fix.Zero;
                        p.ThighL = V(-0.15f, -0.5f, rising ? 0.85f : 0.4f);
                        p.CalfL = V(-0.08f, -1f, -0.45f);
                        p.ThighR = V(0.15f, -0.8f, rising ? 0.3f : 0.15f);
                        p.CalfR = V(0.08f, -1f, -0.25f);
                        p.UpperArmL = V(-0.7f, -0.4f, 0.15f);
                        p.ForeArmL = V(-0.4f, -0.1f, 0.6f);
                        p.UpperArmR = V(0.7f, -0.3f, 0.2f);
                        p.ForeArmR = V(0.3f, -0.1f, 0.9f);
                        p.SpinePitch = rising ? -5f : 8f;
                    }
                    break;
                case FighterAction.BlockStun when f.LowGuardStun:
                    CrouchPose(ref p, true);
                    p.SpinePitch -= 8f;
                    break;
                case FighterAction.Block:
                case FighterAction.BlockStun:
                {
                    p.UpperArmL = V(-0.35f, -0.35f, 0.75f);
                    p.ForeArmL = V(0.55f, 0.75f, 0.35f);
                    p.UpperArmR = V(0.35f, -0.35f, 0.75f);
                    p.ForeArmR = V(-0.55f, 0.75f, 0.35f);
                    p.ThighL = V(-0.32f, -1f, 0.15f);
                    p.CalfL = V(-0.18f, -1f, -0.1f);
                    p.ThighR = V(0.32f, -1f, -0.15f);
                    p.CalfR = V(0.18f, -1f, -0.15f);
                    p.SpinePitch = 12f;
                    p.BodyDrop = 0.1f;
                    if (f.Action == FighterAction.BlockStun) p.SpinePitch = 2f;
                    break;
                }
                case FighterAction.HitStun:
                {
                    float shake = Mathf.Sin(_time * 40f) * 4f;
                    p.UpperArmL = V(-0.8f, -0.4f, -0.3f);
                    p.ForeArmL = V(-0.6f, 0.2f, 0.1f);
                    p.UpperArmR = V(0.8f, -0.4f, -0.3f);
                    p.ForeArmR = V(0.6f, 0.2f, 0.1f);
                    p.SpinePitch = -20f + shake;
                    p.SpineRoll = shake;
                    p.BodyDrop = 0.06f;
                    break;
                }
                case FighterAction.Launched:
                    p.UpperArmL = V(-0.85f, 0.35f, -0.2f);
                    p.ForeArmL = V(-0.6f, 0.6f, 0.1f);
                    p.UpperArmR = V(0.85f, 0.35f, -0.2f);
                    p.ForeArmR = V(0.6f, 0.6f, 0.1f);
                    p.ThighL = V(-0.15f, -0.7f, 0.6f);
                    p.CalfL = V(-0.1f, -1f, 0.1f);
                    p.ThighR = V(0.15f, -0.9f, 0.3f);
                    p.CalfR = V(0.1f, -1f, -0.1f);
                    p.SpinePitch = -18f;
                    p.BodyPitch = -40f;
                    break;
                case FighterAction.Knockdown:
                case FighterAction.KO:
                case FighterAction.RingOut:
                    p.UpperArmL = V(-0.9f, 0f, 0.1f);
                    p.ForeArmL = V(-0.9f, 0.2f, 0.2f);
                    p.UpperArmR = V(0.9f, 0f, 0.1f);
                    p.ForeArmR = V(0.9f, 0.2f, 0.2f);
                    p.ThighL = V(-0.2f, -1f, 0.1f);
                    p.CalfL = V(-0.2f, -1f, 0f);
                    p.ThighR = V(0.2f, -1f, 0.05f);
                    p.CalfR = V(0.2f, -1f, 0f);
                    p.BodyPitch = -86f;
                    p.BodyDrop = -0.18f;
                    break;
                case FighterAction.GetUp:
                {
                    float t = Mathf.Clamp01(f.ActionTicks / (float)GameConfig.GetUpTicks);
                    p.BodyPitch = Mathf.Lerp(-86f, 0f, t);
                    p.BodyDrop = Mathf.Lerp(-0.18f, 0.15f, t);
                    p.ThighL = V(-0.2f, -0.6f, 0.75f);
                    p.CalfL = V(-0.1f, -1f, -0.3f);
                    break;
                }
                case FighterAction.Landing:
                    p.ThighL = V(-0.18f, -0.6f, 0.75f);
                    p.CalfL = V(-0.1f, -1f, -0.35f);
                    p.ThighR = V(0.18f, -0.7f, 0.5f);
                    p.CalfR = V(0.1f, -1f, -0.45f);
                    p.SpinePitch = 22f;
                    p.BodyDrop = 0.22f;
                    break;
                case FighterAction.Parried:
                case FighterAction.GuardBroken:
                {
                    float wobble = Mathf.Sin(_time * 9f) * 8f;
                    p.UpperArmL = V(-0.7f, 0.5f, 0.1f);
                    p.ForeArmL = V(-0.4f, 0.9f, 0.1f);
                    p.UpperArmR = V(0.7f, 0.5f, 0.1f);
                    p.ForeArmR = V(0.4f, 0.9f, 0.1f);
                    p.SpinePitch = -14f;
                    p.SpineRoll = wobble;
                    p.BodyDrop = 0.04f;
                    break;
                }
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                    CrouchPose(ref p, f.Action == FighterAction.CrouchBlock);
                    break;
                case FighterAction.Victory:
                    p.UpperArmR = V(0.25f, 1f, 0.15f);
                    p.ForeArmR = V(0.1f, 1f, 0f);
                    break;
                case FighterAction.Attack:
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    if (m != null) AttackPose(ref p, ref f, m, flying);
                    break;
            }

            if (c.Stance == FighterStance.Boxer && (f.Action == FighterAction.Idle || f.Action == FighterAction.Move || (f.Action == FighterAction.Air && !flying)))
            {
                // Boxing guard: fists up in front of the face.
                float bob = Mathf.Sin(_time * (f.Action == FighterAction.Move ? 9f : 4f)) * 0.06f;
                p.UpperArmL = V(-0.32f, -0.55f + bob, 0.62f);
                p.ForeArmL = V(0.18f, 0.9f, 0.4f);
                p.UpperArmR = V(0.32f, -0.6f - bob, 0.5f);
                p.ForeArmR = V(-0.12f, 0.9f, 0.32f);
                p.SpinePitch += 6f;
            }

            if (c.Stance == FighterStance.Gunner && armed && (f.Action == FighterAction.Idle || f.Action == FighterAction.Move || (f.Action == FighterAction.Air && !flying)))
            {
                if (f.WeaponMode == 0 && _v.LauncherHolder != null)
                {
                    // Launcher held at the right hip, braced under the arm: right hand down on the rear grip,
                    // shoulders turned so the left hand reaches the top handle.
                    float bob = Mathf.Sin(_time * (f.Action == FighterAction.Move ? 9f : 3f)) * 0.02f;
                    HipCarry(ref p, 0.03f + bob);
                }
                else if (f.WeaponMode == 0)
                {
                    // Launcher held ready at the hip, pointing forward, the off hand on the tube.
                    float bob = Mathf.Sin(_time * (f.Action == FighterAction.Move ? 9f : 3f)) * 0.04f;
                    p.UpperArmR = V(0.35f, -0.75f + bob, 0.35f);
                    p.ForeArmR = V(0.05f, 0.05f, 1f);
                    p.UpperArmL = V(-0.15f, -0.6f, 0.75f);
                    p.ForeArmL = V(0.45f, 0.05f, 0.9f);
                }
                else
                {
                    // Pistol held low and ready.
                    p.UpperArmR = V(0.25f, -0.9f, 0.3f);
                    p.ForeArmR = V(0.05f, -0.3f, 1f);
                }
            }

            if (c.Weapon == "Staff" && armed && (f.Action == FighterAction.Idle || f.Action == FighterAction.Move || (f.Action == FighterAction.Air && !flying)))
            {
                // Staff held in front, tip angled up and forward; the off hand rests near it.
                float sway = Mathf.Sin(_time * (f.Action == FighterAction.Move ? 9f : 2f)) * 0.05f;
                // Gripped at the center, forearm forward, the staff standing upright beside the body.
                p.UpperArmR = V(0.3f, -0.85f + sway, 0.3f);
                p.ForeArmR = V(0.08f, 0.1f, 1f);
                p.StaffDir = V(0.06f, 1f, 0.12f);
                p.UpperArmL = V(-0.3f, -0.9f, 0.25f);
                p.ForeArmL = V(0.15f, -0.4f, 0.9f);
            }
            if (c.HeavyIsGuard && armed && (f.Action == FighterAction.Block || (f.Action == FighterAction.BlockStun && !f.LowGuardStun)))
            {
                // Guard: the staff held sideways across the front of the body, right hand at its center.
                p.UpperArmR = V(0.3f, -0.45f, 0.85f);
                p.ForeArmR = V(-0.35f, 0.25f, 0.9f);
                p.StaffDir = V(-1f, 0.12f, 0.05f);
                p.UpperArmL = V(-0.45f, -0.4f, 0.8f);
                p.ForeArmL = V(0.1f, 0.25f, 0.95f);
                p.SpinePitch = f.Action == FighterAction.BlockStun ? 2f : 8f;
                if (c.Stance == FighterStance.Gunner && f.WeaponMode == 0 && _v.LauncherHolder != null)
                {
                    // Launcher guard: the tube held sideways across the chest with both hands, like the mage's staff.
                    p.UpperArmR = V(0.35f, -0.5f, 0.8f);
                    p.ForeArmR = V(-0.2f, 0.3f, 0.95f);
                    p.UpperArmL = V(-0.4f, -0.5f, 0.75f);
                    p.ForeArmL = V(0.2f, 0.3f, 0.95f);
                    p.SpineYaw = 0f;
                    p.LauncherBack = 0f;
                    p.LauncherGuard = 1f;
                }
                else if (c.Stance == FighterStance.Gunner)
                {
                    // Pistol guard: the off arm raised in front of the face, the pistol tucked close.
                    p.UpperArmL = V(-0.3f, -0.35f, 0.85f);
                    p.ForeArmL = V(0.55f, 0.75f, 0.3f);
                    p.UpperArmR = V(0.3f, -0.7f, 0.5f);
                    p.ForeArmR = V(-0.1f, 0.55f, 0.8f);
                }
            }

            if (_beast) BeastPose(ref p, ref f, c, dt, speed);

            if (f.Buff == BuffId.TwinBlades && f.Action == FighterAction.Attack)
            {
                // The off hand mirrors the sword hand during Twin Blades attacks.
                p.UpperArmL = new Vector3(-p.UpperArmR.x, p.UpperArmR.y, p.UpperArmR.z);
                p.ForeArmL = new Vector3(-p.ForeArmR.x, p.ForeArmR.y, p.ForeArmR.z);
            }

            p.BodyYaw = _spinYaw;
            p.SpineDir = Quaternion.Euler(p.SpinePitch * 0.6f, 0f, -p.SpineRoll * 0.6f) * _restSpine;
            p.NeckDir = Quaternion.Euler(p.SpinePitch, 0f, -p.SpineRoll) * _restNeck;
            p.PelvisUp = _restPelvisUp;
            p.PelvisRight = _restPelvisRight;
            if (p.StaffDir.sqrMagnitude < 1e-6f)
            {
                // Default fist grip: the staff stands at right angles to the forearm, as upright as it can.
                Vector3 fa = p.ForeArmR.normalized;
                Vector3 d = Vector3.ProjectOnPlane(Vector3.up, fa);
                if (d.sqrMagnitude < 0.04f) d = Vector3.ProjectOnPlane(Vector3.forward, fa);
                p.StaffDir = d.normalized;
            }
            return p;
        }

        /// <summary>Low crouch (legs folded, body dropped). Guard: forearms crossed low in front.</summary>
        private void CrouchPose(ref Pose p, bool guard)
        {
            p.ThighL = V(-0.3f, -0.35f, 0.9f);
            p.CalfL = V(-0.12f, -1f, -0.3f);
            p.ThighR = V(0.3f, -0.55f, 0.75f);
            p.CalfR = V(0.12f, -1f, -0.45f);
            p.BodyDrop = 0.55f * _restPelvisHeight;
            p.SpinePitch = 16f;
            if (guard)
            {
                p.UpperArmL = V(-0.35f, -0.3f, 0.85f);
                p.ForeArmL = V(0.7f, 0.35f, 0.45f);
                p.UpperArmR = V(0.35f, -0.3f, 0.85f);
                p.ForeArmR = V(-0.7f, 0.35f, 0.45f);
                p.SpinePitch = 24f;
            }
            else
            {
                p.UpperArmL = V(-0.4f, -0.8f, 0.45f);
                p.ForeArmL = V(-0.1f, -0.2f, 1f);
                p.UpperArmR = V(0.4f, -0.8f, 0.45f);
                p.ForeArmR = V(0.1f, -0.2f, 1f);
            }
        }

        /// <summary>Swings a rest direction forward (positive) or back (negative) by degrees around the body's side axis.</summary>
        private static Vector3 Fwd(Vector3 dir, float degrees)
        {
            return Quaternion.AngleAxis(-degrees, Vector3.right) * dir;
        }

        /// <summary>
        /// The beast keeps its sculpted hunched stance as the base and moves its limbs around it: knuckle-walking,
        /// big two-handed swings and slams, a crossed-arm guard, and a roar when it awakens.
        /// </summary>
        private void BeastPose(ref Pose p, ref FighterState f, CharacterDef c, float dt, float speed)
        {
            Vector3 ual = _restLimb[0], fal = _restLimb[1], uar = _restLimb[2], far = _restLimb[3];
            Vector3 thl = _restLimb[4], cal = _restLimb[5], thr = _restLimb[6], car = _restLimb[7];
            float breathe = Mathf.Sin(_time * (f.Buff == BuffId.Awakened ? 4.5f : 2f));
            float drop = p.BodyDrop;

            // Rest stance by default.
            p.UpperArmL = Fwd(ual, breathe * 2f);
            p.ForeArmL = fal;
            p.UpperArmR = Fwd(uar, -breathe * 2f);
            p.ForeArmR = far;
            p.ThighL = thl;
            p.CalfL = cal;
            p.ThighR = thr;
            p.CalfR = car;
            p.SpinePitch = breathe * 2f;
            p.BodyDrop = breathe * 0.02f;

            if (_intro && (f.Action == FighterAction.Idle || f.Action == FighterAction.Move))
            {
                // Round intro: the beast pounds its chest.
                ChestBeat(ref p, 13f);
                return;
            }

            switch (f.Action)
            {
                case FighterAction.Move:
                {
                    float maxSpeed = c.MoveSpeed.ToFloat() * GameConfig.TickRate;
                    float s = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f));
                    float ph = _runPhase;
                    p.ThighL = Fwd(thl, Mathf.Sin(ph) * 28f * s);
                    p.CalfL = Fwd(cal, -Mathf.Max(0f, Mathf.Cos(ph)) * 22f * s);
                    p.ThighR = Fwd(thr, -Mathf.Sin(ph) * 28f * s);
                    p.CalfR = Fwd(car, -Mathf.Max(0f, -Mathf.Cos(ph)) * 22f * s);
                    p.UpperArmL = Fwd(ual, -Mathf.Sin(ph) * 30f * s);
                    p.ForeArmL = Fwd(fal, -Mathf.Sin(ph) * 20f * s);
                    p.UpperArmR = Fwd(uar, Mathf.Sin(ph) * 30f * s);
                    p.ForeArmR = Fwd(far, Mathf.Sin(ph) * 20f * s);
                    p.SpinePitch = 4f * s;
                    p.BodyDrop = Mathf.Abs(Mathf.Sin(ph)) * 0.08f * s;
                    break;
                }
                case FighterAction.Block:
                case FighterAction.BlockStun:
                case FighterAction.CrouchBlock:
                    p.UpperArmL = V(-0.45f, 0.15f, 0.9f);
                    p.ForeArmL = V(0.65f, 0.65f, 0.35f);
                    p.UpperArmR = V(0.45f, 0.15f, 0.9f);
                    p.ForeArmR = V(-0.65f, 0.65f, 0.35f);
                    p.SpinePitch = 6f;
                    if (f.Action == FighterAction.CrouchBlock || f.LowGuardStun) p.BodyDrop = 0.35f * _restPelvisHeight;
                    break;
                case FighterAction.Crouch:
                    p.ThighL = Fwd(thl, 30f);
                    p.CalfL = Fwd(cal, -28f);
                    p.ThighR = Fwd(thr, 30f);
                    p.CalfR = Fwd(car, -28f);
                    p.BodyDrop = 0.35f * _restPelvisHeight;
                    p.SpinePitch = 10f;
                    break;
                case FighterAction.HitStun:
                case FighterAction.Parried:
                case FighterAction.GuardBroken:
                    p.UpperArmL = Fwd(ual, -35f);
                    p.UpperArmR = Fwd(uar, -35f);
                    p.SpinePitch = -18f + Mathf.Sin(_time * 30f) * 4f;
                    break;
                case FighterAction.Air:
                case FighterAction.Landing:
                    p.ThighL = Fwd(thl, 25f);
                    p.ThighR = Fwd(thr, 25f);
                    p.UpperArmL = V(-0.8f, 0.3f, 0.4f);
                    p.UpperArmR = V(0.8f, 0.3f, 0.4f);
                    p.BodyDrop = f.Action == FighterAction.Landing ? 0.2f : 0f;
                    break;
                case FighterAction.Victory:
                    ChestBeat(ref p, 12f);
                    break;
                case FighterAction.Launched:
                case FighterAction.Knockdown:
                case FighterAction.KO:
                case FighterAction.RingOut:
                case FighterAction.GetUp:
                    // Whole-body tumble from the shared pose; limbs flail a little around the rest stance.
                    p.UpperArmL = Fwd(ual, -60f);
                    p.UpperArmR = Fwd(uar, -60f);
                    p.BodyDrop = drop;
                    break;
                case FighterAction.Attack:
                {
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    if (m != null) BeastAttackPose(ref p, ref f, m);
                    break;
                }
            }
        }

        /// <summary>Gorilla chest beat: fists alternate against the chest while the beast rears up.</summary>
        private void ChestBeat(ref Pose p, float speed)
        {
            float beat = Mathf.Sin(_time * speed);
            p.UpperArmL = V(-0.55f, 0.05f, 0.85f);
            p.UpperArmR = V(0.55f, 0.05f, 0.85f);
            p.ForeArmL = beat > 0f ? V(0.85f, 0.15f, -0.45f) : V(0.2f, 0.5f, 0.8f);
            p.ForeArmR = beat > 0f ? V(-0.2f, 0.5f, 0.8f) : V(-0.85f, 0.15f, -0.45f);
            p.SpinePitch = -16f;
            p.BodyDrop = -0.04f;
            if (Mathf.Sign(beat) != Mathf.Sign(_lastBeat)) ChestBeatThisFrame = true;
            _lastBeat = beat;
        }

        private void BeastAttackPose(ref Pose p, ref FighterState f, MoveDef m)
        {
            int start, end;
            Phases(m, out start, out end);
            int t = f.MoveTick;
            float a = t < start ? 0f : (t > end ? 1f : (t - start) / (float)Mathf.Max(1, end - start));
            float wind = t < start ? t / (float)Mathf.Max(1, start) : 1f;
            float recover = t > end ? (t - end) / (float)Mathf.Max(1, m.Total - end) : 0f;
            float ease = Mathf.SmoothStep(0f, 1f, a);
            float back = 1f - recover;
            Vector3 restUAL = _restLimb[0], restFAL = _restLimb[1], restUAR = _restLimb[2], restFAR = _restLimb[3];

            switch (m.Pose)
            {
                case MovePose.Swing:
                case MovePose.SwingReverse:
                {
                    // A wide horizontal claw swipe with one arm (right for Swing, left for SwingReverse).
                    bool left = m.Pose == MovePose.SwingReverse;
                    float sign = left ? -1f : 1f;
                    float theta = (t < start ? Mathf.Lerp(0f, 110f, wind) : Mathf.Lerp(110f, -70f, ease)) * Mathf.Deg2Rad;
                    Vector3 arm = V(sign * Mathf.Sin(theta), 0.05f, Mathf.Cos(theta) + 0.3f);
                    Vector3 rest = left ? restUAL : restUAR;
                    Vector3 dir = Vector3.Slerp(rest, arm, t < start ? wind : back);
                    if (left)
                    {
                        p.UpperArmL = dir;
                        p.ForeArmL = dir;
                    }
                    else
                    {
                        p.UpperArmR = dir;
                        p.ForeArmR = dir;
                    }
                    p.SpineYaw = sign * Mathf.Lerp(30f, -30f, ease) * back;
                    p.SpinePitch = 6f;
                    break;
                }
                case MovePose.JabLeft:
                {
                    // Heavy left punch.
                    Vector3 pulledL = V(-0.45f, 0.2f, -0.7f);
                    Vector3 strikeL = V(-0.05f, 0.1f, 1f);
                    Vector3 dirL = t < start ? Vector3.Slerp(restUAL, pulledL, wind) : Vector3.Slerp(strikeL, restUAL, recover);
                    p.UpperArmL = dirL;
                    p.ForeArmL = t < start ? Vector3.Slerp(restFAL, V(-0.3f, 0.5f, 0.3f), wind) : dirL;
                    p.SpineYaw = t < start ? -30f * wind : Mathf.Lerp(22f, 0f, recover);
                    p.SpinePitch = t < start ? -6f * wind : 10f * back;
                    break;
                }
                case MovePose.Thrust:
                {
                    // Crushing Punch: pull the right fist far back, then drive it straight ahead.
                    Vector3 pulled = V(0.45f, 0.2f, -0.8f);
                    Vector3 strike = V(0.05f, 0.12f, 1f);
                    Vector3 dir = t < start ? Vector3.Slerp(restUAR, pulled, wind) : Vector3.Slerp(strike, restUAR, recover);
                    p.UpperArmR = dir;
                    p.ForeArmR = t < start ? Vector3.Slerp(restFAR, V(0.3f, 0.5f, 0.3f), wind) : dir;
                    p.UpperArmL = Fwd(restUAL, -20f);
                    p.SpineYaw = t < start ? 35f * wind : Mathf.Lerp(-25f, 0f, recover);
                    p.SpinePitch = t < start ? -8f * wind : 12f * back;
                    break;
                }
                case MovePose.Overhead:
                case MovePose.Slam:
                {
                    // Both fists raised high, then hammered down.
                    Vector3 upL = V(-0.25f, 1f, -0.15f);
                    Vector3 upR = V(0.25f, 1f, -0.15f);
                    float low = m.Pose == MovePose.Slam ? -0.85f : -0.45f;
                    Vector3 downL = V(-0.15f, low, 0.8f);
                    Vector3 downR = V(0.15f, low, 0.8f);
                    if (t < start)
                    {
                        p.UpperArmL = Vector3.Slerp(restUAL, upL, wind);
                        p.UpperArmR = Vector3.Slerp(restUAR, upR, wind);
                    }
                    else
                    {
                        p.UpperArmL = Vector3.Slerp(Vector3.Slerp(upL, downL, ease), restUAL, recover);
                        p.UpperArmR = Vector3.Slerp(Vector3.Slerp(upR, downR, ease), restUAR, recover);
                    }
                    p.ForeArmL = p.UpperArmL;
                    p.ForeArmR = p.UpperArmR;
                    p.SpinePitch = t < start ? -18f * wind : Mathf.Lerp(-18f, 30f, ease) * back;
                    p.BodyDrop = t < start ? -0.05f * wind : Mathf.Lerp(0f, 0.25f, ease) * back;
                    break;
                }
                case MovePose.Flurry:
                {
                    // Rampage: fists pound forward in turn, then a double-fist finisher.
                    bool active = t >= start && t <= end;
                    float k = active ? 0.5f + 0.5f * Mathf.Sin(t * 1.1f) : 0f;
                    Vector3 fwd = V(0f, 0.1f, 1f);
                    p.UpperArmR = Vector3.Slerp(Fwd(restUAR, -10f), fwd, k);
                    p.ForeArmR = p.UpperArmR;
                    p.UpperArmL = Vector3.Slerp(fwd, Fwd(restUAL, -10f), k);
                    p.ForeArmL = p.UpperArmL;
                    p.SpineYaw = Mathf.Lerp(-15f, 15f, k);
                    p.SpinePitch = 10f;
                    break;
                }
                case MovePose.Grab:
                {
                    Vector3 reachL = t < start ? V(-0.9f, 0.2f, 0.5f) : V(-0.15f, 0f, 1f);
                    Vector3 reachR = new Vector3(-reachL.x, reachL.y, reachL.z);
                    p.UpperArmL = Vector3.Slerp(reachL, restUAL, recover);
                    p.ForeArmL = p.UpperArmL;
                    p.UpperArmR = Vector3.Slerp(reachR, restUAR, recover);
                    p.ForeArmR = p.UpperArmR;
                    p.SpinePitch = 14f;
                    break;
                }
                case MovePose.Sweep:
                {
                    // Tail sweep: drop low and spin once.
                    p.BodyDrop = 0.3f * _restPelvisHeight;
                    if (t >= start - 4 && t <= end + 8) _spinYaw = Mathf.Lerp(0f, 360f, Mathf.Clamp01((t - (start - 4)) / (float)Mathf.Max(1, end + 12 - start)));
                    break;
                }
                case MovePose.Roar:
                {
                    float k = t < start ? wind : back;
                    p.UpperArmL = Vector3.Slerp(restUAL, V(-1f, 0.5f, 0.2f), k);
                    p.ForeArmL = Vector3.Slerp(restFAL, V(-0.7f, 0.9f, 0.1f), k);
                    p.UpperArmR = Vector3.Slerp(restUAR, V(1f, 0.5f, 0.2f), k);
                    p.ForeArmR = Vector3.Slerp(restFAR, V(0.7f, 0.9f, 0.1f), k);
                    p.SpinePitch = Mathf.Lerp(0f, -30f, k);
                    p.BodyDrop = Mathf.Lerp(0f, -0.12f, k);
                    break;
                }
                case MovePose.Dodge:
                    p.SpinePitch = 20f;
                    p.BodyDrop = 0.2f;
                    break;
            }
        }

        /// <summary>Startup, active and recovery windows, taken from the move's hitboxes or spawn ticks.</summary>
        internal static void Phases(MoveDef m, out int start, out int end)
        {
            start = -1;
            end = -1;
            for (int i = 0; i < m.Hitboxes.Length; i++)
            {
                if (start < 0 || m.Hitboxes[i].Start < start) start = m.Hitboxes[i].Start;
                if (m.Hitboxes[i].End > end) end = m.Hitboxes[i].End;
            }
            for (int i = 0; i < m.Spawns.Length; i++)
            {
                if (start < 0 || m.Spawns[i].Tick < start) start = m.Spawns[i].Tick;
                if (m.Spawns[i].Tick + 4 > end) end = m.Spawns[i].Tick + 4;
            }
            if (m.TeleportDistance > Fix.Zero)
            {
                start = m.TeleportTick;
                end = m.TeleportTick + 4;
            }
            if (start < 0)
            {
                start = Mathf.Max(1, m.Total / 3);
                end = Mathf.Max(start + 2, m.Total / 2);
            }
            if (end <= start) end = start + 2;
        }

        /// <summary>Vex's hip carry (reference: heavy launcher held low at the right hip). lift tilts the muzzle up or down.</summary>
        private static void HipCarry(ref Pose p, float lift)
        {
            lift = Mathf.Clamp(lift, -0.7f, 0.7f);
            p.UpperArmR = V(0.13f, -0.95f, 0.3f);
            p.ForeArmR = V(0.12f, -0.9f + lift * 0.3f, 0.4f);
            p.UpperArmL = V(0.75f, -0.32f + lift * 0.3f, 0.55f);
            p.ForeArmL = V(0.7f, 0.12f + lift * 0.4f, 0.5f);
            p.SpineYaw = 30f;
            p.SpinePitch = 5f;
            p.LauncherDir = V(0f, lift, 1f);
        }

        private void AttackPose(ref Pose p, ref FighterState f, MoveDef m, bool flying)
        {
            int start, end;
            Phases(m, out start, out end);
            int t = f.MoveTick;
            // a: 0 during windup, 0..1 across the active window, 1 during recovery.
            float a = t < start ? 0f : (t > end ? 1f : (t - start) / (float)Mathf.Max(1, end - start));
            float wind = t < start ? t / (float)Mathf.Max(1, start) : 1f;
            float recover = t > end ? (t - end) / (float)Mathf.Max(1, m.Total - end) : 0f;
            float ease = Mathf.SmoothStep(0f, 1f, a);

            switch (m.Pose)
            {
                case MovePose.Swing:
                case MovePose.SwingReverse:
                {
                    bool reverse = m.Pose == MovePose.SwingReverse;
                    float from = reverse ? -85f : 115f;
                    float to = reverse ? 105f : -75f;
                    float theta = Mathf.Lerp(from, to, ease) * Mathf.Deg2Rad;
                    float lift = Mathf.Lerp(0.25f, -0.05f, ease);
                    p.UpperArmR = V(Mathf.Sin(theta), lift, Mathf.Cos(theta) + 0.25f);
                    p.ForeArmR = V(Mathf.Sin(theta - 0.35f), lift + 0.05f, Mathf.Cos(theta - 0.35f) + 0.2f);
                    // Staff: one end leads the swing, the staff lying across the arc.
                    p.StaffDir = reverse ? V(Mathf.Cos(theta), 0.2f, -Mathf.Sin(theta)) : V(-Mathf.Cos(theta), 0.2f, Mathf.Sin(theta));
                    p.UpperArmL = V(-0.45f, -0.6f, 0.45f);
                    p.ForeArmL = V(0.1f, -0.2f, 0.9f);
                    p.SpineYaw = Mathf.Lerp(reverse ? -35f : 38f, reverse ? 35f : -38f, ease);
                    p.SpinePitch = 8f;
                    p.ThighL = V(-0.25f, -1f, 0.35f);
                    p.ThighR = V(0.25f, -1f, -0.25f);
                    p.BodyDrop = 0.08f;
                    break;
                }
                case MovePose.Thrust:
                {
                    Vector3 back = V(0.4f, -0.15f, -0.7f);
                    Vector3 forward = V(0.08f, 0.05f, 1f);
                    float k = t < start ? wind : 1f - recover * 0.7f;
                    p.UpperArmR = Vector3.Slerp(back, forward, t < start ? 0f : k);
                    p.ForeArmR = Vector3.Slerp(V(0.3f, 0.1f, 0.4f), V(0.03f, 0.05f, 1f), t < start ? 0f : k);
                    p.StaffDir = Vector3.Slerp(V(0.06f, 1f, 0.12f), V(-0.25f, 0.03f, 1f), t < start ? wind : 1f);
                    p.UpperArmL = V(-0.5f, -0.6f, -0.2f);
                    p.ForeArmL = V(-0.3f, -0.4f, 0.4f);
                    p.SpineYaw = t < start ? 30f : -18f;
                    p.SpinePitch = t < start ? 0f : 14f;
                    p.ThighL = V(-0.2f, -1f, 0.55f);
                    p.CalfL = V(-0.1f, -1f, -0.1f);
                    p.ThighR = V(0.2f, -1f, -0.45f);
                    p.BodyDrop = 0.1f;
                    break;
                }
                case MovePose.JabLeft:
                {
                    Vector3 back = V(-0.35f, -0.4f, 0.5f);
                    Vector3 forward = V(-0.06f, 0.08f, 1f);
                    float k = t < start ? 0f : 1f - recover * 0.8f;
                    p.UpperArmL = Vector3.Slerp(back, forward, k);
                    p.ForeArmL = Vector3.Slerp(V(0.15f, 0.9f, 0.4f), V(-0.02f, 0.08f, 1f), k);
                    p.UpperArmR = V(0.32f, -0.6f, 0.5f);
                    p.ForeArmR = V(-0.12f, 0.9f, 0.32f);
                    p.SpineYaw = t < start ? -10f : 18f;
                    p.SpinePitch = 8f;
                    p.ThighL = V(-0.2f, -1f, 0.4f);
                    p.ThighR = V(0.2f, -1f, -0.3f);
                    p.BodyDrop = 0.08f;
                    break;
                }
                case MovePose.Overhead:
                case MovePose.Slam:
                {
                    Vector3 up = V(0.12f, 1f, -0.25f);
                    Vector3 down = m.Pose == MovePose.Slam ? V(0.05f, -0.7f, 0.75f) : V(0.08f, -0.35f, 1f);
                    Vector3 dir = t < start ? Vector3.Slerp(V(0.3f, -0.6f, 0.4f), up, wind) : Vector3.Slerp(up, down, ease);
                    p.UpperArmR = dir;
                    p.ForeArmR = dir;
                    p.UpperArmL = new Vector3(-dir.x * 0.3f - 0.15f, dir.y, dir.z).normalized;
                    p.ForeArmL = new Vector3(0.25f, dir.y, dir.z).normalized;
                    p.SpinePitch = t < start ? Mathf.Lerp(0f, -14f, wind) : Mathf.Lerp(-14f, m.Pose == MovePose.Slam ? 38f : 26f, ease);
                    p.BodyDrop = t < start ? 0f : Mathf.Lerp(0f, m.Pose == MovePose.Slam ? 0.28f : 0.14f, ease);
                    p.ThighL = V(-0.25f, -1f, 0.45f);
                    p.CalfL = V(-0.12f, -1f, -0.2f);
                    p.ThighR = V(0.25f, -1f, -0.3f);
                    break;
                }
                case MovePose.Uppercut:
                {
                    Vector3 low = V(0.35f, -0.9f, 0.35f);
                    Vector3 high = V(0.1f, 1f, 0.4f);
                    Vector3 dir = t < start ? low : Vector3.Slerp(low, high, ease);
                    p.UpperArmR = dir;
                    p.ForeArmR = dir;
                    p.UpperArmL = V(-0.6f, -0.5f, 0.3f);
                    p.SpinePitch = t < start ? 18f : Mathf.Lerp(18f, -16f, ease);
                    p.BodyDrop = t < start ? 0.2f * wind : Mathf.Lerp(0.2f, -0.05f, ease);
                    p.ThighL = V(-0.2f, -0.8f, 0.6f);
                    p.CalfL = V(-0.1f, -1f, -0.2f);
                    break;
                }
                case MovePose.Spin:
                {
                    p.UpperArmR = V(1f, -0.05f, 0.15f);
                    p.ForeArmR = V(1f, 0f, 0.25f);
                    p.UpperArmL = V(-1f, -0.05f, 0.15f);
                    p.ForeArmL = V(-1f, 0f, 0.25f);
                    p.SpinePitch = 6f;
                    p.BodyDrop = 0.1f;
                    if (m.Total > 120)
                    {
                        _spinYaw = (t * 22f) % 360f;
                    }
                    else if (t >= start && t <= end + 6)
                    {
                        _spinYaw = Mathf.Lerp(0f, 360f, Mathf.Clamp01((t - start) / (float)Mathf.Max(1, end + 6 - start)));
                    }
                    break;
                }
                case MovePose.Cast:
                {
                    Vector3 aim = V(0.1f, flying ? -0.25f : 0.2f, 1f);
                    Vector3 charge = V(0.45f, 0.5f, -0.25f);
                    p.UpperArmR = t < start ? Vector3.Slerp(V(0.3f, -1f, 0.1f), charge, wind) : aim;
                    p.ForeArmR = t < start ? Vector3.Slerp(V(0.1f, -0.5f, 0.8f), charge, wind) : aim;
                    p.UpperArmL = V(-0.35f, -0.4f, 0.65f);
                    p.ForeArmL = V(0.1f, -0.1f, 1f);
                    p.SpinePitch = t < start ? -6f : 8f;
                    p.SpineYaw = t < start ? 15f : -8f;
                    break;
                }
                case MovePose.Channel:
                {
                    float pulse = Mathf.Sin(_time * 10f) * 0.08f;
                    p.UpperArmL = V(-0.75f, 0.7f + pulse, 0.2f);
                    p.ForeArmL = V(-0.5f, 1f, 0.2f);
                    p.UpperArmR = V(0.75f, 0.7f - pulse, 0.2f);
                    p.ForeArmR = V(0.5f, 1f, 0.2f);
                    p.SpinePitch = -10f;
                    p.BodyDrop = 0.04f;
                    break;
                }
                case MovePose.Flurry:
                {
                    HitboxDef finish = null;
                    for (int i = 0; i < m.Hitboxes.Length; i++)
                    {
                        if (m.Hitboxes[i].Group == 1) finish = m.Hitboxes[i];
                    }
                    if (finish != null && t >= finish.Start - 6)
                    {
                        float fk = Mathf.Clamp01((t - (finish.Start - 6)) / 8f);
                        bool launcher = (finish.Flags & HitFlags.Launch) != 0;
                        Vector3 strike = launcher ? V(0.1f, 1f, 0.4f) : V(0.06f, 0.08f, 1f);
                        Vector3 load = launcher ? V(0.35f, -0.9f, 0.35f) : V(0.4f, -0.2f, -0.5f);
                        p.UpperArmR = Vector3.Slerp(load, strike, fk);
                        p.ForeArmR = Vector3.Slerp(load, strike, fk);
                        p.UpperArmL = V(-0.32f, -0.55f, 0.62f);
                        p.ForeArmL = V(0.18f, 0.9f, 0.4f);
                        p.SpinePitch = launcher ? Mathf.Lerp(18f, -16f, fk) : 14f;
                        p.BodyDrop = launcher ? Mathf.Lerp(0.2f, -0.05f, fk) : 0.1f;
                        break;
                    }
                    bool active = t >= start && t <= (finish != null ? finish.Start - 7 : end + 6);
                    float jab = active ? 0.5f + 0.5f * Mathf.Sin(t * 1.25f) : 0f;
                    p.UpperArmR = Vector3.Slerp(V(0.35f, -0.2f, -0.3f), V(0.08f, 0.05f, 1f), jab);
                    p.ForeArmR = Vector3.Slerp(V(0.2f, 0.1f, 0.6f), V(0.02f, 0.05f, 1f), jab);
                    p.UpperArmL = Vector3.Slerp(V(-0.08f, 0.05f, 1f), V(-0.35f, -0.2f, -0.3f), jab);
                    p.ForeArmL = Vector3.Slerp(V(-0.02f, 0.05f, 1f), V(-0.2f, 0.1f, 0.6f), jab);
                    p.SpineYaw = Mathf.Lerp(-12f, 12f, jab);
                    p.SpinePitch = 10f;
                    p.BodyDrop = 0.1f;
                    p.ThighL = V(-0.25f, -1f, 0.4f);
                    p.ThighR = V(0.25f, -1f, -0.3f);
                    break;
                }
                case MovePose.Grab:
                {
                    Vector3 reach = t < start ? V(0.3f, -0.3f, 0.6f) : V(0.15f, 0f, 1f);
                    p.UpperArmR = reach;
                    p.ForeArmR = reach;
                    p.UpperArmL = new Vector3(-reach.x, reach.y, reach.z);
                    p.ForeArmL = new Vector3(-reach.x, reach.y, reach.z);
                    p.SpinePitch = 18f;
                    p.BodyDrop = 0.1f;
                    break;
                }
                case MovePose.Sweep:
                {
                    // Drop low and swing the right leg around in a circle along the floor.
                    CrouchPose(ref p, false);
                    float k = t < start ? wind : 1f - recover;
                    float theta = Mathf.Lerp(80f, -60f, t < start ? 0f : ease) * Mathf.Deg2Rad;
                    p.ThighR = Vector3.Slerp(p.ThighR, V(Mathf.Sin(theta), -0.25f, Mathf.Cos(theta)), k);
                    p.CalfR = Vector3.Slerp(p.CalfR, V(Mathf.Sin(theta), -0.2f, Mathf.Cos(theta)), k);
                    p.UpperArmL = V(-0.5f, -1f, 0.1f);
                    p.ForeArmL = V(-0.2f, -1f, 0f);
                    p.SpineYaw = Mathf.Lerp(25f, -25f, ease);
                    p.SpinePitch = 22f;
                    break;
                }
                case MovePose.Aim:
                {
                    // Launcher on the shoulder. Aims down for the rocket blast, up for missile rain.
                    bool down = false;
                    bool up = false;
                    for (int i = 0; i < m.Spawns.Length; i++)
                    {
                        ProjectileDef pd = GameContent.Projectiles[m.Spawns[i].ProjectileId];
                        if (pd.Downward) down = true;
                        if (pd.FromSky) up = true;
                    }
                    float aimPitch = f.HasAim ? Mathf.Clamp(f.AimPitch * InputFrame.AimPitchStep, -85f, 70f) * Mathf.Deg2Rad : 0f;
                    Vector3 aim = down ? V(0.1f, -1f, 0.25f) : up ? V(0.1f, 1f, 0.35f) : V(0.02f, Mathf.Sin(aimPitch) + 0.03f, Mathf.Cos(aimPitch));
                    p.LauncherDir = aim;
                    float recoil = t >= start && t <= start + 5 ? 1f - (t - start) / 5f : 0f;
                    p.UpperArmR = Vector3.Slerp(V(0.45f, -0.35f, 0.75f), down ? V(0.3f, -0.7f, 0.5f) : V(0.4f, -0.2f, 0.85f), Mathf.Min(1f, wind * 1.5f));
                    p.ForeArmR = Vector3.Slerp(V(0.05f, 0.05f, 1f), aim, Mathf.Min(1f, wind * 1.5f));
                    p.UpperArmL = V(-0.15f, -0.45f, 0.85f);
                    p.ForeArmL = Vector3.Slerp(V(0.45f, 0.05f, 0.9f), aim, 0.7f);
                    if (_v.LauncherHolder != null && !down && !up)
                    {
                        // Hip-fired: hands stay on the grips while the launcher follows the crosshair and kicks on firing.
                        HipCarry(ref p, aim.y);
                        p.LauncherDir = aim + V(0f, recoil * 0.25f, 0f);
                        p.SpinePitch -= recoil * 6f;
                    }
                    p.SpinePitch = (up ? -14f : down ? 18f : 4f) - recoil * 10f;
                    p.SpineYaw = 12f;
                    p.BodyDrop = 0.06f + recoil * 0.05f;
                    p.ThighL = V(-0.25f, -1f, 0.35f);
                    p.ThighR = V(0.25f, -1f, -0.3f);
                    break;
                }
                case MovePose.Lunge:
                {
                    // Low slide, weapon tip leading toward the opponent.
                    float k = t < start ? wind : 1f - recover;
                    p.ThighL = Vector3.Slerp(V(-0.15f, -1f, 0.1f), V(-0.2f, -0.35f, 0.95f), k);
                    p.CalfL = Vector3.Slerp(V(-0.06f, -1f, -0.05f), V(-0.1f, -1f, -0.2f), k);
                    p.ThighR = Vector3.Slerp(V(0.15f, -1f, -0.05f), V(0.2f, -0.6f, -0.8f), k);
                    p.CalfR = Vector3.Slerp(V(0.06f, -1f, -0.1f), V(0.1f, -0.1f, -1f), k);
                    p.UpperArmR = Vector3.Slerp(V(0.3f, -0.8f, 0.4f), V(0.15f, -0.15f, 1f), k);
                    p.ForeArmR = Vector3.Slerp(V(0.05f, 0.6f, 0.8f), V(0.02f, 0.05f, 1f), k);
                    p.UpperArmL = Vector3.Slerp(V(-0.3f, -0.9f, 0.25f), V(-0.2f, -0.3f, 0.95f), k);
                    p.ForeArmL = Vector3.Slerp(V(0.15f, -0.4f, 0.9f), V(0.35f, 0f, 0.95f), k);
                    p.StaffDir = Vector3.Slerp(V(0.06f, 1f, 0.12f), V(-0.22f, -0.04f, 1f), k);
                    p.SpinePitch = 26f * k;
                    p.BodyDrop = 0.45f * _restPelvisHeight * k;
                    break;
                }
                case MovePose.Throw:
                {
                    // Overhand throw: weight back, arm cocked behind the head, then a whip forward at the release.
                    float k = t < start ? 0f : Mathf.Clamp01((t - start + 1) / 4f);
                    float settle = t > end ? recover : 0f;
                    p.UpperArmR = Vector3.Slerp(Vector3.Slerp(V(0.45f, 0.55f, -0.55f), V(0.25f, 0.2f, 1f), k), V(0.3f, -0.8f, 0.4f), settle);
                    p.ForeArmR = Vector3.Slerp(Vector3.Slerp(V(0.2f, 0.95f, -0.25f), V(0.05f, -0.15f, 1f), k), V(0.1f, -0.5f, 0.8f), settle);
                    p.UpperArmL = V(-0.35f, -0.25f, 0.9f);
                    p.ForeArmL = V(-0.1f, -0.2f, 1f);
                    p.SpineYaw = Mathf.Lerp(t < start ? 35f * wind : 35f, -25f, k) * (1f - settle);
                    p.SpinePitch = Mathf.Lerp(-10f * wind, 18f, k) * (1f - settle);
                    p.ThighL = V(-0.2f, -1f, 0.45f);
                    p.ThighR = V(0.22f, -1f, -0.35f);
                    p.BodyDrop = 0.06f;
                    break;
                }
                case MovePose.Strikes:
                {
                    // Three hard thrusts, staff tip forward, each timed to its own hit window.
                    float ext = 0f;
                    for (int i = 0; i < m.Hitboxes.Length; i++)
                    {
                        int s = m.Hitboxes[i].Start, e = m.Hitboxes[i].End;
                        float v = t < s - 4 ? 0f : t < s ? (t - (s - 4)) / 4f : t <= e + 1 ? 1f : Mathf.Max(0f, 1f - (t - e - 1) / 3f);
                        ext = Mathf.Max(ext, v);
                    }
                    float settle = t > end ? 1f - recover : 1f;
                    ext *= settle;
                    p.UpperArmR = Vector3.Slerp(V(0.45f, -0.4f, -0.2f), V(0.12f, -0.1f, 1f), ext);
                    p.ForeArmR = Vector3.Slerp(V(0.1f, 0.1f, 1f), V(0.02f, 0.03f, 1f), ext);
                    p.StaffDir = V(-0.25f, 0.03f, 1f);
                    p.UpperArmL = V(-0.35f, -0.5f, 0.7f);
                    p.ForeArmL = V(0.4f, 0.05f, 0.9f);
                    p.SpineYaw = Mathf.Lerp(28f, -18f, ext) * settle;
                    p.SpinePitch = 10f * settle;
                    p.ThighL = V(-0.2f, -1f, 0.4f);
                    p.ThighR = V(0.22f, -1f, -0.3f);
                    p.BodyDrop = 0.1f * _restPelvisHeight * settle;
                    break;
                }
                case MovePose.GuardSlide:
                {
                    // Slips backward low, staff held sideways in front like the guard.
                    float k = Mathf.Min(1f, t / 4f) * Mathf.Clamp01((m.Total - t) / 6f);
                    p.UpperArmR = V(0.3f, -0.45f, 0.85f);
                    p.ForeArmR = V(-0.35f, 0.25f, 0.9f);
                    p.StaffDir = V(-1f, 0.12f, 0.05f);
                    p.UpperArmL = V(-0.45f, -0.4f, 0.8f);
                    p.ForeArmL = V(0.1f, 0.25f, 0.95f);
                    p.ThighL = Vector3.Slerp(V(-0.15f, -1f, 0.1f), V(-0.2f, -0.55f, 0.8f), k);
                    p.CalfL = Vector3.Slerp(V(-0.06f, -1f, -0.05f), V(-0.1f, -1f, -0.25f), k);
                    p.ThighR = Vector3.Slerp(V(0.15f, -1f, -0.05f), V(0.2f, -0.7f, 0.4f), k);
                    p.CalfR = Vector3.Slerp(V(0.06f, -1f, -0.1f), V(0.1f, -1f, -0.5f), k);
                    p.SpinePitch = 18f * k;
                    p.BodyDrop = 0.4f * _restPelvisHeight * k;
                    break;
                }
                case MovePose.Shoot:
                {
                    float recoil = t >= start && t <= start + 3 ? 1f - (t - start) / 3f : 0f;
                    float pitch = f.HasAim ? Mathf.Clamp(f.AimPitch * InputFrame.AimPitchStep, -60f, 60f) * Mathf.Deg2Rad : 0f;
                    Vector3 aim = V(0.05f, Mathf.Sin(pitch) + 0.08f + recoil * 0.35f, Mathf.Cos(pitch));
                    float k = Mathf.Min(1f, wind * 2f);
                    p.UpperArmR = Vector3.Slerp(V(0.25f, -0.9f, 0.3f), aim, k);
                    p.ForeArmR = Vector3.Slerp(V(0.05f, -0.3f, 1f), aim, k);
                    p.UpperArmL = V(-0.3f, -1f, 0.1f);
                    p.ForeArmL = V(-0.1f, -0.7f, 0.5f);
                    p.SpineYaw = 14f;
                    p.SpinePitch = 2f - recoil * 4f;
                    break;
                }
                case MovePose.Roar:
                {
                    float k = t < start ? wind : 1f - recover * 0.6f;
                    p.UpperArmL = Vector3.Slerp(V(-0.4f, -0.8f, 0.3f), V(-1f, 0.45f, 0.15f), k);
                    p.ForeArmL = Vector3.Slerp(V(-0.2f, -0.6f, 0.6f), V(-0.8f, 0.8f, 0.1f), k);
                    p.UpperArmR = Vector3.Slerp(V(0.4f, -0.8f, 0.3f), V(1f, 0.45f, 0.15f), k);
                    p.ForeArmR = Vector3.Slerp(V(0.2f, -0.6f, 0.6f), V(0.8f, 0.8f, 0.1f), k);
                    p.SpinePitch = Mathf.Lerp(10f, -24f, k);
                    p.BodyDrop = Mathf.Lerp(0.15f, -0.05f, k);
                    break;
                }
                case MovePose.Dodge:
                {
                    p.ThighL = V(-0.2f, -0.6f, 0.7f);
                    p.CalfL = V(-0.1f, -1f, -0.4f);
                    p.ThighR = V(0.2f, -0.6f, 0.6f);
                    p.CalfR = V(0.1f, -1f, -0.45f);
                    p.UpperArmL = V(-0.4f, -0.6f, 0.5f);
                    p.UpperArmR = V(0.4f, -0.6f, 0.5f);
                    p.SpinePitch = 32f;
                    p.BodyDrop = 0.32f;
                    int relative = FixMath.AngleDelta(f.Facing, f.MoveDirection);
                    float side = relative / (float)FixMath.QuarterTurn;
                    p.BodyRoll = Mathf.Clamp(side, -1f, 1f) * -18f;
                    break;
                }
            }
        }

        private void Apply(ref Pose p)
        {
            Transform body = _v.Body;
            body.localRotation = Quaternion.Euler(p.BodyPitch, p.BodyYaw, p.BodyRoll);
            float lift = p.BodyPitch < -60f ? 0.22f : 0f;
            body.localPosition = new Vector3(0f, -p.BodyDrop + lift, 0f);

            PoseRig rig = _v.Rig;
            if (rig == null) return;
            rig.ResetToRest();

            // Pelvis: rotate from its rest frame to the wanted frame (up = pelvis to chest, right = across the hips).
            if (rig.Pelvis != null)
            {
                Quaternion rest = Frame(_restPelvisUp, _restPelvisRight);
                Quaternion want = Frame(p.PelvisUp, p.PelvisRight);
                Quaternion delta = want * Quaternion.Inverse(rest);
                rig.Pelvis.rotation = body.rotation * delta * Quaternion.Inverse(body.rotation) * rig.Pelvis.rotation;
            }

            // Spine and neck: aim by direction, then twist around the spine for the shoulder turn.
            Transform spine = rig.SpineLow != null ? rig.SpineLow : rig.Pelvis;
            if (spine != null && rig.Chest != null && spine != rig.Chest)
            {
                PoseRig.Aim(spine, rig.Chest, body.TransformDirection(p.SpineDir));
                Twist(spine, rig.Chest, p.SpineYaw * 0.6f);
            }
            if (rig.Chest != null && rig.Head != null)
            {
                PoseRig.Aim(rig.Chest, rig.Head, body.TransformDirection(p.NeckDir));
                Twist(rig.Chest, rig.Head, p.SpineYaw * 0.4f);
            }

            PoseRig.Aim(rig.UpperArmL, rig.ForeArmL, body.TransformDirection(p.UpperArmL));
            PoseRig.Aim(rig.ForeArmL, rig.HandL, body.TransformDirection(p.ForeArmL));
            PoseRig.Aim(rig.UpperArmR, rig.ForeArmR, body.TransformDirection(p.UpperArmR));
            PoseRig.Aim(rig.ForeArmR, rig.HandR, body.TransformDirection(p.ForeArmR));
            PoseRig.Aim(rig.ThighL, rig.CalfL, body.TransformDirection(p.ThighL));
            PoseRig.Aim(rig.CalfL, rig.FootL, body.TransformDirection(p.CalfL));
            PoseRig.Aim(rig.ThighR, rig.CalfR, body.TransformDirection(p.ThighR));
            PoseRig.Aim(rig.CalfR, rig.FootR, body.TransformDirection(p.CalfR));

            if (_v.LauncherHolder != null && rig.Chest != null)
            {
                Vector3 dir = p.LauncherDir.sqrMagnitude > 1e-6f ? p.LauncherDir.normalized : Vector3.forward;
                // Carried low at the right hip (the holder is behind the launcher's middle, near the rear grip).
                Vector3 shoulderPos = rig.Chest.position + body.TransformDirection(new Vector3(0.2f, -0.2f, 0.05f));
                Quaternion shoulderRot = Quaternion.LookRotation(body.TransformDirection(dir), body.up);
                Quaternion backRot = Quaternion.LookRotation(body.TransformDirection(new Vector3(-0.55f, 1f, 0f).normalized), -body.forward);
                // On the back, the launcher's middle sits between the shoulder blades (the holder is behind its center).
                Vector3 backPos = rig.Chest.position + body.TransformDirection(new Vector3(0f, -0.02f, -0.24f)) - backRot * new Vector3(0f, 0f, FighterRigBuilder.LauncherForwardOffset);
                float k = Mathf.SmoothStep(0f, 1f, p.LauncherBack);
                Vector3 pos = Vector3.Lerp(shoulderPos, backPos, k);
                Quaternion rot = Quaternion.Slerp(shoulderRot, backRot, k);
                float g = Mathf.SmoothStep(0f, 1f, p.LauncherGuard);
                if (g > 0.001f)
                {
                    // Guard: the launcher's middle sits in front of the chest, the tube level and pointing to the left.
                    Quaternion guardRot = Quaternion.LookRotation(body.TransformDirection(new Vector3(-1f, 0.1f, 0f).normalized), body.forward);
                    Vector3 guardPos = rig.Chest.position + body.TransformDirection(new Vector3(0f, -0.1f, 0.42f)) - guardRot * new Vector3(0f, 0f, FighterRigBuilder.LauncherForwardOffset);
                    pos = Vector3.Lerp(pos, guardPos, g);
                    rot = Quaternion.Slerp(rot, guardRot, g);
                }
                _v.LauncherHolder.position = pos;
                _v.LauncherHolder.rotation = rot;
            }
            if (_v.PistolHolder != null && rig.HandR != null && rig.ForeArmR != null)
            {
                Vector3 along = (rig.HandR.position - rig.ForeArmR.position).normalized;
                Vector3 up = Vector3.ProjectOnPlane(body.up, along);
                if (up.sqrMagnitude < 1e-4f) up = body.forward;
                _v.PistolHolder.position = rig.HandR.position + along * 0.04f;
                _v.PistolHolder.rotation = Quaternion.LookRotation(along, up.normalized);
            }

            if (_v.WeaponHolder != null && p.StaffDir.sqrMagnitude > 1e-6f)
            {
                _v.WeaponHolder.rotation = Quaternion.FromToRotation(Vector3.up, body.TransformDirection(p.StaffDir.normalized));
            }
        }

        private static Quaternion Frame(Vector3 up, Vector3 right)
        {
            Vector3 forward = Vector3.Cross(right, up);
            if (forward.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private static void Twist(Transform bone, Transform child, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.01f) return;
            Vector3 axis = child.position - bone.position;
            if (axis.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
        }
    }
}
