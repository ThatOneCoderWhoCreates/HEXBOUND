using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HEXBOUND.Presentation
{
    /// <summary>One sampled motion-capture pose, as directions in the fighter's body space (x right, y up, z forward).</summary>
    public struct MocapFrame
    {
        public Vector3 UpperArmL, ForeArmL, UpperArmR, ForeArmR;
        public Vector3 ThighL, CalfL, ThighR, CalfR;
        public Vector3 SpineDir, NeckDir, PelvisUp, PelvisRight;
        /// <summary>Shoulder line twist relative to the hips, in degrees.</summary>
        public float SpineYaw;
        /// <summary>Pelvis height divided by the standing pelvis height (1 = standing, near 0 = lying down).</summary>
        public float PelvisHeightRatio;
    }

    /// <summary>
    /// Motion-capture clips from the KinemIQ Free Animation Pack (CC0, Quaternius Universal skeleton),
    /// loaded from Resources/Animations/KinemIQ. Clips play on a hidden copy of the KinemIQ mannequin,
    /// and the resulting limb directions are copied onto any fighter rig (retargeting by direction).
    /// </summary>
    public sealed class MocapLibrary
    {
        private const string Folder = "Animations/KinemIQ";
        private static MocapLibrary _shared;
        private static bool _loaded;

        private readonly GameObject _rigPrefab;
        private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The shared library, or null if the animation files are not in the project.</summary>
        public static MocapLibrary Shared
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _shared = TryLoad();
                }
                return _shared;
            }
        }

        private MocapLibrary(GameObject rigPrefab)
        {
            _rigPrefab = rigPrefab;
        }

        private static MocapLibrary TryLoad()
        {
            GameObject rig = Resources.Load<GameObject>(Folder + "/KinemIQ_Mannequin");
            if (rig == null) return null;
            var lib = new MocapLibrary(rig);
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(Folder))
            {
                if (clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                if (!lib._clips.ContainsKey(clip.name)) lib._clips.Add(clip.name, clip);
            }
            if (lib._clips.Count == 0) return null;
            return lib;
        }

        public bool Has(string clipName)
        {
            return !string.IsNullOrEmpty(clipName) && _clips.ContainsKey(clipName);
        }

        public float Length(string clipName)
        {
            AnimationClip clip;
            return _clips.TryGetValue(clipName, out clip) ? Mathf.Max(clip.length, 0.01f) : 1f;
        }

        internal AnimationClip Clip(string clipName)
        {
            AnimationClip clip;
            return _clips.TryGetValue(clipName, out clip) ? clip : null;
        }

        public MocapSampler CreateSampler(Transform parent)
        {
            return new MocapSampler(this, _rigPrefab, parent);
        }
    }

    /// <summary>A hidden mannequin that evaluates clips on demand and reports limb directions.</summary>
    public sealed class MocapSampler : IDisposable
    {
        private readonly MocapLibrary _library;
        private readonly GameObject _go;
        private readonly Animator _animator;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private readonly Dictionary<string, AnimationClipPlayable> _playables = new Dictionary<string, AnimationClipPlayable>();
        private readonly PoseRig _rig;
        private Vector3 _right = Vector3.right;
        private Vector3 _forward = Vector3.forward;
        private float _restPelvisHeight = 1f;
        private readonly bool _valid;

        internal MocapSampler(MocapLibrary library, GameObject prefab, Transform parent)
        {
            _library = library;
            _go = UnityEngine.Object.Instantiate(prefab, parent, false);
            _go.name = "Mocap Source (hidden)";
            _go.transform.localPosition = new Vector3(0f, -500f, 0f);
            _go.transform.localRotation = Quaternion.identity;
            foreach (Renderer r in _go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            _animator = _go.GetComponent<Animator>();
            if (_animator == null) _animator = _go.AddComponent<Animator>();
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.runtimeAnimatorController = null;

            _graph = PlayableGraph.Create("HEXBOUND Mocap");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _output = AnimationPlayableOutput.Create(_graph, "Pose", _animator);

            _rig = PoseRig.FromHierarchy(_go.transform);
            _valid = _rig.HasArms && _rig.HasLegs && _rig.Pelvis != null && _rig.Chest != null && _rig.Head != null;
            if (!_valid) return;

            // Standing reference: the first frame of a relaxed idle (or the bind pose if missing).
            if (_library.Has("Idle_relaxed")) Evaluate("Idle_relaxed", 0f);
            Vector3 right = _rig.ThighR.position - _rig.ThighL.position;
            right.y = 0f;
            if (right.sqrMagnitude > 1e-8f) _right = right.normalized;
            _forward = Vector3.Cross(_right, Vector3.up);
            Transform ball = FindChildNamed(_go.transform, "ball_l");
            if (ball != null && Vector3.Dot(ball.position - _rig.FootL.position, _forward) < 0f)
            {
                _forward = -_forward;
                _right = -_right;
            }
            // Keep the reference frame relative to the hidden mannequin, which turns with the fighter.
            _right = _go.transform.InverseTransformDirection(_right);
            _forward = _go.transform.InverseTransformDirection(_forward);
            _restPelvisHeight = Mathf.Max(0.05f, _rig.Pelvis.position.y - _go.transform.position.y);
        }

        public bool Valid
        {
            get { return _valid; }
        }

        private static Transform FindChildNamed(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        private bool Evaluate(string clipName, float time)
        {
            AnimationClipPlayable playable;
            if (!_playables.TryGetValue(clipName, out playable))
            {
                AnimationClip clip = _library.Clip(clipName);
                if (clip == null) return false;
                playable = AnimationClipPlayable.Create(_graph, clip);
                playable.SetApplyFootIK(false);
                _playables.Add(clipName, playable);
            }
            _output.SetSourcePlayable(playable);
            playable.SetTime(time);
            playable.SetTime(time);
            _graph.Evaluate(0f);
            return true;
        }

        /// <summary>Samples a clip at a time in seconds and fills the frame. Mirror swaps left and right.</summary>
        public bool Sample(string clipName, float time, bool mirror, ref MocapFrame frame)
        {
            if (!_valid || !Evaluate(clipName, time)) return false;

            Vector3 origin = _go.transform.position;
            Vector3 pel = Local(_rig.Pelvis.position, origin);
            Vector3 chest = Local(_rig.Chest.position, origin);
            Vector3 spine = _rig.SpineLow != null && _rig.SpineLow != _rig.Chest ? Local(_rig.SpineLow.position, origin) : pel;
            Vector3 head = Local(_rig.Head.position, origin);
            Vector3 uaL = Local(_rig.UpperArmL.position, origin), faL = Local(_rig.ForeArmL.position, origin), hL = Local(_rig.HandL.position, origin);
            Vector3 uaR = Local(_rig.UpperArmR.position, origin), faR = Local(_rig.ForeArmR.position, origin), hR = Local(_rig.HandR.position, origin);
            Vector3 thL = Local(_rig.ThighL.position, origin), caL = Local(_rig.CalfL.position, origin), ftL = Local(_rig.FootL.position, origin);
            Vector3 thR = Local(_rig.ThighR.position, origin), caR = Local(_rig.CalfR.position, origin), ftR = Local(_rig.FootR.position, origin);

            if (mirror)
            {
                pel = M(pel); chest = M(chest); spine = M(spine); head = M(head);
                Vector3 t;
                t = M(uaL); uaL = M(uaR); uaR = t;
                t = M(faL); faL = M(faR); faR = t;
                t = M(hL); hL = M(hR); hR = t;
                t = M(thL); thL = M(thR); thR = t;
                t = M(caL); caL = M(caR); caR = t;
                t = M(ftL); ftL = M(ftR); ftR = t;
            }

            frame.UpperArmL = Dir(faL - uaL);
            frame.ForeArmL = Dir(hL - faL);
            frame.UpperArmR = Dir(faR - uaR);
            frame.ForeArmR = Dir(hR - faR);
            frame.ThighL = Dir(caL - thL);
            frame.CalfL = Dir(ftL - caL);
            frame.ThighR = Dir(caR - thR);
            frame.CalfR = Dir(ftR - caR);
            frame.SpineDir = Dir(chest - spine);
            frame.NeckDir = Dir(head - chest);
            frame.PelvisUp = Dir(chest - pel);
            frame.PelvisRight = Dir(thR - thL);
            Vector3 hips = thR - thL;
            hips.y = 0f;
            Vector3 shoulders = uaR - uaL;
            shoulders.y = 0f;
            frame.SpineYaw = hips.sqrMagnitude > 1e-8f && shoulders.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(hips, shoulders, Vector3.up) : 0f;
            frame.PelvisHeightRatio = pel.y / _restPelvisHeight;
            return true;
        }

        private Vector3 Local(Vector3 world, Vector3 origin)
        {
            Vector3 d = world - origin;
            Transform t = _go.transform;
            return new Vector3(Vector3.Dot(d, t.TransformDirection(_right)), Vector3.Dot(d, t.up), Vector3.Dot(d, t.TransformDirection(_forward)));
        }

        private static Vector3 M(Vector3 v)
        {
            return new Vector3(-v.x, v.y, v.z);
        }

        private static Vector3 Dir(Vector3 v)
        {
            return v.sqrMagnitude > 1e-10f ? v.normalized : Vector3.up;
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_go != null) UnityEngine.Object.Destroy(_go);
        }
    }
}
