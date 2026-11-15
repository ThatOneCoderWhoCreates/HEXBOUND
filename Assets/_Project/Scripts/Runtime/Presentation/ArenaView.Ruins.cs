using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Sky Ruins: a long, cracked stone courtyard on a mountain top, with raised spawn platforms, corner terraces
    /// (two with ruined temples), broken walls and columns for cover and a collapsed altar in the middle. Rock spires
    /// rise out of the mist around it and snowy peaks stand far away.
    /// The static scene is one model, Resources/Arena/Ruins/Ruins.glb, built from the simulation's TerrainBox list so
    /// every wall, platform and stair is exactly where the game logic has it (Tools/RuinsBuilder). This code adds the
    /// sky, light, mist, drifting dust and optional Modly models from Resources/Arena/Ruins/Models: Arch (framing the
    /// spawn and side exits), Temple (on a neighboring peak), RuinedWall and Column (scenery outside the edge).
    /// If Ruins.glb is missing, plain blocks are built from the same TerrainBox list so the map is still playable.
    /// </summary>
    public sealed partial class ArenaView
    {
        private const string RuinsFolder = "Arena/Ruins/";
        private const string RuinsModelFolder = "Arena/Ruins/Models/";

        private readonly List<Transform> _drifting = new List<Transform>();
        private readonly List<Vector3> _driftBase = new List<Vector3>();

        /// <summary>A breakable structure (temple or walkway): its scenery node, health bar and collapse state.</summary>
        private sealed class StructureView
        {
            public GameObject Node;
            public Bounds Bounds;
            public int Health = -1;
            public bool Collapsed;
            public float BarTimer;
            public float Shake;
            public Transform Bar;
            public Transform BarFill;
            public Vector3 Rest;
        }

        private sealed class Chunk
        {
            public Transform T;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Age;
            public float Ground;
            public bool Resting;
        }

        private readonly StructureView[] _structures = new StructureView[Arena.MaxStructures];
        private readonly List<Chunk> _chunks = new List<Chunk>();
        private Material _chunkMaterial;

        private void ClearRuins()
        {
            _drifting.Clear();
            _driftBase.Clear();
            for (int i = 0; i < _structures.Length; i++) _structures[i] = null;
            _chunks.Clear();
        }

        private void BuildRuins()
        {
            UsesSkybox = true;
            BackgroundColor = new Color(0.70f, 0.75f, 0.82f);

            GameObject prefab = Resources.Load<GameObject>(RuinsFolder + "Ruins");
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab, _mapRoot.transform, false);
                instance.name = "Ruins Model";
                RemoveColliders(instance);
                foreach (Animator a in instance.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.transform.name + " " + (r.transform.parent != null ? r.transform.parent.name : "");
                    bool far = n.Contains("Mountains") || n.Contains("Spires");
                    r.shadowCastingMode = far ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    r.receiveShadows = !n.Contains("Mountains");
                }
                for (int g = 0; g < _structures.Length; g++)
                {
                    Transform node = FindTransform(instance.transform, "Structure " + g);
                    if (node != null) AddStructure(g, node.gameObject);
                }
            }
            else
            {
                BuildRuinsFallback();
            }

            AddRuinsModels();
            BuildRuinsMist();
            BuildRuinsDust();
            SetupRuinsLighting();
        }

        private void UpdateRuins(float dt)
        {
            UpdateStructures(dt);
            for (int i = 0; i < _drifting.Count; i++)
            {
                _drifting[i].localPosition = _driftBase[i] + new Vector3(Mathf.Sin(_time * 0.05f + i) * 6f, 0f, Mathf.Cos(_time * 0.04f + i * 1.3f) * 4f);
            }
        }

        // ------------------------------------------------------------------ Fallback (no Ruins.glb)

        private void BuildRuinsFallback()
        {
            Material stone = MaterialFactory.Lit(new Color(0.55f, 0.5f, 0.43f), 0.1f);
            Material floor = MaterialFactory.Lit(new Color(0.47f, 0.44f, 0.39f), 0.1f);

            // Floor: a fan following the simulation's edge.
            const int seg = 144;
            var v = new Vector3[seg + 1];
            var t = new int[seg * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                int angle = i * FixMath.FullTurn / seg;
                float r = Arena.EdgeRadius(Arena.Layout(Arena.MapRuins), angle).ToFloat();
                float a = i * Mathf.PI * 2f / seg;
                v[i + 1] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                t[i * 3] = 0; t[i * 3 + 1] = i + 1; t[i * 3 + 2] = (i + 1) % seg + 1;
            }
            var mesh = new Mesh { name = "Ruins Floor", vertices = v, triangles = t };
            mesh.RecalculateNormals();
            var go = new GameObject("Ruins Floor");
            go.transform.SetParent(_mapRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = floor;

            TerrainBox[] boxes = Arena.Layout(Arena.MapRuins).Terrain;
            for (int i = 0; i < boxes.Length; i++)
            {
                TerrainBox b = boxes[i];
                float x0 = b.MinX.ToFloat(), x1 = b.MaxX.ToFloat(), z0 = b.MinZ.ToFloat(), z1 = b.MaxZ.ToFloat(), h = b.Height.ToFloat();
                if (b.Ramp == 0)
                {
                    float y0 = b.Bottom.ToFloat();
                    GameObject cube = MaterialFactory.Primitive(PrimitiveType.Cube, _mapRoot.transform, stone, "Block " + i);
                    cube.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + h) * 0.5f, (z0 + z1) * 0.5f);
                    cube.transform.localScale = new Vector3(x1 - x0, h - y0, z1 - z0);
                    if (b.Group >= 0) AddFallbackStructurePart(b.Group, cube);
                    continue;
                }
                const int steps = 8;
                for (int k = 0; k < steps; k++)
                {
                    float f0 = (float)k / steps;
                    float y = h * (k + 1) / steps;
                    float sx0 = x0, sx1 = x1, sz0 = z0, sz1 = z1;
                    if (b.Ramp == 1) sx0 = x0 + (x1 - x0) * f0;
                    else if (b.Ramp == 2) sx1 = x1 - (x1 - x0) * f0;
                    else if (b.Ramp == 3) sz0 = z0 + (z1 - z0) * f0;
                    else sz1 = z1 - (z1 - z0) * f0;
                    GameObject step = MaterialFactory.Primitive(PrimitiveType.Cube, _mapRoot.transform, stone, "Step " + i + "." + k);
                    step.transform.localPosition = new Vector3((sx0 + sx1) * 0.5f, y * 0.5f, (sz0 + sz1) * 0.5f);
                    step.transform.localScale = new Vector3(sx1 - sx0, y, sz1 - sz0);
                    if (b.Group >= 0) AddFallbackStructurePart(b.Group, step);
                }
            }
        }

        // ------------------------------------------------------------------ Optional Modly models

        private void AddRuinsModels()
        {
            GameObject arch = Resources.Load<GameObject>(RuinsModelFolder + "Arch");
            if (arch != null)
            {
                // At the foot of the stairs down the mountain behind each spawn and of the paths out of the side alcoves.
                PlaceModel(arch, new Vector3(0f, -7f, 50.5f), Quaternion.LookRotation(Vector3.back), 7.5f, false, true);
                PlaceModel(arch, new Vector3(0f, -7f, -50.5f), Quaternion.LookRotation(Vector3.forward), 7.5f, false, true);
                PlaceModel(arch, new Vector3(39.5f, -7f, 0f), Quaternion.LookRotation(Vector3.left), 6.5f, false, true);
                PlaceModel(arch, new Vector3(-39.5f, -7f, 0f), Quaternion.LookRotation(Vector3.right), 6.5f, false, true);
            }

            GameObject temple = Resources.Load<GameObject>(RuinsModelFolder + "Temple");
            if (temple != null)
            {
                // A larger temple on a neighboring peak, and a small one far across the mist.
                PlaceModel(temple, new Vector3(-60f, -8.5f, 85f), Quaternion.Euler(0f, 160f, 0f), 13f, false, true);
                PlaceModel(temple, new Vector3(95f, -15.5f, -70f), Quaternion.Euler(0f, -40f, 0f), 10f, false, false);
            }

            GameObject wall = Resources.Load<GameObject>(RuinsModelFolder + "RuinedWall");
            if (wall != null)
            {
                float[] angles = { 35f, 140f, 215f, 320f };
                for (int i = 0; i < angles.Length; i++)
                {
                    float a = angles[i] * Mathf.Deg2Rad;
                    int simAngle = Mathf.RoundToInt(angles[i] / 360f * FixMath.FullTurn);
                    float r = Arena.EdgeRadius(Arena.Layout(Arena.MapRuins), simAngle).ToFloat() + 2.2f;
                    var p = new Vector3(Mathf.Sin(a) * r, -0.4f, Mathf.Cos(a) * r);
                    PlaceModel(wall, p, Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.z)), 2.6f, false, true);
                }
            }

            GameObject column = Resources.Load<GameObject>(RuinsModelFolder + "Column");
            if (column != null)
            {
                float[] angles = { 58f, 122f, 238f, 302f };
                for (int i = 0; i < angles.Length; i++)
                {
                    float a = angles[i] * Mathf.Deg2Rad;
                    int simAngle = Mathf.RoundToInt(angles[i] / 360f * FixMath.FullTurn);
                    float r = Arena.EdgeRadius(Arena.Layout(Arena.MapRuins), simAngle).ToFloat() + 1.6f;
                    PlaceModel(column, new Vector3(Mathf.Sin(a) * r, -0.3f, Mathf.Cos(a) * r), Quaternion.Euler(0f, i * 70f, 0f), 5.5f, false, true);
                }
            }
        }

        // ------------------------------------------------------------------ Mist and dust

        private void BuildRuinsMist()
        {
            // A sea of mist between the spires, below the courtyard.
            var sea = new GameObject("Mist Sea").transform;
            sea.SetParent(_mapRoot.transform, false);
            sea.localPosition = new Vector3(0f, -16f, 0f);
            CloudPuffs(sea, new Color(0.93f, 0.95f, 0.98f, 0.55f), new Vector3(520f, 8f, 520f), 30f, 80f, 70f, 110f, ParticleSystemRenderMode.HorizontalBillboard, 420);

            // Slow wisps drifting past the cliffs just below the edge (never over the courtyard).
            var wisps = new GameObject("Mist Wisps").transform;
            wisps.SetParent(_mapRoot.transform, false);
            wisps.localPosition = new Vector3(0f, -6f, 0f);
            ParticleSystem ps = CloudPuffs(wisps, new Color(0.95f, 0.96f, 1f, 0.32f), Vector3.one, 10f, 22f, 40f, 70f, ParticleSystemRenderMode.Billboard, 70);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.scale = Vector3.one;
            shape.radius = 95f;
            shape.radiusThickness = 0.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);

            // Far banks to soften the horizon under the mountains.
            var banks = new GameObject("Mist Banks").transform;
            banks.SetParent(_mapRoot.transform, false);
            banks.localPosition = new Vector3(0f, -10f, 0f);
            ParticleSystem bankPs = CloudPuffs(banks, new Color(0.9f, 0.92f, 0.96f, 0.4f), Vector3.one, 60f, 120f, 80f, 120f, ParticleSystemRenderMode.Billboard, 110);
            ParticleSystem.ShapeModule bankShape = bankPs.shape;
            bankShape.shapeType = ParticleSystemShapeType.Circle;
            bankShape.scale = Vector3.one;
            bankShape.radius = 240f;
            bankShape.radiusThickness = 0.3f;
            bankShape.rotation = new Vector3(90f, 0f, 0f);
            bankPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bankPs.Clear(true);
            bankPs.Play(true);

            GameObject below = MaterialFactory.Primitive(PrimitiveType.Quad, _mapRoot.transform, MaterialFactory.Glow(new Color(0.78f, 0.81f, 0.86f)), "Mist Floor");
            below.transform.localPosition = new Vector3(0f, -30f, 0f);
            below.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            below.transform.localScale = Vector3.one * 1600f;
            below.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private void BuildRuinsDust()
        {
            // Fine dust and a few dry leaves carried across the courtyard by the mountain wind.
            var go = new GameObject("Wind Dust");
            go.transform.SetParent(_mapRoot.transform, false);
            go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new Color(0.85f, 0.78f, 0.62f, 0.55f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 50f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 4f, 72f);
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleAlpha(Color.white, CloudTexture);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
        }

        // ------------------------------------------------------------------ Breakable structures

        private void AddStructure(int g, GameObject node)
        {
            Bounds b;
            if (!RendererBounds(node.transform, out b)) return;
            var v = new StructureView { Node = node, Bounds = b, Rest = node.transform.localPosition };
            BuildStructureBar(v);
            _structures[g] = v;
        }

        private readonly Dictionary<int, GameObject> _fallbackGroups = new Dictionary<int, GameObject>();

        private void AddFallbackStructurePart(int g, GameObject part)
        {
            GameObject node;
            if (!_fallbackGroups.TryGetValue(g, out node))
            {
                node = new GameObject("Structure " + g);
                node.transform.SetParent(_mapRoot.transform, false);
                _fallbackGroups[g] = node;
            }
            part.transform.SetParent(node.transform, true);
            Bounds b;
            if (RendererBounds(node.transform, out b))
            {
                if (_structures[g] == null) { _structures[g] = new StructureView { Node = node, Bounds = b, Rest = node.transform.localPosition }; BuildStructureBar(_structures[g]); }
                else _structures[g].Bounds = b;
            }
        }

        private void BuildStructureBar(StructureView v)
        {
            var bar = new GameObject("Health Bar").transform;
            bar.SetParent(_mapRoot.transform, false);
            bar.position = new Vector3(v.Bounds.center.x, v.Bounds.max.y + 1.2f, v.Bounds.center.z);
            GameObject back = MaterialFactory.Primitive(PrimitiveType.Quad, bar, MaterialFactory.Glow(new Color(0.08f, 0.06f, 0.05f)), "Back");
            back.transform.localScale = new Vector3(3.2f, 0.32f, 1f);
            GameObject fill = MaterialFactory.Primitive(PrimitiveType.Quad, bar, MaterialFactory.Glow(new Color(1.6f, 1.1f, 0.45f)), "Fill");
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fill.transform.localScale = new Vector3(3.0f, 0.2f, 1f);
            foreach (Renderer r in bar.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            v.Bar = bar;
            v.BarFill = fill.transform;
            bar.gameObject.SetActive(false);
        }

        /// <summary>Health bars, shaking when hit, and the collapse when a structure breaks (or its return in a new round).</summary>
        private void SyncStructures(GameState s)
        {
            int max = Arena.Layout(Arena.MapRuins).StructureHealth;
            for (int g = 0; g < _structures.Length; g++)
            {
                StructureView v = _structures[g];
                if (v == null) continue;
                int hp = s != null && s.Setup.Map == Arena.MapRuins ? s.StructureHealth[g] : max;
                if (v.Health >= 0 && hp < v.Health && hp > 0)
                {
                    v.BarTimer = 3f;
                    v.Shake = 0.6f;
                }
                if (hp <= 0 && !v.Collapsed) Collapse(v);
                else if (hp > 0 && v.Collapsed)
                {
                    v.Collapsed = false;
                    v.Node.SetActive(true);
                    ClearChunks();
                }
                v.Health = hp;
                if (v.BarFill != null)
                {
                    float k = Mathf.Clamp01(hp / (float)Mathf.Max(1, max));
                    v.BarFill.localScale = new Vector3(3.0f * k, 0.2f, 1f);
                    v.BarFill.localPosition = new Vector3(-1.5f * (1f - k), 0f, -0.01f);
                }
            }
        }

        private void UpdateStructures(float dt)
        {
            Camera cam = Camera.main;
            for (int g = 0; g < _structures.Length; g++)
            {
                StructureView v = _structures[g];
                if (v == null) continue;
                v.BarTimer -= dt;
                bool showBar = v.BarTimer > 0f && !v.Collapsed;
                if (v.Bar != null)
                {
                    if (v.Bar.gameObject.activeSelf != showBar) v.Bar.gameObject.SetActive(showBar);
                    if (showBar && cam != null) v.Bar.rotation = Quaternion.LookRotation(v.Bar.position - cam.transform.position);
                }
                if (v.Shake > 0f && !v.Collapsed)
                {
                    v.Shake = Mathf.MoveTowards(v.Shake, 0f, dt * 2.5f);
                    v.Node.transform.localPosition = v.Rest + new Vector3(Mathf.Sin(_time * 70f), 0f, Mathf.Cos(_time * 61f)) * 0.04f * v.Shake;
                }
            }

            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                Chunk c = _chunks[i];
                c.Age += dt;
                if (!c.Resting)
                {
                    c.Velocity += Vector3.down * 22f * dt;
                    c.T.position += c.Velocity * dt;
                    c.T.Rotate(c.Spin * dt, Space.World);
                    float bottom = c.Ground + c.T.localScale.y * 0.4f;
                    if (c.T.position.y < bottom)
                    {
                        Vector3 p = c.T.position;
                        p.y = bottom;
                        c.T.position = p;
                        if (c.Velocity.y < -3f)
                        {
                            c.Velocity = new Vector3(c.Velocity.x * 0.4f, -c.Velocity.y * 0.25f, c.Velocity.z * 0.4f);
                            c.Spin *= 0.4f;
                        }
                        else c.Resting = true;
                    }
                }
                // Rubble stays for a while, then sinks away.
                if (c.Age > 9f) c.T.position += Vector3.down * dt * 0.5f;
                if (c.Age > 12f)
                {
                    Object.Destroy(c.T.gameObject);
                    _chunks.RemoveAt(i);
                }
            }
        }

        private void Collapse(StructureView v)
        {
            v.Collapsed = true;
            v.Node.SetActive(false);
            if (v.Bar != null) v.Bar.gameObject.SetActive(false);
            if (_chunkMaterial == null) _chunkMaterial = MaterialFactory.Lit(new Color(0.55f, 0.5f, 0.42f), 0.08f);
            Bounds b = v.Bounds;
            int count = Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.z * 0.6f), 24, 70);
            for (int i = 0; i < count; i++)
            {
                GameObject go = MaterialFactory.Primitive(PrimitiveType.Cube, _mapRoot.transform, _chunkMaterial, "Rubble");
                Object.Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                var p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.center.y, b.max.y), Random.Range(b.min.z, b.max.z));
                go.transform.position = p;
                go.transform.rotation = Random.rotation;
                float size = Random.Range(0.35f, 1.1f);
                go.transform.localScale = new Vector3(size * Random.Range(0.8f, 1.6f), size * Random.Range(0.5f, 1f), size * Random.Range(0.8f, 1.4f));
                FixVec3 fp = new FixVec3(Fix.FromRaw((long)(p.x * 65536f)), Fix.FromInt(10), Fix.FromRaw((long)(p.z * 65536f)));
                float ground = Arena.IsInside(fp) ? Arena.GroundAt(fp).ToFloat() : -30f;
                _chunks.Add(new Chunk
                {
                    T = go.transform,
                    Velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-1f, 2f), Random.Range(-2.5f, 2.5f)),
                    Spin = Random.insideUnitSphere * 220f,
                    Ground = ground,
                });
            }
        }

        private void ClearChunks()
        {
            foreach (Chunk c in _chunks) if (c.T != null) Object.Destroy(c.T.gameObject);
            _chunks.Clear();
        }

        // ------------------------------------------------------------------ Light, sky, post-processing

        private void SetupRuinsLighting()
        {
            // Late afternoon sun from the south-west: long shadows across the flagstones.
            var keyGo = new GameObject("Sun");
            keyGo.transform.SetParent(_mapRoot.transform, false);
            keyGo.transform.rotation = Quaternion.Euler(38f, 52f, 0f);
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.88f, 0.72f);
            key.intensity = 1.45f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.85f;

            var skyGo = new GameObject("Sky Fill");
            skyGo.transform.SetParent(_mapRoot.transform, false);
            skyGo.transform.rotation = Quaternion.Euler(60f, -130f, 0f);
            Light fill = skyGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.62f, 0.72f, 0.95f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;

            var bounceGo = new GameObject("Mist Bounce");
            bounceGo.transform.SetParent(_mapRoot.transform, false);
            bounceGo.transform.rotation = Quaternion.Euler(-60f, 20f, 0f);
            Light bounce = bounceGo.AddComponent<Light>();
            bounce.type = LightType.Directional;
            bounce.color = new Color(0.85f, 0.86f, 0.9f);
            bounce.intensity = 0.2f;
            bounce.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.49f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.78f, 0.84f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 560f;
            RenderSettings.skybox = RuinsSkyMaterial();
            if (RenderSettings.skybox == null) UsesSkybox = false;

            var volumeGo = new GameObject("Post Processing");
            volumeGo.transform.SetParent(_mapRoot.transform, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.1f);
            color.saturation.Override(8f);
            color.contrast.Override(16f);
            color.colorFilter.Override(new Color(1f, 0.98f, 0.95f));
            ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));
            volume.sharedProfile = profile;
        }

        /// <summary>Clear mountain sky: deep blue above, pale haze at the horizon, a band of high thin clouds.</summary>
        private static Material RuinsSkyMaterial()
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Material keep = Resources.Load<Material>(MaterialFactory.ShaderKeepFolder + "/Skybox_Panoramic");
                if (keep != null) shader = keep.shader;
            }
            if (shader == null) return null;

            const int w = 1024, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Ruins Sky", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            float[] stops = { -90f, -5f, 0f, 6f, 20f, 50f, 90f };
            Color[] cols =
            {
                new Color(0.66f, 0.70f, 0.76f), new Color(0.80f, 0.83f, 0.87f), new Color(0.88f, 0.88f, 0.86f),
                new Color(0.76f, 0.82f, 0.90f), new Color(0.52f, 0.65f, 0.84f), new Color(0.32f, 0.47f, 0.74f), new Color(0.22f, 0.36f, 0.64f),
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
                    if (e > 3f && e < 45f)
                    {
                        float band = Mathf.Clamp01(1f - Mathf.Abs(e - 20f) / 25f);
                        float n = Mathf.PerlinNoise(x * 0.01f, y * 0.06f) * 0.65f + Mathf.PerlinNoise(x * 0.035f + 5f, y * 0.2f) * 0.35f;
                        float cloud = Mathf.Clamp01((n - 0.48f) * 2.6f) * band;
                        p = Color.Lerp(p, new Color(0.97f, 0.97f, 0.96f), cloud * 0.6f);
                    }
                    px[y * w + x] = p;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            var m = new Material(shader) { name = "Ruins Sky" };
            m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Mapping")) m.SetFloat("_Mapping", 1f);
            if (m.HasProperty("_ImageType")) m.SetFloat("_ImageType", 0f);
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1f);
            if (m.HasProperty("_Rotation")) m.SetFloat("_Rotation", 0f);
            return m;
        }
    }
}
