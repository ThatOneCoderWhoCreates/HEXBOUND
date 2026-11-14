using System;
using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Turns simulation events into visual effects and sounds, and draws projectiles.
    /// Rollback can replay the same tick more than once, so every event is played at most once (by key).
    /// Projectiles are drawn straight from the game state, so they are always correct after a rollback.
    /// </summary>
    public sealed class EffectsPlayer
    {
        private enum FxKind
        {
            Flash,
            Ring,
            Spark,
        }

        private sealed class Fx
        {
            public GameObject Go;
            public Material Material;
            public FxKind Kind;
            public float Age;
            public float Life;
            public Vector3 Velocity;
            public Color Color;
            public float StartScale;
            public float EndScale;
            public bool Active;
        }

        private sealed class ProjectileView
        {
            public GameObject Go;
            public TrailRenderer Trail;
            public int SerialId;
            public bool Seen;
            /// <summary>Points along its flight direction and does not pulse (missiles, bullets).</summary>
            public bool Oriented;
        }

        /// <summary>Raised for floating text (damage numbers, PARRY, GUARD BREAK...). Position is in world space.</summary>
        public event Action<Vector3, string, Color, float> Popup;

        /// <summary>Raised when the camera should shake (strength 0-1).</summary>
        public event Action<float> Shake;

        private readonly Transform _root;
        private readonly SfxPlayer _sfx;
        private readonly List<Fx> _pool = new List<Fx>();
        private readonly HashSet<long> _played = new HashSet<long>();
        private readonly Queue<KeyValuePair<int, long>> _playedOrder = new Queue<KeyValuePair<int, long>>();
        private readonly Dictionary<int, ProjectileView> _projectiles = new Dictionary<int, ProjectileView>();
        private readonly List<int> _removeList = new List<int>();
        private Mesh _sphere;
        private Mesh _cube;
        private Mesh _cylinder;
        private FighterView[] _fighters = new FighterView[2];
        private int _localIndex;

        public EffectsPlayer(Transform parent, SfxPlayer sfx)
        {
            _root = new GameObject("Effects").transform;
            _root.SetParent(parent, false);
            _sfx = sfx;
            _sphere = PrimitiveMesh(PrimitiveType.Sphere);
            _cube = PrimitiveMesh(PrimitiveType.Cube);
            _cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        }

        public void SetFighters(FighterView a, FighterView b, int localIndex)
        {
            _fighters[0] = a;
            _fighters[1] = b;
            _localIndex = localIndex;
        }

        public void Clear()
        {
            foreach (Fx fx in _pool)
            {
                fx.Active = false;
                fx.Go.SetActive(false);
            }
            foreach (ProjectileView pv in _projectiles.Values)
            {
                if (pv.Go != null) UnityEngine.Object.Destroy(pv.Go);
            }
            _projectiles.Clear();
            _played.Clear();
            _playedOrder.Clear();
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(go);
            return mesh;
        }

        public void HandleEvents(EventBuffer events, int currentTick)
        {
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                long key = e.Key;
                if (_played.Contains(key)) continue;
                _played.Add(key);
                _playedOrder.Enqueue(new KeyValuePair<int, long>(e.Tick, key));
                Play(e);
            }
            while (_playedOrder.Count > 0 && _playedOrder.Peek().Key < currentTick - 240)
            {
                _played.Remove(_playedOrder.Dequeue().Value);
            }
        }

        private Color FighterColor(int index, float intensity)
        {
            if (index < 0 || index > 1 || _fighters[index] == null) return new Color(intensity, intensity, intensity);
            CharacterDef c = _fighters[index].Character;
            return new Color(c.ColorR / 255f * intensity, c.ColorG / 255f * intensity, c.ColorB / 255f * intensity, 1f);
        }

        private void Play(SimEvent e)
        {
            Vector3 pos = FighterView.ToVector(e.Position);
            switch (e.Type)
            {
                case SimEventType.MoveStart:
                {
                    MoveDef m = GameContent.GetMove(e.Param);
                    if (m == null) break;
                    if (m.Pose == MovePose.Cast || m.Pose == MovePose.Channel) _sfx.Play(Sfx.Cast, 0.6f);
                    else if (m.Pose == MovePose.Overhead || m.Pose == MovePose.Slam || m.Pose == MovePose.Spin) _sfx.Play(Sfx.SwingHeavy, 0.8f);
                    else _sfx.Play(Sfx.SwingLight, 0.7f);
                    if (m.Slot == CooldownSlot.Ultimate) Burst(pos + Vector3.up, FighterColor(e.Fighter, 4f), 18, 6f, 0.5f);
                    break;
                }
                case SimEventType.Hit:
                case SimEventType.GrabHit:
                {
                    bool heavy = e.Param >= 70;
                    Color c = FighterColor(e.Fighter == 0 ? 1 : 0, 5f);
                    Flash(pos, new Color(4f, 3.5f, 2.5f), 0.12f, 0.3f, heavy ? 1.8f : 1.1f);
                    Burst(pos, c, heavy ? 16 : 9, heavy ? 9f : 6f, 0.35f);
                    Burst(pos, new Color(5f, 4.5f, 3.5f), heavy ? 10 : 5, heavy ? 14f : 10f, 0.18f);
                    if (heavy)
                    {
                        // Shockwave ring around the impact for big hits.
                        Ring(pos, new Color(4f, 3f, 2f), 0.22f, 0.2f, 2.6f);
                        RaiseShake(Mathf.Clamp(e.Param / 200f, 0.25f, 0.8f));
                    }
                    _sfx.Play(heavy ? Sfx.HitHeavy : Sfx.Hit, 1f);
                    if (e.Type == SimEventType.GrabHit) _sfx.Play(Sfx.BodySlam, 0.9f);
                    RaisePopup(pos + Vector3.up * 0.4f, e.Param.ToString(), e.Type == SimEventType.GrabHit ? new Color(1f, 0.6f, 0.2f) : Color.white, heavy ? 1.3f : 1f);
                    if (e.Type == SimEventType.GrabHit) RaisePopup(pos + Vector3.up * 0.9f, "THROW", new Color(1f, 0.6f, 0.2f), 1.1f);
                    if (e.Fighter >= 0 && _fighters[e.Fighter] != null) _fighters[e.Fighter].Punch(heavy ? 1f : 0.6f);
                    RaiseShake(heavy ? 0.5f : 0.22f);
                    break;
                }
                case SimEventType.Blocked:
                    Flash(pos, new Color(0.6f, 1.6f, 3f), 0.1f, 0.15f, 1.2f);
                    _sfx.Play(Sfx.Block, 0.8f);
                    if (e.Param > 0) RaisePopup(pos + Vector3.up * 0.4f, e.Param.ToString(), new Color(0.6f, 0.75f, 1f), 0.8f);
                    break;
                case SimEventType.Parried:
                    Flash(pos, new Color(5f, 5f, 5f), 0.18f, 0.2f, 2.2f);
                    Ring(pos, new Color(3f, 3f, 4f), 0.3f, 0.2f, 3f);
                    _sfx.Play(Sfx.Parry, 1f, 0f);
                    RaisePopup(pos + Vector3.up * 0.8f, "PARRY!", new Color(1f, 1f, 0.6f), 1.5f);
                    RaiseShake(0.35f);
                    break;
                case SimEventType.GuardBreak:
                    Burst(pos, new Color(5f, 2.2f, 0.5f), 20, 8f, 0.5f);
                    _sfx.Play(Sfx.GuardBreak, 1f);
                    RaisePopup(pos + Vector3.up * 0.8f, "GUARD BREAK", new Color(1f, 0.55f, 0.2f), 1.4f);
                    RaiseShake(0.5f);
                    break;
                case SimEventType.ProjectileHit:
                    Flash(pos, new Color(4f, 3f, 5f), 0.14f, 0.2f, 1.4f);
                    _sfx.Play(Sfx.ProjectileHit, 0.7f);
                    break;
                case SimEventType.ProjectileClash:
                    Burst(pos, new Color(5f, 5f, 5f), 14, 7f, 0.4f);
                    _sfx.Play(Sfx.Clash, 0.9f);
                    break;
                case SimEventType.ProjectileExpire:
                    Flash(pos, new Color(2f, 1.5f, 3f), 0.1f, 0.1f, 0.6f);
                    break;
                case SimEventType.Explosion:
                {
                    ProjectileDef def = e.Param >= 0 && e.Param < GameContent.Projectiles.Length ? GameContent.Projectiles[e.Param] : null;
                    float radius = def != null ? Mathf.Max(0.8f, def.ExplodeRadius.ToFloat()) : 1.5f;
                    Vector3 ground = new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
                    Flash(ground + Vector3.up * 0.4f, new Color(7f, 3.2f, 0.9f), 0.22f, radius * 0.4f, radius * 1.3f);
                    Flash(ground + Vector3.up * 0.4f, new Color(5f, 5f, 4f), 0.08f, radius * 0.2f, radius * 0.7f);
                    Ring(ground + Vector3.up * 0.06f, new Color(5f, 2.2f, 0.6f), 0.45f, 0.3f, radius * 2.2f);
                    Burst(ground + Vector3.up * 0.3f, new Color(6f, 2.6f, 0.6f), 22, 6f + radius * 2f, 0.55f, true);
                    Burst(ground + Vector3.up * 0.5f, new Color(0.35f, 0.32f, 0.3f), 14, 2.5f + radius, 1.1f, true);
                    _sfx.Play(Sfx.Explosion, Mathf.Clamp01(0.55f + radius * 0.15f));
                    RaiseShake(Mathf.Clamp(radius * 0.22f, 0.2f, 0.9f));
                    break;
                }
                case SimEventType.WeaponSwitch:
                    if (e.Fighter == _localIndex) _sfx.Play(Sfx.UiClick, 0.7f, 0f);
                    RaisePopup(pos + Vector3.up * 2.2f, e.Param == 0 ? "LAUNCHER" : "PISTOL", FighterColor(e.Fighter, 1.2f), 0.7f);
                    break;
                case SimEventType.Jump:
                    Ring(pos + Vector3.up * 0.05f, new Color(1.5f, 1.5f, 2f), 0.25f, 0.3f, 1.4f);
                    _sfx.Play(Sfx.Jump, 0.5f);
                    break;
                case SimEventType.Land:
                    Ring(pos + Vector3.up * 0.05f, new Color(1.2f, 1.2f, 1.6f), 0.25f, 0.3f, 1.6f);
                    bool slam = e.Param == (int)FighterAction.Launched || e.Param == (int)FighterAction.KO;
                    _sfx.Play(slam ? Sfx.BodySlam : Sfx.Land, slam ? 0.9f : 0.6f);
                    break;
                case SimEventType.Dodge:
                    Ring(pos + Vector3.up * 0.05f, FighterColor(e.Fighter, 2f), 0.2f, 0.2f, 1.3f);
                    _sfx.Play(Sfx.Dodge, 0.6f);
                    break;
                case SimEventType.Teleport:
                    Flash(pos + Vector3.up, FighterColor(e.Fighter, 4f), 0.2f, 0.3f, 1.6f);
                    Burst(pos + Vector3.up, FighterColor(e.Fighter, 4f), 12, 4f, 0.4f);
                    _sfx.Play(Sfx.Teleport, 0.8f);
                    break;
                case SimEventType.BuffStart:
                    Ring(pos + Vector3.up * 0.1f, FighterColor(e.Fighter, 4f), 0.5f, 0.3f, 6f);
                    Flash(pos + Vector3.up, FighterColor(e.Fighter, 5f), 0.25f, 0.4f, 2.5f);
                    _sfx.Play(Sfx.Buff, 1f, 0f);
                    RaisePopup(pos + Vector3.up * 2.2f, e.Param == (int)BuffId.TwinBlades ? "TWIN BLADES" : "LEVITATE", FighterColor(e.Fighter, 1.2f), 1.3f);
                    RaiseShake(0.3f);
                    break;
                case SimEventType.BuffEnd:
                    Flash(pos + Vector3.up, FighterColor(e.Fighter, 2f), 0.15f, 0.2f, 1.2f);
                    break;
                case SimEventType.Recharge:
                    Ring(pos + Vector3.up * 0.1f, new Color(0.6f, 1.4f, 4f), 0.4f, 0.2f, 2.2f);
                    Burst(pos + Vector3.up * 1f, new Color(0.6f, 1.4f, 4f), 10, 2.5f, 0.6f, true);
                    _sfx.Play(Sfx.Magic, 0.8f);
                    RaisePopup(pos + Vector3.up * 2.1f, "+" + e.Param + " MAGIC", new Color(0.5f, 0.75f, 1f), 0.9f);
                    break;
                case SimEventType.Heal:
                    Ring(pos + Vector3.up * 0.1f, new Color(0.6f, 4f, 1.2f), 0.4f, 0.2f, 2.2f);
                    Burst(pos + Vector3.up * 1f, new Color(0.6f, 4f, 1.2f), 12, 2.5f, 0.7f, true);
                    _sfx.Play(Sfx.Magic, 0.7f, 0.1f);
                    RaisePopup(pos + Vector3.up * 2.1f, "+" + e.Param + " HP", new Color(0.5f, 1f, 0.6f), 0.9f);
                    break;
                case SimEventType.NotEnoughMagic:
                    if (e.Fighter == _localIndex)
                    {
                        _sfx.Play(Sfx.Denied, 0.8f, 0f);
                        RaisePopup(pos + Vector3.up * 2.1f, "NOT ENOUGH MAGIC", new Color(0.55f, 0.65f, 1f), 0.9f);
                    }
                    break;
                case SimEventType.Awaken:
                {
                    Color purple = new Color(3.2f, 0.8f, 5.5f);
                    Ring(pos + Vector3.up * 0.1f, purple, 0.8f, 0.6f, 9f);
                    Ring(pos + Vector3.up * 1.2f, purple, 0.6f, 0.4f, 6f);
                    Flash(pos + Vector3.up * 1.5f, purple, 0.5f, 0.8f, 5f);
                    Burst(pos + Vector3.up * 1.5f, purple, 40, 9f, 1f, true);
                    _sfx.Play(Sfx.BodySlam, 1f, 0f);
                    _sfx.Play(Sfx.Buff, 1f, 0f);
                    RaisePopup(pos + Vector3.up * 3.6f, "AWAKENED", new Color(0.85f, 0.5f, 1f), 1.6f);
                    RaiseShake(1.2f);
                    break;
                }
                case SimEventType.KO:
                    Flash(pos + Vector3.up, new Color(6f, 6f, 6f), 0.3f, 0.6f, 4f);
                    Burst(pos + Vector3.up, new Color(5f, 4f, 3f), 30, 12f, 0.7f);
                    _sfx.Play(Sfx.Ko, 1f, 0f);
                    RaiseShake(1f);
                    break;
                case SimEventType.RingOut:
                    Vector3 lava = new Vector3(pos.x, Arena.LavaHeight.ToFloat() + 0.1f, pos.z);
                    Ring(lava, new Color(5f, 1.8f, 0.4f), 0.7f, 1f, 7f);
                    Flash(lava, new Color(6f, 2.5f, 0.6f), 0.4f, 1f, 4f);
                    Burst(lava, new Color(5f, 2f, 0.5f), 30, 9f, 0.9f, true);
                    _sfx.Play(Sfx.Explosion, 1f, 0f);
                    RaiseShake(0.8f);
                    break;
                case SimEventType.RoundIntro:
                case SimEventType.RoundFight:
                case SimEventType.RoundEnd:
                case SimEventType.MatchEnd:
                    _sfx.Play(Sfx.Announce, 0.7f, 0f);
                    break;
            }
        }

        private void RaisePopup(Vector3 pos, string text, Color color, float size)
        {
            if (Popup != null) Popup(pos, text, color, size);
        }

        private void RaiseShake(float amount)
        {
            if (Shake != null) Shake(amount);
        }

        private Fx Get(Mesh mesh, FxKind kind)
        {
            Fx fx = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active && _pool[i].Kind == kind)
                {
                    fx = _pool[i];
                    break;
                }
            }
            if (fx == null)
            {
                var go = new GameObject("Fx " + kind);
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                fx = new Fx { Go = go, Kind = kind, Material = MaterialFactory.Additive(Color.white) };
                mr.sharedMaterial = fx.Material;
                _pool.Add(fx);
            }
            fx.Active = true;
            fx.Age = 0f;
            fx.Go.SetActive(true);
            fx.Velocity = Vector3.zero;
            return fx;
        }

        private void Flash(Vector3 pos, Color color, float life, float startScale, float endScale)
        {
            Fx fx = Get(_sphere, FxKind.Flash);
            fx.Go.transform.position = pos;
            fx.Go.transform.rotation = Quaternion.identity;
            fx.Life = life;
            fx.Color = color;
            fx.StartScale = startScale;
            fx.EndScale = endScale;
        }

        private void Ring(Vector3 pos, Color color, float life, float startScale, float endScale)
        {
            Fx fx = Get(_cylinder, FxKind.Ring);
            fx.Go.transform.position = pos;
            fx.Go.transform.rotation = Quaternion.identity;
            fx.Life = life;
            fx.Color = color;
            fx.StartScale = startScale;
            fx.EndScale = endScale;
        }

        private void Burst(Vector3 pos, Color color, int count, float speed, float life, bool rising = false)
        {
            for (int i = 0; i < count; i++)
            {
                Fx fx = Get(_cube, FxKind.Spark);
                fx.Go.transform.position = pos;
                fx.Go.transform.rotation = UnityEngine.Random.rotation;
                Vector3 dir = UnityEngine.Random.onUnitSphere;
                if (rising) dir = new Vector3(dir.x * 0.4f, Mathf.Abs(dir.y) + 0.6f, dir.z * 0.4f);
                fx.Velocity = dir * speed * UnityEngine.Random.Range(0.4f, 1f);
                fx.Life = life * UnityEngine.Random.Range(0.6f, 1.2f);
                fx.Color = color;
                fx.StartScale = 0.12f;
                fx.EndScale = 0.01f;
            }
        }

        public void Update(float deltaTime)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                Fx fx = _pool[i];
                if (!fx.Active) continue;
                fx.Age += deltaTime;
                float t = fx.Age / fx.Life;
                if (t >= 1f)
                {
                    fx.Active = false;
                    fx.Go.SetActive(false);
                    continue;
                }
                float s = Mathf.Lerp(fx.StartScale, fx.EndScale, 1f - (1f - t) * (1f - t));
                Color c = fx.Color;
                c.a = 1f - t;
                MaterialFactory.SetColor(fx.Material, c * (1f - t * 0.7f));
                Transform tr = fx.Go.transform;
                switch (fx.Kind)
                {
                    case FxKind.Flash:
                        tr.localScale = Vector3.one * s;
                        break;
                    case FxKind.Ring:
                        tr.localScale = new Vector3(s, 0.01f, s);
                        break;
                    case FxKind.Spark:
                        tr.position += fx.Velocity * deltaTime;
                        fx.Velocity *= 1f - deltaTime * 3f;
                        fx.Velocity += Vector3.down * 6f * deltaTime;
                        tr.localScale = Vector3.one * s;
                        break;
                }
            }
        }

        /// <summary>Creates, moves and removes projectile visuals to match the game state.</summary>
        public void SyncProjectiles(GameState state)
        {
            foreach (ProjectileView pv in _projectiles.Values) pv.Seen = false;

            for (int i = 0; i < state.Projectiles.Length; i++)
            {
                ProjectileState p = state.Projectiles[i];
                if (!p.Active) continue;
                ProjectileView view;
                if (!_projectiles.TryGetValue(p.SerialId, out view))
                {
                    view = CreateProjectile(p);
                    _projectiles[p.SerialId] = view;
                }
                view.Seen = true;
                Vector3 pos = FighterView.ToVector(p.Position);
                Vector3 vel = FighterView.ToVector(p.Velocity);
                view.Go.transform.position = pos;
                if (vel.sqrMagnitude > 1e-6f) view.Go.transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
                float pulse = view.Oriented ? 1f : 1f + Mathf.Sin(Time.unscaledTime * 25f + p.SerialId) * 0.1f;
                view.Go.transform.localScale = Vector3.one * pulse;
            }

            _removeList.Clear();
            foreach (KeyValuePair<int, ProjectileView> kv in _projectiles)
            {
                if (!kv.Value.Seen) _removeList.Add(kv.Key);
            }
            for (int i = 0; i < _removeList.Count; i++)
            {
                ProjectileView pv = _projectiles[_removeList[i]];
                if (pv.Go != null) UnityEngine.Object.Destroy(pv.Go);
                _projectiles.Remove(_removeList[i]);
            }
        }

        private ProjectileView CreateProjectile(ProjectileState p)
        {
            ProjectileDef def = GameContent.Projectiles[p.DefId];
            Color c = FighterColor(p.Owner, 5f);
            var go = new GameObject("Projectile " + def.Name);
            go.transform.SetParent(_root, false);
            float r = def.Radius.ToFloat();
            GameObject core;
            if (def.VisualId == LauncherFighter.MissileVisual)
            {
                // Missile: dark body with a glowing nose; flame and smoke trail behind.
                core = MaterialFactory.Primitive(PrimitiveType.Capsule, go.transform, MaterialFactory.Lit(new Color(0.18f, 0.2f, 0.22f), 0.6f, 0.7f), "Body");
                core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                core.transform.localScale = new Vector3(0.22f, 0.38f, 0.22f);
                GameObject nose = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(c), "Nose");
                nose.transform.localPosition = new Vector3(0f, 0f, 0.34f);
                nose.transform.localScale = Vector3.one * 0.2f;
                nose.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                GameObject flame = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(new Color(6f, 2.6f, 0.6f)), "Flame");
                flame.transform.localPosition = new Vector3(0f, 0f, -0.42f);
                flame.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
                flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                TrailRenderer smoke = go.AddComponent<TrailRenderer>();
                smoke.time = 0.55f;
                smoke.startWidth = 0.35f;
                smoke.endWidth = 0.9f;
                smoke.sharedMaterial = MaterialFactory.Additive(new Color(0.9f, 0.5f, 0.25f, 0.35f));
                smoke.shadowCastingMode = ShadowCastingMode.Off;
                var flameLight = new GameObject("Light").AddComponent<Light>();
                flameLight.transform.SetParent(go.transform, false);
                flameLight.type = LightType.Point;
                flameLight.color = new Color(1f, 0.55f, 0.2f);
                flameLight.range = 4.5f;
                flameLight.intensity = 3f;
                return new ProjectileView { Go = go, Trail = smoke, SerialId = p.SerialId, Oriented = true };
            }
            if (def.VisualId == LauncherFighter.BulletVisual)
            {
                core = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(new Color(6f, 5f, 2.5f)), "Bullet");
                core.transform.localScale = new Vector3(0.09f, 0.09f, 0.35f);
                core.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                TrailRenderer streak = go.AddComponent<TrailRenderer>();
                streak.time = 0.08f;
                streak.startWidth = 0.08f;
                streak.endWidth = 0f;
                streak.sharedMaterial = MaterialFactory.Additive(new Color(4f, 3f, 1.5f));
                streak.shadowCastingMode = ShadowCastingMode.Off;
                return new ProjectileView { Go = go, Trail = streak, SerialId = p.SerialId, Oriented = true };
            }
            if (def.VisualId == 1)
            {
                // Sword wave: a wide, thin crescent.
                core = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(c), "Core");
                core.transform.localScale = new Vector3(r * 2.4f, r * 0.9f, r * 0.35f);
            }
            else
            {
                core = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(c), "Core");
                core.transform.localScale = Vector3.one * r * (def.VisualId == 5 ? 1.6f : 1.2f);
            }
            core.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = r * 1.4f;
            trail.endWidth = 0f;
            trail.sharedMaterial = MaterialFactory.Additive(c * 0.6f);
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.color = c / 5f;
            light.range = 3.5f;
            light.intensity = 2f;

            return new ProjectileView { Go = go, Trail = trail, SerialId = p.SerialId };
        }
    }
}
