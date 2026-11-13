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

        public FighterAnimator(FighterVisual visual)
        {
            _v = visual;
            CaptureRest();
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

        public void Animate(ref FighterState f, CharacterDef c, float deltaTime, float horizontalSpeed, bool celebrating = false)
        {
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
            _spinYaw = 0f;

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
            return p;
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
