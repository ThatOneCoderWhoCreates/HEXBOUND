using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Builds the hexagonal arena, the void around it, lighting and post-processing, all from code.
    /// The arena shape matches HEXBOUND.Simulation.Arena exactly.
    /// </summary>
    public sealed class ArenaView
    {
        public GameObject Root { get; private set; }

        private readonly Material _edgeMaterial;
        private readonly Transform[] _floaters = new Transform[18];
        private readonly Vector3[] _floaterBase = new Vector3[18];
        private float _time;

        public ArenaView(Transform parent)
        {
            Root = new GameObject("Arena");
            Root.transform.SetParent(parent, false);

            float radius = Arena.Circumradius.ToFloat();
            Material slab = MaterialFactory.Lit(new Color(0.11f, 0.1f, 0.16f), 0.2f);
            Material tileA = MaterialFactory.Lit(new Color(0.2f, 0.19f, 0.27f), 0.55f, 0.1f);
            Material tileB = MaterialFactory.Lit(new Color(0.16f, 0.15f, 0.22f), 0.45f, 0.1f);
            Material tileCenter = MaterialFactory.Lit(new Color(0.24f, 0.18f, 0.34f), 0.6f, 0.1f, new Color(0.25f, 0.08f, 0.5f));
            _edgeMaterial = MaterialFactory.Glow(new Color(0.7f, 0.35f, 2.6f));

            // Base slab: a hexagonal prism slightly below the tiles.
            var slabGo = new GameObject("Slab");
            slabGo.transform.SetParent(Root.transform, false);
            slabGo.AddComponent<MeshFilter>().sharedMesh = HexPrism(radius, 1.6f, -0.06f);
            var slabRenderer = slabGo.AddComponent<MeshRenderer>();
            slabRenderer.sharedMaterial = slab;

            // Hex tiles on top.
            Mesh tileMesh = HexPrism(0.92f, 0.12f, 0f);
            float tileR = 1.0f;
            float colStep = 1.5f * tileR;
            float rowStep = Mathf.Sqrt(3f) * tileR;
            int count = 0;
            for (int col = -10; col <= 10; col++)
            {
                for (int row = -10; row <= 10; row++)
                {
                    float x = col * colStep;
                    float z = row * rowStep + ((col & 1) != 0 ? rowStep * 0.5f : 0f);
                    var p = new FixVec3(Fix.Milli((int)(x * 1000)), Fix.Zero, Fix.Milli((int)(z * 1000)));
                    if (!Arena.IsInside(p, Fix.Milli(900))) continue;
                    var tile = new GameObject("Tile");
                    tile.transform.SetParent(Root.transform, false);
                    tile.transform.localPosition = new Vector3(x, -0.12f, z);
                    tile.AddComponent<MeshFilter>().sharedMesh = tileMesh;
                    var mr = tile.AddComponent<MeshRenderer>();
                    bool center = Mathf.Abs(x) < 0.1f && Mathf.Abs(z) < 0.1f;
                    mr.sharedMaterial = center ? tileCenter : (((col + row) & 1) == 0 ? tileA : tileB);
                    count++;
                }
            }

            // Glowing rim along the six edges.
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = Corner(radius, i);
                Vector3 b = Corner(radius, i + 1);
                GameObject edge = MaterialFactory.Primitive(PrimitiveType.Cube, Root.transform, _edgeMaterial, "Edge");
                edge.transform.localPosition = (a + b) * 0.5f + Vector3.up * 0.02f;
                edge.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                edge.transform.localScale = new Vector3(0.14f, 0.1f, Vector3.Distance(a, b));
                edge.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                GameObject pillar = MaterialFactory.Primitive(PrimitiveType.Cylinder, Root.transform, slab, "Pillar");
                pillar.transform.localPosition = a * 1.0f + Vector3.down * 0.6f;
                pillar.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
                GameObject crystal = MaterialFactory.Primitive(PrimitiveType.Sphere, Root.transform, _edgeMaterial, "Crystal");
                crystal.transform.localPosition = a + Vector3.up * 0.9f;
                crystal.transform.localScale = new Vector3(0.35f, 0.6f, 0.35f);
                crystal.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Floating hex rocks in the void, for depth.
            Material rock = MaterialFactory.Lit(new Color(0.13f, 0.11f, 0.19f), 0.15f);
            Mesh rockMesh = HexPrism(1f, 1f, 0f);
            var rng = new System.Random(7);
            for (int i = 0; i < _floaters.Length; i++)
            {
                float ang = i / (float)_floaters.Length * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f;
                float dist = 26f + (float)rng.NextDouble() * 30f;
                float y = -12f + (float)rng.NextDouble() * 18f;
                var go = new GameObject("Floater");
                go.transform.SetParent(Root.transform, false);
                go.transform.localPosition = new Vector3(Mathf.Sin(ang) * dist, y, Mathf.Cos(ang) * dist);
                float s = 1.2f + (float)rng.NextDouble() * 3.5f;
                go.transform.localScale = new Vector3(s, s * (0.3f + (float)rng.NextDouble()), s);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = rockMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = rock;
                _floaters[i] = go.transform;
                _floaterBase[i] = go.transform.localPosition;
            }

            SetupLighting(parent);
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
            for (int i = 0; i < _floaters.Length; i++)
            {
                if (_floaters[i] == null) continue;
                _floaters[i].localPosition = _floaterBase[i] + Vector3.up * Mathf.Sin(_time * 0.4f + i) * 0.6f;
                _floaters[i].Rotate(0f, deltaTime * (4f + i % 5), 0f, Space.Self);
            }
            float pulse = 2.2f + Mathf.Sin(_time * 1.6f) * 0.6f;
            MaterialFactory.SetColor(_edgeMaterial, new Color(0.35f * pulse, 0.16f * pulse, pulse));
        }

        private static Vector3 Corner(float radius, int index)
        {
            float a = (30f + 60f * index) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        /// <summary>Hexagonal prism with corners at 30 + 60k degrees, top face at topY.</summary>
        public static Mesh HexPrism(float radius, float height, float topY)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            float bottom = topY - height;

            // Top face.
            int topCenter = verts.Count;
            verts.Add(new Vector3(0, topY, 0));
            for (int i = 0; i < 6; i++) verts.Add(Corner(radius, i) + Vector3.up * topY);
            for (int i = 0; i < 6; i++)
            {
                tris.Add(topCenter);
                tris.Add(topCenter + 1 + i);
                tris.Add(topCenter + 1 + (i + 1) % 6);
            }

            // Bottom face.
            int botCenter = verts.Count;
            verts.Add(new Vector3(0, bottom, 0));
            for (int i = 0; i < 6; i++) verts.Add(Corner(radius, i) + Vector3.up * bottom);
            for (int i = 0; i < 6; i++)
            {
                tris.Add(botCenter);
                tris.Add(botCenter + 1 + (i + 1) % 6);
                tris.Add(botCenter + 1 + i);
            }

            // Sides (separate vertices for flat shading).
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = Corner(radius, i);
                Vector3 b = Corner(radius, i + 1);
                int s = verts.Count;
                verts.Add(a + Vector3.up * topY);
                verts.Add(b + Vector3.up * topY);
                verts.Add(b + Vector3.up * bottom);
                verts.Add(a + Vector3.up * bottom);
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
            return mesh;
        }

        private void SetupLighting(Transform parent)
        {
            var sunGo = new GameObject("Key Light");
            sunGo.transform.SetParent(Root.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;

            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(Root.transform, false);
            rimGo.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.4f, 1f);
            rim.intensity = 0.6f;
            rim.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.3f, 0.45f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.17f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.06f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.11f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 95f;
            RenderSettings.skybox = null;

            var volumeGo = new GameObject("Post Processing");
            volumeGo.transform.SetParent(Root.transform, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(8f);
            color.contrast.Override(8f);
            volume.sharedProfile = profile;
        }
    }
}
