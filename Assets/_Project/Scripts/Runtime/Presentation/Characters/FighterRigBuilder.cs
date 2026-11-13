using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HEXBOUND.Presentation
{
    /// <summary>The visual objects of one fighter.</summary>
    public sealed class FighterVisual
    {
        /// <summary>Placed at the fighter's position and turned to its facing.</summary>
        public Transform Root;
        /// <summary>Child of Root, used for whole-body tilt, spin and bob.</summary>
        public Transform Body;
        public PoseRig Rig;
        public bool FromModel;
        public float Height;
        /// <summary>Second blade shown during Twin Blades.</summary>
        public GameObject OffhandBlade;
        /// <summary>Glow ring shown under the fighter while an ultimate buff is active.</summary>
        public GameObject BuffAura;
        public Light AuraLight;
        public Renderer[] Renderers;
        public Material AccentMaterial;
    }

    /// <summary>
    /// Builds fighter visuals: a rigged model from Resources/Fighters/&lt;Weapon&gt; if one exists,
    /// otherwise a primitive humanoid made of capsules. Both are driven by the same PoseRig.
    /// </summary>
    public static class FighterRigBuilder
    {
        public static FighterVisual Build(CharacterDef character, Transform parent, int playerIndex)
        {
            var root = new GameObject("Fighter " + (playerIndex + 1) + " - " + character.Name).transform;
            root.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);

            Color baseColor = new Color(character.ColorR / 255f, character.ColorG / 255f, character.ColorB / 255f);
            Color accentHdr = baseColor * 3.2f;
            accentHdr.a = 1f;

            var visual = new FighterVisual
            {
                Root = root,
                Body = body,
                Height = character.Height.ToFloat(),
                AccentMaterial = MaterialFactory.Glow(accentHdr),
            };

            GameObject prefab = string.IsNullOrEmpty(character.ModelName) ? null : Resources.Load<GameObject>("Fighters/" + character.ModelName);
            bool built = false;
            if (prefab != null)
            {
                built = TryBuildFromModel(prefab, visual, character);
            }
            if (!built)
            {
                BuildPrimitive(visual, character, baseColor);
            }

            BuildExtras(visual, character, accentHdr, playerIndex);
            visual.Renderers = root.GetComponentsInChildren<Renderer>(true);
            return visual;
        }

        private static bool TryBuildFromModel(GameObject prefab, FighterVisual visual, CharacterDef character)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.name = "Model";
            foreach (Animator a in instance.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (Animation a in instance.GetComponentsInChildren<Animation>(true)) a.enabled = false;

            var container = new GameObject("Model Container").transform;
            container.SetParent(visual.Body, false);
            instance.transform.SetParent(container, false);
            instance.transform.localPosition = Vector3.zero;

            PoseRig rig = PoseRig.FromHierarchy(instance.transform);
            if (!rig.HasArms || !rig.HasLegs || rig.Head == null)
            {
                Debug.LogWarning("HEXBOUND: model for " + character.Name + " is missing bones the animator needs. Using placeholder body instead.");
                Object.Destroy(container.gameObject);
                return false;
            }

            // Turn the model so it faces +Z: its right side is where the right upper arm is.
            Vector3 right = rig.UpperArmR.position - rig.UpperArmL.position;
            right.y = 0f;
            if (right.sqrMagnitude > 1e-6f)
            {
                Vector3 forward = Vector3.Cross(right.normalized, Vector3.up);
                Vector3 localForward = container.InverseTransformDirection(forward);
                localForward.y = 0f;
                container.localRotation = Quaternion.FromToRotation(localForward.normalized, Vector3.forward) * container.localRotation;
            }

            // Scale so the head sits at about 92% of the fighter's height, and put the feet on the floor.
            float minY = float.MaxValue;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                minY = Mathf.Min(minY, r.bounds.min.y);
                var skinned = r as SkinnedMeshRenderer;
                if (skinned != null) skinned.updateWhenOffscreen = true;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            minY = Mathf.Min(minY, rig.FootL.position.y, rig.FootR.position.y);
            float headHeight = rig.Head.position.y - minY;
            if (headHeight > 1e-4f)
            {
                float scale = visual.Height * 0.92f / headHeight;
                container.localScale = Vector3.one * scale;
                container.localPosition = new Vector3(0f, -minY * scale, 0f);
            }

            visual.Rig = rig;
            visual.FromModel = true;
            rig.CaptureRest();
            return true;
        }

        /// <summary>A simple humanoid made of capsules and spheres, in a T-pose, with named bones.</summary>
        private static void BuildPrimitive(FighterVisual visual, CharacterDef character, Color baseColor)
        {
            float h = visual.Height / 1.75f;
            Material skin = MaterialFactory.Lit(new Color(0.82f, 0.68f, 0.6f), 0.3f);
            Material cloth = MaterialFactory.Lit(baseColor * 0.75f, 0.3f);
            Material clothDark = MaterialFactory.Lit(baseColor * 0.35f, 0.25f);
            Material trim = MaterialFactory.Lit(new Color(0.85f, 0.75f, 0.45f), 0.7f, 0.8f);

            Transform B(string name, Transform parent, Vector3 worldOffset)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = worldOffset * h;
                return t;
            }

            Transform pelvis = B("Pelvis", visual.Body, new Vector3(0, 0.95f, 0));
            Transform spine = B("Spine", pelvis, new Vector3(0, 0.15f, 0));
            Transform chest = B("Spine Chest", spine, new Vector3(0, 0.22f, 0));
            Transform head = B("Head", chest, new Vector3(0, 0.33f, 0));
            Transform uaL = B("L UpperArm", chest, new Vector3(-0.2f, 0.18f, 0));
            Transform faL = B("L ForeArm", uaL, new Vector3(-0.28f, 0, 0));
            Transform hL = B("L Hand", faL, new Vector3(-0.26f, 0, 0));
            Transform uaR = B("R UpperArm", chest, new Vector3(0.2f, 0.18f, 0));
            Transform faR = B("R ForeArm", uaR, new Vector3(0.28f, 0, 0));
            Transform hR = B("R Hand", faR, new Vector3(0.26f, 0, 0));
            Transform thL = B("L Thigh", pelvis, new Vector3(-0.11f, -0.03f, 0));
            Transform caL = B("L Calf", thL, new Vector3(0, -0.43f, 0));
            Transform ftL = B("L Foot", caL, new Vector3(0, -0.42f, 0));
            Transform thR = B("R Thigh", pelvis, new Vector3(0.11f, -0.03f, 0));
            Transform caR = B("R Calf", thR, new Vector3(0, -0.43f, 0));
            Transform ftR = B("R Foot", caR, new Vector3(0, -0.42f, 0));

            Limb(pelvis, Vector3.zero, new Vector3(0, 0.15f, 0) * h, 0.17f * h, clothDark);
            Limb(spine, Vector3.zero, new Vector3(0, 0.24f, 0) * h, 0.19f * h, cloth);
            Limb(chest, Vector3.zero, new Vector3(0, 0.2f, 0) * h, 0.2f * h, cloth);
            GameObject headBall = MaterialFactory.Primitive(PrimitiveType.Sphere, head, skin, "Head Mesh");
            headBall.transform.localPosition = new Vector3(0, 0.1f, 0) * h;
            headBall.transform.localScale = Vector3.one * 0.25f * h;
            GameObject hood = MaterialFactory.Primitive(PrimitiveType.Sphere, head, clothDark, "Hood");
            hood.transform.localPosition = new Vector3(0, 0.14f, -0.03f) * h;
            hood.transform.localScale = new Vector3(0.29f, 0.27f, 0.29f) * h;
            Limb(uaL, Vector3.zero, faL.localPosition, 0.07f * h, cloth);
            Limb(faL, Vector3.zero, hL.localPosition, 0.06f * h, skin);
            Limb(uaR, Vector3.zero, faR.localPosition, 0.07f * h, cloth);
            Limb(faR, Vector3.zero, hR.localPosition, 0.06f * h, skin);
            Limb(thL, Vector3.zero, caL.localPosition, 0.09f * h, clothDark);
            Limb(caL, Vector3.zero, ftL.localPosition, 0.075f * h, clothDark);
            Limb(thR, Vector3.zero, caR.localPosition, 0.09f * h, clothDark);
            Limb(caR, Vector3.zero, ftR.localPosition, 0.075f * h, clothDark);
            GameObject robe = MaterialFactory.Primitive(PrimitiveType.Cylinder, pelvis, cloth, "Robe");
            robe.transform.localPosition = new Vector3(0, -0.22f, 0) * h;
            robe.transform.localScale = new Vector3(0.42f, 0.25f, 0.36f) * h;
            GameObject belt = MaterialFactory.Primitive(PrimitiveType.Cylinder, pelvis, trim, "Belt");
            belt.transform.localPosition = new Vector3(0, 0.02f, 0) * h;
            belt.transform.localScale = new Vector3(0.36f, 0.025f, 0.3f) * h;

            // Weapon held in the right hand, pointing along the arm (the T-pose arm points along +X).
            BuildWeapon(character.Weapon, hR, h, trim, baseColor);

            var rig = new PoseRig
            {
                Pelvis = pelvis,
                SpineLow = spine,
                Chest = chest,
                Head = head,
                UpperArmL = uaL,
                ForeArmL = faL,
                HandL = hL,
                UpperArmR = uaR,
                ForeArmR = faR,
                HandR = hR,
                ThighL = thL,
                CalfL = caL,
                FootL = ftL,
                ThighR = thR,
                CalfR = caR,
                FootR = ftR,
            };
            rig.CaptureRest();
            visual.Rig = rig;
            visual.FromModel = false;
        }

        private static void Limb(Transform bone, Vector3 from, Vector3 to, float radius, Material material)
        {
            GameObject capsule = MaterialFactory.Primitive(PrimitiveType.Capsule, bone, material, "Segment");
            Vector3 dir = to - from;
            float length = dir.magnitude;
            capsule.transform.localPosition = from + dir * 0.5f;
            capsule.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            capsule.transform.localScale = new Vector3(radius * 2f, Mathf.Max(length * 0.5f + radius * 0.5f, radius), radius * 2f);
        }

        private static void BuildWeapon(string weapon, Transform hand, float h, Material trim, Color baseColor)
        {
            Color glow = baseColor * 4f;
            glow.a = 1f;
            if (weapon == "Fists")
            {
                Material glove = MaterialFactory.Lit(new Color(0.75f, 0.1f, 0.1f), 0.6f);
                GameObject g = MaterialFactory.Primitive(PrimitiveType.Sphere, hand, glove, "Glove R");
                g.transform.localPosition = new Vector3(0.06f, 0, 0) * h;
                g.transform.localScale = Vector3.one * 0.15f * h;
                Transform left = hand.parent != null && hand.parent.parent != null && hand.parent.parent.parent != null ? hand.parent.parent.parent.Find("L UpperArm/L ForeArm/L Hand") : null;
                if (left != null)
                {
                    GameObject gl = MaterialFactory.Primitive(PrimitiveType.Sphere, left, glove, "Glove L");
                    gl.transform.localPosition = new Vector3(-0.06f, 0, 0) * h;
                    gl.transform.localScale = Vector3.one * 0.15f * h;
                }
            }
            else if (weapon == "Wand")
            {
                GameObject rod = MaterialFactory.Primitive(PrimitiveType.Cylinder, hand, MaterialFactory.Lit(new Color(0.25f, 0.15f, 0.1f), 0.5f), "Wand");
                rod.transform.localPosition = new Vector3(0.2f, 0, 0) * h;
                rod.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                rod.transform.localScale = new Vector3(0.035f, 0.2f, 0.035f) * h;
                GameObject tip = MaterialFactory.Primitive(PrimitiveType.Sphere, hand, MaterialFactory.Glow(glow), "Wand Tip");
                tip.transform.localPosition = new Vector3(0.42f, 0, 0) * h;
                tip.transform.localScale = Vector3.one * 0.08f * h;
                tip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            else if (weapon == "Staff")
            {
                GameObject shaft = MaterialFactory.Primitive(PrimitiveType.Cylinder, hand, MaterialFactory.Lit(new Color(0.3f, 0.2f, 0.12f), 0.4f), "Staff");
                shaft.transform.localPosition = new Vector3(0.1f, 0, 0) * h;
                shaft.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                shaft.transform.localScale = new Vector3(0.05f, 0.85f, 0.05f) * h;
                GameObject orb = MaterialFactory.Primitive(PrimitiveType.Sphere, hand, MaterialFactory.Glow(glow), "Staff Orb");
                orb.transform.localPosition = new Vector3(0.95f, 0, 0) * h;
                orb.transform.localScale = Vector3.one * 0.13f * h;
            }
            else
            {
                GameObject blade = MaterialFactory.Primitive(PrimitiveType.Cube, hand, MaterialFactory.Lit(new Color(0.78f, 0.82f, 0.9f), 0.85f, 0.9f), "Sword");
                blade.transform.localPosition = new Vector3(0.55f, 0, 0) * h;
                blade.transform.localScale = new Vector3(0.85f, 0.025f, 0.07f) * h;
                GameObject guard = MaterialFactory.Primitive(PrimitiveType.Cube, hand, trim, "Guard");
                guard.transform.localPosition = new Vector3(0.1f, 0, 0) * h;
                guard.transform.localScale = new Vector3(0.04f, 0.05f, 0.22f) * h;
            }
        }

        private static void BuildExtras(FighterVisual visual, CharacterDef character, Color accentHdr, int playerIndex)
        {
            PoseRig rig = visual.Rig;

            // Second sword for Twin Blades, held in the left hand along the arm.
            if (character.UltimateBuff == BuffId.TwinBlades && rig.HandL != null && rig.ForeArmL != null)
            {
                var blade = new GameObject("Twin Blade");
                blade.transform.SetParent(rig.HandL, false);
                Vector3 alongArm = rig.HandL.InverseTransformDirection((rig.HandL.position - rig.ForeArmL.position).normalized);
                float worldLength = 1.0f;
                float lossy = Mathf.Max(rig.HandL.lossyScale.x, 1e-4f);
                GameObject mesh = MaterialFactory.Primitive(PrimitiveType.Cube, blade.transform, visual.AccentMaterial, "Blade");
                mesh.transform.localRotation = Quaternion.FromToRotation(Vector3.right, alongArm);
                mesh.transform.localPosition = alongArm * (worldLength * 0.55f / lossy);
                mesh.transform.localScale = new Vector3(worldLength, 0.03f, 0.08f) / lossy;
                mesh.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                blade.SetActive(false);
                visual.OffhandBlade = blade;
            }

            var aura = new GameObject("Aura");
            aura.transform.SetParent(visual.Root, false);
            GameObject ring = MaterialFactory.Primitive(PrimitiveType.Cylinder, aura.transform, visual.AccentMaterial, "Ring");
            ring.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            ring.transform.localPosition = new Vector3(0, 0.03f, 0);
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Aura Light");
            lightGo.transform.SetParent(aura.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 3f;
            light.color = accentHdr / 3.2f;
            aura.SetActive(false);
            visual.BuffAura = aura;
            visual.AuraLight = light;

            // Small marker under each fighter so players can tell them apart.
            var marker = MaterialFactory.Primitive(PrimitiveType.Cylinder, visual.Root, MaterialFactory.Glow(playerIndex == 0 ? new Color(0.4f, 1.2f, 2.5f) : new Color(2.5f, 0.6f, 0.4f)), "Player Marker");
            marker.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);
            marker.transform.localPosition = new Vector3(0, 0.015f, 0);
            marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
