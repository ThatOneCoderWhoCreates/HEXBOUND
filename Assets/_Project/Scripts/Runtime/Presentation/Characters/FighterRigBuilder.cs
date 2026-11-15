using System.Collections.Generic;
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
        /// <summary>Launcher: the missile launcher (primary) and pistol (secondary) props.</summary>
        public GameObject PrimaryProp;
        public GameObject SecondaryProp;
        /// <summary>Glowing swipe trails on the striking hand / weapon tip, shown during attacks.</summary>
        public TrailRenderer TrailR;
        public TrailRenderer TrailL;
        /// <summary>Staff: the holder in the right hand; the animator turns it each frame so the staff points where the pose wants.</summary>
        public Transform WeaponHolder;

        /// <summary>What shows while each weapon (0 primary, 1 secondary) is in hand; hidden after it is thrown.</summary>
        public GameObject[] HeldWeapon = new GameObject[2];
        /// <summary>Inactive copies of each weapon, cloned for a thrown (spinning) or dropped (lying) weapon.</summary>
        public GameObject[] WeaponTemplate = new GameObject[2];
        /// <summary>Sword built into the character mesh: the skinned renderer and its materials with and without the blade.</summary>
        public SkinnedMeshRenderer WeaponSkin;
        public Material[] ArmedMaterials;
        public Material[] UnarmedMaterials;
        /// <summary>Vex: the launcher (on the shoulder or slung on the back) and the pistol (in the right hand), placed by the animator.</summary>
        public Transform LauncherHolder;
        public Transform PistolHolder;
        /// <summary>Beast: fire blasting from the shoulder exhausts while it is awakened.</summary>
        public GameObject ExhaustFire;
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
            SetupWeaponVisuals(visual, character, accentHdr, parent);
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
            bool hasWeaponModel = !string.IsNullOrEmpty(character.WeaponModel) && Resources.Load<GameObject>("Fighters/" + character.WeaponModel) != null;
            if (!hasWeaponModel) BuildWeapon(character.Weapon, hR, h, trim, baseColor);

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
            else if (weapon == "Launcher")
            {
                // The launcher and pistol props are added for every Launcher body in BuildExtras.
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

        /// <summary>
        /// Two fire jets from the back of the beast's shoulder pods. Positions come from the model: the pods sit
        /// about 0.52 model units above the chest bone, 0.47 to each side and 0.45 behind it (model shoulder span 1.2).
        /// </summary>
        private static GameObject BuildExhaust(FighterVisual visual, PoseRig rig)
        {
            Transform body = visual.Body;
            Vector3 chest = body.InverseTransformPoint(rig.Chest.position);
            Vector3 l = body.InverseTransformPoint(rig.UpperArmL.position);
            Vector3 r = body.InverseTransformPoint(rig.UpperArmR.position);
            float unit = Mathf.Max(0.1f, (r - l).magnitude / 1.2f);
            var root = new GameObject("Exhaust Fire");
            root.transform.SetParent(rig.Chest, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 local = chest + new Vector3(side * 0.47f, 0.52f, -0.45f) * unit;
                var go = new GameObject(side < 0 ? "Exhaust L" : "Exhaust R");
                go.transform.SetParent(root.transform, false);
                go.transform.position = body.TransformPoint(local);
                go.transform.rotation = body.rotation * Quaternion.LookRotation(new Vector3(0f, 0.75f, -1f).normalized, Vector3.up);
                Fire(go, unit);
                var lightGo = new GameObject("Exhaust Light");
                lightGo.transform.SetParent(go.transform, false);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.85f, 0.4f, 1f);
                light.range = 4f * unit;
                light.intensity = 4f;
            }
            root.SetActive(false);
            return root;
        }

        private static void Fire(GameObject go, float unit)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * unit, 6f * unit);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * unit, 0.7f * unit);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f), new Color(0.9f, 0.45f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.gravityModifier = -0.4f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 90f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.12f * unit;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.15f), 0.3f), new GradientColorKey(new Color(0.6f, 0.2f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.5f;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.ParticleAdditive(new Color(2.5f, 1.6f, 1.6f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
        }

        /// <summary>World length of the mage's staff, and where along it the hand grips (fraction from the butt).</summary>
        private const float StaffWorldLength = 1.9f;
        private const float StaffGrip = 0.5f;

        /// <summary>
        /// Puts a weapon model (Resources/Fighters/&lt;WeaponModel&gt;, a staff modeled along +Y) in the right hand,
        /// pointing along the forearm with the grip at the hand. Returns the distance from the hand to the tip, or 0.
        /// </summary>
        private static float AttachWeaponModel(FighterVisual visual, CharacterDef character)
        {
            PoseRig rig = visual.Rig;
            if (string.IsNullOrEmpty(character.WeaponModel) || rig == null || rig.HandR == null || rig.ForeArmR == null) return 0f;
            GameObject prefab = Resources.Load<GameObject>("Fighters/" + character.WeaponModel);
            if (prefab == null) return 0f;
            GameObject weapon = Object.Instantiate(prefab);
            weapon.name = "Weapon " + character.WeaponModel;
            foreach (Animator a in weapon.GetComponentsInChildren<Animator>(true)) a.enabled = false;

            // Measure the model's length along its own up axis.
            Bounds local = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                if (!any) { local = b; any = true; }
                else local.Encapsulate(b);
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            float modelLength = any ? Mathf.Max(0.01f, local.size.y) : 2f;
            float scale = StaffWorldLength / modelLength;

            Transform hand = rig.HandR;
            Vector3 along = (hand.position - rig.ForeArmR.position).normalized;
            var holder = new GameObject("Weapon Holder").transform;
            holder.SetParent(hand, false);
            holder.position = hand.position;
            holder.rotation = Quaternion.FromToRotation(Vector3.up, along);
            weapon.transform.SetParent(holder, false);
            float lossy = Mathf.Max(holder.lossyScale.x, 1e-4f);
            weapon.transform.localScale = Vector3.one * (scale / lossy);
            weapon.transform.localRotation = Quaternion.identity;
            // Grip point (StaffGrip of the length from the butt) sits in the hand.
            weapon.transform.localPosition = new Vector3(0f, -(local.min.y + modelLength * StaffGrip) * scale / lossy, 0f);
            visual.WeaponHolder = holder;
            return StaffWorldLength * (1f - StaffGrip) * 0.95f;
        }

        // ------------------------------------------------------------------ Weapons in hand, thrown and dropped

        /// <summary>Lengths of the Vex weapon models in meters.</summary>
        public const float LauncherLength = 1.25f;
        public const float PistolLength = 0.25f;
        public const float RocketLength = 0.6f;
        /// <summary>The launcher sits this far forward of its holder, so the shoulder is near its rear and the rear grip is in front of the chest.</summary>
        public const float LauncherForwardOffset = 0.47f;

        private static void SetupWeaponVisuals(FighterVisual visual, CharacterDef character, Color accentHdr, Transform sceneParent)
        {
            if (!character.CanThrowWeapon || visual.Rig == null) return;
            PoseRig rig = visual.Rig;
            var templates = new GameObject("Weapon Templates").transform;
            templates.SetParent(visual.Root, false);
            templates.gameObject.SetActive(false);

            if (character.Weapon == "Sword")
            {
                // The sword is part of the character mesh (material "...weapon"): hide it by swapping in an invisible material.
                foreach (SkinnedMeshRenderer smr in visual.Body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Material[] mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null || mats[i].name.IndexOf("weapon", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        visual.WeaponSkin = smr;
                        visual.ArmedMaterials = mats;
                        visual.UnarmedMaterials = (Material[])mats.Clone();
                        visual.UnarmedMaterials[i] = MaterialFactory.Additive(Color.clear);
                        visual.WeaponTemplate[0] = ExtractSubmesh(smr, i, mats[i], templates);
                        break;
                    }
                    if (visual.WeaponSkin != null) break;
                }
            }
            else if (character.Weapon == "Staff" && visual.WeaponHolder != null)
            {
                visual.HeldWeapon[0] = visual.WeaponHolder.gameObject;
                GameObject prefab = Resources.Load<GameObject>("Fighters/" + character.WeaponModel);
                if (prefab != null) visual.WeaponTemplate[0] = Template(prefab, templates, StaffWorldLength);
            }
            else if (character.Weapon == "Wand" && rig.HandR != null && rig.ForeArmR != null)
            {
                // A short dark wand with a glowing tip, held along the forearm.
                var wand = new GameObject("Wand").transform;
                wand.SetParent(rig.HandR, false);
                Vector3 along = rig.HandR.InverseTransformDirection((rig.HandR.position - rig.ForeArmR.position).normalized);
                float lossy = Mathf.Max(rig.HandR.lossyScale.x, 1e-4f);
                wand.localRotation = Quaternion.FromToRotation(Vector3.up, along);
                wand.localScale = Vector3.one / lossy;
                BuildWand(wand, accentHdr);
                visual.HeldWeapon[0] = wand.gameObject;
                var t = new GameObject("Wand Template").transform;
                t.SetParent(templates, false);
                BuildWand(t, accentHdr);
                visual.WeaponTemplate[0] = t.gameObject;
            }
            else if (character.Weapon == "Launcher")
            {
                GameObject launcher = Resources.Load<GameObject>("Fighters/VexLauncher");
                GameObject pistol = Resources.Load<GameObject>("Fighters/VexPistol");
                if (launcher != null && pistol != null && rig.Chest != null && rig.HandR != null)
                {
                    // Model axes after import: launcher muzzle +X, pistol muzzle -X; both with their top along +Y.
                    visual.LauncherHolder = Holder(launcher, rig.Chest, "Launcher Holder", LauncherLength, Quaternion.FromToRotation(Vector3.right, Vector3.forward), new Vector3(0f, 0f, LauncherForwardOffset));
                    visual.PistolHolder = Holder(pistol, rig.HandR, "Pistol Holder", PistolLength, Quaternion.FromToRotation(Vector3.left, Vector3.forward), new Vector3(0f, 0.05f, 0.05f));
                    visual.HeldWeapon[0] = visual.LauncherHolder.gameObject;
                    visual.HeldWeapon[1] = visual.PistolHolder.gameObject;
                    visual.WeaponTemplate[0] = Template(launcher, templates, LauncherLength);
                    visual.WeaponTemplate[1] = Template(pistol, templates, PistolLength);
                }
                else
                {
                    visual.HeldWeapon[0] = visual.PrimaryProp;
                    visual.HeldWeapon[1] = visual.SecondaryProp;
                    if (visual.PrimaryProp != null) visual.WeaponTemplate[0] = CloneTemplate(visual.PrimaryProp, templates);
                    if (visual.SecondaryProp != null) visual.WeaponTemplate[1] = CloneTemplate(visual.SecondaryProp, templates);
                }
            }
        }

        private static void BuildWand(Transform parent, Color accentHdr)
        {
            GameObject stick = MaterialFactory.Primitive(PrimitiveType.Cylinder, parent, MaterialFactory.Lit(new Color(0.18f, 0.11f, 0.08f), 0.4f), "Stick");
            stick.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            stick.transform.localScale = new Vector3(0.025f, 0.2f, 0.025f);
            GameObject tip = MaterialFactory.Primitive(PrimitiveType.Sphere, parent, MaterialFactory.Glow(accentHdr), "Tip");
            tip.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            tip.transform.localScale = Vector3.one * 0.05f;
            tip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A model scaled so its longest side is the given length, centered on its holder with an extra offset (meters, holder space).</summary>
        private static Transform Holder(GameObject prefab, Transform bone, string name, float length, Quaternion modelRotation, Vector3 offset)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(bone, false);
            float lossy = Mathf.Max(bone.lossyScale.x, 1e-4f);
            holder.localScale = Vector3.one / lossy;
            GameObject model = Object.Instantiate(prefab, holder, false);
            model.name = prefab.name;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            model.transform.localRotation = modelRotation;
            FitCentered(model.transform, holder, length);
            model.transform.localPosition += offset;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            return holder;
        }

        private static GameObject Template(GameObject prefab, Transform parent, float length)
        {
            var holder = new GameObject(prefab.name + " Template").transform;
            holder.SetParent(parent, false);
            GameObject model = Object.Instantiate(prefab, holder, false);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            FitCentered(model.transform, holder, length);
            return holder.gameObject;
        }

        private static GameObject CloneTemplate(GameObject source, Transform parent)
        {
            GameObject copy = Object.Instantiate(source, parent, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            copy.SetActive(true);
            return copy;
        }

        /// <summary>Scales a model so its longest side is the length (world meters) and centers it on the holder.</summary>
        private static void FitCentered(Transform model, Transform holder, float length)
        {
            Bounds b;
            if (!WorldBounds(model, out b)) return;
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            model.localScale = model.localScale * (length / Mathf.Max(largest, 1e-4f));
            WorldBounds(model, out b);
            model.position += holder.position - b.center;
        }

        private static bool WorldBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
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

        /// <summary>Copies one submesh of a skinned mesh (in its rest pose) into a standalone, centered prop.</summary>
        private static GameObject ExtractSubmesh(SkinnedMeshRenderer smr, int submesh, Material material, Transform parent)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            int[] tris = baked.GetTriangles(submesh);
            Vector3[] verts = baked.vertices;
            Vector3[] normals = baked.normals;
            Vector2[] uvs = baked.uv;
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i < tris.Length; i++)
            {
                int old = tris[i];
                int idx;
                if (!map.TryGetValue(old, out idx))
                {
                    idx = v.Count;
                    map[old] = idx;
                    v.Add(smr.transform.TransformPoint(verts[old]));
                    n.Add(normals.Length > old ? smr.transform.TransformDirection(normals[old]) : Vector3.up);
                    uv.Add(uvs.Length > old ? uvs[old] : Vector2.zero);
                }
                t.Add(idx);
            }
            Object.Destroy(baked);
            if (v.Count == 0) return null;
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in v) center += p;
            center /= v.Count;
            for (int i = 0; i < v.Count; i++) v[i] -= center;
            var mesh = new Mesh { name = "Weapon Prop" };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Sword Template");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static TrailRenderer MakeEndTrail(Transform holder, float worldOffset, Color accentHdr)
        {
            var go = new GameObject("Staff End Trail");
            go.transform.SetParent(holder, false);
            float lossy = Mathf.Max(holder.lossyScale.x, 1e-4f);
            go.transform.localPosition = new Vector3(0f, worldOffset / lossy, 0f);
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.04f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            Color c = accentHdr * 0.55f;
            c.a = 0.85f;
            trail.sharedMaterial = MaterialFactory.Additive(c);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            return trail;
        }

        private static TrailRenderer MakeTrail(Transform hand, Transform forearm, float worldReach, Color accentHdr, float width)
        {
            var go = new GameObject("Swipe Trail");
            go.transform.SetParent(hand, false);
            Vector3 along = hand.InverseTransformDirection((hand.position - forearm.position).normalized);
            float lossy = Mathf.Max(hand.lossyScale.x, 1e-4f);
            go.transform.localPosition = along * (worldReach / lossy);
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.04f;
            trail.startWidth = width;
            trail.endWidth = 0f;
            Color c = accentHdr * 0.55f;
            c.a = 0.85f;
            trail.sharedMaterial = MaterialFactory.Additive(c);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            return trail;
        }

        private static void BuildExtras(FighterVisual visual, CharacterDef character, Color accentHdr, int playerIndex)
        {
            PoseRig rig = visual.Rig;
            float weaponReach = AttachWeaponModel(visual, character);

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

            // Launcher and pistol, held in the right hand along the forearm (placeholders when the Vex weapon models are missing).
            bool gunModels = Resources.Load<GameObject>("Fighters/VexLauncher") != null && Resources.Load<GameObject>("Fighters/VexPistol") != null;
            if (character.SecondaryWeapon != null && rig.HandR != null && rig.ForeArmR != null && !gunModels)
            {
                Vector3 along = rig.HandR.InverseTransformDirection((rig.HandR.position - rig.ForeArmR.position).normalized);
                float lossy = Mathf.Max(rig.HandR.lossyScale.x, 1e-4f);
                Quaternion toArm = Quaternion.FromToRotation(Vector3.up, along);

                var launcher = new GameObject("Missile Launcher");
                launcher.transform.SetParent(rig.HandR, false);
                Material metal = MaterialFactory.Lit(new Color(0.2f, 0.24f, 0.26f), 0.55f, 0.8f);
                GameObject tube = MaterialFactory.Primitive(PrimitiveType.Cylinder, launcher.transform, metal, "Tube");
                tube.transform.localRotation = toArm;
                tube.transform.localPosition = along * (0.05f / lossy);
                tube.transform.localScale = new Vector3(0.24f, 0.62f, 0.24f) / lossy;
                GameObject muzzle = MaterialFactory.Primitive(PrimitiveType.Cylinder, launcher.transform, visual.AccentMaterial, "Muzzle Ring");
                muzzle.transform.localRotation = toArm;
                muzzle.transform.localPosition = along * (0.66f / lossy);
                muzzle.transform.localScale = new Vector3(0.27f, 0.03f, 0.27f) / lossy;
                muzzle.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                visual.PrimaryProp = launcher;

                var pistol = new GameObject("Pistol");
                pistol.transform.SetParent(rig.HandR, false);
                GameObject barrel = MaterialFactory.Primitive(PrimitiveType.Cube, pistol.transform, metal, "Barrel");
                barrel.transform.localRotation = toArm;
                barrel.transform.localPosition = along * (0.14f / lossy);
                barrel.transform.localScale = new Vector3(0.06f, 0.26f, 0.09f) / lossy;
                GameObject sight = MaterialFactory.Primitive(PrimitiveType.Cube, pistol.transform, visual.AccentMaterial, "Sight");
                sight.transform.localRotation = toArm;
                sight.transform.localPosition = along * (0.24f / lossy);
                sight.transform.localScale = new Vector3(0.065f, 0.04f, 0.095f) / lossy;
                pistol.SetActive(false);
                visual.SecondaryProp = pistol;
            }

            // Swipe trails: from the weapon tip (sword, staff) or the fists.
            if (visual.WeaponHolder != null)
            {
                // Staff held at the center: a trail on each end.
                visual.TrailR = MakeEndTrail(visual.WeaponHolder, weaponReach, accentHdr);
                visual.TrailL = MakeEndTrail(visual.WeaponHolder, -weaponReach, accentHdr);
            }
            else if (rig.HandR != null && rig.ForeArmR != null && character.SecondaryWeapon == null)
            {
                float reach = weaponReach > 0f ? weaponReach : character.Weapon == "Sword" ? 1.0f : character.Weapon == "Staff" ? 1.1f : character.Weapon == "Wand" ? 0.45f : 0.12f;
                visual.TrailR = MakeTrail(rig.HandR, rig.ForeArmR, reach, accentHdr, character.Weapon == "Fists" || character.Stance == FighterStance.Beast ? 0.18f : 0.12f);
                if (rig.HandL != null && rig.ForeArmL != null && (character.Weapon == "Fists" || character.Stance == FighterStance.Beast || character.UltimateBuff == BuffId.TwinBlades))
                {
                    visual.TrailL = MakeTrail(rig.HandL, rig.ForeArmL, character.UltimateBuff == BuffId.TwinBlades ? 1.0f : 0.12f, accentHdr, 0.16f);
                }
            }

            if (character.Stance == FighterStance.Beast && rig.Chest != null && rig.UpperArmL != null && rig.UpperArmR != null)
            {
                visual.ExhaustFire = BuildExhaust(visual, rig);
            }

            var aura = new GameObject("Aura");
            aura.transform.SetParent(visual.Root, false);
            GameObject ring = MaterialFactory.Primitive(PrimitiveType.Cylinder, aura.transform, visual.AccentMaterial, "Ring");
            float ringSize = Mathf.Max(1.6f, character.Radius.ToFloat() * 3.6f);
            ring.transform.localScale = new Vector3(ringSize, 0.01f, ringSize);
            ring.transform.localPosition = new Vector3(0, 0.03f, 0);
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Aura Light");
            lightGo.transform.SetParent(aura.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(5f, ringSize * 3f);
            light.intensity = 3f;
            light.color = accentHdr / 3.2f;
            aura.SetActive(false);
            visual.BuffAura = aura;
            visual.AuraLight = light;

            // Small marker under each fighter so players can tell them apart.
            var marker = MaterialFactory.Primitive(PrimitiveType.Cylinder, visual.Root, MaterialFactory.Glow(playerIndex == 0 ? new Color(0.4f, 1.2f, 2.5f) : new Color(2.5f, 0.6f, 0.4f)), "Player Marker");
            float markerSize = Mathf.Max(0.9f, character.Radius.ToFloat() * 2f);
            marker.transform.localScale = new Vector3(markerSize, 0.005f, markerSize);
            marker.transform.localPosition = new Vector3(0, 0.015f, 0);
            marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
