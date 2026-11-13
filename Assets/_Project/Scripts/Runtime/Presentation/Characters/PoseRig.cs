using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// The bones of a humanoid that the procedural animator drives.
    /// Bones are found by name, so this works for common rigs (3ds Max Biped "Bip001-L-UpperArm",
    /// Character Creator "CC_Base_L_Upperarm", Mixamo "mixamorig:LeftArm") and for the primitive rig.
    /// Posing is done by aiming each limb segment at a direction, which does not depend on how
    /// the original bone axes were set up.
    /// </summary>
    public sealed class PoseRig
    {
        public Transform Pelvis;
        public Transform SpineLow;
        public Transform Chest;
        public Transform Head;
        public Transform UpperArmL, ForeArmL, HandL;
        public Transform UpperArmR, ForeArmR, HandR;
        public Transform ThighL, CalfL, FootL;
        public Transform ThighR, CalfR, FootR;

        private Transform[] _bones = Array.Empty<Transform>();
        private Quaternion[] _rest = Array.Empty<Quaternion>();

        public bool HasArms
        {
            get { return UpperArmL != null && ForeArmL != null && HandL != null && UpperArmR != null && ForeArmR != null && HandR != null; }
        }

        public bool HasLegs
        {
            get { return ThighL != null && CalfL != null && FootL != null && ThighR != null && CalfR != null && FootR != null; }
        }

        public void CaptureRest()
        {
            var list = new List<Transform>();
            foreach (Transform t in new[] { Pelvis, SpineLow, Chest, Head, UpperArmL, ForeArmL, HandL, UpperArmR, ForeArmR, HandR, ThighL, CalfL, FootL, ThighR, CalfR, FootR })
            {
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            _bones = list.ToArray();
            _rest = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++) _rest[i] = _bones[i].localRotation;
        }

        public void ResetToRest()
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] != null) _bones[i].localRotation = _rest[i];
            }
        }

        /// <summary>Rotates the bone so the segment from bone to child points along worldDirection.</summary>
        public static void Aim(Transform bone, Transform child, Vector3 worldDirection, float weight = 1f)
        {
            if (bone == null || child == null || weight <= 0f) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f || worldDirection.sqrMagnitude < 1e-8f) return;
            Quaternion delta = Quaternion.FromToRotation(current, worldDirection);
            if (weight < 1f) delta = Quaternion.Slerp(Quaternion.identity, delta, weight);
            bone.rotation = delta * bone.rotation;
        }

        public static PoseRig FromHierarchy(Transform root)
        {
            var rig = new PoseRig();
            var spines = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string[] tokens = Tokenize(t.name);
                if (tokens.Length == 0 || IsHelper(tokens)) continue;
                int side = Side(tokens, t.name);
                if (Has(tokens, "pelvis") || Has(tokens, "hips")) { if (rig.Pelvis == null) rig.Pelvis = t; }
                else if (HasPrefix(tokens, "spine") || Has(tokens, "waist")) spines.Add(t);
                else if (Has(tokens, "head")) { if (rig.Head == null) rig.Head = t; }
                else if (Has(tokens, "upperarm") || (Has(tokens, "arm") && side != 0 && !Has(tokens, "fore"))) Assign(ref rig.UpperArmL, ref rig.UpperArmR, side, t);
                else if (Has(tokens, "forearm") || Has(tokens, "lowerarm")) Assign(ref rig.ForeArmL, ref rig.ForeArmR, side, t);
                else if (Has(tokens, "hand")) Assign(ref rig.HandL, ref rig.HandR, side, t);
                else if (Has(tokens, "thigh") || Has(tokens, "upleg") || Has(tokens, "upperleg")) Assign(ref rig.ThighL, ref rig.ThighR, side, t);
                else if (Has(tokens, "calf") || Has(tokens, "leg") || Has(tokens, "lowerleg") || Has(tokens, "shin")) Assign(ref rig.CalfL, ref rig.CalfR, side, t);
                else if (Has(tokens, "foot")) Assign(ref rig.FootL, ref rig.FootR, side, t);
            }
            spines.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            if (spines.Count > 0)
            {
                rig.SpineLow = spines[0];
                rig.Chest = spines[spines.Count - 1];
            }
            rig.CaptureRest();
            return rig;
        }

        private static void Assign(ref Transform left, ref Transform right, int side, Transform t)
        {
            if (side < 0 && left == null) left = t;
            else if (side > 0 && right == null) right = t;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null)
            {
                d++;
                t = t.parent;
            }
            return d;
        }

        private static string[] Tokenize(string name)
        {
            string lower = name.ToLowerInvariant().Replace("mixamorig:", "mixamorig ");
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            foreach (char c in lower)
            {
                if (char.IsLetter(c))
                {
                    current.Append(c);
                }
                else
                {
                    if (current.Length > 0) tokens.Add(current.ToString());
                    current.Length = 0;
                }
            }
            if (current.Length > 0) tokens.Add(current.ToString());

            // Split Mixamo style names like "leftupleg" into "left" + "upleg".
            var result = new List<string>();
            foreach (string tok in tokens)
            {
                if (tok.StartsWith("left") && tok.Length > 4)
                {
                    result.Add("left");
                    result.Add(tok.Substring(4));
                }
                else if (tok.StartsWith("right") && tok.Length > 5)
                {
                    result.Add("right");
                    result.Add(tok.Substring(5));
                }
                else
                {
                    result.Add(tok);
                }
            }
            return result.ToArray();
        }

        private static readonly string[] HelperWords =
        {
            "twist", "share", "end", "finger", "toe", "nub", "eye", "eyeild", "jaw", "teeth", "tongue", "breast",
            "ribs", "cloth", "cloak", "hair", "dummy", "prop", "fx", "lip", "cheek", "forehead", "shulder", "shoulder",
            "clavicle", "index", "middle", "mid", "ring", "pinky", "thumb", "facial", "upperjaw", "bone", "neck", "roll",
        };

        private static bool IsHelper(string[] tokens)
        {
            foreach (string tok in tokens)
            {
                foreach (string h in HelperWords)
                {
                    if (tok == h) return true;
                }
                if (tok.StartsWith("finger") || tok.StartsWith("toe") || tok.Contains("dummy") || tok.Contains("twist")) return true;
            }
            return false;
        }

        private static int Side(string[] tokens, string raw)
        {
            foreach (string tok in tokens)
            {
                if (tok == "l" || tok == "left") return -1;
                if (tok == "r" || tok == "right") return 1;
            }
            string lower = raw.ToLowerInvariant();
            if (lower.EndsWith(".l") || lower.EndsWith("_l")) return -1;
            if (lower.EndsWith(".r") || lower.EndsWith("_r")) return 1;
            return 0;
        }

        private static bool Has(string[] tokens, string word)
        {
            foreach (string tok in tokens)
            {
                if (tok == word) return true;
            }
            return false;
        }

        private static bool HasPrefix(string[] tokens, string prefix)
        {
            foreach (string tok in tokens)
            {
                if (tok.StartsWith(prefix)) return true;
            }
            return false;
        }
    }
}
