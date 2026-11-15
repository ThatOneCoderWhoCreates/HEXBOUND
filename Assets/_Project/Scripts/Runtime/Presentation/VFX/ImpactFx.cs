using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Physical-looking impact effects shared by every map: soft dust kicked up from the ground (landings, dodges,
    /// footsteps when running, heavy knockdowns, explosions), stretched hot sparks for weapon hits and clashes, and
    /// small stone chips for explosions and crystal or wall hits. Three particle systems emitted on demand, so
    /// there is no per-effect allocation.
    /// </summary>
    public sealed class ImpactFx
    {
        private readonly ParticleSystem _dust;
        private readonly ParticleSystem _sparks;
        private readonly ParticleSystem _chips;
        private ParticleSystem.EmitParams _emit;

        /// <summary>Base dust color for the current map (sandy stone on Sky Ruins, pale on the Sanctuary, ash on the Lava Pit).</summary>
        public Color DustColor = new Color(0.62f, 0.57f, 0.5f, 0.55f);

        public ImpactFx(Transform parent)
        {
            var root = new GameObject("Impact Fx").transform;
            root.SetParent(parent, false);
            _dust = Make(root, "Dust", 400, false, DustTexture);
            _sparks = Make(root, "Sparks", 300, true, MaterialFactory.SoftDot);
            _chips = MakeChips(root);
        }

        public void SetMap(int map)
        {
            DustColor = map == Arena.MapRuins ? new Color(0.62f, 0.56f, 0.47f, 0.6f)
                : map == Arena.MapSanctuary ? new Color(0.86f, 0.82f, 0.88f, 0.45f)
                : new Color(0.35f, 0.3f, 0.28f, 0.6f);
        }

        /// <summary>A ring of dust rolling outward along the ground.</summary>
        public void Dust(Vector3 pos, int count, float spread, float size, float strength = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _emit.position = pos + dir * Random.Range(0.05f, 0.35f) + Vector3.up * Random.Range(0.02f, 0.2f);
                _emit.velocity = dir * spread * Random.Range(0.5f, 1.1f) + Vector3.up * Random.Range(0.2f, 0.9f) * strength;
                _emit.startSize = size * Random.Range(0.6f, 1.3f);
                _emit.startLifetime = Random.Range(0.6f, 1.3f) * Mathf.Lerp(0.8f, 1.4f, strength * 0.5f);
                Color c = DustColor;
                c.a *= Mathf.Clamp01(0.55f + 0.45f * strength);
                _emit.startColor = c;
                _emit.rotation = Random.Range(0f, 360f);
                _dust.Emit(_emit, 1);
            }
        }

        /// <summary>Fast, bright streaks thrown away from a hit (mostly along the given direction, if any).</summary>
        public void Sparks(Vector3 pos, Vector3 direction, int count, float speed, Color hdr)
        {
            bool aimed = direction.sqrMagnitude > 1e-4f;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (aimed) dir = (direction.normalized * 1.3f + dir).normalized;
                _emit.position = pos;
                _emit.velocity = dir * speed * Random.Range(0.4f, 1.1f);
                _emit.startSize = Random.Range(0.025f, 0.06f);
                _emit.startLifetime = Random.Range(0.15f, 0.4f);
                _emit.startColor = hdr;
                _emit.rotation = 0f;
                _sparks.Emit(_emit, 1);
            }
        }

        /// <summary>Small stone chips that fly up and fall back with gravity.</summary>
        public void Chips(Vector3 pos, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.5f;
                _emit.position = pos + Vector3.up * 0.1f;
                _emit.velocity = dir.normalized * speed * Random.Range(0.4f, 1f);
                _emit.startSize = Random.Range(0.05f, 0.14f);
                _emit.startLifetime = Random.Range(0.6f, 1.1f);
                _emit.startColor = new Color(0.5f, 0.46f, 0.4f) * Random.Range(0.75f, 1.1f);
                _emit.rotation3D = Random.insideUnitSphere * 180f;
                _chips.Emit(_emit, 1);
            }
            _emit.rotation3D = Vector3.zero;
        }

        public void Clear()
        {
            _dust.Clear();
            _sparks.Clear();
            _chips.Clear();
        }

        private static ParticleSystem Make(Transform parent, string name, int max, bool additive, Texture2D tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = additive ? 0.6f : -0.02f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = additive
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f))
                : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1.2f), new Keyframe(1f, 2f)));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                additive
                    ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (!additive)
            {
                // Dust slows down quickly in the air.
                ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 0.4f;
                limit.dampen = 0.12f;
                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            }

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (additive)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.5f;
                r.sharedMaterial = MaterialFactory.ParticleAdditive(Color.white);
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.sharedMaterial = AlphaMaterial(tex);
                r.sortMode = ParticleSystemSortMode.Distance;
            }
            ps.Play();
            return ps;
        }

        private static ParticleSystem MakeChips(Transform parent)
        {
            var go = new GameObject("Stone Chips");
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.2f;
            main.startRotation3D = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(cube);
            r.sharedMaterial = MaterialFactory.Lit(Color.white, 0.1f);
            r.shadowCastingMode = ShadowCastingMode.On;
            ps.Play();
            return ps;
        }

        private static Material AlphaMaterial(Texture2D tex)
        {
            Shader shader = MaterialFactory.FindShader("Universal Render Pipeline/Particles/Unlit", "Particles_Additive");
            var m = new Material(shader) { name = "Dust" };
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
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            return m;
        }

        private static Texture2D _dustTexture;

        /// <summary>A soft, lumpy puff with a darker core, like a cloud of dry dirt.</summary>
        private static Texture2D DustTexture
        {
            get
            {
                if (_dustTexture != null) return _dustTexture;
                const int size = 64;
                _dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Dust Puff", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float n = Mathf.PerlinNoise(x * 0.11f + 5f, y * 0.11f + 1f) * 0.6f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f + 9f) * 0.4f;
                        float a = Mathf.Clamp01(1f - r / (0.5f + 0.5f * n));
                        a = a * a;
                        byte c = (byte)(255f * Mathf.Lerp(0.82f, 1f, n));
                        px[y * size + x] = new Color32(c, c, c, (byte)(a * 255f));
                    }
                }
                _dustTexture.SetPixels32(px);
                _dustTexture.Apply();
                return _dustTexture;
            }
        }
    }
}
