using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// The battleground for the selected map (Arena.MapLava or Arena.MapSanctuary); SetMap rebuilds it when the map changes.
    /// Lava Pit: the lava arena stage (Lava_Stage.fbx) used whole. Its flat center platform is the
    /// fighting floor, with the top placed at height 0 to match HEXBOUND.Simulation.Arena, and its three rock
    /// spikes are solid in the simulation. Two small floating islands drift in the sky beyond the wall as scenery.
    /// Models load from Resources/Arena. If the stage is missing, a plain floor shaped like the simulation's
    /// edge table is built instead, so the game always works.
    /// </summary>
    public sealed partial class ArenaView
    {
        // Lava stage measurements (FBX units, measured from Lava_Stage.fbx). Used at its original scale.
        private const float LavaOuterWidthFbx = 78.14f;
        private const float LavaFloorFbx = 7.4f;
        private const float LavaBaseCenterXFbx = 0.42f;

        // Floating island scenery (floating_island_2k.fbx).
        private const float IslandWidthFbx = 2680.06f;

        public GameObject Root { get; private set; }

        /// <summary>The map currently shown (-1 before the first SetMap).</summary>
        public int Map { get; private set; } = -1;

        /// <summary>True when the camera should draw the skybox; otherwise it clears to BackgroundColor.</summary>
        public bool UsesSkybox { get; private set; }
        public Color BackgroundColor { get; private set; } = new Color(0.12f, 0.04f, 0.03f);

        private GameObject _mapRoot;
        private Material _lavaMaterial;
        private Material _fallbackRim;
        private float _time;

        public ArenaView(Transform parent, int map)
        {
            Root = new GameObject("Arena");
            Root.transform.SetParent(parent, false);
            SetMap(map);
        }

        /// <summary>Shows the given map, rebuilding the scenery if it changed. Returns true if it changed.</summary>
        public bool SetMap(int map)
        {
            if (map < 0 || map >= Arena.MapCount) map = Arena.MapLava;
            if (map == Map) return false;
            Map = map;
            if (_mapRoot != null) Object.Destroy(_mapRoot);
            _mapRoot = new GameObject(map == Arena.MapSanctuary ? "Celestial Sanctuary" : "Lava Pit");
            _mapRoot.transform.SetParent(Root.transform, false);
            _lavaMaterial = null;
            _fallbackRim = null;
            _skyIslands.Clear();
            _skyIslandBase.Clear();
            ClearSanctuary();

            if (map == Arena.MapSanctuary)
            {
                BuildSanctuary();
                return true;
            }

            UsesSkybox = false;
            BackgroundColor = new Color(0.12f, 0.04f, 0.03f);
            bool stage = BuildLavaStage();
            if (!stage) BuildFallbackFloor();
            BuildSkyIslands();
            BuildEmbers();
            SetupLighting();
            return true;
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
            if (Map == Arena.MapSanctuary) UpdateSanctuary(deltaTime);
            if (_lavaMaterial != null)
            {
                Vector2 flow = new Vector2(_time * 0.004f, _time * 0.0025f);
                _lavaMaterial.mainTextureOffset = flow;
                float pulse = 1f + Mathf.Sin(_time * 0.9f) * 0.15f;
                _lavaMaterial.SetColor("_EmissionColor", new Color(2.2f, 0.75f, 0.18f) * pulse);
            }
            for (int i = 0; i < _skyIslands.Count; i++)
            {
                _skyIslands[i].localPosition = _skyIslandBase[i] + Vector3.up * Mathf.Sin(_time * 0.35f + i * 2f) * 0.8f;
                _skyIslands[i].Rotate(0f, deltaTime * (1.5f + i), 0f, Space.Self);
            }
            if (_fallbackRim != null)
            {
                float pulse = 2.2f + Mathf.Sin(_time * 1.6f) * 0.6f;
                MaterialFactory.SetColor(_fallbackRim, new Color(pulse, 0.45f * pulse, 0.12f * pulse));
            }
        }

        // ------------------------------------------------------------------ Floating islands (scenery)

        private readonly System.Collections.Generic.List<Transform> _skyIslands = new System.Collections.Generic.List<Transform>();
        private readonly System.Collections.Generic.List<Vector3> _skyIslandBase = new System.Collections.Generic.List<Vector3>();

        private void BuildSkyIslands()
        {
            GameObject prefab = Resources.Load<GameObject>("Arena/Island/floating_island_2k");
            if (prefab == null) return;
            AddSkyIsland(prefab, 40f, 50f, 15f, 10f);
            AddSkyIsland(prefab, 205f, 53f, 23f, 6.5f);
        }

        private void AddSkyIsland(GameObject prefab, float angleDegrees, float distance, float height, float width)
        {
            var holder = new GameObject("Sky Island").transform;
            holder.SetParent(_mapRoot.transform, false);
            GameObject instance = Object.Instantiate(prefab, holder, false);
            RemoveColliders(instance);
            Renderer top = FindRenderer(instance.transform, "island");
            if (top == null) top = LargestRenderer(instance.transform);
            float measured = top != null ? top.bounds.size.x : IslandWidthFbx;
            holder.localScale = Vector3.one * (width / Mathf.Max(measured, 1e-4f));
            float a = angleDegrees * Mathf.Deg2Rad;
            holder.localPosition = new Vector3(Mathf.Sin(a) * distance, height, Mathf.Cos(a) * distance);
            holder.localRotation = Quaternion.Euler(0f, angleDegrees * 3f, 0f);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            _skyIslands.Add(holder);
            _skyIslandBase.Add(holder.localPosition);
        }

        /// <summary>Simple stone floor following the simulation's edge, used when the island model is missing.</summary>
        private void BuildFallbackFloor()
        {
            int n = Arena.EdgeSamples;
            var top = new Vector3[n];
            ArenaLayout layout = Arena.Layout(Arena.MapLava);
            for (int i = 0; i < n; i++)
            {
                int angle = i * FixMath.FullTurn / n;
                float r = Arena.EdgeRadius(layout, angle).ToFloat() + 0.25f;
                float a = i * Mathf.PI * 2f / n;
                top[i] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            }

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            verts.Add(new Vector3(0f, -0.02f, 0f));
            for (int i = 0; i < n; i++) verts.Add(top[i] + Vector3.down * 0.02f);
            for (int i = 0; i < n; i++)
            {
                tris.Add(0);
                tris.Add(1 + i);
                tris.Add(1 + (i + 1) % n);
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = top[i];
                Vector3 c = top[(i + 1) % n];
                int s = verts.Count;
                verts.Add(a + Vector3.down * 0.02f);
                verts.Add(c + Vector3.down * 0.02f);
                verts.Add(c * 0.55f + Vector3.down * 6f);
                verts.Add(a * 0.55f + Vector3.down * 6f);
                tris.Add(s);
                tris.Add(s + 2);
                tris.Add(s + 1);
                tris.Add(s);
                tris.Add(s + 3);
                tris.Add(s + 2);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Fallback Floor");
            go.transform.SetParent(_mapRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.22f, 0.19f, 0.18f), 0.25f);

            _fallbackRim = MaterialFactory.Glow(new Color(2.2f, 1f, 0.25f));
            for (int i = 0; i < n; i++)
            {
                Vector3 a = top[i];
                Vector3 c = top[(i + 1) % n];
                GameObject edge = MaterialFactory.Primitive(PrimitiveType.Cube, _mapRoot.transform, _fallbackRim, "Edge");
                edge.transform.localPosition = (a + c) * 0.5f;
                edge.transform.localRotation = Quaternion.LookRotation(c - a, Vector3.up);
                edge.transform.localScale = new Vector3(0.12f, 0.08f, Vector3.Distance(a, c));
                edge.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ Lava stage (background)

        private bool BuildLavaStage()
        {
            GameObject prefab = Resources.Load<GameObject>("Arena/LavaStage/Lava_Stage");
            if (prefab == null) return false;

            var holder = new GameObject("Lava Stage").transform;
            holder.SetParent(_mapRoot.transform, false);
            GameObject instance = Object.Instantiate(prefab, holder, false);
            instance.name = "Lava Stage Model";
            RemoveColliders(instance);

            // Bring the model to FBX units (whatever unit scale Unity imported with).
            Renderer outer = FindRenderer(instance.transform, "outer");
            float measured = outer != null ? outer.bounds.size.x : 0f;
            float s = measured > 1e-4f ? LavaOuterWidthFbx / measured : 1f;
            holder.localScale = Vector3.one * s;

            // The simulation's edge table and spike positions assume Unity mirrored the FBX X axis on import.
            // If this import did not, mirror the visual so the platform edge and spikes still match the rules.
            Renderer platform = FindRenderer(instance.transform, "base");
            if (platform != null && platform.bounds.center.x > LavaBaseCenterXFbx * 0.5f) holder.localScale = new Vector3(-s, s, s);

            holder.localPosition = new Vector3(0f, -LavaFloorFbx, 0f);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                bool isPlatform = platform != null && r == platform;
                r.shadowCastingMode = isPlatform ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            Renderer lava = FindRenderer(instance.transform, "lower");
            if (lava != null)
            {
                _lavaMaterial = lava.material;
                Texture tex = _lavaMaterial.mainTexture;
                _lavaMaterial.EnableKeyword("_EMISSION");
                _lavaMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (tex != null && _lavaMaterial.HasProperty("_EmissionMap") && _lavaMaterial.GetTexture("_EmissionMap") == null) _lavaMaterial.SetTexture("_EmissionMap", tex);
                _lavaMaterial.SetColor("_EmissionColor", new Color(2.2f, 0.75f, 0.18f));
            }

            // Sky dome: show its texture as-is, unaffected by lights.
            Renderer sky = FindRenderer(instance.transform, "sky");
            if (sky != null)
            {
                Texture tex = sky.sharedMaterial != null ? sky.sharedMaterial.mainTexture : null;
                Material unlit = MaterialFactory.Glow(new Color(0.9f, 0.75f, 0.7f));
                Material m = new Material(unlit);
                if (tex != null)
                {
                    m.mainTexture = tex;
                    m.SetTexture("_BaseMap", tex);
                }
                m.SetFloat("_Cull", 0f);
                sky.sharedMaterial = m;
                sky.receiveShadows = false;
            }
            return true;
        }

        // ------------------------------------------------------------------ Atmosphere

        private void BuildEmbers()
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(_mapRoot.transform, false);
            go.transform.localPosition = new Vector3(0f, Arena.LavaHeight.ToFloat() - 0.5f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.3f, 0.05f));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 45f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 1f, 70f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.3f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ParticleMaterial(new Color(3f, 1.4f, 0.4f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
        }

        private static Material ParticleMaterial(Color hdr)
        {
            Shader shader = MaterialFactory.FindShader("Universal Render Pipeline/Particles/Unlit", "Particles_Additive");
            if (shader == null) return MaterialFactory.Additive(hdr);
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", hdr);
            return m;
        }

        private void SetupLighting()
        {
            var sunGo = new GameObject("Key Light");
            sunGo.transform.SetParent(_mapRoot.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.74f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;

            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(_mapRoot.transform, false);
            rimGo.transform.rotation = Quaternion.Euler(18f, 150f, 0f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.4f, 1f);
            rim.intensity = 0.45f;
            rim.shadows = LightShadows.None;

            // Lava glow from below, lighting the underside of the island and the fighters' legs.
            var underGo = new GameObject("Lava Up Light");
            underGo.transform.SetParent(_mapRoot.transform, false);
            underGo.transform.rotation = Quaternion.Euler(-70f, 20f, 0f);
            Light under = underGo.AddComponent<Light>();
            under.type = LightType.Directional;
            under.color = new Color(1f, 0.42f, 0.12f);
            under.intensity = 0.55f;
            under.shadows = LightShadows.None;

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                var pointGo = new GameObject("Lava Glow " + i);
                pointGo.transform.SetParent(_mapRoot.transform, false);
                pointGo.transform.localPosition = new Vector3(Mathf.Sin(a) * 18f, -6f, Mathf.Cos(a) * 18f);
                Light p = pointGo.AddComponent<Light>();
                p.type = LightType.Point;
                p.color = new Color(1f, 0.45f, 0.12f);
                p.range = 26f;
                p.intensity = 4f;
                p.shadows = LightShadows.None;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.27f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.16f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.12f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.16f, 0.06f, 0.04f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.skybox = null;

            var volumeGo = new GameObject("Post Processing");
            volumeGo.transform.SetParent(_mapRoot.transform, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.15f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.68f);
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(10f);
            color.contrast.Override(10f);
            color.colorFilter.Override(new Color(1f, 0.95f, 0.9f));
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ Helpers

        internal static void RemoveColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }

        internal static Transform FindTransform(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        private static Renderer FindRenderer(Transform root, string name)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (string.Equals(r.name, name, System.StringComparison.OrdinalIgnoreCase)) return r;
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) return r;
            }
            return null;
        }

        private static Renderer LargestRenderer(Transform root)
        {
            Renderer best = null;
            float size = 0f;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                float s = r.bounds.size.sqrMagnitude;
                if (s > size)
                {
                    size = s;
                    best = r;
                }
            }
            return best;
        }
    }
}
