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
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
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
