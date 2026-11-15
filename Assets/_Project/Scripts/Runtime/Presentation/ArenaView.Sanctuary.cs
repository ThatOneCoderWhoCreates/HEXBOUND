using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Celestial Sanctuary: a floating stone courtyard above a sea of clouds, with a temple gate, cherry trees,
    /// bridges to an outer garden ring, floating cliffs with waterfalls and a broken stone ring in the sky.
    /// The static scene is one model, Resources/Arena/Sanctuary/Sanctuary.glb. This code adds everything that moves
    /// or glows: the rune circle, the crystal pillars (solid in the simulation), lights, waterfalls, clouds, petals,
    /// swaying banners, the gate portal, the sky and post-processing.
    /// Optional models in Resources/Arena/Sanctuary/Models replace the built-in pieces when present:
    /// TempleGate, CherryTree, CelestialArch, Lantern, Banner, FloatingIsland, ArcaneCrystal. StonePillar and
    /// WaterfallCliff add extra scenery (stone pillars at the outer bridge ends, waterfall cliffs rising from the clouds).
    /// </summary>
    public sealed partial class ArenaView
    {
        private const string SanctuaryFolder = "Arena/Sanctuary/";
        private const string ModelFolder = "Arena/Sanctuary/Models/";

        private sealed class Sway
        {
            public Transform T;
            public Quaternion Rest;
            public float Phase;
        }

        private sealed class Pulse
        {
            public Light Light;
            public float Intensity;
            public float Phase;
        }

        private readonly List<Sway> _sway = new List<Sway>();
        private readonly List<Pulse> _pulses = new List<Pulse>();
        private readonly List<Material> _waterMaterials = new List<Material>();
        private readonly List<Transform> _spinning = new List<Transform>();
        private readonly List<CrystalView> _crystals = new List<CrystalView>();
        private readonly List<Debris> _debris = new List<Debris>();
        private Transform _rune;
        private Material _runeMaterial;
        private Material _edgeMaterial;
        private Material _crystalMaterial;
        private Material _portalMaterial;
        private GameObject _gateModel;

        private static readonly Color RuneGold = new Color(1.7f, 1.1f, 0.42f);
        private static readonly Color EdgeGold = new Color(1.5f, 0.95f, 0.35f);
        private static readonly Color CrystalGlow = new Color(1.1f, 0.45f, 2.6f);
        private static readonly Color PortalColor = new Color(0.8f, 0.6f, 2.4f);

        private void ClearSanctuary()
        {
            _sway.Clear();
            _pulses.Clear();
            _waterMaterials.Clear();
            _spinning.Clear();
            _crystals.Clear();
            _debris.Clear();
            _rune = null;
            _runeMaterial = null;
            _edgeMaterial = null;
            _crystalMaterial = null;
            _portalMaterial = null;
            _gateModel = null;
        }

        private void BuildSanctuary()
        {
            UsesSkybox = true;
            BackgroundColor = new Color(0.80f, 0.68f, 0.88f);

            Transform scene = null;
            GameObject prefab = Resources.Load<GameObject>(SanctuaryFolder + "Sanctuary");
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab, _mapRoot.transform, false);
                instance.name = "Sanctuary Model";
                RemoveColliders(instance);
                foreach (Animator a in instance.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                scene = instance.transform;
                SetupSanctuaryShadows(scene);
                ReplaceWithModels(scene);
                CollectBanners(scene);
                ReplaceBanners(scene);
                AddExtraScenery();
                BuildWaterfalls(scene);
                BuildPortal(scene);
            }
            else
            {
                BuildSanctuaryFallbackFloor();
            }

            BuildRune();
            BuildEdgeLine();
            BuildCrystals();
            BuildFarIslands();
            BuildCloudSea();
            BuildPetals();
            SetupSanctuaryLighting();
        }

        private void UpdateSanctuary(float dt)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(_time * 1.3f);
            if (_rune != null)
            {
                _rune.localRotation = Quaternion.Euler(90f, _time * 4f, 0f);
                MaterialFactory.SetColor(_runeMaterial, RuneGold * pulse);
            }
            if (_edgeMaterial != null) MaterialFactory.SetColor(_edgeMaterial, EdgeGold * (0.75f + 0.25f * Mathf.Sin(_time * 2.1f)));
            if (_crystalMaterial != null) _crystalMaterial.SetColor("_EmissionColor", CrystalGlow * (0.8f + 0.25f * Mathf.Sin(_time * 1.7f)));
            if (_portalMaterial != null) MaterialFactory.SetColor(_portalMaterial, PortalColor * (0.8f + 0.2f * Mathf.Sin(_time * 2.3f)));
            for (int i = 0; i < _sway.Count; i++)
            {
                Sway s = _sway[i];
                float swing = Mathf.Sin(_time * 1.4f + s.Phase) * 6f + Mathf.Sin(_time * 3.3f + s.Phase * 1.7f) * 1.5f;
                float twist = Mathf.Sin(_time * 0.9f + s.Phase) * 4f;
                s.T.localRotation = s.Rest * Quaternion.Euler(swing, twist, 0f);
            }
            for (int i = 0; i < _pulses.Count; i++)
            {
                Pulse p = _pulses[i];
                p.Light.intensity = p.Intensity * (0.82f + 0.18f * Mathf.Sin(_time * 1.7f + p.Phase));
            }
            for (int i = 0; i < _waterMaterials.Count; i++)
            {
                Material m = _waterMaterials[i];
                Vector2 o = m.mainTextureOffset;
                o.y += dt * (1.1f + i * 0.07f);
                if (o.y > 100f) o.y -= 100f;
                m.mainTextureOffset = o;
                if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", o);
            }
            for (int i = 0; i < _spinning.Count; i++) _spinning[i].Rotate(0f, 0f, dt * 12f, Space.Self);
            UpdateCrystals(dt);
        }

        // ------------------------------------------------------------------ Static scene

        private static void SetupSanctuaryShadows(Transform scene)
        {
            foreach (Renderer r in scene.GetComponentsInChildren<Renderer>(true))
            {
                r.receiveShadows = true;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            foreach (string far in new[] { "Mountains", "Cliffs", "CelestialRing", "OuterRing" })
            {
                Transform t = FindTransform(scene, far);
                if (t == null) continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Swaps the built-in pieces for Modly models found in Resources/Arena/Sanctuary/Models.</summary>
        private void ReplaceWithModels(Transform scene)
        {
            GameObject gate = Resources.Load<GameObject>(ModelFolder + "TempleGate");
            if (gate != null) _gateModel = FitReplace(FindTransform(scene, "Gate"), gate, false);

            GameObject arch = Resources.Load<GameObject>(ModelFolder + "CelestialArch");
            if (arch != null) FitReplace(FindTransform(scene, "CelestialRing"), arch, true);

            GameObject tree = Resources.Load<GameObject>(ModelFolder + "CherryTree");
            GameObject lantern = Resources.Load<GameObject>(ModelFolder + "Lantern");
            if (tree == null && lantern == null) return;
            var targets = new List<Transform>();
            foreach (Transform t in scene.GetComponentsInChildren<Transform>(true)) targets.Add(t);
            for (int i = 0; i < targets.Count; i++)
            {
                Transform t = targets[i];
                if (tree != null && t.name.StartsWith("Tree_")) FitReplace(t, tree, false);
                else if (lantern != null && t.name.StartsWith("Lantern_")) FitReplace(t, lantern, false);
            }
        }

        /// <summary>
        /// Hides the target piece and puts the model in its place: same rotation, scaled to the same height
        /// (or the same largest size), standing on the same base point.
        /// </summary>
        private static GameObject FitReplace(Transform target, GameObject prefab, bool byLargestSize)
        {
            if (target == null || prefab == null) return null;
            Bounds want;
            if (!RendererBounds(target, out want)) return null;
            GameObject model = Object.Instantiate(prefab, target.parent, false);
            model.name = prefab.name + " (" + target.name + ")";
            RemoveColliders(model);
            foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            model.transform.localPosition = target.localPosition;
            model.transform.localRotation = target.localRotation;
            model.transform.localScale = Vector3.one;
            Bounds have;
            if (!RendererBounds(model.transform, out have))
            {
                Object.Destroy(model);
                return null;
            }
            float wantSize = byLargestSize ? Mathf.Max(want.size.x, want.size.y, want.size.z) : want.size.y;
            float haveSize = byLargestSize ? Mathf.Max(have.size.x, have.size.y, have.size.z) : have.size.y;
            model.transform.localScale = Vector3.one * (wantSize / Mathf.Max(haveSize, 1e-4f));
            RendererBounds(model.transform, out have);
            model.transform.position += new Vector3(want.center.x - have.center.x, want.min.y - have.min.y, want.center.z - have.center.z);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            target.gameObject.SetActive(false);
            return model;
        }

        /// <summary>
        /// Banner model (crossbar and cloth) hung from each banner pivot, about 1.7 m wide, so it still sways.
        /// The built-in cloth on that pivot is hidden.
        /// </summary>
        private void ReplaceBanners(Transform scene)
        {
            GameObject prefab = Resources.Load<GameObject>(ModelFolder + "Banner");
            if (prefab == null) return;
            for (int i = 0; i < _sway.Count; i++)
            {
                Transform pivot = _sway[i].T;
                foreach (Renderer r in pivot.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                GameObject model = Object.Instantiate(prefab, pivot, false);
                model.name = "Banner Model";
                RemoveColliders(model);
                Bounds b;
                if (!RendererBounds(model.transform, out b)) continue;
                float width = Mathf.Max(b.size.x, b.size.z);
                model.transform.localScale = Vector3.one * (1.7f / Mathf.Max(width, 1e-4f) / Mathf.Max(pivot.lossyScale.x, 1e-4f));
                RendererBounds(model.transform, out b);
                model.transform.position += new Vector3(pivot.position.x - b.center.x, pivot.position.y + 0.1f - b.max.y, pivot.position.z - b.center.z);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        /// <summary>Stone pillars at the outer ends of the three bridges, and waterfall cliffs rising out of the clouds.</summary>
        private void AddExtraScenery()
        {
            GameObject pillar = Resources.Load<GameObject>(ModelFolder + "StonePillar");
            if (pillar != null)
            {
                foreach (float deg in new[] { 90f, 180f, 270f })
                {
                    float a = deg * Mathf.Deg2Rad;
                    var along = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var side = new Vector3(along.z, 0f, -along.x);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 p = along * 25.4f + side * (2.9f * s) + Vector3.up * -0.6f;
                        PlaceModel(pillar, p, Quaternion.LookRotation(-along), 3.4f, false, true);
                    }
                }
            }

            GameObject cliff = Resources.Load<GameObject>(ModelFolder + "WaterfallCliff");
            if (cliff != null)
            {
                Vector3[] spots = { new Vector3(-50f, -14f, -34f), new Vector3(57f, -13f, -8f), new Vector3(-58f, -12f, 22f), new Vector3(-24f, -14f, -62f) };
                float[] sizes = { 24f, 20f, 22f, 22f };
                for (int i = 0; i < spots.Length; i++)
                {
                    Vector3 toCenter = new Vector3(-spots[i].x, 0f, -spots[i].z).normalized;
                    PlaceModel(cliff, spots[i], Quaternion.LookRotation(toCenter), sizes[i], true, false);
                }
            }
        }

        /// <summary>Places a model standing on the given point, scaled to a height (or largest size).</summary>
        private GameObject PlaceModel(GameObject prefab, Vector3 basePoint, Quaternion rotation, float size, bool byLargestSize, bool castShadows)
        {
            GameObject model = Object.Instantiate(prefab, _mapRoot.transform, false);
            RemoveColliders(model);
            foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            model.transform.localRotation = rotation;
            Bounds b;
            if (!RendererBounds(model.transform, out b)) return model;
            float have = byLargestSize ? Mathf.Max(b.size.x, b.size.y, b.size.z) : b.size.y;
            model.transform.localScale = Vector3.one * (size / Mathf.Max(have, 1e-4f));
            RendererBounds(model.transform, out b);
            model.transform.position += new Vector3(basePoint.x - b.center.x, basePoint.y - b.min.y, basePoint.z - b.center.z);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            return model;
        }

        private static bool RendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return any;
        }

        private void CollectBanners(Transform scene)
        {
            int i = 0;
            foreach (Transform t in scene.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Banner_")) continue;
                _sway.Add(new Sway { T = t, Rest = t.localRotation, Phase = i * 1.37f });
                i++;
            }
        }

        private void BuildSanctuaryFallbackFloor()
        {
            GameObject floor = MaterialFactory.Primitive(PrimitiveType.Cylinder, _mapRoot.transform, MaterialFactory.Lit(new Color(0.56f, 0.53f, 0.58f), 0.3f), "Fallback Courtyard");
            float r = Arena.SanctuaryRadius.ToFloat();
            floor.transform.localScale = new Vector3(r * 2f, 0.2f, r * 2f);
            floor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        }

        // ------------------------------------------------------------------ Rune circle and edge line

        private void BuildRune()
        {
            Texture2D tex = Resources.Load<Texture2D>(SanctuaryFolder + "Rune");
            if (tex == null) return;
            _runeMaterial = MaterialFactory.Additive(RuneGold);
            _runeMaterial.mainTexture = tex;
            _runeMaterial.SetTexture("_BaseMap", tex);
            GameObject quad = MaterialFactory.Primitive(PrimitiveType.Quad, _mapRoot.transform, _runeMaterial, "Rune Circle");
            quad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            quad.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 18.4f;
            _rune = quad.transform;
        }

        /// <summary>A thin glowing gold ring just inside the ring-out edge.</summary>
        private void BuildEdgeLine()
        {
            float r = Arena.SanctuaryRadius.ToFloat();
            const int seg = 160;
            var verts = new Vector3[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = dir * (r - 0.12f) + Vector3.up * 0.03f;
                verts[i * 2 + 1] = dir * (r - 0.02f) + Vector3.up * 0.03f;
                int j = (i + 1) % seg;
                tris[i * 6] = i * 2;
                tris[i * 6 + 1] = j * 2;
                tris[i * 6 + 2] = j * 2 + 1;
                tris[i * 6 + 3] = i * 2;
                tris[i * 6 + 4] = j * 2 + 1;
                tris[i * 6 + 5] = i * 2 + 1;
            }
            var mesh = new Mesh { name = "Edge Line" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            var go = new GameObject("Edge Line");
            go.transform.SetParent(_mapRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            _edgeMaterial = MaterialFactory.Additive(EdgeGold);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _edgeMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Crystal pillars

        private void BuildCrystals()
        {
            ArenaLayout layout = Arena.Layout(Arena.MapSanctuary);
            GameObject model = Resources.Load<GameObject>(ModelFolder + "ArcaneCrystal");
            _crystalMaterial = new Material(MaterialFactory.Lit(new Color(0.62f, 0.42f, 1f), 0.85f, 0f, CrystalGlow));
            Mesh tall = CrystalMesh(0.36f, 3.1f);
            Mesh small = CrystalMesh(0.24f, 1.7f);
            for (int i = 0; i < layout.Pillars.Length; i++)
            {
                Vector3 p = FighterView.ToVector(layout.Pillars[i]);
                var holder = new GameObject("Crystal " + i).transform;
                holder.SetParent(_mapRoot.transform, false);
                holder.localPosition = new Vector3(p.x, 0.22f, p.z);
                holder.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
                var content = new GameObject("Crystal Body").transform;
                content.SetParent(holder, false);

                if (model != null)
                {
                    GameObject m = Object.Instantiate(model, content, false);
                    RemoveColliders(m);
                    foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
                    Bounds b;
                    if (RendererBounds(m.transform, out b))
                    {
                        // About 2.7 m across, so the outer shards end where the pillar's solid area stops a fighter.
                        m.transform.localScale = Vector3.one * (2.7f / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 1e-4f));
                        RendererBounds(m.transform, out b);
                        m.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.min.y, holder.position.z - b.center.z);
                    }
                }
                else
                {
                    AddCrystal(content, tall, Vector3.zero, Vector3.zero, 1f, _crystalMaterial);
                    AddCrystal(content, small, new Vector3(0.42f, 0f, 0.1f), new Vector3(0f, 0f, -22f), 1f, _crystalMaterial);
                    AddCrystal(content, small, new Vector3(-0.32f, 0f, 0.3f), new Vector3(18f, 0f, 20f), 0.85f, _crystalMaterial);
                    AddCrystal(content, small, new Vector3(0.05f, 0f, -0.42f), new Vector3(-24f, 0f, 4f), 0.75f, _crystalMaterial);
                    AddCrystal(content, small, new Vector3(-0.38f, 0f, -0.25f), new Vector3(-10f, 0f, 26f), 0.6f, _crystalMaterial);
                }

                // What is left on the pedestal once the crystal breaks: a few dull stubs.
                var stub = new GameObject("Broken Stub").transform;
                stub.SetParent(holder, false);
                Mesh stubMesh = CrystalMesh(0.2f, 0.55f);
                AddCrystal(stub, stubMesh, new Vector3(0.1f, 0f, 0.05f), new Vector3(8f, 0f, -12f), 1f, StubMaterial);
                AddCrystal(stub, stubMesh, new Vector3(-0.3f, 0f, 0.2f), new Vector3(-20f, 30f, 25f), 0.7f, StubMaterial);
                AddCrystal(stub, stubMesh, new Vector3(0.25f, 0f, -0.3f), new Vector3(25f, 60f, 10f), 0.6f, StubMaterial);
                stub.gameObject.SetActive(false);

                var lightGo = new GameObject("Crystal Light");
                lightGo.transform.SetParent(holder, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.72f, 0.45f, 1f);
                light.range = 7f;
                light.intensity = 2.4f;
                light.shadows = LightShadows.None;
                var pulse = new Pulse { Light = light, Intensity = light.intensity, Phase = i * 1.3f };
                _pulses.Add(pulse);

                GameObject sparkles = Sparkles(holder, new Color(1.6f, 0.9f, 3f), new Vector3(1.2f, 0.4f, 1.2f), 6f, 3.5f);
                CrystalBarResult bar = HealthBarParts(holder);
                _crystals.Add(new CrystalView
                {
                    Holder = holder, Body = content, Stub = stub.gameObject, Sparkles = sparkles, Glow = pulse, BaseIntensity = pulse.Intensity,
                    Health = layout.PillarHealth, Bar = bar.Bar, BarFill = bar.Fill,
                });
            }
        }

        private void AddCrystal(Transform parent, Mesh mesh, Vector3 position, Vector3 euler, float scale, Material material)
        {
            var go = new GameObject("Crystal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>A six-sided crystal: a prism with a pointed top, flat-shaded.</summary>
        private static Mesh CrystalMesh(float radius, float height)
        {
            const int sides = 6;
            float body = height * 0.72f;
            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                ring0[i] = new Vector3(Mathf.Sin(a) * radius * 0.85f, 0f, Mathf.Cos(a) * radius * 0.85f);
                ring1[i] = new Vector3(Mathf.Sin(a) * radius, body, Mathf.Cos(a) * radius);
            }
            var tip = new Vector3(0f, height, 0f);
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddTri(v, t, ring0[i], ring1[i], ring1[j]);
                AddTri(v, t, ring0[i], ring1[j], ring0[j]);
                AddTri(v, t, ring1[i], tip, ring1[j]);
                AddTri(v, t, ring0[j], Vector3.zero, ring0[i]);
            }
            var mesh = new Mesh { name = "Crystal" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int s = v.Count;
            v.Add(a);
            v.Add(b);
            v.Add(c);
            t.Add(s);
            t.Add(s + 1);
            t.Add(s + 2);
        }

        // ------------------------------------------------------------------ Breakable crystals

        private sealed class CrystalView
        {
            public Transform Holder;
            public Transform Body;
            public GameObject Stub;
            public GameObject Sparkles;
            public Pulse Glow;
            public float BaseIntensity;
            public int Health;
            public bool Broken;
            public float Shake;
            public float BarTimer;
            public Transform Bar;
            public Transform BarFill;
        }

        private sealed class Debris
        {
            public Transform T;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Age;
            public float Life;
            public float Scale;
        }

        private static Material _stubMaterial;

        private static Material StubMaterial
        {
            get
            {
                if (_stubMaterial == null) _stubMaterial = MaterialFactory.Lit(new Color(0.32f, 0.24f, 0.42f), 0.6f, 0f, new Color(0.25f, 0.1f, 0.5f));
                return _stubMaterial;
            }
        }

        /// <summary>
        /// Matches the crystals to the game state: dimmer and shaking as they lose health, shattering when they
        /// break, whole again when a new round starts. Pass null outside a match (all crystals whole).
        /// </summary>
        public void SyncPillars(GameState s)
        {
            if (Map != Arena.MapSanctuary || _crystals.Count == 0) return;
            int max = Arena.Layout(Arena.MapSanctuary).PillarHealth;
            for (int i = 0; i < _crystals.Count; i++)
            {
                CrystalView c = _crystals[i];
                int hp = s != null && s.Setup.Map == Arena.MapSanctuary && i < s.PillarHealth.Length ? s.PillarHealth[i] : max;
                if (hp < c.Health && hp > 0)
                {
                    c.Shake = 1f;
                    c.BarTimer = 3f;
                }
                if (hp <= 0 && !c.Broken)
                {
                    c.Broken = true;
                    Shatter(c);
                }
                else if (hp > 0 && c.Broken)
                {
                    c.Broken = false;
                }
                c.Health = hp;
                c.Body.gameObject.SetActive(!c.Broken);
                c.Stub.SetActive(c.Broken);
                c.Sparkles.SetActive(!c.Broken);
                float k = max > 0 ? Mathf.Clamp01(hp / (float)max) : 1f;
                c.Glow.Intensity = c.Broken ? 0f : c.BaseIntensity * (0.3f + 0.7f * k);
                if (c.BarFill != null) c.BarFill.localScale = new Vector3(k, 1f, 1f);
                if (c.BarFill != null) c.BarFill.localPosition = new Vector3((k - 1f) * 0.62f, 0f, -0.01f);
            }
        }

        private void UpdateCrystals(float dt)
        {
            Camera cam = Camera.main;
            for (int i = 0; i < _crystals.Count; i++)
            {
                CrystalView c = _crystals[i];
                if (c.Shake > 0f)
                {
                    c.Shake = Mathf.Max(0f, c.Shake - dt * 4f);
                    float a = c.Shake * 4f;
                    c.Body.localRotation = Quaternion.Euler(Mathf.Sin(_time * 70f) * a, 0f, Mathf.Cos(_time * 63f) * a);
                    c.Body.localScale = Vector3.one * (1f + c.Shake * 0.05f);
                }
                c.BarTimer = Mathf.Max(0f, c.BarTimer - dt);
                if (c.Bar != null)
                {
                    bool show = c.BarTimer > 0f && !c.Broken;
                    if (c.Bar.gameObject.activeSelf != show) c.Bar.gameObject.SetActive(show);
                    if (show && cam != null) c.Bar.rotation = Quaternion.LookRotation(c.Bar.position - cam.transform.position, Vector3.up);
                }
            }

            float floor = Arena.SanctuaryRadius.ToFloat();
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                Debris d = _debris[i];
                d.Age += dt;
                if (d.T == null || d.Age >= d.Life)
                {
                    if (d.T != null) Object.Destroy(d.T.gameObject);
                    _debris.RemoveAt(i);
                    continue;
                }
                d.Velocity += Vector3.down * 16f * dt;
                Vector3 p = d.T.localPosition + d.Velocity * dt;
                bool overFloor = new Vector2(p.x, p.z).magnitude < floor + 0.3f;
                if (overFloor && p.y < 0.05f && d.Velocity.y < 0f)
                {
                    p.y = 0.05f;
                    d.Velocity = new Vector3(d.Velocity.x * 0.55f, -d.Velocity.y * 0.3f, d.Velocity.z * 0.55f);
                    d.Spin *= 0.5f;
                }
                d.T.localPosition = p;
                d.T.Rotate(d.Spin * dt, Space.Self);
                float fade = Mathf.Clamp01((d.Life - d.Age) / 0.6f);
                d.T.localScale = Vector3.one * d.Scale * fade;
            }
        }

        /// <summary>The crystal falls apart: shards fly out, bounce on the floor (or drop off the edge) and fade.</summary>
        private void Shatter(CrystalView c)
        {
            Vector3 center = c.Holder.localPosition;
            for (int k = 0; k < 18; k++)
            {
                float a = k * 2.399f;
                float h = 0.35f + (k % 5) * 0.16f;
                var go = new GameObject("Crystal Shard");
                go.transform.SetParent(_mapRoot.transform, false);
                go.transform.localPosition = center + new Vector3(Mathf.Sin(a) * 0.4f, 0.4f + (k % 6) * 0.35f, Mathf.Cos(a) * 0.4f);
                go.transform.localRotation = Quaternion.Euler(k * 37f, k * 71f, k * 13f);
                go.AddComponent<MeshFilter>().sharedMesh = ShardMesh(h);
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _crystalMaterial;
                r.shadowCastingMode = ShadowCastingMode.On;
                float speed = 2.5f + (k % 4) * 1.2f;
                _debris.Add(new Debris
                {
                    T = go.transform,
                    Velocity = new Vector3(Mathf.Sin(a) * speed, 3f + (k % 3) * 2f, Mathf.Cos(a) * speed),
                    Spin = new Vector3(200f + k * 23f, 150f + k * 17f, 90f),
                    Life = 3.2f + (k % 4) * 0.4f,
                    Scale = 1f,
                });
            }
        }

        private static Mesh _shardSmall;
        private static Mesh _shardLarge;

        private static Mesh ShardMesh(float height)
        {
            if (height < 0.6f)
            {
                if (_shardSmall == null) _shardSmall = CrystalMesh(0.1f, 0.45f);
                return _shardSmall;
            }
            if (_shardLarge == null) _shardLarge = CrystalMesh(0.15f, 0.8f);
            return _shardLarge;
        }

        /// <summary>A small health bar over the crystal, shown for a few seconds after it is hit.</summary>
        private CrystalBarResult HealthBarParts(Transform holder)
        {
            var bar = new GameObject("Health Bar").transform;
            bar.SetParent(holder, false);
            bar.localPosition = new Vector3(0f, 3.3f, 0f);
            GameObject back = MaterialFactory.Primitive(PrimitiveType.Quad, bar, MaterialFactory.Glow(new Color(0.05f, 0.03f, 0.08f)), "Back");
            back.transform.localScale = new Vector3(1.3f, 0.14f, 1f);
            back.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var fillPivot = new GameObject("Fill").transform;
            fillPivot.SetParent(bar, false);
            fillPivot.localScale = Vector3.one;
            GameObject fill = MaterialFactory.Primitive(PrimitiveType.Quad, fillPivot, MaterialFactory.Glow(new Color(1.6f, 0.7f, 2.6f)), "Fill Quad");
            fill.transform.localScale = new Vector3(1.24f, 0.09f, 1f);
            fill.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            bar.gameObject.SetActive(false);
            return new CrystalBarResult { Bar = bar, Fill = fillPivot };
        }

        private struct CrystalBarResult
        {
            public Transform Bar;
            public Transform Fill;
        }

        // ------------------------------------------------------------------ Gate portal, waterfalls

        private void BuildPortal(Transform scene)
        {
            Transform anchor = FindTransform(scene, "Anchor_Portal");
            if (anchor != null)
            {
                var holder = new GameObject("Gate Portal").transform;
                holder.SetParent(_mapRoot.transform, false);
                holder.position = anchor.position;
                holder.rotation = anchor.rotation;
                Vector2 glowSize = new Vector2(6.5f, 7.5f);
                float sigilSize = 3.6f;
                Bounds gb;
                if (_gateModel != null && RendererBounds(_gateModel.transform, out gb))
                {
                    // The Modly gate's archway: about 17% of its width and half its height, centered low in the middle.
                    holder.position = new Vector3(gb.center.x, gb.min.y + gb.size.y * 0.31f, gb.center.z);
                    glowSize = new Vector2(gb.size.x * 0.2f, gb.size.y * 0.5f);
                    sigilSize = gb.size.x * 0.15f;
                }

                _portalMaterial = MaterialFactory.Additive(PortalColor);
                _portalMaterial.mainTexture = MaterialFactory.SoftDot;
                _portalMaterial.SetTexture("_BaseMap", MaterialFactory.SoftDot);
                GameObject glow = MaterialFactory.Primitive(PrimitiveType.Quad, holder, _portalMaterial, "Portal Glow");
                glow.transform.localScale = new Vector3(glowSize.x, glowSize.y, 1f);
                glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                Texture2D rune = Resources.Load<Texture2D>(SanctuaryFolder + "Rune");
                if (rune != null)
                {
                    Material m = MaterialFactory.Additive(new Color(1.0f, 0.75f, 2.2f));
                    m.mainTexture = rune;
                    m.SetTexture("_BaseMap", rune);
                    GameObject sigil = MaterialFactory.Primitive(PrimitiveType.Quad, holder, m, "Portal Sigil");
                    sigil.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                    sigil.transform.localScale = Vector3.one * sigilSize;
                    sigil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    _spinning.Add(sigil.transform);
                }

                var lightGo = new GameObject("Portal Light");
                lightGo.transform.SetParent(holder, false);
                lightGo.transform.localPosition = new Vector3(0f, 0f, 1.5f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.7f, 0.55f, 1f);
                light.range = 12f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
                _pulses.Add(new Pulse { Light = light, Intensity = light.intensity, Phase = 0.5f });
                Sparkles(holder, new Color(1.2f, 1f, 3f), new Vector3(5f, 6f, 0.5f), 10f, 2.5f);
            }

            Transform window = FindTransform(scene, "Anchor_GateWindow");
            if (window != null)
            {
                var lightGo = new GameObject("Gate Window Light");
                lightGo.transform.SetParent(_mapRoot.transform, false);
                lightGo.transform.position = window.position;
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.75f, 0.45f);
                light.range = 9f;
                light.intensity = 2f;
                light.shadows = LightShadows.None;
            }
        }

        private void BuildWaterfalls(Transform scene)
        {
            Texture2D streaks = WaterTexture();
            foreach (Transform t in scene.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Anchor_Waterfall_")) continue;
                float length = 18f, width = 2.5f;
                foreach (string piece in t.name.Split('_'))
                {
                    float value;
                    if (piece.Length > 1 && piece[0] == 'L' && float.TryParse(piece.Substring(1), out value)) length = value;
                    if (piece.Length > 1 && piece[0] == 'W' && float.TryParse(piece.Substring(1), out value)) width = value;
                }
                var holder = new GameObject("Waterfall").transform;
                holder.SetParent(_mapRoot.transform, false);
                holder.position = t.position;
                holder.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized, Vector3.up);

                Material water = MaterialFactory.Additive(new Color(0.55f, 0.72f, 1.0f, 0.9f));
                water.mainTexture = streaks;
                water.SetTexture("_BaseMap", streaks);
                water.mainTextureScale = new Vector2(1f, length / 6f);
                water.SetTextureScale("_BaseMap", new Vector2(1f, length / 6f));
                _waterMaterials.Add(water);

                var go = new GameObject("Water");
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = WaterfallMesh(width, length);
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = water;
                mr.shadowCastingMode = ShadowCastingMode.Off;

                var mist = new GameObject("Mist").transform;
                mist.SetParent(holder, false);
                mist.localPosition = new Vector3(0f, -length * 0.85f, 1.5f);
                CloudPuffs(mist, new Color(1f, 1f, 1f, 0.35f), new Vector3(width * 2f, 2f, 3f), 4f, 6f, 3f, 7f, ParticleSystemRenderMode.Billboard, 40);
            }
        }

        /// <summary>A strip that leaves the cliff edge with a small arc and falls straight down, widening a little.</summary>
        private static Mesh WaterfallMesh(float width, float length)
        {
            const int rows = 18;
            var v = new Vector3[(rows + 1) * 2];
            var uv = new Vector2[(rows + 1) * 2];
            var t = new int[rows * 6];
            for (int i = 0; i <= rows; i++)
            {
                float k = i / (float)rows;
                float y = -k * length;
                float z = 0.6f + 1.4f * Mathf.Sqrt(Mathf.Min(1f, k * 6f));
                float w = width * (1f + 0.35f * k) * 0.5f;
                v[i * 2] = new Vector3(-w, y, z);
                v[i * 2 + 1] = new Vector3(w, y, z);
                uv[i * 2] = new Vector2(0f, 1f - k);
                uv[i * 2 + 1] = new Vector2(1f, 1f - k);
            }
            for (int i = 0; i < rows; i++)
            {
                int a = i * 2;
                t[i * 6] = a;
                t[i * 6 + 1] = a + 2;
                t[i * 6 + 2] = a + 1;
                t[i * 6 + 3] = a + 1;
                t[i * 6 + 4] = a + 2;
                t[i * 6 + 5] = a + 3;
            }
            var mesh = new Mesh { name = "Waterfall" };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D WaterTexture()
        {
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Water Streaks", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
                    float streak = Mathf.PerlinNoise(x * 0.35f, y * 0.02f) * 0.7f + Mathf.PerlinNoise(x * 0.9f, y * 0.06f) * 0.5f;
                    float a = Mathf.Clamp01((streak - 0.35f) * 1.6f) * edge;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Sky islands, clouds, petals

        private void BuildFarIslands()
        {
            GameObject prefab = Resources.Load<GameObject>(ModelFolder + "FloatingIsland");
            if (prefab == null) prefab = Resources.Load<GameObject>("Arena/Island/floating_island_2k");
            if (prefab == null) return;
            AddSkyIsland(prefab, 35f, 78f, 18f, 14f);
            AddSkyIsland(prefab, 108f, 92f, 6f, 20f);
            AddSkyIsland(prefab, 150f, 70f, 26f, 10f);
            AddSkyIsland(prefab, 212f, 96f, 12f, 22f);
            AddSkyIsland(prefab, 252f, 82f, 30f, 12f);
            AddSkyIsland(prefab, 298f, 104f, 8f, 24f);
            AddSkyIsland(prefab, 332f, 66f, 22f, 9f);
        }

        private void BuildCloudSea()
        {
            // Flat puffs forming the sea of clouds under the arena.
            var sea = new GameObject("Cloud Sea").transform;
            sea.SetParent(_mapRoot.transform, false);
            sea.localPosition = new Vector3(0f, -11f, 0f);
            CloudPuffs(sea, new Color(1f, 0.93f, 0.97f, 0.6f), new Vector3(420f, 6f, 420f), 26f, 70f, 70f, 110f, ParticleSystemRenderMode.HorizontalBillboard, 420);

            // Tall soft banks far away to blur the horizon.
            var banks = new GameObject("Cloud Banks").transform;
            banks.SetParent(_mapRoot.transform, false);
            banks.localPosition = new Vector3(0f, -4f, 0f);
            ParticleSystem bankPs = CloudPuffs(banks, new Color(1f, 0.9f, 0.96f, 0.4f), Vector3.zero, 45f, 95f, 80f, 120f, ParticleSystemRenderMode.Billboard, 120);
            ParticleSystem.ShapeModule shape = bankPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 230f;
            shape.radiusThickness = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // A plain floor far below so nothing shows through the gaps between clouds.
            GameObject below = MaterialFactory.Primitive(PrimitiveType.Quad, _mapRoot.transform, MaterialFactory.Glow(new Color(0.78f, 0.67f, 0.86f)), "Cloud Floor");
            below.transform.localPosition = new Vector3(0f, -24f, 0f);
            below.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            below.transform.localScale = Vector3.one * 1400f;
            below.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private ParticleSystem CloudPuffs(Transform parent, Color color, Vector3 box, float minSize, float maxSize, float minLife, float maxLife, ParticleSystemRenderMode mode, int count)
        {
            var go = new GameObject("Clouds");
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = maxLife;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = count / ((minLife + maxLife) * 0.5f);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = ParticleAlpha(Color.white, CloudTexture);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 4f;

            ps.Play();
            return ps;
        }

        private void BuildPetals()
        {
            var go = new GameObject("Cherry Petals");
            go.transform.SetParent(_mapRoot.transform, false);
            go.transform.localPosition = new Vector3(0f, 13f, 4f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 14f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.86f), new Color(1f, 0.85f, 0.93f));
            main.maxParticles = 320;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 20f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(56f, 2f, 56f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            vel.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.25f;

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ParticleAlpha(Color.white, PetalTexture);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
        }

        private GameObject Sparkles(Transform parent, Color hdr, Vector3 box, float rate, float life)
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = hdr;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.ParticleAdditive(hdr);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return go;
        }

        private static Material ParticleAlpha(Color color, Texture2D tex)
        {
            Shader shader = MaterialFactory.FindShader("Universal Render Pipeline/Particles/Unlit", "Particles_Additive");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            return m;
        }

        private static Texture2D _cloudTexture;
        private static Texture2D _petalTexture;

        /// <summary>A soft, lumpy white puff.</summary>
        private static Texture2D CloudTexture
        {
            get
            {
                if (_cloudTexture != null) return _cloudTexture;
                const int size = 128;
                _cloudTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Cloud Puff", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float n = Mathf.PerlinNoise(x * 0.05f + 3f, y * 0.05f + 7f) * 0.6f + Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.4f;
                        float a = Mathf.Clamp01(1f - r / (0.55f + 0.45f * n));
                        a = a * a * (3f - 2f * a);
                        float shade = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(0.5f - dy * 0.5f + n * 0.3f));
                        byte c = (byte)(255f * shade);
                        px[y * size + x] = new Color32(c, c, (byte)Mathf.Min(255f, c * 1.02f), (byte)(a * 255f));
                    }
                }
                _cloudTexture.SetPixels32(px);
                _cloudTexture.Apply();
                return _cloudTexture;
            }
        }

        private static Texture2D PetalTexture
        {
            get
            {
                if (_petalTexture != null) return _petalTexture;
                const int size = 32;
                _petalTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Petal", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = ((x + 0.5f) / size * 2f - 1f) / 0.55f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float notch = dy > 0.6f && Mathf.Abs(dx) < 0.25f ? 0f : 1f;
                        float a = Mathf.Clamp01((1f - (dx * dx + dy * dy)) * 4f) * notch;
                        px[y * size + x] = new Color32(255, (byte)(225 + 30 * dy), 240, (byte)(a * 255f));
                    }
                }
                _petalTexture.SetPixels32(px);
                _petalTexture.Apply();
                return _petalTexture;
            }
        }

        // ------------------------------------------------------------------ Sky and light

        private void SetupSanctuaryLighting()
        {
            var keyGo = new GameObject("Key Light");
            keyGo.transform.SetParent(_mapRoot.transform, false);
            keyGo.transform.rotation = Quaternion.Euler(44f, -28f, 0f);
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.9f, 0.84f);
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;

            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(_mapRoot.transform, false);
            rimGo.transform.rotation = Quaternion.Euler(16f, 165f, 0f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(1f, 0.68f, 0.88f);
            rim.intensity = 0.55f;
            rim.shadows = LightShadows.None;

            var bounceGo = new GameObject("Cloud Bounce Light");
            bounceGo.transform.SetParent(_mapRoot.transform, false);
            bounceGo.transform.rotation = Quaternion.Euler(-65f, 30f, 0f);
            Light bounce = bounceGo.AddComponent<Light>();
            bounce.type = LightType.Directional;
            bounce.color = new Color(0.75f, 0.65f, 0.95f);
            bounce.intensity = 0.3f;
            bounce.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.41f, 0.60f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.43f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.22f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.68f, 0.88f);
            // Clear air over the courtyard and the gate; haze only on the far islands, cliffs and mountains.
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 520f;
            RenderSettings.skybox = SkyMaterial();
            if (RenderSettings.skybox == null) UsesSkybox = false;

            var volumeGo = new GameObject("Post Processing");
            volumeGo.transform.SetParent(_mapRoot.transform, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.96f));
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.saturation.Override(14f);
            color.contrast.Override(14f);
            color.colorFilter.Override(new Color(1f, 0.97f, 1f));
            volume.sharedProfile = profile;
        }

        /// <summary>A panoramic sky: deep violet above, pink and lavender at the horizon, with soft cloud streaks.</summary>
        private static Material SkyMaterial()
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Material keep = Resources.Load<Material>(MaterialFactory.ShaderKeepFolder + "/Skybox_Panoramic");
                if (keep != null) shader = keep.shader;
            }
            if (shader == null) return null;

            const int w = 1024, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Sanctuary Sky", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            float[] stops = { -90f, -6f, 0f, 7f, 22f, 50f, 90f };
            Color[] cols =
            {
                new Color(0.62f, 0.52f, 0.74f), new Color(0.86f, 0.74f, 0.90f), new Color(0.99f, 0.82f, 0.88f),
                new Color(0.92f, 0.68f, 0.86f), new Color(0.60f, 0.42f, 0.76f), new Color(0.32f, 0.22f, 0.54f), new Color(0.18f, 0.12f, 0.38f),
            };
            for (int y = 0; y < h; y++)
            {
                float e = (y + 0.5f) / h * 180f - 90f;
                Color c = cols[cols.Length - 1];
                for (int k = 0; k < stops.Length - 1; k++)
                {
                    if (e < stops[k + 1])
                    {
                        float f = Mathf.InverseLerp(stops[k], stops[k + 1], e);
                        c = Color.Lerp(cols[k], cols[k + 1], Mathf.SmoothStep(0f, 1f, f));
                        break;
                    }
                }
                for (int x = 0; x < w; x++)
                {
                    Color p = c;
                    if (e > 1f && e < 30f)
                    {
                        float band = Mathf.Clamp01(1f - Mathf.Abs(e - 12f) / 18f);
                        float n = Mathf.PerlinNoise(x * 0.012f, y * 0.09f) * 0.7f + Mathf.PerlinNoise(x * 0.04f + 9f, y * 0.25f) * 0.3f;
                        float cloud = Mathf.Clamp01((n - 0.45f) * 2.4f) * band;
                        p = Color.Lerp(p, new Color(1f, 0.88f, 0.94f), cloud * 0.55f);
                    }
                    px[y * w + x] = p;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            var m = new Material(shader) { name = "Sanctuary Sky" };
            m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Mapping")) m.SetFloat("_Mapping", 1f);
            if (m.HasProperty("_ImageType")) m.SetFloat("_ImageType", 0f);
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1f);
            if (m.HasProperty("_Rotation")) m.SetFloat("_Rotation", 0f);
            return m;
        }
    }
}
