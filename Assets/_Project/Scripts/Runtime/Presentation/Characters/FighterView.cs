using HEXBOUND.Simulation;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    /// <summary>Shows one fighter: position, facing, animation and buff visuals, all read from the simulation state.</summary>
    public sealed class FighterView
    {
        public readonly int Index;
        public readonly CharacterDef Character;
        public readonly FighterVisual Visual;
        private readonly FighterAnimator _animator;

        private Vector3 _prevPos;
        private Vector3 _currPos;
        private float _prevYaw;
        private float _currYaw;
        private int _lastTick = -1;
        private float _punch;
        private int _lastStep;

        /// <summary>True on the frame a foot lands while running (for footstep sounds).</summary>
        public bool StepThisFrame { get; private set; }

        /// <summary>True on the frame the beast's fist hits its chest (for the thump sound).</summary>
        public bool ChestBeatThisFrame { get; private set; }

        public FighterView(CharacterDef character, Transform parent, int index)
        {
            Index = index;
            Character = character;
            Visual = FighterRigBuilder.Build(character, parent, index);
            _animator = new FighterAnimator(Visual, character.Stance == FighterStance.Beast);
        }

        public Vector3 Position
        {
            get { return Visual.Root.position; }
        }

        /// <summary>Point to aim cameras and effects at (upper chest).</summary>
        public Vector3 Chest
        {
            get { return Visual.Root.position + Vector3.up * (Character.Height.ToFloat() * 0.65f); }
        }

        public void Punch(float amount)
        {
            _punch = Mathf.Max(_punch, amount);
        }

        public void Sync(GameState state, ref FighterState f, float alpha, float deltaTime)
        {
            Vector3 pos = ToVector(f.Position);
            float yaw = f.Facing * 360f / FixMath.FullTurn;
            if (_lastTick < 0 || state.Tick < _lastTick || state.Tick - _lastTick > 3 || (pos - _currPos).sqrMagnitude > 4f)
            {
                _prevPos = pos;
                _currPos = pos;
                _prevYaw = yaw;
                _currYaw = yaw;
            }
            else if (state.Tick != _lastTick)
            {
                _prevPos = _currPos;
                _prevYaw = _currYaw;
                _currPos = pos;
                _currYaw = yaw;
            }
            _lastTick = state.Tick;

            Vector3 renderPos = Vector3.Lerp(_prevPos, _currPos, alpha);
            float renderYaw = Mathf.LerpAngle(_prevYaw, _currYaw, alpha);
            if (f.HitStop > 0 && (f.Action == FighterAction.HitStun || f.Action == FighterAction.Launched || f.Action == FighterAction.BlockStun))
            {
                renderPos += Visual.Root.right * Mathf.Sin(Time.unscaledTime * 95f) * 0.045f;
            }
            Visual.Root.position = renderPos;
            Visual.Root.rotation = Quaternion.Euler(0f, renderYaw, 0f);

            Vector3 v = ToVector(f.Velocity);
            v.y = 0f;
            _animator.Animate(ref f, Character, deltaTime, v.magnitude * GameConfig.TickRate, false, state.Phase == MatchPhase.Intro);
            ChestBeatThisFrame = _animator.ChestBeatThisFrame;

            int step = Mathf.FloorToInt(_animator.RunPhase / Mathf.PI);
            StepThisFrame = step != _lastStep && f.Action == FighterAction.Move && f.Grounded;
            _lastStep = step;

            _punch = Mathf.MoveTowards(_punch, 0f, deltaTime * 4f);
            Visual.Body.localScale = new Vector3(1f + _punch * 0.08f, 1f - _punch * 0.06f, 1f + _punch * 0.08f);

            if (Visual.ExhaustFire != null && Visual.ExhaustFire.activeSelf != (f.Buff == BuffId.Awakened)) Visual.ExhaustFire.SetActive(f.Buff == BuffId.Awakened);
            if (Visual.PrimaryProp != null) Visual.PrimaryProp.SetActive(f.WeaponMode == 0);
            if (Visual.SecondaryProp != null) Visual.SecondaryProp.SetActive(f.WeaponMode == 1);

            // Swipe trails while a melee attack is in its active frames.
            bool swinging = false;
            if (f.Action == FighterAction.Attack)
            {
                MoveDef m = GameContent.GetMove(f.MoveId);
                if (m != null && m.Hitboxes.Length > 0 && m.Spawns.Length == 0 && m.Pose != MovePose.Dodge && m.Pose != MovePose.Grab)
                {
                    int start, end;
                    FighterAnimator.Phases(m, out start, out end);
                    swinging = f.MoveTick >= start - 3 && f.MoveTick <= end + 2;
                }
            }
            if (Visual.TrailR != null) Visual.TrailR.emitting = swinging;
            if (Visual.TrailL != null) Visual.TrailL.emitting = swinging && (Character.UltimateBuff != BuffId.TwinBlades || f.Buff == BuffId.TwinBlades);

            bool buffed = f.Buff != BuffId.None;
            if (Visual.OffhandBlade != null) Visual.OffhandBlade.SetActive(f.Buff == BuffId.TwinBlades);
            if (Visual.BuffAura != null)
            {
                Visual.BuffAura.SetActive(buffed);
                if (buffed)
                {
                    Visual.BuffAura.transform.localPosition = new Vector3(0f, f.Buff == BuffId.Levitate ? -renderPos.y + 0.02f : 0f, 0f);
                    Visual.BuffAura.transform.Rotate(0f, deltaTime * 90f, 0f, Space.Self);
                    Visual.AuraLight.intensity = 2.5f + Mathf.Sin(Time.unscaledTime * 6f) * 0.8f;
                }
            }

            bool hidden = f.Action == FighterAction.RingOut;
            if (Visual.Renderers != null)
            {
                for (int i = 0; i < Visual.Renderers.Length; i++)
                {
                    if (Visual.Renderers[i] != null && Visual.Renderers[i].enabled == hidden) Visual.Renderers[i].enabled = !hidden;
                }
            }
        }

        public void Destroy()
        {
            _animator.Dispose();
            if (Visual.Root != null) Object.Destroy(Visual.Root.gameObject);
        }

        public static Vector3 ToVector(FixVec3 v)
        {
            return new Vector3(v.X.ToFloat(), v.Y.ToFloat(), v.Z.ToFloat());
        }
    }
}
