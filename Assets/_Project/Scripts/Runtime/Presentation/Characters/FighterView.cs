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

        /// <summary>True while this fighter stands close enough to its dropped weapon to pick it up with G.</summary>
        public bool CanPickUp { get; private set; }

        // Thrown (spinning) and dropped (lying, glowing) copies of each weapon.
        private readonly GameObject[] _flying = new GameObject[2];
        private readonly GameObject[] _dropped = new GameObject[2];
        private readonly Material[] _dropGlow = new Material[2];
        private bool _armedShown = true;

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
            if (Visual.PrimaryProp != null && Visual.HeldWeapon[0] != Visual.PrimaryProp) Visual.PrimaryProp.SetActive(f.WeaponMode == 0);
            if (Visual.SecondaryProp != null && Visual.HeldWeapon[1] != Visual.SecondaryProp) Visual.SecondaryProp.SetActive(f.WeaponMode == 1);
            SyncWeapons(state, ref f, deltaTime);

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

        /// <summary>
        /// Weapons follow the state: in hand while held (Vex's launcher stays slung on her back in pistol mode,
        /// the pistol only shows in pistol mode), spinning through the air after a throw, lying on the floor where it landed.
        /// </summary>
        private void SyncWeapons(GameState state, ref FighterState f, float deltaTime)
        {
            if (!Character.CanThrowWeapon) return;
            bool gone = f.Action == FighterAction.RingOut;
            for (int w = 0; w < 2; w++)
            {
                bool held = (f.WeaponsHeld & (1 << w)) != 0;
                GameObject inHand = Visual.HeldWeapon[w];
                if (inHand != null)
                {
                    bool show = held && !gone;
                    if (Character.SecondaryWeapon != null && w == 1) show &= f.WeaponMode == 1;
                    if (Character.SecondaryWeapon != null && w == 0 && Visual.LauncherHolder == null) show &= f.WeaponMode == 0;
                    if (inHand.activeSelf != show) inHand.SetActive(show);
                }
            }
            if (Visual.WeaponSkin != null)
            {
                bool armed = (f.WeaponsHeld & 1) != 0;
                if (armed != _armedShown)
                {
                    _armedShown = armed;
                    Visual.WeaponSkin.sharedMaterials = armed ? Visual.ArmedMaterials : Visual.UnarmedMaterials;
                }
            }

            CanPickUp = false;
            for (int w = 0; w < 2; w++)
            {
                GameObject template = Visual.WeaponTemplate[w];
                if (template == null) continue;
                int bit = 1 << w;

                // In flight: follow the projectile that carries it, spinning end over end.
                bool flying = false;
                if ((f.WeaponsThrown & bit) != 0)
                {
                    for (int k = 0; k < state.Projectiles.Length; k++)
                    {
                        ProjectileState p = state.Projectiles[k];
                        if (!p.Active || p.Owner != Index || p.Carries != w + 1) continue;
                        flying = true;
                        Vector3 target = ToVector(p.Position);
                        if (_flying[w] == null)
                        {
                            _flying[w] = Spawn(template, "Thrown Weapon");
                            _flying[w].transform.position = target;
                        }
                        Transform t = _flying[w].transform;
                        t.position = Vector3.Lerp(t.position, target, 0.6f);
                        Vector3 vel = ToVector(p.Velocity);
                        vel.y = 0f;
                        Vector3 side = vel.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, vel.normalized) : Vector3.right;
                        t.Rotate(side, 900f * deltaTime, Space.World);
                    }
                }
                if (!flying && _flying[w] != null)
                {
                    Object.Destroy(_flying[w]);
                    _flying[w] = null;
                }

                // On the floor: lying flat with a slow pulsing glow ring; the owner can pick it up when close.
                bool dropped = (f.WeaponsDropped & bit) != 0;
                if (dropped)
                {
                    Vector3 at = ToVector(w == 0 ? f.DropPos0 : f.DropPos1);
                    if (_dropped[w] == null) _dropped[w] = DroppedVisual(template, w);
                    _dropped[w].transform.position = at;
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f + w);
                    Color c = new Color(Character.ColorR / 255f, Character.ColorG / 255f, Character.ColorB / 255f) * 2.2f * pulse;
                    c.a = 1f;
                    MaterialFactory.SetColor(_dropGlow[w], c);
                    Vector3 d = ToVector(f.Position) - at;
                    d.y = 0f;
                    if (d.magnitude <= FighterLogic.PickUpRange.ToFloat() && !gone) CanPickUp = true;
                }
                else if (_dropped[w] != null)
                {
                    Object.Destroy(_dropped[w]);
                    _dropped[w] = null;
                }
            }
        }

        private static GameObject Spawn(GameObject template, string name)
        {
            GameObject go = Object.Instantiate(template);
            go.name = name;
            go.SetActive(true);
            return go;
        }

        /// <summary>The weapon lying on its side just above the floor, with a glowing ring under it.</summary>
        private GameObject DroppedVisual(GameObject template, int weapon)
        {
            var root = new GameObject("Dropped Weapon");
            GameObject model = Object.Instantiate(template, root.transform, false);
            model.SetActive(true);
            model.transform.localPosition = Vector3.zero;
            // Lay the longest side flat.
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y >= Mathf.Max(b.size.x, b.size.z)) model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * model.transform.localRotation;
            any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any) model.transform.position += new Vector3(-b.center.x, 0.04f - b.min.y, -b.center.z);
            model.transform.Rotate(0f, 37f * (weapon + 1), 0f, Space.World);
            _dropGlow[weapon] = MaterialFactory.Additive(Color.white);
            _dropGlow[weapon].mainTexture = MaterialFactory.SoftDot;
            _dropGlow[weapon].SetTexture("_BaseMap", MaterialFactory.SoftDot);
            GameObject ring = MaterialFactory.Primitive(PrimitiveType.Quad, root.transform, _dropGlow[weapon], "Glow");
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 1.6f;
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        public void Destroy()
        {
            for (int w = 0; w < 2; w++)
            {
                if (_flying[w] != null) Object.Destroy(_flying[w]);
                if (_dropped[w] != null) Object.Destroy(_dropped[w]);
            }
            _animator.Dispose();
            if (Visual.Root != null) Object.Destroy(Visual.Root.gameObject);
        }

        public static Vector3 ToVector(FixVec3 v)
        {
            return new Vector3(v.X.ToFloat(), v.Y.ToFloat(), v.Z.ToFloat());
        }
    }
}
