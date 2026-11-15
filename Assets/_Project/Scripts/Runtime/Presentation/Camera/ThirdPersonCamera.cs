using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Orbit camera behind the local fighter, rotated with the mouse or right stick (Genshin Impact style).
    /// With lock-on, it swings behind the player to keep the opponent in view.
    /// The camera is local only: it never affects the simulation except through the movement direction.
    /// </summary>
    public sealed class ThirdPersonCamera
    {
        public readonly Camera Camera;

        public float Yaw;
        public float Pitch = 16f;
        public float Distance = 6.2f;

        private Vector3 _focus;
        private Vector3 _focusVelocity;
        private float _shake;
        private bool _hasFocus;

        public ThirdPersonCamera(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            Camera = go.AddComponent<Camera>();
            Camera.fieldOfView = 58f;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 450f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.12f, 0.04f, 0.03f);
            go.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>Menu view: slow orbit around the arena.</summary>
        public void UpdateMenu(float deltaTime)
        {
            Yaw += deltaTime * 5f;
            Pitch = 17f;
            Vector3 focus = new Vector3(0f, -1.5f, 0f);
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Camera.transform.position = focus - rot * Vector3.forward * 27f;
            Camera.transform.rotation = rot;
            _hasFocus = false;
        }

        public void UpdateFollow(Vector3 player, Vector3 opponent, bool lockOn, Vector2 look, bool invertY, float deltaTime)
        {
            Yaw += look.x;
            Pitch -= look.y * (invertY ? -1f : 1f);
            Pitch = Mathf.Clamp(Pitch, -15f, 65f);

            Vector3 focusTarget = player + Vector3.up * 1.45f;
            if (lockOn)
            {
                Vector3 toOpp = opponent - player;
                toOpp.y = 0f;
                if (toOpp.sqrMagnitude > 0.25f)
                {
                    float targetYaw = Mathf.Atan2(toOpp.x, toOpp.z) * Mathf.Rad2Deg;
                    Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-6f * deltaTime));
                    Pitch = Mathf.Lerp(Pitch, 14f, 1f - Mathf.Exp(-3f * deltaTime));
                }
                focusTarget = Vector3.Lerp(focusTarget, opponent + Vector3.up * 1.2f, 0.3f);
            }

            if (!_hasFocus)
            {
                _focus = focusTarget;
                _hasFocus = true;
            }
            _focus = Vector3.SmoothDamp(_focus, focusTarget, ref _focusVelocity, 0.06f, Mathf.Infinity, deltaTime);

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 back = -(rot * Vector3.forward);
            // Walls, columns and platforms (Sky Ruins) between the fighter and the camera: move the camera in front of
            // them at once, and back out smoothly once the view is clear.
            float allowed = Distance;
            for (int i = 1; i <= 24; i++)
            {
                float d = Distance * i / 24f;
                if (BlockedAt(_focus + back * d))
                {
                    allowed = Mathf.Max(0.9f, d - 0.4f);
                    break;
                }
            }
            _pull = allowed < _pull ? allowed : Mathf.MoveTowards(_pull, allowed, deltaTime * 5f);
            Vector3 pos = _focus + back * Mathf.Min(Distance, _pull);
            FixVec3 fp = ToFix(pos);
            if (Arena.IsInside(fp))
            {
                float ground = Arena.GroundAt(fp).ToFloat();
                if (pos.y < ground + 0.35f) pos.y = ground + 0.35f;
            }
            else if (pos.y < 0.35f && Mathf.Abs(player.x) < 13.5f && Mathf.Abs(player.z) < 13.5f)
            {
                pos.y = 0.35f;
            }

            _shake = Mathf.MoveTowards(_shake, 0f, deltaTime * 2.5f);
            if (_shake > 0f)
            {
                float t = Time.unscaledTime * 55f;
                pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * _shake * 0.35f;
            }

            Camera.transform.position = pos;
            Camera.transform.rotation = Quaternion.LookRotation(_focus - pos, Vector3.up);
        }

        private float _pull = 100f;

        private static FixVec3 ToFix(Vector3 p)
        {
            return new FixVec3(Fix.FromRaw((long)(p.x * 65536f)), Fix.FromRaw((long)(p.y * 65536f)), Fix.FromRaw((long)(p.z * 65536f)));
        }

        /// <summary>True if the point is inside (or within 0.2 m of) a solid block of the current map.</summary>
        private static bool BlockedAt(Vector3 p)
        {
            TerrainBox[] t = Arena.Terrain;
            if (t.Length == 0) return false;
            for (int i = 0; i < t.Length; i++)
            {
                TerrainBox b = t[i];
                if (!Arena.IsActive(ref b)) continue;
                if (p.x < b.MinX.ToFloat() - 0.2f || p.x > b.MaxX.ToFloat() + 0.2f) continue;
                if (p.z < b.MinZ.ToFloat() - 0.2f || p.z > b.MaxZ.ToFloat() + 0.2f) continue;
                FixVec3 fp = ToFix(p);
                float top = b.TopAt(fp.X, fp.Z).ToFloat();
                if (p.y < top + 0.2f && (!b.Floating || p.y > b.Bottom.ToFloat() - 0.2f)) return true;
            }
            return false;
        }

        /// <summary>Snaps the camera behind a fighter facing the given yaw (used at round start).</summary>
        public void SnapBehind(float yaw)
        {
            Yaw = yaw;
            Pitch = 16f;
            _hasFocus = false;
        }
    }
}
