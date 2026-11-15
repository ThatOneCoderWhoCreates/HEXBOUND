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
            /// <summary>Golden dragon: the body segments that follow the head's path.</summary>
            public DragonBody Dragon;
        }

        /// <summary>A serpent body made of glowing segments that follows the path the head flew along.</summary>
        private sealed class DragonBody
        {
            public GameObject Root;
            public Transform[] Segments;
            public readonly List<Vector3> Path = new List<Vector3>();
            public float Phase;
        }

        private const int DragonSegments = 26;
        private const float DragonSpacing = 0.42f;

        private DragonBody BuildDragonBody(Material glow)
        {
            var body = new DragonBody { Root = new GameObject("Golden Dragon Body") };
            body.Root.transform.SetParent(_root, false);
            body.Segments = new Transform[DragonSegments];
            for (int i = 0; i < DragonSegments; i++)
            {
                float k = i / (float)(DragonSegments - 1);
                GameObject seg = MaterialFactory.Primitive(PrimitiveType.Sphere, body.Root.transform, glow, "Segment " + i);
                seg.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                float size = Mathf.Lerp(0.95f, 0.18f, k * k);
                seg.transform.localScale = new Vector3(size, size * 0.85f, size * 1.4f);
                if (i % 3 == 1 && i < DragonSegments - 4)
                {
                    // Back fins.
                    GameObject fin = MaterialFactory.Primitive(PrimitiveType.Cube, seg.transform, glow, "Fin");
                    fin.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    fin.transform.localPosition = new Vector3(0f, 0.55f, -0.1f);
                    fin.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                    fin.transform.localScale = new Vector3(0.08f, 0.5f, 0.35f);
                }
                body.Segments[i] = seg.transform;
            }
            return body;
        }

        /// <summary>Moves the dragon's body along the recorded path with a swimming wave.</summary>
        private static void UpdateDragonBody(DragonBody body, Vector3 head, Vector3 forward, float deltaTime)
        {
            List<Vector3> path = body.Path;
            if (path.Count == 0 || (path[path.Count - 1] - head).sqrMagnitude > 0.0025f) path.Add(head);
            if (path.Count > 400) path.RemoveRange(0, path.Count - 400);
            body.Phase += deltaTime * 7f;

            // Walk back along the path, dropping a segment every DragonSpacing meters.
            int index = path.Count - 1;
            Vector3 cursor = head;
            Vector3 prev = head;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            for (int i = 0; i < body.Segments.Length; i++)
            {
                float need = DragonSpacing * (i == 0 ? 0.9f : 1f);
                Vector3 point = cursor;
                bool found = false;
                while (index >= 0)
                {
                    Vector3 target = path[index];
                    float len = (cursor - target).magnitude;
                    if (len >= need)
                    {
                        point = Vector3.Lerp(cursor, target, need / Mathf.Max(len, 1e-5f));
                        cursor = point;
                        found = true;
                        break;
                    }
                    need -= len;
                    cursor = target;
                    index--;
                }
                // Past the start of the path the body trails straight back.
                if (!found)
                {
                    point = cursor - forward * need;
                    cursor = point;
                }
                float wave = Mathf.Sin(body.Phase - i * 0.55f);
                float amp = Mathf.Lerp(0.15f, 0.55f, i / (float)body.Segments.Length);
                Vector3 offset = side * wave * amp + Vector3.up * Mathf.Cos(body.Phase * 0.8f - i * 0.45f) * amp * 0.6f;
                Transform t = body.Segments[i];
                t.position = point + offset;
                Vector3 dir = prev - point;
                if (dir.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                prev = point;
            }
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
            Impact = new ImpactFx(_root);
        }

        /// <summary>Dust, sparks and stone chips (also used by MatchPresenter for running footsteps).</summary>
        public readonly ImpactFx Impact;

        public void SetFighters(FighterView a, FighterView b, int localIndex)
        {
            _fighters[0] = a;
            _fighters[1] = b;
            _localIndex = localIndex;
        }

        public void Clear()
        {
            Impact.Clear();
            foreach (Fx fx in _pool)
            {
                fx.Active = false;
                fx.Go.SetActive(false);
            }
            foreach (ProjectileView pv in _projectiles.Values)
            {
                if (pv.Go != null) UnityEngine.Object.Destroy(pv.Go);
                if (pv.Dragon != null && pv.Dragon.Root != null) UnityEngine.Object.Destroy(pv.Dragon.Root);
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
            Impact.SetMap(Arena.CurrentMap);
            switch (e.Type)
            {
                case SimEventType.MoveStart:
                {
                    MoveDef m = GameContent.GetMove(e.Param);
                    if (m == null) break;
                    if (m.Pose == MovePose.Aim || m.Pose == MovePose.Shoot) { }
                    else if (m.Pose == MovePose.Throw) _sfx.Play(Sfx.SwingHeavy, 0.7f);
                    else if (m.Pose == MovePose.Cast || m.Pose == MovePose.Channel) _sfx.Play(Sfx.Cast, 0.6f);
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
                    {
                        Vector3 away = Vector3.zero;
                        int attacker = e.Fighter == 0 ? 1 : 0;
                        if (e.Fighter >= 0 && _fighters[attacker] != null) away = pos - _fighters[attacker].Visual.Root.position;
                        away.y = Mathf.Max(away.y, 0f);
                        Impact.Sparks(pos, away, heavy ? 22 : 10, heavy ? 11f : 7f, new Color(6f, 4.2f, 2f));
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
                    Impact.Sparks(pos, Vector3.zero, 12, 7f, new Color(3f, 4f, 6f));
                    _sfx.Play(Sfx.Block, 0.8f);
                    if (e.Param > 0) RaisePopup(pos + Vector3.up * 0.4f, e.Param.ToString(), new Color(0.6f, 0.75f, 1f), 0.8f);
                    break;
                case SimEventType.Parried:
                    Flash(pos, new Color(5f, 5f, 5f), 0.18f, 0.2f, 2.2f);
                    Ring(pos, new Color(3f, 3f, 4f), 0.3f, 0.2f, 3f);
                    Impact.Sparks(pos, Vector3.zero, 30, 12f, new Color(7f, 7f, 6f));
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
                    Impact.Sparks(pos, Vector3.zero, 18, 9f, new Color(6f, 5f, 4f));
                    _sfx.Play(Sfx.Clash, 0.9f);
                    break;
                case SimEventType.ProjectileExpire:
                    Flash(pos, new Color(2f, 1.5f, 3f), 0.1f, 0.1f, 0.6f);
                    break;
                case SimEventType.Explosion:
                {
                    ProjectileDef def = e.Param >= 0 && e.Param < GameContent.Projectiles.Length ? GameContent.Projectiles[e.Param] : null;
                    float radius = def != null ? Mathf.Max(0.8f, def.ExplodeRadius.ToFloat()) : 1.5f;
                    if (def != null && def.VisualId == StaffFighter.DragonVisual)
                    {
                        Flash(pos, new Color(9f, 6.5f, 2.2f), 0.35f, radius * 0.4f, radius * 1.6f);
                        Ring(pos, new Color(6f, 4.2f, 1.2f), 0.5f, 0.4f, radius * 2.6f);
                        Burst(pos, new Color(6f, 4.5f, 1.4f), 40, 10f, 0.9f, true);
                        _sfx.Play(Sfx.Explosion, 1f);
                        RaiseShake(0.9f);
                        break;
                    }
                    Vector3 ground = new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
                    Flash(ground + Vector3.up * 0.4f, new Color(7f, 3.2f, 0.9f), 0.22f, radius * 0.4f, radius * 1.3f);
                    Flash(ground + Vector3.up * 0.4f, new Color(5f, 5f, 4f), 0.08f, radius * 0.2f, radius * 0.7f);
                    Ring(ground + Vector3.up * 0.06f, new Color(5f, 2.2f, 0.6f), 0.45f, 0.3f, radius * 2.2f);
                    Burst(ground + Vector3.up * 0.3f, new Color(6f, 2.6f, 0.6f), 22, 6f + radius * 2f, 0.55f, true);
                    Burst(ground + Vector3.up * 0.5f, new Color(0.35f, 0.32f, 0.3f), 6, 2.5f + radius, 1.1f, true);
                    Impact.Dust(ground, 16 + (int)(radius * 4f), 2.2f + radius, 0.9f + radius * 0.4f, 1.6f);
                    Impact.Chips(ground, 8 + (int)(radius * 3f), 6f + radius);
                    Impact.Sparks(ground + Vector3.up * 0.3f, Vector3.up, 16, 10f, new Color(7f, 3.5f, 1f));
                    _sfx.Play(Sfx.Explosion, Mathf.Clamp01(0.55f + radius * 0.15f));
                    RaiseShake(Mathf.Clamp(radius * 0.22f, 0.2f, 0.9f));
                    break;
                }
                case SimEventType.ProjectileSpawn:
                {
                    ProjectileDef def = e.Param >= 0 && e.Param < GameContent.Projectiles.Length ? GameContent.Projectiles[e.Param] : null;
                    if (def == null) break;
                    if (def.VisualId == LauncherFighter.MissileVisual)
                    {
                        _sfx.Play(Sfx.RocketLaunch, 0.9f);
                        Flash(pos, new Color(6f, 3.5f, 1.2f), 0.12f, 0.4f, 1.6f);
                        Burst(pos, new Color(2f, 1.6f, 1.2f), 10, 3f, 0.6f, true);
                    }
                    else if (def.VisualId == LauncherFighter.BulletVisual)
                    {
                        _sfx.Play(Sfx.PistolShot, 0.8f);
                        Flash(pos, new Color(7f, 5f, 2f), 0.06f, 0.15f, 0.7f);
                    }
                    break;
                }
                case SimEventType.WeaponThrow:
                    _sfx.Play(Sfx.WeaponThrow, 0.9f);
                    break;
                case SimEventType.WeaponDrop:
                    _sfx.Play(Sfx.WeaponDrop, 0.8f);
                    Burst(pos + Vector3.up * 0.1f, new Color(1.6f, 1.4f, 1.2f), 4, 2.5f, 0.35f);
                    Impact.Dust(pos, 6, 1.2f, 0.35f, 0.6f);
                    break;
                case SimEventType.NeedWeapon:
                    if (e.Fighter == _localIndex)
                    {
                        _sfx.Play(Sfx.Denied, 0.7f, 0f);
                        RaisePopup(pos + Vector3.up * 2.2f, "NEED YOUR WEAPON", new Color(1f, 0.55f, 0.4f), 0.8f);
                    }
                    break;
                case SimEventType.WeaponLost:
                    RaisePopup(pos + Vector3.up * 1.5f, "WEAPON LOST", FighterColor(e.Fighter, 1.2f), 0.9f);
                    break;
                case SimEventType.WeaponPickup:
                    _sfx.Play(Sfx.WeaponPickup, 0.9f);
                    Ring(pos + Vector3.up * 0.05f, FighterColor(e.Fighter, 3f), 0.3f, 0.3f, 1.8f);
                    break;
                case SimEventType.RecoilJump:
                    Ring(pos + Vector3.up * 0.05f, new Color(3f, 2f, 1f), 0.35f, 0.4f, 2.6f);
                    Impact.Dust(pos, 10, 2.4f, 0.7f, 1.2f);
                    _sfx.Play(Sfx.Jump, 0.8f);
                    break;
                case SimEventType.WeaponSwitch:
                    if (e.Fighter == _localIndex) _sfx.Play(Sfx.UiClick, 0.7f, 0f);
                    RaisePopup(pos + Vector3.up * 2.2f, e.Param == 0 ? "LAUNCHER" : "PISTOL", FighterColor(e.Fighter, 1.2f), 0.7f);
                    break;
                case SimEventType.Jump:
                    Impact.Dust(pos, 5, 1.3f, 0.4f, 0.6f);
                    _sfx.Play(Sfx.Jump, 0.5f);
                    break;
                case SimEventType.Land:
                    bool slam = e.Param == (int)FighterAction.Launched || e.Param == (int)FighterAction.KO;
                    Impact.Dust(pos, slam ? 16 : 7, slam ? 2.6f : 1.6f, slam ? 0.8f : 0.45f, slam ? 1.5f : 0.8f);
                    if (slam) Impact.Chips(pos, 5, 3f);
                    _sfx.Play(slam ? Sfx.BodySlam : Sfx.Land, slam ? 0.9f : 0.6f);
                    break;
                case SimEventType.Dodge:
                    Ring(pos + Vector3.up * 0.05f, FighterColor(e.Fighter, 2f), 0.2f, 0.2f, 1.3f);
                    Impact.Dust(pos, 6, 1.8f, 0.45f, 0.7f);
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
                case SimEventType.PillarHit:
                    Burst(pos, new Color(1.6f, 0.8f, 3f), 8, 4f, 0.45f);
                    Flash(pos, new Color(1.6f, 0.8f, 3f), 0.15f, 0.4f, 1.4f);
                    Impact.Sparks(pos, Vector3.zero, 10, 6f, new Color(3f, 1.8f, 6f));
                    _sfx.Play(Sfx.Clash, 0.45f, 0f);
                    break;
                case SimEventType.PillarBreak:
                    Flash(pos, new Color(3f, 1.6f, 5f), 0.35f, 1f, 5f);
                    Ring(pos - Vector3.up * 0.9f, new Color(2f, 1f, 3.5f), 0.6f, 0.5f, 6f);
                    Burst(pos, new Color(1.8f, 0.9f, 3.2f), 40, 8f, 0.9f);
                    Impact.Dust(new Vector3(pos.x, 0f, pos.z), 20, 3f, 1.1f, 1.6f);
                    _sfx.Play(Sfx.GuardBreak, 1f, 0f);
                    _sfx.Play(Sfx.Explosion, 0.5f, 0f);
                    RaiseShake(0.6f);
                    break;
                case SimEventType.StructureHit:
                    Impact.Chips(pos, 4, 4f);
                    Impact.Dust(new Vector3(pos.x, pos.y - 0.3f, pos.z), 4, 1.2f, 0.6f, 0.8f);
                    _sfx.Play(Sfx.Block, 0.35f, 0.15f);
                    break;
                case SimEventType.StructureBreak:
                {
                    Vector3 floor = new Vector3(pos.x, Mathf.Max(0f, pos.y - 4f), pos.z);
                    Impact.Dust(floor, 40, 5f, 2.2f, 2.2f);
                    Impact.Dust(pos, 20, 3f, 1.6f, 1.4f);
                    Impact.Chips(pos, 30, 7f);
                    _sfx.Play(Sfx.Explosion, 1f, 0f);
                    _sfx.Play(Sfx.GuardBreak, 0.9f, 0f);
                    _sfx.Play(Sfx.BodySlam, 1f, 0f);
                    RaisePopup(pos + Vector3.up * 1.5f, "COLLAPSE!", new Color(1f, 0.75f, 0.45f), 1.6f);
                    RaiseShake(1f);
                    break;
                }
                case SimEventType.Crushed:
                    RaisePopup(pos + Vector3.up * 2.2f, "CRUSHED!", new Color(1f, 0.35f, 0.3f), 1.6f);
                    break;
                case SimEventType.RingOut:
                    if (Arena.CurrentMap == Arena.MapSanctuary || Arena.CurrentMap == Arena.MapRuins)
                    {
                        // Falls into the sea of clouds: a soft white puff and a ring of mist.
                        Vector3 cloud = new Vector3(pos.x, -7f, pos.z);
                        Ring(cloud, new Color(2.2f, 2f, 2.6f), 0.7f, 1f, 8f);
                        Flash(cloud, new Color(3f, 2.6f, 3.4f), 0.4f, 1f, 4f);
                        Burst(cloud, new Color(2f, 1.8f, 2.4f), 30, 7f, 1.0f, true);
                        _sfx.Play(Sfx.Explosion, 0.7f, 0f);
                        RaiseShake(0.6f);
                        break;
                    }
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
                if (view.Dragon != null)
                {
                    // The head weaves a little as it flies; the body follows its path.
                    Vector3 fwd = vel.sqrMagnitude > 1e-6f ? vel.normalized : view.Go.transform.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                    float t = Time.time * 4f + p.SerialId;
                    view.Go.transform.position = pos + side * Mathf.Sin(t) * 0.35f + Vector3.up * Mathf.Sin(t * 0.7f) * 0.25f;
                    UpdateDragonBody(view.Dragon, view.Go.transform.position, fwd, Time.deltaTime);
                }
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
                if (pv.Dragon != null && pv.Dragon.Root != null) UnityEngine.Object.Destroy(pv.Dragon.Root);
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
            if (def.VisualId == CommonMoves.ThrownVisual)
            {
                // The fighter's view draws the spinning weapon itself; only a faint air trail here.
                TrailRenderer air = go.AddComponent<TrailRenderer>();
                air.time = 0.12f;
                air.startWidth = 0.25f;
                air.endWidth = 0f;
                air.sharedMaterial = MaterialFactory.Additive(new Color(1.2f, 1.2f, 1.4f, 0.4f));
                air.shadowCastingMode = ShadowCastingMode.Off;
                return new ProjectileView { Go = go, Trail = air, SerialId = p.SerialId, Oriented = true };
            }
            if (def.VisualId == LauncherFighter.MissileVisual)
            {
                // Missile: the rocket model (or a dark capsule with a glowing nose); flame and smoke trail behind.
                GameObject rocket = Resources.Load<GameObject>("Fighters/VexRocket");
                if (rocket != null)
                {
                    GameObject model = UnityEngine.Object.Instantiate(rocket, go.transform, false);
                    foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(col);
                    // After import the rocket's nose points along -X; turn it to +Z (the flight direction).
                    model.transform.localRotation = Quaternion.FromToRotation(Vector3.left, Vector3.forward);
                    Bounds rb = new Bounds();
                    bool any = false;
                    foreach (Renderer rr in model.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!any) { rb = rr.bounds; any = true; }
                        else rb.Encapsulate(rr.bounds);
                        rr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    if (any)
                    {
                        model.transform.localScale = Vector3.one * (FighterRigBuilder.RocketLength / Mathf.Max(rb.size.x, rb.size.y, rb.size.z, 1e-4f));
                        any = false;
                        foreach (Renderer rr in model.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!any) { rb = rr.bounds; any = true; }
                            else rb.Encapsulate(rr.bounds);
                        }
                        model.transform.position += go.transform.position - rb.center;
                    }
                }
                else
                {
                    core = MaterialFactory.Primitive(PrimitiveType.Capsule, go.transform, MaterialFactory.Lit(new Color(0.18f, 0.2f, 0.22f), 0.6f, 0.7f), "Body");
                    core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    core.transform.localScale = new Vector3(0.22f, 0.38f, 0.22f);
                    GameObject nose = MaterialFactory.Primitive(PrimitiveType.Sphere, go.transform, MaterialFactory.Glow(c), "Nose");
                    nose.transform.localPosition = new Vector3(0f, 0f, 0.34f);
                    nose.transform.localScale = Vector3.one * 0.2f;
                    nose.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
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
            if (def.VisualId == StaffFighter.DragonVisual)
            {
                // Golden dragon: translucent glowing head with horns and eyes, a long swimming body and sparks.
                Material gold = MaterialFactory.Additive(new Color(2.6f, 1.7f, 0.45f, 0.75f));
                Material bright = MaterialFactory.Glow(new Color(8f, 6f, 2.5f));
                Transform h = go.transform;
                void Part(PrimitiveType type, Material mat, Vector3 at, Vector3 scale, Vector3 euler, string name)
                {
                    GameObject part = MaterialFactory.Primitive(type, h, mat, name);
                    part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    part.transform.localPosition = at;
                    part.transform.localRotation = Quaternion.Euler(euler);
                    part.transform.localScale = scale;
                }
                Part(PrimitiveType.Sphere, gold, Vector3.zero, new Vector3(0.95f, 0.75f, 1.35f), Vector3.zero, "Skull");
                Part(PrimitiveType.Sphere, gold, new Vector3(0f, -0.08f, 0.75f), new Vector3(0.6f, 0.42f, 1.0f), Vector3.zero, "Snout");
                Part(PrimitiveType.Sphere, gold, new Vector3(0f, -0.42f, 0.55f), new Vector3(0.5f, 0.2f, 0.95f), new Vector3(18f, 0f, 0f), "Jaw");
                Part(PrimitiveType.Capsule, gold, new Vector3(0.3f, 0.45f, -0.35f), new Vector3(0.1f, 0.55f, 0.1f), new Vector3(-55f, 0f, -20f), "Horn L");
                Part(PrimitiveType.Capsule, gold, new Vector3(-0.3f, 0.45f, -0.35f), new Vector3(0.1f, 0.55f, 0.1f), new Vector3(-55f, 0f, 20f), "Horn R");
                Part(PrimitiveType.Capsule, gold, new Vector3(0.35f, -0.25f, 0.95f), new Vector3(0.04f, 0.6f, 0.04f), new Vector3(-100f, 0f, -35f), "Whisker L");
                Part(PrimitiveType.Capsule, gold, new Vector3(-0.35f, -0.25f, 0.95f), new Vector3(0.04f, 0.6f, 0.04f), new Vector3(-100f, 0f, 35f), "Whisker R");
                Part(PrimitiveType.Sphere, bright, new Vector3(0.26f, 0.18f, 0.5f), Vector3.one * 0.14f, Vector3.zero, "Eye L");
                Part(PrimitiveType.Sphere, bright, new Vector3(-0.26f, 0.18f, 0.5f), Vector3.one * 0.14f, Vector3.zero, "Eye R");

                var sparks = new GameObject("Sparks");
                sparks.transform.SetParent(h, false);
                ParticleSystem ps = sparks.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.15f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 400;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 160f;
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.7f;
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                ParticleSystemRenderer pr = sparks.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = MaterialFactory.ParticleAdditive(new Color(3f, 2.2f, 0.9f));
                pr.shadowCastingMode = ShadowCastingMode.Off;
                ps.Play();

                var glow = new GameObject("Light").AddComponent<Light>();
                glow.transform.SetParent(h, false);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.75f, 0.3f);
                glow.range = 9f;
                glow.intensity = 5f;

                Vector3 at = FighterView.ToVector(p.Position);
                Flash(at, new Color(8f, 5.5f, 1.8f), 0.3f, 0.5f, 3.5f);
                Burst(at, new Color(6f, 4f, 1.2f), 30, 7f, 0.7f, true);
                _sfx.Play(Sfx.Buff, 1f, 0f);
                _sfx.Play(Sfx.Explosion, 0.6f, 0.1f);
                RaiseShake(0.5f);
                return new ProjectileView { Go = go, SerialId = p.SerialId, Oriented = true, Dragon = BuildDragonBody(gold) };
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
