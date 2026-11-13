using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Draws a match: both fighters, projectiles, effects and (optionally) hitboxes.
    /// It only reads the game state. It never changes it.
    /// </summary>
    public sealed class MatchPresenter
    {
        public readonly EffectsPlayer Effects;
        public FighterView[] Fighters { get; private set; } = new FighterView[2];
        public bool ShowHitboxes;

        private readonly Transform _root;
        private readonly List<GameObject> _debugShapes = new List<GameObject>();
        private Material _hurtMaterial;
        private Material _hitMaterial;

        public MatchPresenter(Transform parent, SfxPlayer sfx)
        {
            _root = new GameObject("Match").transform;
            _root.SetParent(parent, false);
            Effects = new EffectsPlayer(_root, sfx);
        }

        public bool HasMatch
        {
            get { return Fighters[0] != null; }
        }

        public void Build(GameState state, int localIndex)
        {
            Clear();
            for (int i = 0; i < 2; i++)
            {
                CharacterDef c = GameContent.GetCharacter(state.Fighters[i].CharacterId);
                Fighters[i] = new FighterView(c, _root, i);
            }
            Effects.SetFighters(Fighters[0], Fighters[1], localIndex);
        }

        public void Clear()
        {
            for (int i = 0; i < 2; i++)
            {
                if (Fighters[i] != null) Fighters[i].Destroy();
                Fighters[i] = null;
            }
            Effects.Clear();
            foreach (GameObject go in _debugShapes)
            {
                if (go != null) Object.Destroy(go);
            }
            _debugShapes.Clear();
        }

        public void Render(GameState state, EventBuffer events, float alpha, float deltaTime)
        {
            if (!HasMatch) return;
            for (int i = 0; i < 2; i++)
            {
                Fighters[i].Sync(state, ref state.Fighters[i], alpha, deltaTime);
            }
            if (events != null) Effects.HandleEvents(events, state.Tick);
            Effects.SyncProjectiles(state);
            Effects.Update(deltaTime);
            DrawHitboxes(state);
        }

        private void DrawHitboxes(GameState state)
        {
            int used = 0;
            if (ShowHitboxes)
            {
                if (_hurtMaterial == null)
                {
                    _hurtMaterial = MaterialFactory.Additive(new Color(0.1f, 0.6f, 0.2f, 0.35f));
                    _hitMaterial = MaterialFactory.Additive(new Color(0.9f, 0.1f, 0.1f, 0.5f));
                }
                for (int i = 0; i < 2; i++)
                {
                    FighterState f = state.Fighters[i];
                    CharacterDef c = GameContent.GetCharacter(f.CharacterId);
                    Vector3 pos = FighterView.ToVector(f.Position);
                    float r = c.Radius.ToFloat();
                    float h = c.Height.ToFloat();
                    GameObject hurt = DebugShape(used++, PrimitiveType.Capsule, _hurtMaterial);
                    hurt.transform.position = pos + Vector3.up * h * 0.5f;
                    hurt.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);

                    if (f.Action != FighterAction.Attack) continue;
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    if (m == null) continue;
                    foreach (HitboxDef hb in m.Hitboxes)
                    {
                        if (f.MoveTick < hb.Start || f.MoveTick > hb.End) continue;
                        Vector3 fwd = FighterView.ToVector(FixVec3.FromAngle(f.Facing));
                        GameObject hit = DebugShape(used++, PrimitiveType.Sphere, _hitMaterial);
                        hit.transform.position = pos + fwd * hb.Forward.ToFloat() + Vector3.up * hb.Up.ToFloat();
                        hit.transform.localScale = Vector3.one * hb.Radius.ToFloat() * 2f;
                    }
                }
                for (int i = 0; i < state.Projectiles.Length; i++)
                {
                    ProjectileState p = state.Projectiles[i];
                    if (!p.Active) continue;
                    GameObject hit = DebugShape(used++, PrimitiveType.Sphere, _hitMaterial);
                    hit.transform.position = FighterView.ToVector(p.Position);
                    hit.transform.localScale = Vector3.one * GameContent.Projectiles[p.DefId].Radius.ToFloat() * 2f;
                }
            }
            for (int i = used; i < _debugShapes.Count; i++)
            {
                if (_debugShapes[i].activeSelf) _debugShapes[i].SetActive(false);
            }
        }

        private GameObject DebugShape(int index, PrimitiveType type, Material material)
        {
            while (_debugShapes.Count <= index)
            {
                _debugShapes.Add(null);
            }
            GameObject go = _debugShapes[index];
            if (go == null || go.name != type.ToString())
            {
                if (go != null) Object.Destroy(go);
                go = MaterialFactory.Primitive(type, _root, material, type.ToString());
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                _debugShapes[index] = go;
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.SetActive(true);
            return go;
        }
    }
}
