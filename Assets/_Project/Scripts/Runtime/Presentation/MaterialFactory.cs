using System.Collections.Generic;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    /// <summary>Creates and caches URP materials at runtime for placeholder art and effects.</summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;
        private static Shader _unlit;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");

        private static Shader LitShader
        {
            get
            {
                if (_lit == null) _lit = FindShader("Universal Render Pipeline/Lit", "Lit_Emission");
                return _lit;
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                if (_unlit == null) _unlit = FindShader("Universal Render Pipeline/Unlit", "Unlit_Additive");
                return _unlit;
            }
        }

        /// <summary>Folder (under Resources) of materials that keep the URP shaders and their variants in Windows builds.</summary>
        public const string ShaderKeepFolder = "Materials/ShaderKeep";

        /// <summary>
        /// Finds a shader by name. A built game only contains shaders that some asset uses, so if the name
        /// lookup fails, the shader is taken from a keep-alive material in Resources/Materials/ShaderKeep.
        /// </summary>
        public static Shader FindShader(string shaderName, string keepMaterial)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null) return shader;
            Material keep = Resources.Load<Material>(ShaderKeepFolder + "/" + keepMaterial);
            if (keep != null && keep.shader != null) return keep.shader;
            Debug.LogError("WUSHEN: shader not found in this build: " + shaderName);
            return Shader.Find("Hidden/InternalErrorShader");
        }

        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f, Color? emission = null)
        {
            string key = "lit" + color + smoothness + metallic + emission;
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(LitShader);
            m.SetColor(BaseColor, color);
            m.color = color;
            m.SetFloat(Smoothness, smoothness);
            m.SetFloat(Metallic, metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor(EmissionColor, emission.Value);
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Opaque unlit color. Use HDR values above 1 to glow with bloom.</summary>
        public static Material Glow(Color hdrColor)
        {
            string key = "glow" + hdrColor;
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(UnlitShader);
            m.SetColor(BaseColor, hdrColor);
            m.color = hdrColor;
            Cache[key] = m;
            return m;
        }

        /// <summary>A new additive transparent material (not cached, so its color can be faded per effect).</summary>
        public static Material Additive(Color hdrColor)
        {
            var m = new Material(UnlitShader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetColor(m, hdrColor);
            return m;
        }

        private static Texture2D _softDot;

        /// <summary>A round, soft-edged white texture for particles.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int size = 64;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Soft Dot", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                _softDot.SetPixels32(pixels);
                _softDot.Apply();
                return _softDot;
            }
        }

        /// <summary>Additive particle material with a soft round texture (fire, sparks, glow).</summary>
        public static Material ParticleAdditive(Color hdr)
        {
            Shader shader = FindShader("Universal Render Pipeline/Particles/Unlit", "Particles_Additive");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", hdr);
            m.SetTexture("_BaseMap", SoftDot);
            m.mainTexture = SoftDot;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            m.SetColor(BaseColor, c);
            m.color = c;
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, Material material, string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
    }
}
